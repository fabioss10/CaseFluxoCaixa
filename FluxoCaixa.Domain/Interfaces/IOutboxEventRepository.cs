using FluxoCaixa.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace FluxoCaixa.Domain.Interfaces
{
    public interface IOutboxEventRepository
    {
        Task AdicionarAsync(OutboxEvent evento);

        Task<List<OutboxEvent>> ObterPendentesAsync();

        Task AtualizarAsync(OutboxEvent evento);
    }
}
