# Architecture Decision Records (ADRs)

Este documento registra as decisões arquiteturais críticas tomadas durante o design e desenvolvimento do sistema de Fluxo de Caixa, detalhando o contexto, a justificativa técnica e as consequências de cada escolha.

---

## 📄 ADR 01: Adoção do Transactional Outbox Pattern

### Status
Aprovado

### Contexto
O sistema precisa garantir que cada lançamento financeiro registrado (crédito ou débito) resulte em uma atualização precisa e confiável do saldo diário consolidado, além de preparar o terreno para integrações futuras com outros microsserviços de forma assíncrona. Tentar recalcular o saldo ou disparar notificações de rede diretamente na requisição síncrona da API introduz lentidão e o risco de falhas catastróficas por indisponibilidade de infraestrutura.

### Decisão
Implementação do padrão Transactional Outbox. No momento da criação do lançamento, a API persiste o registro do Lancamento e a intenção do evento na tabela OutboxEvents de forma síncrona, sob o escopo della mesma transação local do banco de dados (ACID) coordenada pelo padrão Unit of Work. Um serviço em segundo plano (OutboxWorker) processa esses eventos de forma assíncrona a cada 3 segundos.

### Detalhes Técnicos de Resiliência de Rede
O banco de dados e o broker de mensageria são sistemas independentes. Não existe uma transação ACID nativa que englobe ambos (Dual Write). Tentar usar Two-Phase Commit (2PC) gera lentidão catastrófica. O Outbox no banco resolve isso garantindo que a informação de que um lançamento aconteceu nunca seja perdida. O Worker atua como um Relay (Retransmissor). Se o destino falhar, o evento permanece pendente, garantindo a política de entrega At-Least-Once Delivery sem corromper a consistência do fluxo de caixa.

### Evolução de Escala Máxima (CDC com Debezium)
Para escalar a arquitetura a níveis globais sem competir por conexões, o Worker em C# pode ser substituído por ferramentas de Change Data Capture (CDC) como o Debezium (Kafka Connect). O Debezium lê diretamente os arquivos binários de log do SQL Server (Transaction Log), gerando impacto zero de I/O nas tabelas e transmitindo os eventos com latência de milissegundos. Graças ao uso estrito de Inversão de Dependência (DIP), o Core do Domínio e a API C# permaneceriam 100% intactos nessa transição.

### Consequências
* **Positivas:** Eliminação total do risco de Dual Write (gravar o dado mas falhar ao gerar o evento). Alta responsabilidade e tempo de resposta otimizado na API. Garantia de consistência eventual confiável.
* **Negativas:** Introduz a necessidade de gerenciar o crescimento físico da tabela de Outbox no banco de dados através de políticas de expurgo (Pruning).

---

## 📄 ADR 02: Uso de UUIDv7 para Identificadores Universais Sequenciais

### Status
Aprovado

### Contexto
O padrão Transactional Outbox exige que o payload do evento contenha o ID do lançamento antes que ele seja fisicamente salvo. O uso de chaves incrementais (IDENTITY) forçaria a aplicação a esperar o retorno do banco para descobrir o ID, gerando travas sequenciais. Por outro lado, GUIDs tradicionais (v4) são totalmente aleatórios e destroem a performance do banco devido à fragmentação severa de índices (Page Splitting).

### Decisão
Adoção do UUIDv7 para todas as chaves primárias de transações. O UUIDv7 incorpora um componente de data/hora (timestamp) nos seus primeiros 48 bits, tornando-o naturalmente sequencial ao longo do tempo.

### Consequências
* **Positivas:** Inserções físicas ocorrem sempre no final das páginas de disco, eliminando a fragmentação de índices e otimizando o I/O sob alta concorrência. Funciona como chave de idempotência global nativa para os consumidores. Permite gerar o ID na aplicação antes do insert.
* **Negativas:** Armazenamento ligeiramente maior (16 bytes) quando comparado a inteiros tradicionais de 4 ou 8 bytes.

---

## 📄 ADR 03: Data como Chave Primária e Índice Clusterizado no Saldo

### Status
Aprovado

### Contexto
O requisito de negócio exige estritamente a disponibilização do "saldo diário consolidado", e a rota de consulta lê os dados filtrando por uma data específica (api/saldos/{data}). Usar IDs numéricos artificiais introduziria a possibilidade de existirem duas linhas de saldo para a mesma data (inconsistência) e exigiria índices secundários para busca.

### Decisão
Definição della coluna Data (DateOnly) como a Chave Primária física della tabela SaldosConsolidados.

