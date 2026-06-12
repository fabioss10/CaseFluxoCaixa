using FluxoCaixa.Application.Services;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Interfaces;
using Moq;
using System.Text.Json;

namespace FluxoCaixa.Tests.Application
{
    /// <summary>
    /// Suite de testes focada no componente ProcessadorOutboxService.
    /// Isola o processador das dependências físicas de I/O de banco de dados 
    /// utilizando Mocking via biblioteca Moq, permitindo a validação deterministicamente pura 
    /// das invariantes de negócio, resiliência do laço e atomicidade em cenários de processamento em lote (Batch).
    /// </summary>
    public class ProcessadorOutboxServiceTests
    {
        private readonly Mock<IUnitOfWorkRepository> _uowMock;
        private readonly ProcessadorOutboxService _sut;




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
            decimal saldoVindoDoDiaAnterior = 50m; // Adicionado histórico para validar a nova regra de domínio

            // Criação das invariantes de domínio enriquecidas
            var lancamento1 = new Lancamento(TipoLancamento.Credito, 100m);
            var lancamento2 = new Lancamento(TipoLancamento.Debito, 30m);

            // O payload do Outbox transporta as propriedades públicas mapeadas no DTO/Entidade
            var evento1 = new OutboxEvent(lancamento1.Id, JsonSerializer.Serialize(lancamento1));
            var evento2 = new OutboxEvent(lancamento2.Id, JsonSerializer.Serialize(lancamento2));
            var listaEventos = new List<OutboxEvent> { evento1, evento2 };

            _uowMock.Setup(x => x.OutboxEvents.ObterPendentesAsync(It.IsAny<CancellationToken>()))
                    .ReturnsAsync(listaEventos);

            // aceitar 'It.IsAny<DateOnly>()' para capturar
            // dinamicamente tanto a busca do saldo de 'Hoje' quanto a busca retroativa do saldo de 'Ontem'.
            SaldoConsolidado saldoExistenteInMemoria = null;

            _uowMock.Setup(x => x.SaldosConsolidados.ObterPorDataAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((DateOnly dataSolicitada, CancellationToken ct) =>
                    {
                        // Se a Service estiver pedindo o dia de Ontem, devolvemos um saldo fake com o valor histórico (50)
                        var dataHoje = DateOnly.FromDateTime(DateTime.UtcNow);
                        if (dataSolicitada == dataHoje.AddDays(-1))
                        {
                            return SaldoConsolidado.CriarSaldoVazio(dataSolicitada, saldoVindoDoDiaAnterior);
                        }

                        // Se for a data de Hoje, simula o comportamento do ChangeTracker (.Local)
                        return saldoExistenteInMemoria;
                    });

            _uowMock.Setup(x => x.SaldosConsolidados.AdicionarAsync(It.IsAny<SaldoConsolidado>()))
                    .Callback<SaldoConsolidado>(s => saldoExistenteInMemoria = s)
                    .Returns(Task.CompletedTask);

            // -----------------------------------------------------------------------------------
            // ACT: Execução da unidade lógica sob teste (Disparo do Worker/Processador)
            // -----------------------------------------------------------------------------------
            await _sut.ProcessarAsync(CancellationToken.None);

            // -----------------------------------------------------------------------------------
            // ASSERT: Conferência dos resultados e validação das expectativas de negócio
            // -----------------------------------------------------------------------------------
            Assert.NotNull(saldoExistenteInMemoria);

            //  MATEMÁTICA ATUALIZADA: 50 (Ontem) + 100 (Crédito) - 30 (Débito) = 120m
            Assert.Equal(120m, saldoExistenteInMemoria.Saldo);
            Assert.Equal(100m, saldoExistenteInMemoria.TotalCreditos);
            Assert.Equal(30m, saldoExistenteInMemoria.TotalDebitos);

            Assert.Equal(StatusEvento.Processado, evento1.Status);
            Assert.Equal(StatusEvento.Processado, evento2.Status);

            _uowMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// OBJETIVO: Validar a política de tolerância a falhas e isolamento de erros (Graceful Degradation).
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
            Assert.Equal(StatusEvento.Erro, eventoCorrompido.Status);
            Assert.Equal(StatusEvento.Processado, eventoValido.Status);

            _uowMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
