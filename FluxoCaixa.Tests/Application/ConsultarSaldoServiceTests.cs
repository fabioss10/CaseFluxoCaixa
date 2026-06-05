using Xunit;
using FluxoCaixa.Application.Services;
using FluxoCaixa.Application.DTOs;
using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Interfaces;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FluxoCaixa.Tests.Application
{
    /// <summary>
    /// Suíte de testes para a ConsultarSaldoService.
    /// OBJETIVO: Garantir a integridade do fluxo de leitura pura (Read-Only) da aplicação.
    /// Justificativa de Design: Valida as regras de transformação de dados (Mapeamento de Entidade para DTO)
    /// e a resiliência da borda de negócio ao lidar com registros inexistentes no banco de dados.
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
        /// <para>PREMISSA TÉCNICA: O repositório deve retornar uma entidade rica e a Service deve transformá-la fielmente no DTO de resposta.</para>
        /// <para>CRITÉRIO DE SUCESSO: Todas as propriedades matemáticas e metadados de auditoria (UltimaAtualizacao) batem de ponta a ponta.</para>
        /// </summary>
        [Fact]
        public async Task ObterPorDataAsync_QuandoSaldoExiste_DeveMapearEFielmenteRetornarDTOComDados()
        {
            // -----------------------------------------------------------------------------------
            // ARRANGE: Instanciação da entidade de domínio e treinamento do mock de leitura
            // -----------------------------------------------------------------------------------
            var dataConsulta = new DateOnly(2026, 6, 5);

            // Usado a fábrica do domínio enriquecido para criar um saldo limpo
            var lancamentoInicial = new Lancamento(TipoLancamento.Credito, 200m);
            var saldoFake = SaldoConsolidado.CriarComLancamento(lancamentoInicial);

            // Adiciondo mais uma movimentação para rechear os dados do teste
            var lancamentoDebito = new Lancamento(TipoLancamento.Debito, 50m);
            saldoFake.AplicarLancamento(lancamentoDebito); // Saldo final deve ser 150 (200 - 50)

            _saldoRepositoryMock.Setup(x => x.ObterPorDataAsync(dataConsulta, It.IsAny<CancellationToken>()))
                .ReturnsAsync(saldoFake);

            // -----------------------------------------------------------------------------------
            // ACT: Execução da consulta através da Service
            // -----------------------------------------------------------------------------------
            var resultado = await _sut.ObterPorDataAsync(dataConsulta, CancellationToken.None);

            // -----------------------------------------------------------------------------------
            // ASSERT: Validação das correspondências de propriedades (Mapeamento Seguro)
            // -----------------------------------------------------------------------------------
            Assert.NotNull(resultado);
            Assert.Equal(dataConsulta, resultado.Data);
            Assert.Equal(150m, resultado.Saldo);
            Assert.Equal(200m, resultado.TotalCreditos);
            Assert.Equal(50m, resultado.TotalDebitos);
            Assert.Equal(saldoFake.UltimaAtualizacao, resultado.UltimaAtualizacao);

            // Confirma que o repositório de I/O foi consultado exatamente uma vez
            _saldoRepositoryMock.Verify(x => x.ObterPorDataAsync(dataConsulta, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// OBJETIVO: Validar o tratamento defensivo de dados para datas sem movimentação financeira.
        /// <para>PREMISSA TÉCNICA: O repositório retornará 'null' ao simular a ausência de registro no banco.</para>
        /// <para>CRITÉRIO DE SUCESSO: A Service intercepta o nulo com segurança e inicializa um objeto amigável zerado, blindando a API contra NullReferenceExceptions.</para>
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
