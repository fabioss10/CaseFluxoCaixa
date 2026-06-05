# 🗺️ Visão Estrutural (Diagramas) - Modelo C4

Este documento descreve a arquitetura do sistema utilizando o **Modelo C4**, permitindo visualizar a solução desde o contexto de negócio até a decomposição interna dos componentes do ecossistema.

---

## 🛑 Nível 1: Diagrama de Contexto (System Context)
Mostra o escopo do sistema, como ele se posiciona no ecossistema de negócio e como os atores e sistemas externos interagem com ele.

```mermaid
C4Context
    title Diagrama de Contexto do Sistema de Fluxo de Caixa
    
    Person(comerciante, "Comerciante / Usuário", "Usuário do sistema que precisa registrar lançamentos e consultar o saldo consolidado.")
    System(sistemaFluxo, "Sistema de Fluxo de Caixa", "Gerencia o fluxo de caixa, registra débitos/créditos de forma atômica e consolida os saldos diários de forma assíncrona.")
    System_Ext(mensageria, "Broker de Mensageria (Evolução)", "Sistema externo (RabbitMQ/Kafka) para onde os eventos de outbox validados serão retransmitidos.")

    Rel(comerciante, sistemaFluxo, "Registra lançamentos e consulta saldos", "HTTP / JSON")
    Rel(sistemaFluxo, mensageria, "Publica eventos de lançamentos processados", "AMQP / TCP")
```

---

## 📦 Nível 2: Diagrama de Contêiner (Container Diagram)
Mostra a arquitetura de alto nível do sistema, dividida em contêineres executáveis (aplicações, APIs, bancos de dados) e as tecnologias utilizadas.

```mermaid
C4Container
    title Diagrama de Contêiner do Sistema de Fluxo de Caixa
    
    Person(comerciante, "Comerciante / Usuário", "Consumidor do ecossistema.")
    
    System_Boundary(sistema_boundary, "Fronteira do Sistema de Fluxo de Caixa") {
        Container(api, "Web API", "C# .NET 10 / ASP.NET Core", "Expõe os endpoints REST para criação de lançamentos e consulta de saldos. Executa validações de borda (Fail-Fast).")
        Container(worker, "Outbox Worker", "C# .NET 10 / Hosted Service", "Serviço em segundo plano executado em loop (Singleton) que consome os eventos e consolida os saldos em lote.")
        ContainerDb(banco, "Banco de Dados Transacional", "Microsoft SQL Server", "Armazena as tabelas de Lançamentos, Eventos de Outbox e os Saldos Diários Consolidados.")
    }

    Rel(comerciante, api, "Faz requisições HTTP", "HTTPS / JSON (Porta: 7248)")
    Rel(api, banco, "Grava Lançamento + OutboxEvent de forma atômica", "Entity Framework Core 10 / ACID")
    Rel(worker, banco, "Lê eventos pendentes e atualiza o saldo diário em lote", "Entity Framework Core 10 (.Local)")
```

---

## 🧱 Nível 3: Diagrama de Componente (Component Diagram)
Decompõe o contêiner da aplicação para mostrar os componentes lógicos internos e como as interfaces e padrões de projeto (Design Patterns) estão acoplados.

```mermaid
C4Component
    title Diagrama de Componentes Internos
    
    ContainerDb(banco, "Banco de Dados Transacional", "Microsoft SQL Server", "Persistência do ecossistema.")
    
    Container_Boundary(api_worker_components, "Componentes Lógicos Internos (.NET 10)") {
        Component(controller, "Lancamentos / Saldos Controllers", "ASP.NET Core Controller", "Ponto de entrada HTTP. Mapeia rotas e repassa o CancellationToken do HttpContext.")
        Component(validator, "CriarLancamentoRequestValidator", "FluentValidation", "Garante o princípio Fail-Fast na borda, validando valores e garantindo integridade de Enums.")
        Component(serviceEscrita, "CriarLancamentoService", "Application Service", "Coordena a criação das entidades de domínio e orquestra a persistência dupla.")
        Component(serviceLeitura, "ConsultarSaldoService", "Application Service", "Manipula regras de leitura pura e mapeamento defensivo de nulos para o DTO de resposta.")
        Component(uow, "UnitOfWork & Repositories", "Infrastructure Persistence", "Encapsula o DbContext. Garante transações coordenadas e expõe o Change Tracker (.Local).")
        Component(hostedService, "OutboxWorker", "Background Hosted Service", "Bate ritmicamente a cada 3 segundos disparando o processamento assíncrono.")
        Component(processador, "ProcessadorOutboxService", "Application Service", "O coração do sistema. Executa o Micro-batching acumulando lançamentos do mesmo dia na CPU.")
    }

    Rel(controller, validator, "Valida DTO de entrada")
    Rel(controller, serviceEscrita, "Invoca execução de comando", "CancellationToken")
    Rel(controller, serviceLeitura, "Invoca consulta de saldo", "CancellationToken")
    
    Rel(serviceEscrita, uow, "Adiciona entidades no Change Tracker em memória")
    Rel(serviceLeitura, uow, "Consulta dados diretamente via repositório de leitura")
    
    Rel(hostedService, processador, "Dispara execução por ciclo de escopo manual")
    Rel(processador, uow, "Puxa eventos, consome via .Local e executa o CommitAsync em lote")
    
    Rel(uow, banco, "Envia o lote final de comandos SQL unificados", "ADO.NET / TCP")
```
