using FluxoCaixa.Application.DTOs;

namespace FluxoCaixa.Application.Interfaces
{
    public interface ICriarLancamentoService
    {
        Task<Guid> ExecutarAsync(
            CriarLancamentoRequest request, CancellationToken cancellationTokenm);
    }
}
