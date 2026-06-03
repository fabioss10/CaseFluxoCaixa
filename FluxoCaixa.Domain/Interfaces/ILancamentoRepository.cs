using FluxoCaixa.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Interfaces
{
    public interface ILancamentoRepository
    {
        Task AdicionarAsync(Lancamento lancamento);

        Task<Lancamento?> ObterPorIdAsync(Guid id);
    }
}
