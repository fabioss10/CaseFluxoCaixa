using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Enums;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
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
                .OrderBy(x => x.CreatedAt) // Processa os mais antigos primeiro (FIFO)
                .Take(100) //  Limita o lote para liberar o pool rapidamente
                .ToListAsync(cancellationToken);
        }


        public async Task AtualizarAsync(OutboxEvent evento)
        {

            _context.OutboxEvents.Update(evento);


            await Task.CompletedTask;
        }
    }
}
