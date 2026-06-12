using FluxoCaixa.Application.DTOs;

namespace FluxoCaixa.Application.Interfaces
{
    public interface IConsultarSaldoService
    {
        Task<SaldoDiarioResponse?> ObterPorDataAsync(
            DateOnly data, CancellationToken cancellationTokenm);
    }
}
