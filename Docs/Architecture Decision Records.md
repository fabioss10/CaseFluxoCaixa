# 📝 Architecture Decision Records (ADRs)

Este documento registra as decisões arquiteturais críticas tomadas durante o design e desenvolvimento do sistema de Fluxo de Caixa, detalhando o contexto, a justificativa técnica e as consequências de cada escolha.

---

## 📄 ADR 01: Adoção do Transactional Outbox Pattern

### Status
Aprovado

### Contexto
O sistema precisa garantir que cada lançamento financeiro registrado (crédito ou débito) resulte em uma atualização precisa e confiável do saldo diário consolidado, além de preparar o terreno para integrações futuras com outros microsserviços de forma assíncrona. Tentar recalcular o saldo ou disparar notificações de rede diretamente na requisição síncrona da API introduz lentidão e o risco de falhas catastróficas por indisponibilidade de infraestrutura.

### Decisão
Decidi implementar o padrão **Transactional Outbox**. No momento da criação do lançamento, a API persiste o registro do `Lancamento` e a intenção do evento na tabela `OutboxEvents` de forma síncrona, sob o escopo da mesma transação local do banco de dados (ACID) coordenada pelo padrão *Unit of Work*. Um serviço em segundo plano (`OutboxWorker`) processa esses eventos de forma assíncrona a cada 3 segundos.

### Consequências
* **Positivas:** Eliminação total do risco de *Dual Write* (gravar o dado mas falhar ao gerar o evento). Alta responsabilidade e tempo de resposta otimizado na API. Garantia de consistência eventual confiável.
* **Negativas:** Introduz a necessidade de gerenciar o crescimento físico da tabela de Outbox no banco de dados através de políticas de expurgo (*Pruning*).

---

## 📄 ADR 02: Uso de UUIDv7 para Identificadores Universais Sequenciais

### Status
Aprovado

### Contexto
O padrão Transactional Outbox exige que o payload do evento contenha o ID do lançamento antes que ele seja fisicamente salvo. O uso de chaves incrementais (`IDENTITY`) forçaria a aplicação a esperar o retorno do banco para descobrir o ID, gerando travas sequenciais. Por outro lado, GUIDs tradicionais (v4) são totalmente aleatórios e destroem a performance do banco devido à fragmentação severa de índices (*Page Splitting*).

### Decisão
Decidi adotar o **UUIDv7** para todas as chaves primárias de transações. O UUIDv7 incorpora um componente de data/hora (*timestamp*) nos seus primeiros 48 bits, tornando-o naturalmente sequencial ao longo do tempo.

### Consequências
* **Positivas:** Inserções físicas ocorrem sempre no final das páginas de disco, eliminando a fragmentação de índices e otimizando o I/O sob alta concorrência. Funciona como chave de idempotência global nativa para os consumidores. Permite gerar o ID na aplicação antes do insert.
* **Negativas:** Armazenamento ligeiramente maior (16 bytes) quando comparado a inteiros tradicionais de 4 ou 8 bytes.

---

## 📄 ADR 03: Data como Chave Primária e Índice Clusterizado no Saldo

### Status
Aprovado

### Contexto
O requisito de negócio exige estritamente a disponibilização do "saldo diário consolidado", e a rota de consulta lê os dados filtrando por uma data específica (`api/saldos/{data}`). Usar IDs numéricos artificiais introduziria a possibilidade de existirem duas linhas de saldo para a mesma data (inconsistência) e exigiria índices secundários para busca.

### Decisão
Decidi definir a coluna **Data (`DateOnly`)** como a Chave Primária física da tabela `SaldosConsolidados`.

### Consequências
* **Positivas:** Garante uma restrição de unicidade a nível de banco, impedindo fisicamente a duplicação de relatórios para o mesmo dia. O motor do banco de dados cria um Índice Clusterizado sobre a data, organizando a tabela fisicamente em disco de forma sequencial por dia, o que reduz a complexidade da query RESTful para $O(\log N)$ via *Index Seek*.
* **Negativas:** Restringe a granularidade do consolidado estritamente ao nível diário (caso o negócio mude para saldo por hora no futuro, a chave precisará ser revista).

---

## 📄 ADR 04: Agregação Cumulativa em Memória (Micro-batching) via ChangeTracker

### Status
Aprovado

### Contexto
Se o `OutboxWorker` realizasse um comando SQL de `UPDATE` direto no banco de dados para cada evento processado individualmente, o sistema sofreria com gargalos massivos de concorrência e travamento de linhas (*Row-Locking / Deadlocks*), especialmente em cenários com centenas de lançamentos para a mesma data no mesmo lote.

### Decisão
Decidi implementar uma estratégia de **Micro-batching**. O repositório inspeciona a propriedade `.Local` (Cache de Primeiro Nível do EF Core) antes de realizar consultas externas. Se o lote contiver múltiplos lançamentos para o mesmo dia, a mesma instância do saldo em memória RAM é capturada e atualizada cumulativamente na CPU. O `CommitAsync` é invocado apenas uma vez ao final do lote.

### Consequências
* **Positivas:** Redução de até 80% nas viagens de rede (*Roundtrips*) e operações de escrita no banco de dados. Eliminação de conflitos de estado de rastreamento no Entity Framework (`Detached State Exception`).
* **Negativas:** Pequeno aumento temporário no consumo de memória RAM do Worker durante o processamento de lotes muito volumosos.

## 📄 ADR 05: Mitigação de Connection Pool Starvation via Throttling e DbContextPooling

### Status
Aprovado

### Contexto
Durante a execução do teste de estresse de 50 RPS utilizando o NBomber, a volumetria agressiva de concorrência esgotou o limite padrão de conexões simultâneas que o Entity Framework abria com o SQL Server, gerando cenários de *Connection Pooling Starvation* e derrubando a API por timeout. Isso ocorria porque o Worker do Outbox retinha conexões abertas por muito tempo ao processar lotes volumosos sem paginação.

### Decisão
Decidi implementar o padrão de **Throttling e Paginação de Lote** via `.Take(100)` na busca de eventos, além de substituir o registro tradicional do banco de dados na API por **`AddDbContextPool`** e expandir o **`Max Pool Size=500`** na Connection String.

### Consequências
* **Positivas:** O Worker finaliza o escopo de processamento em frações de milissegundos, liberando os sockets de volta para o pool. A API passou a reaproveitar instâncias do contexto em memória RAM, garantindo estabilidade contínua e mantendo o índice de perda estritamente inferior a 5% sob estresse.
* **Negativas:** Exige monitoramento do tamanho ideal do lote (`Take`) caso o tamanho médio do payload dos eventos aumente drasticamente.

