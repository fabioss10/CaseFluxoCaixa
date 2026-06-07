using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FluxoCaixa.Application.Services
{
    public class ProcessadorOutboxService : IProcessadorOutboxService
    {
        private readonly IUnitOfWorkRepository _uow;

        public ProcessadorOutboxService(IUnitOfWorkRepository uow)
        {
            _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        }

        public async Task ProcessarAsync(CancellationToken cancellationToken)
        {
            // =======================================================================================
            // ARQUITETURA DE PROCESSAMENTO EM LOTE (BATCH AGGREGATION & FIRST-LEVEL CACHE)
            // PADRÃO: TRANSACTIONAL OUTBOX + CONSISTÊNCIA EVENTUAL DO SALDO
            // =======================================================================================
            // 1. 
            // O banco de dados e o broker de mensageria (RabbitMQ/Kafka) são sistemas independentes e 
            // fisicamente separados na rede. Não existe uma transação ACID nativa que englobe os dois.
            // Tentar fazer uma transação distribuída (como Two-Phase Commit - 2PC) gera complexidade, 
            // lentidão e pontos de falha catastróficos. 
            // O Outbox funciona porque o Lançamento e o Evento usam o MESMO banco de dados. O mecanismo 
            // de log do banco (WAL) garante atomicidade pura via Unit of Work: ou grava ambos, ou dá rollback.
            //
            // 2. 
            // Se a API publicasse na mensageria direto na rota de criação, cairíamos no risco do 'Dual Write':
            // se o banco salvasse o lançamento, mas a rede oscilasse e o envio para a fila falhasse, o saldo 
            // nunca seria calculado. O Outbox no banco resolve isso garantindo que a informação de que um 
            // lançamento aconteceu nunca seja perdida. O Worker lê do banco porque ele é a 'Fonte da Verdade'.
            //
            // 3. GESTÃO DO CORAÇÃO DO FLUXO DE CAIXA (SOMA EM LOTE)
            // Em sistemas bancários, se este Worker fizesse um UPDATE no banco para cada evento processado, 
            // haveria um gargalo massivo de Row-Locking (Deadlocks). Para resolver isso, movemos a computação 
            // matemática para a CPU: o repositório de Saldos inspeciona primeiro o Change Tracker Local (.Local). 
            // Se o evento anterior do loop já inicializou ou alterou o saldo daquela data, a mesma instância 
            // em memória é capturada e updated cumulativamente (somando/subtraindo os lançamentos).
            //
            // 4. MITIGAÇÃO DE CONNECTION POOL STARVATION VIA THROTTLING (PAGINAÇÃO DE LOTE)
            // Sob cenários de estresse massivo (ex: teste de carga de 50 RPS), buscar volumes irrestritos de 
            // eventos retém conexões com o DbContext por tempo excessivo dentro do laço longo. Isso causa o 
            // esgotamento do pool do ADO.NET (Pool Starvation) e derruba a API por timeout. Para garantir o 
            // SLA estável, o método 'ObterPendentesAsync' implementa Throttling via '.Take(100)'. O lote menor 
            // garante que o processamento seja ultrarápido, o escopo seja finalizado em milissegundos e as 
            // conexões retornem imediatamente ao pool elástico, blindando a API e mantendo a estabilidade.
            //
            // 5. 
            // O uso de NOLOCK é proibido em fluxos financeiros pois introduz o risco de 'Dirty Reads' (Leituras Sujas).
            // O Worker leria saldos de transações fantasmas que ainda não deram commit e que podem sofrer Rollback,
            // gerando quebras de caixa. A contenção de concorrência e Locks é resolvida aqui pelo Micro-batching:
            // ao processar tudo na CPU e dar apenas um único Commit no final do lote, reduzimos o tempo de 
            // travamento da tabela ao mínimo necessário. (Em alta escala, ativa-se o RCSI no banco).
            //
            // 6. CÁLCULO DE VAZÃO (THROUGHPUT) E ESCALABILIDADE ELÁSTICA LINEAR
            // A calibração atual está configurada de forma conservadora para ambiente de desenvolvimento:
            // Com um lote fixo de 100 registros (.Take(100)) executado a cada 3 segundos (Task.Delay(3000)),
            // o Worker atinge uma vazão controlada de ~33,3 eventos processados por segundo (RPS).
            // ESCALABILIDADE DE NÍVEL BANCÁRIO: Graças à blindagem do 'AddDbContextPool' e da agregação na CPU,
            // para escalar o sistema para suportar mais de 5.000 transações por segundo em produção, basta
            // reajustar variáveis de ambiente reduzindo o batimento para 100ms e elevando o lote para '.Take(500)'.
            // O sistema escalará linearmente mantendo o I/O do SQL Server baixo (apenas 10 commits por segundo).
            //
            // 7. ESTRATÉGIA PARA O CRESCIMENTO INDEFINIDO DA TABELA E COMBINAÇÃO COM GUID V7
            // Como esta tabela sofre alta escrita, o acúmulo de milhões de registros históricos geraria 
            // lentidão extrema (Table Scans). Solucionamos isso combinando o Guid v7 ao Índice Filtrado:
            //
            // A) GUID V7 (ESCRITA): Por ser baseado em timestamp, o Guid v7 é naturalmente sequencial. Isso 
            //    garante que as novas inserções da API entrem sempre no fim das páginas físicas do disco, 
            //    eliminando o Page Splitting e mitigando a fragmentação do índice no banco (I/O otimizado).
            //
            // B) ÍNDICE FILTRADO (LEITURA): Criação de um Non-Clustered Index cobrindo APENAS registros com 
            //    Status 'Pendente' ou 'Erro'. Como 99% da tabela histórica estará 'Processado', o índice 
            //    permanece incrivelmente leve. O banco realiza buscas cirúrgicas em uma árvore B-Tree sequencial, 
            //    mantendo a busca do Worker instantânea e independente do tamanho total da tabela.
            //
            // C) PRUNING: Rotina diária (Job) expurga ou move para Cold Storage eventos processados antigos.
            //
            // 8. 
            // Se outros sistemas (como Notificações ou BI) precisassem saber do lançamento, a mensageria 
            // seria disparada dentro deste laço, LOGO APÓS o cálculo do saldo e antes do Commit. O Worker 
            // atuaria como um Relay. Se a fila falhasse, o evento não seria marcado como processado, garantindo 
            // 'At-Least-Once Delivery' sem quebrar ou corromper a consistência do fluxo de caixa.
            //
            
            // 9. EVOLUÇÃO DE ESCALA MÁXIMA (CDC COM DEBEZIUM) & INVERSÃO DE DEPENDÊNCIA
            // Para escalar o sistema a níveis globais sem competir por conexões de banco de dados, o Worker 
            // em C# pode ser substituído por uma ferramenta de Change Data Capture (CDC) como o Debezium (Kafka Connect).
            // O Debezium lê diretamente os arquivos binários de log do SQL Server (Transaction Log), gerando impacto 
            // zero de I/O nas tabelas e transmitindo os eventos com latência de milissegundos.

            // 10. DESACOPLAMENTO (DIP): Como o sistema foi rigidamente implementado usando Inversão de Dependência,
            // o Core do Domínio e a API C# permanecem 100% INTACTOS. A API continua gravando o Outbox de forma atômica
            // via contratos abstratos, provando que a arquitetura isola regras de negócio de decisões de infraestrutura.
            //
            // BENEFÍCIO FINAL: N lançamentos são acumulados e somados em memória (CPU-Bound). O banco de dados 
            // sofre apenas UMA viagem de rede (I/O) no Commit final para atualizar o saldo de todas as transações 
            // daquele dia de uma vez só, garantindo consistência, atomicidade e performance extrema.
            //
            // =======================================================================================




            var eventos = await _uow.OutboxEvents.ObterPendentesAsync(cancellationToken);

            foreach (var evento in eventos)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var lancamento = JsonSerializer.Deserialize<Lancamento>(evento.Payload);
                    if (lancamento == null) continue;

                    var data = lancamento.DataCriacao.Date;

                    // =======================================================================================
                    // EVOLUÇÃO FUTURA DE ALTA ESCALA: CACHE-ASIDE (REDIS) PARA LEITURA CONCORRENTE
                    // ---------------------------------------------------------------------------------------
                    // Cenário Atual: O repositório mitiga buscas repetidas no mesmo lote inspecionando a memória 
                    // local (.Local). Contudo, a cada nova execução do Worker (novos lotes independentes), a primeira 
                    // leitura da data fatalmente gerará um I/O de Read no SQL Server para checar a existência do dia.
                    //
                    // Solução de Escala Global: Centralizar o estado do saldo do dia atual no Redis. O Worker lerá 
                    // o saldo corrente da memória RAM compartilhada. Se a chave existir (Cache Hit), o processador 
                    // evita o 'Select' no banco relacional, atualiza a entidade e faz o 'Write-Through' (atualiza o 
                    // Redis e agenda o commit final no SQL). O banco de dados passa a ser uma camada de persistência 
                    // estritamente de escrita (Write-Heavy Append-Only), maximizando o throughput da API.
                    // =======================================================================================

                    var saldo = await _uow.SaldosConsolidados.ObterPorDataAsync(DateOnly.FromDateTime(data), cancellationToken);

                    if (saldo == null)
                    {
                        var dataOntem = data.AddDays(-1);

                        //Aqui tambem cade o uso do redis para evitar a viagem de rede ao banco de dados, buscando o saldo do dia anterior em memória
                        var saldoOntem = await _uow.SaldosConsolidados.ObterPorDataAsync(DateOnly.FromDateTime(dataOntem));
                        decimal valorSaldoAnterior = saldoOntem?.Saldo ?? 0;

                        saldo = SaldoConsolidado.CriarComLancamento(lancamento, valorSaldoAnterior);
                        await _uow.SaldosConsolidados.AdicionarAsync(saldo);
                    }
                    else
                    {
                        saldo.AplicarLancamento(lancamento);
                       
                    }

                   
                    evento.MarcarComoProcessado();
                }
                catch
                {
                    evento.MarcarComoErro();
                  
                }
            }

            if (eventos.Any())
            {
                // Quando o Commit roda, o EF Core varre os objetos rastreados,
                // descobre quem mudou e faz todos os UPDATEs em um único lote seguro.
                await _uow.CommitAsync(cancellationToken);
            }
        }
    }
}
