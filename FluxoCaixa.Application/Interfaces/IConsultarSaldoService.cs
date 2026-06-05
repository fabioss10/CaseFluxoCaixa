using FluxoCaixa.Application.DTOs;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Application.Interfaces
{
    public interface IConsultarSaldoService
    {
        Task<SaldoDiarioResponse?> ObterPorDataAsync(
            DateOnly data, CancellationToken cancellationTokenm);
    }
}
