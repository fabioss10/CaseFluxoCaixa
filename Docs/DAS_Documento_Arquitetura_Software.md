# 📑 Documento de Arquitetura de Software (DAS)

**Projeto:** Sistema de Controle de Fluxo de Caixa Diário  
**Autor:** Fábio Santos (fabioss10)  
**Tecnologia Base:** .NET 10 (C#) / SQL Server  
**Versão:** 1.0  

---

## 1. Introdução

### 1.1 Objetivo do Documento
Este documento fornece uma visão geral arquitetural abrangente do Sistema de Fluxo de Caixa Diário. Ele serve como guia para a governança do código, decisões de infraestrutura, modelagem de dados e validação de requisitos não-funcionais, assegurando que o sistema atenda às demandas de alta volumetria e consistência exigidas pelo setor bancário.

### 1.2 Escopo do Sistema
O ecossistema é composto por uma Web API e um serviço em segundo plano (Worker) integrados sob a mesma base transacional. O sistema gerencia de forma ágil o registro de lançamentos financeiros e consolida os saldos de forma assíncrona, eliminando gargalos de I/O em tempo real.

---

## 2. Metas e Restrições Arquiteturais

### 2.1 Metas (Atributos de Qualidade / Requisitos Não-Funcionais)
* **Atomicidade Crítica:** Lançamentos e metadados de eventos nunca podem divergir. Falhas na rede ou na infraestrutura não podem gerar estados inconsistentes (Consistência Transacional).
* **Alta Vazão de Escrita (OLTP):** A API de entrada deve responder em tempo mínimo (latência de milissegundos), delegando processamentos pesados para segundo plano.
* **Escalabilidade Concorrente:** O recálculo de saldos de relatórios não pode gerar travamentos (*locks*) nas tabelas transacionais, mesmo sob alta concorrência de acessos.
* **Manutenibilidade e Testabilidade:** O código deve possuir acoplamento fraco, permitindo a substituição de frameworks e a execução de testes unitários isolados de I/O.

### 2.Restrições Técnicas
* A aplicação deve ser desenvolvida em **C#** utilizando a plataforma **.NET 10**.
* O banco de dados relacional deve ser o **Microsoft SQL Server**.
* É obrigatória a inclusão de uma suíte de **Testes Unitários automatizados**.

---

## 3. Visão Lógica (Padrões de Arquitetura e Design)

O ecossistema foi estruturado sob os preceitos de **Clean Architecture** e **Domain-Driven Design (DDD)**, dividindo-se em componentes desacoplados:

### 3.1 Camada de Domínio (Rich Domain Model)
As entidades (`Lancamento`, `OutboxEvent`, `SaldoConsolidado`) contêm o estado e o comportamento de negócio. Utilizam propriedades com modificadores `private set` e construtores específicos para blindar a integridade das regras financeiras no momento de sua criação, em conformidade com o princípio *Fail-Fast*.

### 3.2 Camada de Aplicação (Services e Contracts)
Contém os casos de uso do sistema (`CriarLancamentoService`, `ConsultarSaldoService`, `ProcessadorOutboxService`). Esta camada é totalmente agnóstica a bancos de dados ou frameworks e se comunica com o mundo externo estritamente por interfaces de abstração (DIP - Dependency Inversion Principle).

### 3.3 Camada de Infraestrutura e Persistência
Implementa o padrão **Unit of Work** gerenciando o ciclo de vida do `DbContext` do Entity Framework Core 10. Centraliza as transações físicas em lote e isola a tecnologia de banco de dados do resto da aplicação.

---

## 4. Mecanismos Arquiteturais Chave

O sistema soluciona os desafios tradicionais de consistência distribuída e performance através de três mecanismos principais:

### 4.1 Transactional Outbox Pattern
Para evitar o problema do *Dual Write* na API (gravar o lançamento mas falhar ao atualizar o saldo ou ao notificar outros sistemas por oscilação de rede), o Lançamento e o Evento de auditoria são gravados na mesma transação local do banco de dados (ACID). O `OutboxWorker` (Hosted Service) varre esta tabela a cada 3 segundos, processando os eventos pendentes com segurança.

### 4.2 Micro-batching via Cache de Primeiro Nível (`.Local`)
O processamento em segundo plano opera em lotes. Para evitar concorrência física de linhas no banco (*Row-Locking / Deadlocks*), o repositório inspeciona o Change Tracker em memória do EF Core (`_context.Set().Local`) antes de realizar requisições na rede. Múltiplos lançamentos da mesma data sofrem a computação matemática cumulativa na CPU e resultam em **apenas uma única escrita consolidada por dia** no banco de dados.

### 4.3 Otimização de Chaves e Indexação
* **UUIDv7:** Utilizado para os identificadores globais. Por possuir um componente de tempo (*timestamp*) sequencial nos bits iniciais, as inserções ocorrem sempre no final das páginas de disco, minimizando o *Page Splitting* e otimizando o I/O sob alta carga.
* **Chave Primária por Data:** A tabela de saldo consolidado utiliza a própria Data como chave física. Isso gera um índice clusterizado nativo, ordenando a tabela em disco por dia e reduzindo a complexidade de busca da rota RESTful para $O(\log N)$ via *Index Seek*.
* **Índice Filtrado para o Outbox:** O banco gerencia um índice composto não-clusterizado configurado via Fluent API (`.HasFilter()`), contendo apenas registros onde `Status == Pendente` ou `Status == Erro`. Como 99% da tabela histórica em produção estará marcada como `Processado`, o índice permanece extremamente leve e residente na memória RAM, garantindo buscas instantâneas para o Worker.

---

## 5. Estratégia de Testes

A qualidade do software é validada deterministicamente através de testes unitários com as bibliotecas **xUnit** e **Moq**.

* **Testes de Escreta Transacional:** Verificam se a service adiciona as duas entidades em memória e dispara o commit unificado no final.
* **Testes de Lote e Resiliência:** Simulam o comportamento do Change Tracker em memória local para atestar o cálculo cumulativo correto dos saldos e provam que falhas de parse/JSON isolam o registro corrompido sem derrubar a execução do laço para os demais registros legítimos.
* **Testes de Leitura Defensiva:** Garantem que ausências de registro no banco de dados sejam interceptadas amigavelmente pela service, construindo respostas com valores zerados em vez de repassar nulos e gerar exceções de tela.
