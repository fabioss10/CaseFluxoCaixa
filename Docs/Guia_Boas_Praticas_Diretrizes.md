# Guia de Boas Práticas e Diretrizes de Desenvolvimento

Este documento estabelece as diretrizes arquiteturais, padrões de codificação e boas práticas que devem ser seguidos obrigatoriamente por todo o time de engenharia no desenvolvimento e evolução do Sistema de Fluxo de Caixa.

---

## 1. Princípios de Clean Code e SOLID

### 1.1 Single Responsibility Principle (SRP)
* **Diretriz:** Cada classe ou componente deve possuir apenas um único motivo para mudar. 
* **Aplicação no Projeto:** Os repositórios servem estritamente para persistência em memória local. Os serviços de aplicação (`Services`) coordenam os casos de uso. As entidades de domínio calculam as regras de negócio. Nunca misture consultas SQL ou lógica de infraestrutura dentro das regras de domínio.

### 1.2 Dependency Inversion Principle (DIP)
* **Diretriz:** Módulos de alto nível não devem depender de módulos de baixo nível. Ambos devem depender de abstrações.
* **Aplicação no Projeto:** As camadas de Domínio e Aplicação comunicam-se com a Infraestrutura estritamente por meio de interfaces (ex: `IUnitOfWorkRepository`). O uso de injeção de dependência via construtor é obrigatório. É expressamente proibido instanciar repositórios concretos ou o `DbContext` usando a palavra-chave `new` dentro de serviços ou controladores.

---

## 2. Design de Domínio (Rich Domain Model vs Anemic)

### 2.1 Encapsulamento Estrito e Invariantes
* **Diretriz:** É terminantemente proibido o uso de modelos de domínio anêmicos (classes que servem apenas como sacolas de dados com `get; set;` públicos). As entidades devem proteger seu próprio estado.
* **Aplicação no Projeto:**
  * Todas as propriedades devem utilizar o modificador **`private set`**.
  * A alteração de estado deve ocorrer exclusivamente por meio de métodos de comportamento explícitos (ex: `MarcarComoProcessado()`, `AplicarLancamento()`).
  * Validações de nascimento do objeto (invariantes de borda) devem lançar exceções imediatamente no construtor caso os dados sejam inválidos (Princípio *Fail-Fast*).

### 2.2 Construtores para o ORM
* **Diretriz:** Para garantir que o Entity Framework Core consiga materializar os objetos vindo do banco de dados sem contornar o encapsulamento, defina um construtor sem parâmetros com visibilidade **`private`**.

---

## 3. Estratégia de Persistência, Lote e Performance (EF Core)

### 3.1 Operações em Memória vs Operações de I/O
* **Diretriz:** Compreenda o comportamento do *Change Tracker* para evitar desperdício de recursos de rede e CPU.
  * Métodos de anúncio de mudança como `Add()`, `Update()` e `Remove()` operam estritamente sobre os ponteiros de memória local. **Não deve ser adicionado o parâmetro `CancellationToken`** a esses métodos, pois eles rodam de forma instantânea na CPU.
  * Métodos que disparam viagens físicas de rede (I/O bloqueante) como `ToListAsync()`, `FirstOrDefaultAsync()` e `SaveChangesAsync()` **devem obrigatoriamente aceitar e propagar o `CancellationToken`** para garantir o suporte a *Graceful Shutdown*.

### 3.2 Otimização de Escritas e Agregação Cumulativa (Micro-batching)
* **Diretriz:** Nunca invoque o método `SaveChangesAsync()` dentro de laços de repetição (`foreach` / `while`).
* **Aplicação no Projeto:** Múltiplas alterações devem ser acumuladas no Change Tracker. O repositório deve inspecionar a propriedade `.Local` antes de efetuar buscas externas. Se o lote contiver múltiplos lançamentos para o mesmo dia, a mesma instância do saldo em memória RAM é capturada e atualizada cumulativamente na CPU. O envio físico de comandos SQL deve ser centralizado em uma única chamada atômica do `CommitAsync()` do *Unit of Work* ao final do lote, reduzindo em até 80% as viagens de rede (*Roundtrips*).

