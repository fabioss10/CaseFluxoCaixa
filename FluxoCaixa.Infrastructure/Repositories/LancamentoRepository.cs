using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Repositories
{
    public class LancamentoRepository : ILancamentoRepository
    {
        private readonly FluxoCaixaDbContext _context;

        public LancamentoRepository(FluxoCaixaDbContext context)
        {
            _context = context;
        }

        public async Task AdicionarAsync(Lancamento lancamento)
        {

            await _context.Lancamentos.AddAsync(lancamento);
        }

        public async Task<Lancamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        {

            return await _context.Lancamentos
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
    }
}
