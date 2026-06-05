using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq; 
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Repositories
{
    public class OutboxEventRepository : IOutboxEventRepository
    {
        private readonly FluxoCaixaDbContext _context;

        public OutboxEventRepository(FluxoCaixaDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarAsync(OutboxEvent evento)
        {
            
            await _context.OutboxEvents.AddAsync(evento);
        }

        public async Task<List<OutboxEvent>> ObterPendentesAsync(CancellationToken cancellationToken = default)
        {
            
            return await _context.OutboxEvents
                .Where(x => x.Status == StatusEvento.Pendente)
                .ToListAsync(cancellationToken);
        }

        public async Task AtualizarAsync(OutboxEvent evento)
        {
            
            _context.OutboxEvents.Update(evento);

            
            await Task.CompletedTask;
        }
    }
}