### 3.3 Mitigação de Connection Pool Starvation via Throttling
* **Diretriz:** Serviços em segundo plano (*Workers*) nunca devem realizar consultas volumosas irrestritas à tabela de Outbox, pois isso retém sockets de rede por tempo excessivo, sufocando o pool de conexões da API.
* **Aplicação no Projeto:** O método `ObterPendentesAsync` implementa paginação estrita através do operador `.Take(100)`. Lotes menores garantem processamento ultrarápido, liberação imediata de recursos de volta ao pool do ADO.NET (configurado com `Max Pool Size=500`) e estabilidade da API sob estresse de 50 RPS.

---

## 4. Governança de Validação, Resiliência e Mensageria

### 4.1 Validação de Entrada (Edge Validation)
* **Diretriz:** Falhe o mais rápido possível na borda do sistema para poupar processamento e proteger a estabilidade do banco.
* **Aplicação no Projeto:** Toda validação de formato de JSON, limites de valores e integridade estrita de Enums deve ser feita na camada de entrada via **FluentValidation** com regras como `.IsInEnum()`. Payload corrompido não deve ultrapassar a Controller, retornando automaticamente um `HTTP 400 Bad Request`.

### 4.2 Isolamento de Falhas em Background (Graceful Degradation)
* **Diretriz:** Falhas em dados individuais de processamento em lote não podem derrubar o serviço em background (*Hosted Service*).
* **Aplicação no Projeto:** O laço de repetição do processador de Outbox deve envelopar cada iteração em escopos isolados de `try-catch`. Caso um evento sofra erro de deserialização ou negócio, ele deve ser marcado individualmente com status de `Erro` na memória, permitindo que o loop continue processando os registros subsequentes normais.

### 4.3 Garantia de Entrega e Idempotência (At-Least-Once Delivery)
* **Diretriz:** Sistemas financeiros não podem sofrer com perda de notificações por oscilação de rede (Dual Write).
* **Aplicação no Projeto:** O padrão *Transactional Outbox* garante que o evento nasça na mesma transação ACID do lançamento. O Worker atua como um *Relay* (Retransmissor). Se o destino (mensageria externa) estiver temporariamente offline, o evento não é marcado como processado. Como as chaves primárias utilizam **UUIDv7**, esses identificadores cronológicos sequenciais funcionam como **Chaves de Idempotência Globais** nativas, permitindo que os consumidores rejeitem reprocessamentos duplicados de forma determinística.---

## 5. Convenções de Código e Padrões Git

### 5.1 Nomenclatura de Testes Unitários
* **Diretriz:** Os testes devem seguir o padrão comportamental claro: `Metodo_Cenario_ComportamentoEsperado`.
* **Exemplo:** `ProcessarAsync_DeveConsolidarMultiplosLancamentosDoMesmoDiaEmMemoriaE_CommitaUmaUnicaVez`.

### 5.2 Padrão de Commits Semânticos (Conventional Commits) - Próximos Passos
* **Diretriz Futura Obrigatória:** Para garantir a rastreabilidade do histórico e viabilizar a automação de *Changelogs* em pipelines de CI/CD, **a partir da consolidação deste setup inicial**, todo o time de engenharia deverá adotar obrigatoriamente o padrão de Commits Semânticos para as novas ramificações (*branches*):
  * `feat:` Nova funcionalidade (ex: criação do validador de lançamentos).
  * `fix:` Correção de bug (ex: tratamento de exceção por estado desanexado no EF).
  * `docs:` Alterações em documentações (ex: atualização do C4 Model ou deste guia).
  * `test:` Adição ou refatoração de suítes de testes unitários ou de carga.

## 6. Padrões Avançados de Runtime e Gerenciamento de Recursos (.NET 10)