### Consequências
* **Positivas:** Garante uma restrição de unicidade a nível de banco, impedindo fisicamente a duplicação de relatórios para o mesmo dia. O motor do banco de dados cria um Índice Clusterizado sobre a data, organizando a tabela fisicamente em disco de forma sequencial por dia, reduzindo a complexidade della busca RESTful para uma busca binária ultra-rápida via Index Seek.
* **Negativas:** Restringe a granularidade do consolidado estritamente ao nível diário (caso o negócio mude para saldo por hora no futuro, a chave precisará ser revista).

---

## 📄 ADR 04: Agregação Cumulativa em Memória (Micro-batching) via ChangeTracker

### Status
Aprovado

### Contexto
Se o OutboxWorker realizasse um comando SQL de UPDATE direto no banco de dados para cada evento processado individualmente, o sistema sofreria com gargalos massivos de concorrência e travamento de linhas (Row-Locking / Deadlocks), especialmente em cenários com centenas de lançamentos para a mesma data no mesmo lote.

### Decisão
Implementação de uma estratégia de Micro-batching. O repositório inspeciona a propriedade .Local (Cache de Primeiro Nível do EF Core) antes de realizar consultas externas. Se o lote contiver múltiplos lançamentos para o mesmo dia, a mesma instância do saldo em memória RAM é capturada e atualizada cumulativamente na CPU. O CommitAsync é invocado apenas uma vez ao final do lote.

### Concorrência e Bloqueios de Tabela (Contenção de Locks)
O uso de instruções 'NOLOCK' é expressamente proibido neste fluxo financeiro devido ao risco crítico de Leituras Sujas (Dirty Reads) de transações fantasmas que podem sofrer Rollback. A contenção de concorrência e Locks é resolvida via Micro-batching: ao processar tudo na CPU e efetuar apenas um único Commit final por dia no encerramento do lote, reduz-se o tempo de travamento físico das tabelas do banco ao mínimo necessário.

### Consequências
* **Positivas:** Redução de até 80% nas viagens de rede (Roundtrips) e operações de escrita no banco de dados. Eliminação de conflitos de estado de rastreamento no Entity Framework (Detached State Exception).
* **Negativas:** Pequeno aumento temporário no consumo de memória RAM do Worker durante o processamento de lotes muito volumosos.

## 📄 ADR 05: Mitigação de Connection Pool Starvation via Throttling e DbContextPooling

### Status
Aprovado

### Contexto
Durante a execução do teste de estresse de 50 RPS utilizando o NBomber, a volumetria agressiva de concorrência esgotou o limite padrão de conexões simultâneas que o Entity Framework abria com o SQL Server, gerando cenários de Connection Pooling Starvation e derrubando a API por timeout. Isso ocorria porque o Worker do Outbox retinha conexões abertas por muito tempo ao processar lotes volumosos sem paginação.

### Decisão
Implementação do padrão de Throttling e Paginação de Lote via .Take(100) na busca de eventos, além de substituir o registro tradicional do banco de dados na API por AddDbContextPool e expandir o Max Pool Size=500 na Connection String.

### Calibração de Vazão (Throughput) e Dimensionamento
A calibração configurada para desenvolvimento utiliza um lote fixo de 100 registros (.Take(100)) executado a cada 3 segundos, atingindo uma vazão controlada de ~33,3 eventos por segundo (RPS). Para escalar o sistema para suportar mais de 5.000 transações por segundo em produção, basta reajustar variáveis de ambiente reduzindo o batimento do Hosted Service para 100ms e elevando o lote para .Take(500). O sistema escalará linearmente mantendo o I/O do SQL Server baixo (apenas 10 commits físicos por segundo).

### Consequências
* **Positivas:** O Worker finaliza o escopo de processamento em frações de milissegundos, liberando os sockets de volta para o pool. A API passou a reaproveitar instâncias do contexto em memória RAM, garantindo estabilidade contínua e mantendo o índice de perda estritamente inferior a 5% sob estresse.
* **Negativas:** Exige monitoramento do tamanho ideal do lote (Take) caso o tamanho médio do payload dos eventos aumente drasticamente.

---

## 📄 ADR 06: Isolamento Perimetral de Redes e Segregação de Tráfego de Infraestrutura

### Status
Aprovado

### Contexto
Disponibilizar endpoints de instrumentação técnica, como Health Checks e coleta de métricas, na mesma porta lógica e canal público de rotas de negócios expõe a API a vetores de ataque por varredura de vulnerabilidades, além de misturar o tráfego operacional [|]. Sob testes de estresse agressivos ou instabilidade nas rotas REST de negócios, as sondas de saúde automáticas de orquestradores sofrem timeouts por concorrência de rede, gerando falsos positivos de queda de contêiner.

