using FluxoCaixa.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Interfaces
{
    public interface ISaldoConsolidadoRepository
    {
        Task<SaldoConsolidado?> ObterPorUsuarioAsync(Guid usuarioId);

        Task AdicionarAsync(SaldoConsolidado saldo);

        Task AtualizarAsync(SaldoConsolidado saldo);
    }
}
