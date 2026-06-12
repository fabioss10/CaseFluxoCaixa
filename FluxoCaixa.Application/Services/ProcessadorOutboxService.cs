using FluxoCaixa.Application.Interfaces;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Interfaces;
using System.Text.Json;

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
            // <summary>
            /// Serviço responsável pelo processamento assíncrono do Transactional Outbox.
            /// Executa agregação cumulativa em lote (Micro-batching) na CPU para otimizar escritas.
            /// </summary>
            /// <remarks>
            /// NOTA DE ARQUITETURA: As justificativas detalhadas sobre resiliência de rede, 
            /// mitigação de Pool Starvation, índices filtrados e escalonabilidade via OTLP/CDC 
            /// estão na documentação formal do projeto em: ADR 01, ADR 04, ADR 05 e ADR 07.
            /// </remarks>


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

                    // NOTA: A estratégia de resiliência e a evolução futura para Cache-Aside (Redis)
                    // para mitigar I/O de leitura nesta linha estão documentadas formalmente na ADR 08.

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
