using Xunit;
using FluxoCaixa.Application.Services;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Interfaces;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FluxoCaixa.Tests.Application
{
    /// <summary>
    /// Suite de testes focada no componente ProcessadorOutboxService.
    ///  Isola o processador das dependências físicas de I/O de banco de dados 
    /// utilizando Mocking via biblioteca Moq, permitindo a validação deterministicamente pura 
    /// das invariantes de negócio, resiliência do laço e atomicidade em cenários de processamento em lote (Batch).
    /// </summary>
    public class ProcessadorOutboxServiceTests
    {
        private readonly Mock<IUnitOfWorkRepository> _uowMock;
        private readonly ProcessadorOutboxService _sut; // SUT = System Under Test (Sistema Sob Teste)

        public ProcessadorOutboxServiceTests()
        {
            _uowMock = new Mock<IUnitOfWorkRepository>();
            _sut = new ProcessadorOutboxService(_uowMock.Object);
        }

        /// <summary>
        /// OBJETIVO: Validar a mecânica de agregação cumulativa em memória (Micro-batching) para o mesmo dia.
        /// <para>PREMISSA TÉCNICA: O repositório deve interceptar a intenção no cache de primeiro nível (.Local).</para>
        /// <para>CRITÉRIO DE SUCESSO: Múltiplas transações financeiras sofrem a computação matemática na CPU e o sistema dispara estritamente UMA única viagem de rede (CommitAsync) para persistir o estado final consolidadado.</para>
        /// </summary>
        [Fact]
        public async Task ProcessarAsync_DeveConsolidarMultiplosLancamentosDoMesmoDiaEmMemoriaE_CommitaUmaUnicaVez()
        {
            // -----------------------------------------------------------------------------------
            // ARRANGE: Configuração do cenário, dados de entrada e comportamento dos dublês (Mocks)
            // -----------------------------------------------------------------------------------
            var dataHoje = DateOnly.FromDateTime(DateTime.UtcNow);

            // Criação das invariantes de domínio enriquecidas
            var lancamento1 = new Lancamento(TipoLancamento.Credito, 100m);
            var lancamento2 = new Lancamento(TipoLancamento.Debito, 30m);

            var evento1 = new OutboxEvent(lancamento1.Id, JsonSerializer.Serialize(lancamento1));
            var evento2 = new OutboxEvent(lancamento2.Id, JsonSerializer.Serialize(lancamento2));
            var listaEventos = new List<OutboxEvent> { evento1, evento2 };

            // Treina o Mock do Outbox para simular a fila de eventos pendentes vindos do banco
            _uowMock.Setup(x => x.OutboxEvents.ObterPendentesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(listaEventos);

            // Simulação de comportamento de infraestrutura real (Cache de primeiro nível / .Local):
            // Na primeira iteração do laço, o saldo não existe (retorna null). O Callback captura
            // a primeira inserção e atualiza a referência local simulando o comportamento do DbContext em memória.
            SaldoConsolidado saldoExistente = null;
            _uowMock.Setup(x => x.SaldosConsolidados.ObterPorDataAsync(dataHoje, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(() => saldoExistente);

            _uowMock.Setup(x => x.SaldosConsolidados.AdicionarAsync(It.IsAny<SaldoConsolidado>()))
                    .Callback<SaldoConsolidado>(s => saldoExistente = s)
                    .Returns(Task.CompletedTask);

            // -----------------------------------------------------------------------------------
            // ACT: Execução da unidade lógica sob teste (Disparo do Worker/Processador)
            // -----------------------------------------------------------------------------------
            await _sut.ProcessarAsync(CancellationToken.None);

            // -----------------------------------------------------------------------------------
            // ASSERT: Conferência dos resultados e validação das expectativas de negócio
            // -----------------------------------------------------------------------------------
            Assert.NotNull(saldoExistente);
            Assert.Equal(70m, saldoExistente.Saldo); // Invariante matemática: 100 Crédito - 30 Débito = 70 Saldo líquido
            Assert.Equal(100m, saldoExistente.TotalCreditos);
            Assert.Equal(30m, saldoExistente.TotalDebitos);

            // Validação de transição de estados internos das entidades de domínio
            Assert.Equal(StatusEvento.Processado, evento1.Status);
            Assert.Equal(StatusEvento.Processado, evento2.Status);

            // Prova que o sistema agrupou a escrita física e executou apenas uma chamada de I/O
            _uowMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// OBJETIVO: Validar a política de tolerância a falhas e isolamento de erros (Graceful Degradation).
        /// <para>PREMISSA TÉCNICA: O laço de repetição deve ser protegido por escopos isolados de try-catch.</para>
        /// <para>CRITÉRIO DE SUCESSO: Um payload corrompido com erro de parse/deserialização deve ser marcado individualmente com status 'Erro' sem interromper ou ejetar o processamento dos demais eventos legítimos do lote.</para>
        /// </summary>
        [Fact]
        public async Task ProcessarAsync_DeveIsolarErroDeDeserializacao_MarcarEventoComoErro_E_ContinuarOLaco()
        {
            // -----------------------------------------------------------------------------------
            // ARRANGE: Injeção deliberada de uma anomalia de dados junto a uma transação legítima
            // -----------------------------------------------------------------------------------
            var eventoCorrompido = new OutboxEvent(Guid.NewGuid(), "{ JSON QUEBRADO COM ENUM INVALIDO }");

            var lancamentoValido = new Lancamento(TipoLancamento.Credito, 50m);
            var eventoValido = new OutboxEvent(lancamentoValido.Id, JsonSerializer.Serialize(lancamentoValido));

            var listaEventos = new List<OutboxEvent> { eventoCorrompido, eventoValido };

            _uowMock.Setup(x => x.OutboxEvents.ObterPendentesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(listaEventos);

            _uowMock.Setup(x => x.SaldosConsolidados.ObterPorDataAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((SaldoConsolidado)null);

            // -----------------------------------------------------------------------------------
            // ACT: Processamento do lote misto (Dado íntegro + Dado corrompido)
            // -----------------------------------------------------------------------------------
            await _sut.ProcessarAsync(CancellationToken.None);

            // -----------------------------------------------------------------------------------
            // ASSERT: Avaliação de integridade e não-interrupção do pipeline assíncrono
            // -----------------------------------------------------------------------------------
            // O primeiro evento deve sofrer degradação limpa mudando para o status de falha
            Assert.Equal(StatusEvento.Erro, eventoCorrompido.Status);

            // O segundo evento prova a resiliência: o loop continuou ativo e processou o registro subsequente
            Assert.Equal(StatusEvento.Processado, eventoValido.Status);

            // Confirma que as operações válidas mantiveram a atomicidade final da escrita do lote
            _uowMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
