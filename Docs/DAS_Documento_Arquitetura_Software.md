# Documento de Arquitetura de Software (DAS)

**Projeto:** Sistema de Controle de Fluxo de Caixa Diário  
**Autor:** Fábio Santos (fabioss10)  
**Tecnologia Base:** .NET 10 (C#) / SQL Server / Docker  
**Versão:** 1.1 (Atualizado com Segurança, Observabilidade e Portabilidade)  

---

## 1. Introdução

### 1.1 Objetivo do Documento
Este documento fornece uma visão geral arquitetural abrangente do Sistema de Fluxo de Caixa Diário [|]. Ele serve como guia para a governança do código, decisões de infraestrutura, modelagem de dados e validação de requisitos não-funcionais, assegurando que o sistema atenda às demandas de alta volumetria e consistência exigidas pelo setor bancário.

### 1.2 Escopo do Sistema
O ecossistema é composto por uma Web API protegida e um serviço em segundo plano (Worker) integrados sob a mesma base transacional. O sistema gerencia de forma ágil o registro de lançamentos financeiros e consolida os saldos de forma assíncrona, eliminando gargalos de I/O em tempo real.

---

## 2. Metas e Restrições Arquiteturais

### 2.1 Metas (Atributos de Qualidade / Requisitos Não-Funcionais)
* **Atomicidade Crítica:** Lançamentos e metadados de eventos nunca podem divergir. Falhas na rede ou na infraestrutura não podem gerar estados inconsistentes (Consistência Transacional).
* **Alta Vazão de Escrita (OLTP):** A API de entrada deve responder em tempo mínimo (latência de milissegundos), delegando processamentos pesados para segundo plano.
* **Escalabilidade Concorrente:** O recálculo de saldos de relatórios não pode gerar travamentos (locks) nas tabelas transacionais, mesmo sob alta concorrência de acessos.
* **Manutenibilidade e Testabilidade:** O código deve possuir acoplamento fraco, permitindo a substituição de frameworks e a execução de testes unitários isolados de I/O.
* **Disponibilidade e Vazão sob Carga:** O serviço de consulta de saldo consolidado foi projetado e testado para suportar uma vazão constante de 50 requisições por segundo (RPS), operando com uma taxa de perda ou erro estritamente inferior a 5%. Esta meta é atingida na infraestrutura física graças ao Índice Clusterizado gerado pela Chave Primária por Data na tabela SaldosConsolidados, permitindo buscas instantâneas via Index Seek, liberando as conexões do pool de forma ultra-rápida e mitigando gargalos de timeouts HTTP sob estresse.
* **Segurança e Isolamento de Tráfego:** A aplicação mitiga vetores de ataque e concorrência de rede ao implementar autenticação criptográfica via JWT Bearer [|]. Adicionalmente, adota isolamento perimetral de rede, dividindo fisicamente as portas de entrada de dados públicos das rotas internas de telemetria [|].
* **Observabilidade Pró-Ativa:** A API expõe telemetria avançada em tempo real sobre seu comportamento interno e integridade dos recursos, permitindo que orquestradores tomem ações preditivas de escalonamento antes que ocorram falhas de indisponibilidade de negócios.

### 2.2 Restrições Técnicas
* A aplicação deve ser desenvolvida em C# utilizando a plataforma .NET 10.
* O banco de dados relacional deve ser o Microsoft SQL Server.
* É obrigatória a inclusão de uma suíte de Testes Automatizados (Unitários e de Performance).
* Toda a solução deve ser portável e autocontida via Docker Compose, independente de dependências pré-instaladas no sistema hospedeiro.

---

## 3. Visão Lógica (Padrões de Arquitetura e Design)

O ecossistema foi estruturado sob os preceitos de Clean Architecture e Domain-Driven Design (DDD), dividindo-se em componentes desacoplados:

### 3.1 Camada de Domínio (Rich Domain Model)
As entidades (Lancamento, OutboxEvent, SaldoConsolidado) contêm o estado e o comportamento de negócio. Utilizam propriedades com modificadores private set e construtores específicos para blindar a integridade das regras financeiras no momento de sua criação, em conformidade com o princípio Fail-Fast.

### 3.2 Camada de Aplicação (Services e Contracts)
Contém os casos de uso do sistema (CriarLancamentoService, ConsultarSaldoService, ProcessadorOutboxService). Esta camada é totalmente agnóstica a bancos de dados, segurança ou frameworks, e se comunica com o mundo externo estritamente por interfaces de acesso abstratas (DIP - Dependency Inversion Principle).

### 3.3 Camada de Infraestrutura e Persistência
Implementa o padrão Unit of Work gerenciando o ciclo de vida do DbContext do Entity Framework Core 10. Centraliza as transações físicas em lote através de conexões otimizadas via AddDbContextPool (limitado a 1024 instâncias em memória), maximizando o reuso de conexões físicas e mitigando gargalos de esgotamento de sockets sob estresse.

### 3.4 Encapsulamento de Infraestrutura de Segurança e OpenAPI
Seguindo o Princípio da Responsabilidade Única (SRP), o arquivo Program.cs foi completamente despoluído. Toda a lógica de infraestrutura técnica de autenticação, autorização de escopos e transformadores do OpenAPI v3.1 foi centralizada na classe de extensão DependencyInjection.cs dentro della camada de Infraestrutura, estendendo o contêiner nativo IServiceCollection de forma modular e altamente manutenível.

## 4. Mecanismos Arquiteturais Chave

O sistema soluciona os desafios tradicionais de consistência distribuída e performance através de mecanismos principais:

### 4.1 Transactional Outbox Pattern
Para evitar o problema do Dual Write na API (gravar o lançamento mas falhar ao atualizar o saldo ou ao notificar outros sistemas por oscilação de rede), o Lançamento e o Evento de auditoria são gravados na mesma transação local do banco de dados (ACID). O OutboxWorker (Hosted Service) varre esta tabela a cada 3 segundos, processando os eventos pendentes com segurança.

### 4.2 Micro-batching via Cache de Primeiro Nível (.Local)
O processamento em segundo plano opera em lotes. Para evitar concorrência física de linhas no banco (Row-Locking / Deadlocks), o repositório inspeciona o Change Tracker em memória do EF Core (_context.Set().Local) antes de realizar requisições na rede. Múltiplos lançamentos da mesma data sofrem a computação matemática cumulativa na CPU e resultam em apenas uma única escrita consolidada por dia no banco de dados.

### 4.3 Otimização de Chaves e Indexação
* **UUIDv7:** Utilizado para os identificadores globais. Por possuir um componente de tempo (timestamp) sequencial nos bits iniciais, as inserções ocorrem sempre no final das páginas de disco, minimizando o Page Splitting e otimizando o I/O sob alta carga.
* **Chave Primária por Data:** A tabela de saldo consolidado utiliza a própria Data como chave física. Isso gera um índice clusterizado nativo, ordenando a tabela em disco por dia e reduzindo a complexidade de busca da rota RESTful para uma busca binária via Index Seek.
* **Índice Filtrado para o Outbox:** O banco gerencia um índice composto não-clusterizado configurado via Fluent API (.HasFilter()), contendo apenas registros onde Status for igual a Pendente ou Erro. Como 99% da tabela histórica em produção estará marcada como Processado, o índice permanece extremamente leve e residente na memória RAM, garantindo buscas instantâneas para o Worker.

### 4.4 Autenticação e Autorização Rígida de Tokens (RBAC e Claims-Based)
O barramento e proteção de dados ocorrem na borda do pipeline do ASP.NET Core através do middleware nativo UseAuthentication [|]. 
* **Regras Estritas de Validação:** Configurado com ClockSkew igual a TimeSpan.Zero para eliminar a tolerância padrão de 5 minutos do .NET, forçando a expiração matemática exata do token. Chaves e emissores são extraídos dinamicamente do appsettings.json via mapeamento fortemente tipado JwtOptions.
* **Validação por Assert em Lote:** Para suportar o padrão OAuth 2.0 onde escopos viajam como arrays JSON no mesmo nó ("scope": ["read", "write"]), o mecanismo de autorização utiliza o método .RequireAssertion(). Isso impede falhas de parse de tipos (array de string versus string comum) e valida a presença exata das claims WritePolicy e ReadPolicy.
* **Customização de Falhas Seguras:** Erros de tokens ausentes, corrompidos (401 Unauthorized) ou falta de nível de acesso (403 Forbidden) são interceptados na raiz do protocolo através dos delegados nativos JwtBearerEvents, devolvendo payloads JSON estruturados e padronizados aos clientes em vez de páginas em branco ou estouros de pilha.

### 4.5 Isolamento Perimetral de Redes (Kestrel Multi-Listening)
O servidor Kestrel foi configurado via ConfigureKestrel para escutar e responder em duas portas físicas totalmente segregadas:
* **Porta de Negócios (7248):** Canal público criptografado via HTTPS focado estritamente na exposição dos endpoints de controllers de negócios e documentação Swagger UI.
* **Porta de Infraestrutura (8081):** Canal privado exposto em texto claro (HTTP simples), exclusivo para consumo de orquestradores e telemetria interna. Qualquer requisição de negócios direcionada a esta porta, ou tentativas de monitoramento chamadas na porta 7248, recebem automaticamente o status 404 Not Found.
* **Filtro de Redirecionamento Condicional:** Para evitar que chamadas HTTP na porta 8081 caiam no loop global de segurança, o pipeline implementa o método de decisão app.UseWhen(), isolando o middleware UseHttpsRedirection apenas para requisições de negócios de portas diferentes de 8081.

### 4.6 Telemetria Avançada e Sincronismo Duplo de Saúde
A observabilidade do sistema atende aos padrões de missão crítica de alta disponibilidade através de dois pilares independentes de processamento:
* **Estratégia Dupla de Health Checks:** Mapeia a rota /healthz (texto puro, resposta em microsegundos) para sondas automatizadas de orquestradores de rede; e a rota /healthz/detail, que aciona o formatador isolado de infraestrutura HealthCheckResponseWriter. Esta última devolve um payload JSON identado com o consumo de RAM em megabytes do processo físico (Process.GetCurrentProcess().WorkingSet64) e testes de latência de rede com o banco via CanConnectAsync, mitigando queries pesadas de varredura de tabelas.
* **Instrumentação Nativa via OpenTelemetry Protocol (OTLP):** A aplicação adota o que há de melhor e mais moderno no .NET 10. Ela remove o uso de pacotes de terceiros instáveis (beta) e utiliza os medidores de hardware nativos do runtime (System.Runtime, System.Net.Http) por meio do método de altíssimo desempenho services.AddMetrics(). Os dados consolidados de vazão (RPS) e latência de rotas são descarregados de forma assíncrona em background via AddOtlpExporter diretamente para a imagem oficial do .NET Aspire Dashboard, mantendo a API livre de processamento concorrente de relatórios gráficos.

---

## 5. Estratégia de Implantação e Portabilidade (Docker Compose)

A solução resolve por completo a dependência de infraestruturas locais de terceiros através de um ecossistema multi-container totalmente portátil gerenciado pelo arquivo docker-compose.yml na raiz della solução.

* **Provisionamento Automatizado do Banco:** O contêiner do SQL Server mapeia o script banco_completo.sql em um volume ligado ao diretório /docker-entrypoint-initdb.d/. O container aplica as estruturas e tabelas automaticamente na subida física do serviço.
* **Segurança Inter-Container:** As variáveis de rede utilizam o DNS interno do Docker para comunicação (Server=fluxocaixa-db). A API ativa a flag OTEL_EXPORTER_OTLP_INSECURE=true para forçar o descarregamento gRPC de métricas em texto claro estritamente dentro della rede virtual isolada do Docker, blindando as portas contra vazamentos externos.

---

## 6. Estratégia de Testes

A qualidade, segurança e capacidade de carga da solução são validadas de forma determinística e isolada, segregando a suíte de testes em duas categorias via código:

### 6.1 Testes de Regressão e Regras de Negócio (Rápidos)
Executados de forma isolada de I/O através das bibliotecas xUnit e Moq.
* **Testes de Escrita Transacional:** Verificam se a service adiciona as duas entidades em memória e dispara o commit unificado no final.
* **Testes de Lote e Resiliência:** Simulam o comportamento do Change Tracker em memória local para atestar o cálculo cumulativo correto dos saldos e provam que falhas de parse ou JSON isolam o registro corrompido sem derrubar a execução do laço para os demais registros legítimos.
* **Testes de Leitura Defensiva:** Garantem que ausências de registro no banco de dados sejam interceptadas amigavelmente pela service, construindo respostas com valores zerados em vez de repassar nulos e gerar exceções de tela.

### 6.2 Testes de Performance, Carga e Estresse (NBomber)
Integrados ao xUnit, os cenários de carga utilizam o motor de alta performance do NBomber para validar se a infraestrutura cumpre as metas do setor bancário.
* **Autenticação Automática Pré-Load:** O teste realiza uma chamada síncrona prévia ao endpoint mock /api/Auth/login-analista, captura o token JWT gerado e o injeta dinamicamente no cabeçalho padrão AuthenticationHeaderValue do HttpClient. Isso evita o estresse concorrente do endpoint de autenticação e valida a segurança legítima das policies do pipeline.
* **Segregação Obrigatória via Trait (CI/CD):** Para impedir que testes de carga pesados rodem acidentalmente e travem ou poluam as esteiras automáticas de integração contínua (CI/CD), o método de performance é obrigatoriamente decorado com o atributo [Trait("Category", "Performance")], permitindo a exclusão matemática via terminal através do comando de rotina: dotnet test --filter "Category!=Performance".