### 6.1 Evitando Captive Dependencies (Dependências Prisioneiras)
* **Diretriz:** Um serviço com ciclo de vida longo (*Singleton*) nunca deve injetar diretamente um serviço com ciclo de vida curto (*Scoped*), pois isso aprisiona o recurso em memória, impede a coleta do Garbage Collector e gera vazamento de memória (*Memory Leak*) e concorrência no DbContext.
* **Aplicação no Projeto:** O `OutboxWorker` é um `HostedService` (Singleton). Para consumir com segurança o `IProcessadorOutboxService` e o `IUnitOfWorkRepository` (que são Scoped), o Worker utiliza o padrão **`IServiceScopeFactory`**. A cada ciclo de 3 segundos, um escopo temporário é aberto e destruído via bloco `using`, garantindo a reciclagem correta dos objetos e a liberação das conexões do banco.

### 6.2 DbContext Pooling para Alta Vazão
* **Diretriz:** Em APIs que sofrem alta carga e concorrência (como o SLA de 50 RPS), instanciar e destruir o `DbContext` a cada requisição HTTP gera um custo alto de alocação de memória e abertura de sockets de rede.
* **Aplicação no Projeto:** O registro do banco de dados no `Program.cs` adota o **`AddDbContextPool<FluxoCaixaDbContext>`**. O .NET 10 passa a gerenciar um pool elástico de instâncias pré-alocadas em memória RAM. O reaproveitamento dos contextos reduz drasticamente a latência de inicialização e otimiza a reciclagem do pool de conexões do ADO.NET.

### 6.3 RESTful Semântico e Resiliência de Roteamento (HATEOAS-Friendly)
* **Diretriz:** Endpoints de escrita (`POST`) de APIs maduras devem retornar os metadados do recurso criado e apontar para a rota de consulta do recurso de forma pragmática e desacoplada do motor de roteamento interno do framework.
* **Aplicação no Projeto:** O método `Criar` da `LancamentosController` retorna um `HTTP 200 OK` injetando dinamicamente no corpo da resposta o ID gerado (`UUIDv7`) e a URL relativa de consulta (`UrlConsultaSaldoDoDia: "/api/saldos/YYYY-MM-DD"`). Isso provê capacidade de navegação para o cliente sem acoplar o sucesso da persistência a falhas de reflexão e geração de rotas (*No route matches*), blindando a resiliência do pipeline em produção.

### 6.4 Cláusulas de Guarda e Princípio Fail-Fast no Construtor (Defensive Programming)
* **Diretriz:** Métodos e construtores de classes críticas devem validar suas dependências obrigatórias logo na primeira linha de execução, impedindo que o sistema rode em estado inválido.
* **Aplicação no Projeto:** Todos os construtores da camada de aplicação utilizam Cláusulas de Guarda unindo o operador de coalescência nula ao lançamento de exceções: `_uow = uow ?? throw new ArgumentNullException(nameof(uow));`. Isso garante que falhas de configuração de injeção de dependência no `Program.cs` quebrem a aplicação imediatamente na inicialização (*Fail-Fast*), evitando erros enigmáticos de `NullReferenceException` no meio de uma transação financeira.

### 6.5 Segregação de Responsabilidade de Leitura e Escrita (CQRS Semântico)
* **Diretriz:** Serviços de consulta pura (*Read-Only*) não devem carregar o peso de gerenciamento de estado e transações de componentes de escrita.
* **Aplicação no Projeto:** O sistema aplica o conceito de segregação de responsabilidades. Enquanto os serviços de escrita (`CriarLancamentoService` e `ProcessadorOutboxService`) injetam o `IUnitOfWorkRepository` para coordenar transações complexas, o serviço de consulta (`ConsultarSaldoService`) injeta diretamente o repositório específico de leitura. Isso simplifica a assinatura das classes, economiza recursos do Change Tracker e prepara o sistema para uma futura separação física de bancos de dados de leitura e escrita (CQRS definitivo).

