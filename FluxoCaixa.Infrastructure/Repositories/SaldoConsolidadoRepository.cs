using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FluxoCaixa.Infrastructure.Repositories
{
    public class SaldoConsolidadoRepository : ISaldoConsolidadoRepository
    {
        private readonly FluxoCaixaDbContext _db;

        public SaldoConsolidadoRepository(FluxoCaixaDbContext db)
        {
            _db = db;
        }

        public async Task<SaldoConsolidado?> ObterPorDataAsync(DateOnly data, CancellationToken cancellationToken = default)
        {
            // Procura primeiro na memória local do DbContext (Change Tracker)
            // Se o evento anterior do loop já tiver criado o saldo para este dia, nós pegamos ele daqui!
            var saldoEmMemoria = _db.SaldosConsolidados
                .Local
                .FirstOrDefault(x => x.Data == data);

            if (saldoEmMemoria != null)
            {
                return saldoEmMemoria;
            }

            // 2. Se não encontrou na memória local, aí sim faz a viagem de I/O até o banco de dados
            return await _db.SaldosConsolidados
                .FirstOrDefaultAsync(x => x.Data == data, cancellationToken);
        }


        public async Task AdicionarAsync(SaldoConsolidado saldo)
        {

            await _db.SaldosConsolidados.AddAsync(saldo);
        }

        public async Task AtualizarAsync(SaldoConsolidado saldo)
        {

            _db.SaldosConsolidados.Update(saldo);


            await Task.CompletedTask;
        }


    }
}
