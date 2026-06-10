# Matriz de Integração e Segurança

Este documento formaliza os contratos de comunicação, fluxos de integração e os mecanismos de proteção de dados aplicados no ecossistema do Sistema de Fluxo de Caixa.

---

## 1. Matriz de Integração (Contratos e Protocolos)

A tabela abaixo descreve como os componentes internos e potenciais sistemas externos interagem com a nossa solução.

| Origem (Consumidor) | Destino (Provedor) | Mecanismo / Protocolo | Tipo de Comunicação | Frequência / Vazão | Objetivo do Fluxo |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Comerciante / Frontend** | Web API (`Lancamentos`) | HTTPS / REST / JSON (Porta 7248) | Síncrona (Request-Response) | Sob demanda | Registrar um novo crédito ou débito no fluxo de caixa com validação síncrona de payload. |
| **Comerciante / Frontend** | Web API (`Saldos`) | HTTPS / REST / JSON (Porta 7248) | Síncrona (Request-Response) | Alta (SLA: 50 RPS) | Consultar o saldo diário consolidado de uma data específica via Index Seek. |
| **Outbox Worker** | Banco SQL Server | ADO.NET / TCP / TDS | Assíncrona (Polling Lote) | Ciclos de 3 segundos | Buscar eventos `Pendente`, executar micro-batching e atualizar saldos. |
| **Orquestrador / DevOps** | Web API (`Health / Metrics`) | HTTP / JSON / OpenMetrics (Porta 8081) | Síncrona (Polling Contínuo) | Ciclos de 2s a 5s | Validar a integridade física da RAM e latência do banco sem concorrer com a API pública. |
| **Web API / Worker** | .NET Aspire Dashboard | gRPC / OTLP (Porta 18889) | Assíncrona (Background Stream) | Tempo Real (Push) | Descarregar métricas de hardware e CPU de forma isolada, eliminando o modelo de scrape local. |
| **Outbox Worker** | Broker de Mensageria | AMQP / gRPC / TCP | Assíncrona (Event-Driven) | Disparada por lote | (Evolução) Publicar o evento `LancamentoCriado` utilizando UUIDv7 como chave de idempotência. |
| **Debezium (CDC)** | Banco SQL Server | CDC Engine / Disco | Assíncrona (Log-Streaming) | Tempo Real (Streaming) | (Evolução) Ler os binários do *Transaction Log* sem gerar concorrência de I/O em tabelas físicas. |

---

## 2. Matriz de Segurança por Endpoint (Controle de Acesso)

Seguindo os padrões de governança **OAuth 2.0 e RBAC (Role-Based Access Control)**, cada recurso exposto possui uma política estrita de autorização baseada em escopos e *Claims* contidas no Token JWT [|]. A validação de escopos em lote é executada via expressão lambda baseada em asserção (`RequireAssertion`), garantindo a leitura correta de coleções no mesmo nó estrutural.

| Método HTTP | Endpoint (Rota) | Porta Física | Autenticação Requerida? | Escopo / Permissão (Policy) | Tipo de Restrição | Justificativa de Segurança |
| :---: | :--- | :---: | :---: | :--- | :--- | :--- |
| **POST** | `/api/lancamentos` | **7248** | **Sim** | `WritePolicy` (`fluxocaixa.write`) | Usuários com papel `Gerente` | Impede que agentes não autorizados injetem movimentações financeiras falsas na base. |
| **GET** | `/api/saldos/{data}` | **7248** | **Sim** | `ReadPolicy` (`fluxocaixa.read`) | Papéis de `Gerente` ou `Analista` | Garante o sigilo bancário, restringindo o acesso a tokens emitidos pelo Identity Server homologado. |
| **GET** | `/swagger/index.html` | **7248** | Não | *Nenhuma (Público)* | Ambiente de Dev / Staging | Documentação OpenAPI v3.1 interativa e testes manuais de endpoints. |
| **GET** | `/healthz` | **8081** | Não | *Restrição de Perímetro* | Exclusivo para Sondas Internas | Endpoint de ultra-performance (texto puro) para checagem rápida de integridade do container (Liveness). |
| **GET** | `/healthz/detail` | **8081** | Não | *Restrição de Perímetro* | Engenheiros de Infraestrutura | Payload JSON com diagnóstico detalhado de consumo de RAM do processo e conectividade real de rede. |

## 3. Segurança de Dados (Criptografia e Proteção de Ativos)

### 3.1 Isolamento Perimetral de Redes (Kestrel Multi-Listening)
A infraestrutura do sistema implementa segurança ativa de rede através da segregação física de portas lógicas via `ConfigureKestrel` no servidor de aplicação:
* **Tráfego de Negócios (Porta 7248):** Único canal exposto publicamente para o recebimento de requisições transacionais e exibição de documentação Swagger. Conta com proteção TLS para criptografia ponta a ponta.
* **Tráfego de Infraestrutura (Porta 8081):** Canal privado sem criptografia (HTTP simples), de consumo exclusivo por redes de monitoramento internas da hospedagem. Tentativas de acessar requisições de negócios por este canal, ou de chamar o monitoramento pela porta 7248, resultam preventivamente em erro `404 Not Found`. O pipeline utiliza `app.UseWhen()` para desviar o tráfego desta porta do middleware global de redirecionamento HTTPS.

### 3.2 Dados em Trânsito (Data in Transit)
* **HTTPS / TLS 1.3:** Toda a comunicação externa entre os clientes e a Web API na porta pública é obrigatoriamente criptografada utilizando o protocolo **TLS 1.3**. Requisições HTTP comuns direcionadas a esse canal são rejeitadas na borda.
* **Criptografia Inter-Container (OTLP Securing):** A API ativa a diretiva `OTEL_EXPORTER_OTLP_INSECURE=true` para forçar o descarregamento de telemetria gRPC em texto claro. Esse fluxo trafega de forma estritamente isolada e protegida por criptografia de rede virtual interna gerada pelo Docker Compose, impedindo o vazamento de metadados operacionais para redes externas.

### 3.3 Dados em Repouso (Data at Rest)
* **TDE (Transparent Data Encryption):** No ambiente de produção do SQL Server, as tabelas `Lancamentos`, `OutboxEvents` e `SaldosConsolidados` utilizam criptografia a nível de arquivo de página de dados em disco (TDE), protegendo a base contra cópias ou roubo físico dos arquivos `.mdf` ou `.ldf`.
* **Mascaramento de Payloads e LGPD:** Os dados sensíveis de auditoria e metadados armazenados na coluna `Payload` da tabela de Outbox são serializados de forma estrita, garantindo conformidade com as diretrizes da **LGPD (Lei Geral de Proteção de Dados)**.

### 3.4 Proteção de Credenciais e Segredos (Zero Secrets no Código)
* **Gerenciamento Seguro de Configurações:** Nenhuma senha de banco de dados, chaves privadas de criptografia assimétrica de tokens JWT ou credenciais de mensageria são mantidas hardcoded nos arquivos de código ou no `appsettings.json` do repositório.
* **Mapeamento Strongly-Typed:** O sistema lê os parâmetros do arquivo de configuração de forma injetada via `IOptions<JwtOptions>`. Para produção, a infraestrutura está mapeada para sobrescrever esses nós dinamicamente em tempo de execução através de variáveis de ambiente injetadas de forma externa e segura pelo Docker Compose, estando pronta para integração nativa com cofres de segredos corporativos (como **Azure Key Vault** ou **HashiCorp Vault**).