## 7. Modelagem Avançada de Dados, Contratos e Confiabilidade

### 7.1 Tipagem Cronológica com DateOnly vs DateTime
* **Diretriz:** Em domínios financeiros e de relatórios consolidados, tabelas agregadas por dia nunca devem utilizar o tipo `DateTime` para representar a chave temporal, pois a presença de horas, minutos e milissegundos quebra agrupamentos e exige conversões caras de banco de dados.
* **Aplicação no Projeto:** A tabela de `SaldosConsolidados` utiliza estritamente o tipo **`DateOnly`** em sua chave primária. Isso garante que o fuso horário (Timezone) do servidor não altere o dia da consolidação do saldo, forçando a integridade semântica do requisito de negócio de "Saldo Diário Consolidado" diretamente na tipagem da engine.

### 7.2 Mapeamento Defensivo de Fallback na Leitura (Null Interception)
* **Diretriz:** APIs de consulta nunca devem estourar exceções ou repassar nulos estruturais para o cliente caso o banco de dados não possua registros para a chave informada.
* **Aplicação no Projeto:** O método `ObterPorDataAsync` da `ConsultarSaldoService` intercepta o retorno nulo do repositório de forma defensiva. Em vez de repassar a ausência de dados, o serviço constrói dinamicamente um DTO de resposta zerado e consistente (`Saldo = 0`, `TotalCreditos = 0`, `TotalDebitos = 0`, `UltimaAtualizacao = null`). Isso preserva o contrato previsível da API REST e blinda o frontend contra falhas de renderização.

### 7.3 Isolamento da Camada de Domínio contra Serialização (Agnosticismo de Formato)
* **Diretriz:** Entidades de Domínio Rico não devem carregar atributos de frameworks de serialização (como `[JsonPropertyName]` ou `[JsonIgnore]`) e nem ser expostas diretamente em assinaturas de API, sob o risco de vazamento de escopo.
* **Aplicação no Projeto:** A `CriarLancamentoService` cria o payload do Outbox utilizando um **objeto anônimo estruturado** durante a execução do `JsonSerializer.Serialize`. Isso garante que a entidade rica `Lancamento` permaneça com suas propriedades e comportamentos totalmente isolados, impedindo que mudanças futuras na estrutura interna da entidade quebrem retroativamente os payloads históricos que já estão gravados na fila de Outbox.

### 7.4 Abordagem SUT (System Under Test) e Isolamento de I/O em Cadeia
* **Diretriz:** Testes unitários limpos devem declarar explicitamente o alvo de teste através da convenção SUT, isolando-o de qualquer efeito colateral de rede através de injeções dinâmicas coordenadas (Mocks).
* **Aplicação no Projeto:** A suíte de testes declara a service real como `_sut` (System Under Test) e injeta nela instâncias mockadas via `_uowMock.Object`. Nos testes do processador de lote, implementamos o encadeamento de comportamentos (*Mock Callbacks*): o mock simula o comportamento real do Change Tracker do EF Core, capturando a primeira entidade nova e devolvendo a **mesma instância** na iteração subsequente do loop. Isso permite testar a lógica cumulativa complexa do micro-batching inteiramente na memória RAM, sem precisar de infraestrutura física.

### 7.5 Desacoplamento de Contratos via DTOs de Entrada e Saída (Request/Response)
* **Diretriz:** Controladores de API nunca devem expor ou receber entidades de domínio diretamente nas suas assinaturas. A variação de contratos externos deve ser blindada por objetos de transferência de dados dedicados.
* **Aplicação no Projeto:** O endpoint de criação recebe estritamente um `CriarLancamentoRequest` e a consulta devolve um `SaldoDiarioResponse`. Esse desacoplamento total permite que o banco de dados evolua internamente suas tabelas sem que os clientes integrados na API sofram qualquer impacto de quebra de contrato (Breaking Change).

