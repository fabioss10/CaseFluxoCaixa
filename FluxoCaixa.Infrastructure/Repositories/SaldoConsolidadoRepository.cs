using FluxoCaixa.Domain.Entities;
using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Repositories
{
    public class SaldoConsolidadoRepository
        : ISaldoConsolidadoRepository
    {
        private readonly FluxoCaixaDbContext _db;

        public SaldoConsolidadoRepository(
            FluxoCaixaDbContext db)
        {
            _db = db;
        }

        public async Task<SaldoConsolidado?> ObterPorDataAsync(
            DateOnly data)
        {
            return await _db.SaldosConsolidados
                .FirstOrDefaultAsync(x => x.Data == data);
        }

        public async Task AdicionarAsync(
            SaldoConsolidado saldo)
        {
            await _db.SaldosConsolidados
                .AddAsync(saldo);

            await _db.SaveChangesAsync();
        }

        public async Task AtualizarAsync(
            SaldoConsolidado saldo)
        {
            _db.SaldosConsolidados
                .Update(saldo);

            await _db.SaveChangesAsync();
        }
    }
}