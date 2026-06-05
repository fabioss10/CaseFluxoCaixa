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

## 3. Estratégia de Persistência e Performance (EF Core)

### 3.1 Operações em Memória vs Operações de I/O
* **Diretriz:** Compreenda o comportamento do *Change Tracker* para evitar desperdício de recursos de rede e CPU.
  * Métodos de anúncio de mudança como `Add()`, `Update()` e `Remove()` operam estritamente sobre os ponteiros de memória local. **Não deve ser adicionado o parâmetro `CancellationToken`** a esses métodos, pois eles rodam de forma instantânea na CPU.
  * Métodos que disparam viagens físicas de rede (I/O bloqueante) como `ToListAsync()`, `FirstOrDefaultAsync()` e `SaveChangesAsync()` **devem obrigatoriamente aceitar e propagar o `CancellationToken`** para garantir o suporte a *Graceful Shutdown*.

### 3.2 Otimização de Escritas em Lote (Micro-batching)
* **Diretriz:** Nunca invoque o método `SaveChangesAsync()` dentro de laços de repetição (`foreach` / `while`).
* **Aplicação no Projeto:** Múltiplas alterações devem ser acumuladas no Change Tracker (utilizando a propriedade `.Local` para inspecionar e reaproveitar objetos da mesma chave primária em memória). O envio físico de comandos SQL deve ser centralizado em uma única chamada atômica do `CommitAsync()` do *Unit of Work* ao final do lote.

---

## 4. Governança de Validação e Erros

### 4.1 Validação de Entrada (Edge Validation)
* **Diretriz:** Falhe o mais rápido possível na borda do sistema para poupar processamento e proteger a estabilidade do banco.
* **Aplicação no Projeto:** Toda validação de formato de JSON, limites de valores e integridade estrita de Enums deve ser feita na camada de entrada via **FluentValidation** com regras como `.IsInEnum()`. Payload corrompido não deve ultrapassar a Controller, retornando automaticamente um `HTTP 400 Bad Request`.

### 4.2 Isolamento de Falhas em Background (Workers)
* **Diretriz:** Falhas em dados individuais de processamento em lote não podem derrubar o serviço em background (*Hosted Service*).
* **Aplicação no Projeto:** O laço de repetição do processador de Outbox deve envelopar cada iteração em escopos isolados de `try-catch`. Caso um evento sofra erro de deserialização ou negócio, ele deve ser marcado individualmente com status de `Erro` na memória, permitindo que o loop continue processando os registros subsequentes normais.

---

## 5. Convenções de Código e Padrões Git

### 5.1 Nomenclatura de Testes Unitários
* **Diretriz:** Os testes devem seguir o padrão comportamental claro: `Metodo_Cenario_ComportamentoEsperado`.
* **Exemplo:** `ProcessarAsync_DeveConsolidarMultiplosLancamentosDoMesmoDiaEmMemoriaE_CommitaUmaUnicaVez`.

### 5.2 Padrão de Commits Semânticos (Conventional Commits)
* Ao realizar envios para o repositório, utilize prefixos claros para documentar o histórico de engenharia:
  * `feat:` Nova funcionalidade (ex: criação do validador de lançamentos).
  * `fix:` Correção de bug (ex: tratamento de exceção por estado desanexado no EF).
  * `docs:` Alterações em documentações (ex: atualização do C4 Model ou deste guia).
  * `test:` Adição ou refatoração de suítes de testes unitários ou de carga.