### Decisão
Isolamento perimetral do Kestrel via injeção de escuta em múltiplas portas físicas. Configura-se a porta 7248 exclusivamente para rotas HTTPS públicas de negócios e documentação do Swagger UI [|]. Paralelamente, estabelece-se a porta 8081 para tráfego HTTP sem criptografia focado unicamente nas sondas /healthz, diagnósticos JSON e telemetria OpenTelemetry Protocol (OTLP). Adota-se o middleware condicional app.UseWhen() para contornar restrições globais de redirecionamento SSL neste canal.

### Consequências
* **Positivas:** Isolamento físico completo de tráfego de rede [|]. Garante que o monitoramento responda em regime estável (< 6ms) mesmo durante testes de carga máximos da API de negócios. Protege endpoints sensíveis de infraestrutura contra acessos da internet pública.
* **Negativas:** Eleva a complexidade de infraestrutura e mapeamento de portas lógicas na tabela do arquivo Docker Compose.

---

## 📄 ADR 07: Telemetria Unificada com OpenTelemetry, Índice Filtrado e Aspire Dashboard

### Status
Aprovado

### Contexto
Sistemas financeiros de alta disponibilidade demandam monitoramento ativo e em tempo real sobre latência, taxas de requisições por segundo (RPS) e ciclos do Garbage Collector. Abordagens tradicionais baseadas no desenvolvimento de rotas locais que fabricam grandes arquivos de texto /metrics (modelo Prometheus Scrape) oneram a CPU da aplicação devido aos constantes ciclos de alocação de memória e serialização síncrona a cada ciclo de coleta de rede. Além disso, o acúmulo contínuo de milhões de registros históricos de eventos processados causaria lentidão nas varreduras do Worker devido a Table Scans desnecessários.

### Decisão
Substituição de exportadores baseados em rotas locais de texto pelo protocolo nativo unificado OpenTelemetry Protocol (OTLP) em background por meio do método estável services.AddMetrics() do .NET 10. A API e o Worker descarregam suas telemetrias nativas de hardware de forma assíncrona para a imagem oficial do .NET Aspire Dashboard (porta 18889).

Para mitigar a degradação de leitura histórica, combina-se a escrita sequencial do Guid v7 (que elimina o Page Splitting nas inserções) à criação de um índice não-clusterizado configurado com filtro rigoroso via Fluent API: `.HasFilter("[Status] = 1 OR [Status] = 3")`, correspondendo estritamente a registros com status 'Pendente' (1) ou 'Erro' (3) do Enum real della aplicação.

### Consequências
* **Positivas:** Zero impacto no desempenho de transações financeiras. O banco realiza buscas cirúrgicas em uma árvore B-Tree compacta contendo apenas a fila de trabalho ativa do dia, mantendo a varredura do Worker instantânea e independente do tamanho della tabela. Fornece interface gráfica rica fornecida pela Microsoft para auditorias visuais sob estresse.
* **Negativas:** Exige o provisionamento de um contêiner adicional e a governança de uma rotina diária de expurgo (Pruning) para descarregar dados processados antigos para Cold Storage.

---

## 📄 ADR 08: Evolução Arquitetural para Cache-Aside e Write-Through com Redis

### Status
Proposto (Evolução Futura)

### Contexto
No modelo atual, o repositório mitiga buscas repetidas no mesmo lote inspecionando a memória local do Change Tracker (`.Local`). Contudo, a cada nova execução independente do Worker (novos laços de repetição), a primeira leitura de uma data gera obrigatoriamente uma operação física de leitura (Read I/O) no SQL Server para checar se o registro do dia já existe, concorrendo por conexões com a API de negócios.

### Decisão
Aprovou-se o design conceitual para centralizar e encapsular o estado do saldo corrente do dia atual em uma camada de memória RAM distribuída utilizando o **Redis** (padrão *Cache-Aside*). O Worker passa a buscar o saldo corrente na memória compartilhada.
* Se a chave existir (*Cache Hit*), o sistema evita o `SELECT` no banco relacional, realiza a computação matemática na CPU e atualiza o estado usando *Write-Through* (sincroniza o Redis e agenda o commit final assíncrono no SQL Server).
* O banco de dados relacional passa a atuar estritamente como uma camada de persistência de escrita (*Write-Heavy Append-Only*), blindando as tabelas transacionais contra concorrência de leitura.

### Consequências
* **Positivas:** Redução drástica das operações de leitura no banco SQL Server, permitindo escalabilidade elástica linear e maximizando o throughput geral da API para suportar cenários globais de alta volumetria.
* **Negativas:** Eleva a complexidade della infraestrutura ao introduzir um novo componente de rede (Redis) e exige governança estrita sobre políticas de invalidação de cache e tratamento de inconsistências temporárias em cenários de partição de rede.
