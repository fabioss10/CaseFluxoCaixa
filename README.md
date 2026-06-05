# Sistema de Controle de Fluxo de Caixa Diário

Este projeto consiste em uma solução robusta e resiliente para o controle de fluxo de caixa de comerciantes. Ele possibilita o registro assíncrono de lançamentos (débitos e créditos) e fornece relatórios consolidados de saldo diário com foco em alta performance, resiliência e atomicidade transacional.

---

## Como Rodar a Aplicação Localmente

### 1. Pré-requisitos Técnicos
Antes de iniciar, certifique-se de ter instalado em seu ambiente:
* SDK do .NET 10.0 (ou superior)
* Microsoft SQL Server (instância SQLEXPRESS ativa localmente)

### 2. Clonagem do Projeto
Abra o terminal da sua máquina, escolha uma pasta de trabalho e execute o comando:
```bash
git clone https://github.com/fabioss10
cd CaseFluxoCaixa
```

### 3. Configuração da Base de Dados
Abra o arquivo `appsettings.json` localizado na raiz do projeto da WebAPI (`FluxoCaixa.Api`) e certifique-se de que a string de conexão está apontando para o seu servidor local conforme a configuração homologada:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=FluxoCaixaDb;Trusted_Connection=True;TrustServerCertificate=True"
}
```

### 4. Execução do Script SQL
O banco de dados utiliza tabelas otimizadas para UUIDv7 e chaves baseadas em datas. Execute o script `banco_completo.sql` (disponível na raiz do repositório) diretamente no seu gerenciador de banco de dados (SQL Server Management Studio ou Azure Data Studio) para provisionar a estrutura física.

### 5. Inicialização do Ecossistema (API + Swagger)
A aplicação unifica o recebimento da API, a documentação Swagger e o processamento em segundo plano. Para rodar o ecossistema, execute o comando abaixo a partir da pasta raiz:
```bash
dotnet run --project src/FluxoCaixa.Api/FluxoCaixa.Api.csproj
```
A documentação interativa das rotas estará acessível automaticamente através do endereço do Swagger configurado no pipeline do .NET 10 em seu navegador.

### 6. Execução dos Testes Unitários
Para validar a integridade matemática e comportamental do sistema, execute os testes automáticos via terminal utilizando o comando:
```bash
dotnet test
```

---

## 🏛️ Explicação do Sistema

A solução foi desenvolvida utilizando C# com o ecossistema do .NET 10, adotando princípios de Clean Architecture e Domain-Driven Design (DDD) para resolver de forma performática e resiliente o fluxo de caixa do comerciante. O funcionamento do sistema baseia-se em três etapas integradas:

### Validação de Borda e Escrita Rápida (API)
A entrada de dados é monitorada pelo padrão Fail-Fast através da biblioteca FluentValidation. Assim que um JSON de lançamento (crédito ou débito) atinge a controller, o sistema valida os dados de forma estrita. Se o payload for íntegro, a camada de aplicação cria simultaneamente o registro do Lançamento e a intenção de processamento na tabela de Outbox. Ambas as entidades são enviadas ao banco de dados em uma única transação atômica gerenciada pelo padrão Unit of Work. Isso garante propriedades ACID e impede falhas de escrita dupla.

### Consolidação Assíncrona em Lote (Worker)
Para desonerar a API e evitar lentidões ao comerciante, o cálculo do saldo do dia foi movido para o OutboxWorker (um Hosted Service em segundo plano que roda a cada 3 segundos). Esse Worker atua coletando os eventos pendentes no banco e acionando o processador de negócios. 

Para mitigar problemas de contenção de banco e travamento de linhas (Row-Locking), o processador utiliza uma estratégia de agregação em memória (Micro-batching) por meio do cache de primeiro nível (.Local) do Entity Framework Core. Múltiplos lançamentos que pertencem ao mesmo dia são recuperados, agrupados e somados diretamente na CPU do servidor. Ao término do lote, o Unit of Work dispara apenas um comando SQL de alteração para o banco de dados.

### Modelagem de Infraestrutura Otimizada (.NET 10 & SQL Server)
O banco de dados foi projetado para suportar alta concorrência e crescimento de dados através de duas decisões estratégicas:
* **UUIDv7 como Identificador:** Todas as chaves primárias de transações utilizam UUIDv7 gerados nativamente. Por possuírem uma marca de tempo cronológica sequencial embutida em seus bits iniciais, os registros são inseridos fisicamente sempre no final das páginas de disco do banco, eliminando o estresse de hardware gerado por Page Splitting.
* **Data como Chave do Saldo:** A tabela de saldo diario consolidado utiliza a própria Data como chave primária física. Isso cria uma restrição natural que impede relatórios duplicados para o mesmo dia e gera um índice clusterizado nativo, acelerando drasticamente as rotas de consultas RESTful do comerciante.

### Desacoplamento Arquitetural e Inversão de Dependência (DIP)

O projeto foi estruturado seguindo o princípio da Inversão de Dependência (DIP) do SOLID, garantindo que as regras de negócio residam no núcleo do sistema (camadas de Domínio e Aplicação) de forma totalmente agnóstica a detalhes de infraestrutura ou frameworks externos.

* **Isolamento do Domínio:** As entidades de negócio (`Lancamento`, `OutboxEvent` e `SaldoConsolidado`) não possuem qualquer acoplamento com o Entity Framework Core ou qualquer mecanismo de persistência física. O Domínio define estritamente o comportamento transacional financeiro e valida suas invariantes em memória.
* **Abstração da Camada de Dados via Interfaces:** A camada de Aplicação se comunica com a Persistência exclusivamente através de contratos abstratos (`IUnitOfWorkRepository`, `ILancamentoRepository`, etc.). Isso significa que, se no futuro o banco de dados SQL Server precisar ser substituído pelo PostgreSQL, ou o Entity Framework Core for trocado por Dapper para ganho de performance, a camada de negócio permanecerá 100% intacta, exigindo alterações apenas na camada de infraestrutura.
* **Testabilidade Determinística:** Este nível severo de desacoplamento é o que viabiliza a criação da suíte de testes unitários da aplicação. Utilizando a biblioteca Moq, conseguimos simular com precisão cirúrgica o comportamento do banco de dados e do Change Tracker em memória RAM, testando cenários complexos de concorrência, matemática financeira cumulativa e resiliência a falhas de dados sem abrir uma única conexão de rede real.

### Evoluções de Banco de Dados e Escalabilidade Futura

O sistema foi desenhado sob os preceitos de uma **Arquitetura Evolutiva**. Embora a modelagem atual atenda perfeitamente aos requisitos do comerciante com alta performance, o ecossistema está preparado para escalar para volumes massivos de transações (escala bancária) através das seguintes evoluções de infraestrutura de dados:

* **Habilitação do RCSI (Read Committed Snapshot Isolation):** Para eliminar completamente qualquer disputa de concorrência entre as operações de escrita do *Worker* e as consultas de leitura da API de Saldos, o banco de dados pode ser configurado com o isolamento RCSI. Isso permite que a API leia uma versão *snapshot* dos dados commitados instantaneamente, sem gerar travas (*locks*) e sem ser bloqueada pelas atualizações do processador de lote.
* **Tabelas Otimizadas para Memória (In-Memory OLTP):** A tabela de `SaldosConsolidados`, por ser o ponto central de maior modificação do sistema, pode ser convertida para uma tabela *In-Memory* com durabilidade total (`SCHEMA_AND_DATA`). Isso elimina o gargalo de travas de página em disco, permitindo que o cálculo de saldo atinja latências na casa dos microssegundos.
* **Estratégia de Pruning e Cold Storage para o Outbox:** Como os registros da tabela `OutboxEvents` perdem o valor transacional logo após serem processados com sucesso, deve-se implementar uma rotina agendada (Job/Cron) para expurgo (*Pruning*). Eventos com status `Processado` há mais de 3 dias são automaticamente deletados ou movidos em lote para uma base de histórico (*Cold Storage* ou *Data Lake*), mantendo a tabela principal e o seu índice filtrado sempre extremamente leves e residentes na memória RAM.
* **Particionamento de Tabelas por Data:** Conforme o histórico de lançamentos acumula dezenas de milhões de linhas ao longo dos anos, a tabela `Lancamentos` pode aplicar o particionamento físico em disco com base na coluna `DataCriacao` (ex: uma partição física por mês ou por ano). Isso otimiza rotinas de manutenção, acelera relatórios de BI e garante que queries históricas não impactem a performance das escritas do dia atual.




