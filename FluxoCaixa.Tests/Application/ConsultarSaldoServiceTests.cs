using FluxoCaixa.Application.Services;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Interfaces;
using Moq;

namespace FluxoCaixa.Tests.Application
{
    /// <summary>
    /// Suíte de testes para a ConsultarSaldoService.
    /// OBJETIVO: Garantir a integridade do fluxo de leitura pura (Read-Only) da aplicação.
    /// </summary>
    public class ConsultarSaldoServiceTests
    {
        private readonly Mock<ISaldoConsolidadoRepository> _saldoRepositoryMock;
        private readonly ConsultarSaldoService _sut;

        public ConsultarSaldoServiceTests()
        {
            _saldoRepositoryMock = new Mock<ISaldoConsolidadoRepository>();
            _sut = new ConsultarSaldoService(_saldoRepositoryMock.Object);
        }

        /// <summary>
        /// OBJETIVO: Validar o mapeamento correto dos dados de leitura quando o saldo existe na data consultada.
        /// </summary>
        [Fact]
        public async Task ObterPorDataAsync_QuandoSaldoExiste_DeveMapearEFielmenteRetornarDTOComDados()
        {
            // -----------------------------------------------------------------------------------
            // ARRANGE: Instanciação da entidade de domínio e treinamento do mock de leitura
            // -----------------------------------------------------------------------------------
            decimal saldoVindoDoDiaAnterior = 100m;

            // Criando o cenário com o saldo acumulado de trás
            var lancamentoInicial = new Lancamento(TipoLancamento.Credito, 200m);
            var saldoFake = SaldoConsolidado.CriarComLancamento(lancamentoInicial, saldoVindoDoDiaAnterior);

            var lancamentoDebito = new Lancamento(TipoLancamento.Debito, 50m);
            saldoFake.AplicarLancamento(lancamentoDebito);

            // CÁLCULO INTERNO: 100 (anterior) + 200 (crédito) - 50 (débito) = 250m

            // Usando 'It.IsAny<DateOnly>()' eliminamos a quebra causada pelo fuso horário
            // ou pelo DateTime.UtcNow interno criado na instanciação do Lancamento.
            _saldoRepositoryMock.Setup(x => x.ObterPorDataAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(saldoFake);

            // A consulta enviará a data interna gerada de forma consistente
            var dataConsulta = saldoFake.Data;

            // -----------------------------------------------------------------------------------
            // ACT: Execução da consulta através da Service
            // -----------------------------------------------------------------------------------
            var resultado = await _sut.ObterPorDataAsync(dataConsulta, CancellationToken.None);

            // -----------------------------------------------------------------------------------
            // ASSERT: Validação das correspondências de propriedades (Mapeamento Seguro)
            // -----------------------------------------------------------------------------------
            Assert.NotNull(resultado);
            Assert.Equal(dataConsulta, resultado.Data);
            Assert.Equal(250m, resultado.Saldo);
            Assert.Equal(200m, resultado.TotalCreditos);
            Assert.Equal(50m, resultado.TotalDebitos);
            Assert.Equal(saldoFake.UltimaAtualizacao, resultado.UltimaAtualizacao);

            // Confirma que o repositório de I/O foi consultado exatamente uma vez
            _saldoRepositoryMock.Verify(x => x.ObterPorDataAsync(dataConsulta, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// OBJETIVO: Validar o tratamento defensivo de dados para datas sem movimentação financeira.
        /// </summary>
        [Fact]
        public async Task ObterPorDataAsync_QuandoSaldoNaoExiste_DeveRetornarDTOResilietementeZerado()
        {
            // -----------------------------------------------------------------------------------
            // ARRANGE: Configura o mock de leitura para retornar null de forma explícita
            // -----------------------------------------------------------------------------------
            var dataSemMovimento = new DateOnly(2026, 12, 31);

            _saldoRepositoryMock.Setup(x => x.ObterPorDataAsync(dataSemMovimento, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SaldoConsolidado)null);

            // -----------------------------------------------------------------------------------
            // ACT: Execução da consulta para o cenário de dados vazios
            // -----------------------------------------------------------------------------------
            var resultado = await _sut.ObterPorDataAsync(dataSemMovimento, CancellationToken.None);

            // -----------------------------------------------------------------------------------
            // ASSERT: Validação do comportamento amigável e consistente (Valores padrão de fallback)
            // -----------------------------------------------------------------------------------
            Assert.NotNull(resultado);
            Assert.Equal(dataSemMovimento, resultado.Data);
            Assert.Equal(0m, resultado.Saldo);
            Assert.Equal(0m, resultado.TotalCreditos);
            Assert.Equal(0m, resultado.TotalDebitos);
            Assert.Null(resultado.UltimaAtualizacao);
        }
    }
}
