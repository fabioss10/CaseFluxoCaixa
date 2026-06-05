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
    /// Suíte de testes para a CriarLancamentoService.
    /// OBJETIVO: Garantir a integridade da borda de escrita da API, validando cientificamente 
    /// que o padrão Transactional Outbox respeita a atomicidade da operação (Persistência Dupla Coordenada).
    /// </summary>
    public class CriarLancamentoServiceTests
    {
        private readonly Mock<IUnitOfWorkRepository> _uowMock;
        private readonly CriarLancamentoService _sut;

        public CriarLancamentoServiceTests()
        {
            _uowMock = new Mock<IUnitOfWorkRepository>();
            _sut = new CriarLancamentoService(_uowMock.Object);
        }

        /// <summary>
        /// OBJETIVO: Validar o fluxo feliz de persistência atômica.
        /// <para>PREMISSA TÉCNICA: O serviço deve coordenar a inserção do Lançamento e do OutboxEvent sob o mesmo contexto.</para>
        /// <para>CRITÉRIO DE SUCESSO: Ambos os registros entram no ChangeTracker em memória e o Unit of Work dispara um único CommitAsync.</para>
        /// </summary>
        [Fact]
        public async Task ExecutarAsync_ComRequestValido_DeveAdicionarLancamentoEOutboxEvent_E_ExecutarCommitNivelACID()
        {
            // -----------------------------------------------------------------------------------
            // ARRANGE: Configuração do DTO de Entrada e preparação das escutas do Mock
            // -----------------------------------------------------------------------------------
            var request = new CriarLancamentoRequest
            {
                Tipo = TipoLancamento.Credito,
                Valor = 150.50m
            };

            // Configura os mocks dos repositórios internos do Unit of Work para aceitarem a inserção em memória
            _uowMock.Setup(x => x.Lancamentos.AdicionarAsync(It.IsAny<Lancamento>()))
                .Returns(Task.CompletedTask);

            _uowMock.Setup(x => x.OutboxEvents.AdicionarAsync(It.IsAny<OutboxEvent>()))
                .Returns(Task.CompletedTask);

            _uowMock.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // -----------------------------------------------------------------------------------
            // ACT: Execução da regra de negócio de criação do lançamento
            // -----------------------------------------------------------------------------------
            var resultadoId = await _sut.ExecutarAsync(request, CancellationToken.None);

            // -----------------------------------------------------------------------------------
            // ASSERT: Validação das expectativas e comportamento transacional
            // -----------------------------------------------------------------------------------
            Assert.NotEqual(Guid.Empty, resultadoId);

            // GARANTIA DE ESCRITA DUPLA ATÔMICA: Verifica se o Lançamento foi adicionado no repositório correto
            _uowMock.Verify(x => x.Lancamentos.AdicionarAsync(It.Is<Lancamento>(l =>
                l.Valor == request.Valor && l.Tipo == request.Tipo)), Times.Once);

            // GARANTIA DO EVENTO: Verifica se o Evento correspondente foi gerado com o payload correto
            _uowMock.Verify(x => x.OutboxEvents.AdicionarAsync(It.Is<OutboxEvent>(e =>
                e.LancamentoId == resultadoId && e.EventType == "LancamentoCriado")), Times.Once);

            // GARANTIA DE TRANSACIONALIDADE COMPARTILHADA: Certifica o disparo do commit único e final
            _uowMock.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
