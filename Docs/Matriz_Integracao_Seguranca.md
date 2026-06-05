# Matriz de Integração e Segurança

Este documento formaliza os contratos de comunicação, fluxos de integração e os mecanismos de proteção de dados aplicados no ecossistema do Sistema de Fluxo de Caixa.

---

## 1. Matriz de Integração (Contratos e Protocolos)

A tabela abaixo descreve como os componentes internos e potenciais sistemas externos interagem com a nossa solução.


| Origem (Consumidor) | Destino (Provedor) | Mecanismo / Protocolo | Tipo de Comunicação | Frequência / Vazão | Objetivo do Fluxo |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Comerciante / Frontend** | Web API (`Lancamentos`) | HTTPS / REST / JSON | Síncrona (Request-Response) | Sob demanda | Registrar um novo crédito ou débito no fluxo de caixa. |
| **Comerciante / Frontend** | Web API (`Saldos`) | HTTPS / REST / JSON | Síncrona (Request-Response) | Alta (SLA: 50 RPS) | Consultar o saldo diário consolidado de uma data específica. |
| **Outbox Worker** | Banco SQL Server | ADO.NET / TCP / TDS | Assíncrona (Polling Lote) | Ciclos de 3 segundos | Buscar eventos `Pendente` e executar micro-batching. |
| **Outbox Worker** | Broker de Mensageria | AMQP / gRPC / TCP | Assíncrona (Event-Driven) | Disparada por lote | (Evolução) Publicar o evento `LancamentoCriado` para o ecossistema. |
| **Debezium (CDC)** | Banco SQL Server | CDC Engine / Disco | Assíncrona (Log-Streaming) | Tempo Real (Streaming) | (Evolução) Ler os binários do *Transaction Log* sem fazer queries SQL. |

---

## 2. Matriz de Segurança por Endpoint (Controle de Acesso)

Seguindo os padrões de governança **OAuth 2.0 e RBAC (Role-Based Access Control)**, cada recurso exposto possui uma política estrita de autorização baseada em escopos e *Claims* contidas no Token JWT.


| Método HTTP | Endpoint (Rota) | Autenticação Requerida? | Escopo / Permissão (Policy) | Tipo de Restrição | Justificativa de Segurança |
| :---: | :--- | :---: | :--- | :--- | :--- |
| **POST** | `/api/lancamentos` | **Sim** | `GravarLancamentos` | Usuários / Sistemas Autorizados | Impede que agentes não autorizados injetem movimentações financeiras falsas. |
| **GET** | `/api/saldos/{data}` | **Sim** | `LerSaldos` | Comerciante Dono da Conta | Garante o sigilo bancário, permitindo a leitura apenas de dados autorizados. |
| **GET** | `/swagger` | Não | *Nenhuma (Público)* | Ambiente de Dev / Staging | Exposto apenas para documentação interativa e testes de integração. |

---

## 3. Segurança de Dados (Criptografia e Proteção de Ativos)

### 3.1 Dados em Trânsito (Data in Transit)
* **HTTPS / TLS 1.3:** Toda a comunicação externa entre os clientes e a Web API é obrigatoriamente criptografada utilizando o protocolo **TLS 1.3**. Requisições HTTP comuns são rejeitadas na borda pelo Gateway de API.
* **MAPP / Connection Security:** A comunicação interna entre a Web API/Worker e o Microsoft SQL Server é protegida via protocolo TDS criptografado, utilizando a diretiva `TrustServerCertificate=True` combinada com chaves de criptografia homologadas no ambiente de produção.

### 3.2 Dados em Repouso (Data at Rest)
* **TDE (Transparent Data Encryption):** No ambiente de produção do SQL Server, as tabelas `Lancamentos`, `OutboxEvents` e `SaldosConsolidados` utilizam criptografia a nível de arquivo de página de dados em disco (TDE), protegendo a base contra cópias não autorizadas dos arquivos `.mdf` ou `.ldf`.
* **Mascaramento de Payloads:** Os dados sensíveis armazenados na coluna `Payload` da tabela de Outbox são serializados de forma estrita, garantindo que informações críticas trafeguem protegidas de acordo com as diretrizes da **LGPD (Lei Geral de Proteção de Dados)**.

### 3.3 Proteção de Credenciais e Segredos
* **Zero Secrets no Código:** Nenhuma senha de banco de dados, chaves privadas de tokens JWT ou credenciais de mensageria são salvas hardcoded no arquivo `appsettings.json`. O sistema está preparado para ler essas informações dinamicamente através de variáveis de ambiente seguras ou de um cofre de segredos centralizado (como **Azure Key Vault** ou **HashiCorp Vault**).
