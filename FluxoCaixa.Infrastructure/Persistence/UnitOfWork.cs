using FluxoCaixa.Domain.Interfaces;
using FluxoCaixa.Infrastructure.Repositories;

namespace FluxoCaixa.Infrastructure.Persistence
{
    // <summary>
    /// Enquanto as interfaces dos repositórios isolam o acesso aos dados, esta classe resolve o problema da ATOMICIDADE.
    /// Sem o Unit of Work, se um caso de uso injetar múltiplos repositórios independentes, cada um teria seu próprio método 'Salvar()'.
    /// O Unit of Work garante que todas as operações (ex: criar lançamento E salvar evento no Outbox) compartilhem estritamente 
    /// o mesmo DbContext e a mesma transação do banco de dados, sendo persistidas juntas em uma única operação atômica (tudo ou nada).
    /// </summary>
    public class UnitOfWork : IUnitOfWorkRepository
    {
        private readonly FluxoCaixaDbContext _context;
        private ILancamentoRepository? _lancamentos;
        private IOutboxEventRepository? _outboxEvents;
        private ISaldoConsolidadoRepository? _saldosConsolidados;

        public UnitOfWork(FluxoCaixaDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public ILancamentoRepository Lancamentos =>
            _lancamentos ??= new LancamentoRepository(_context);

        public IOutboxEventRepository OutboxEvents =>
            _outboxEvents ??= new OutboxEventRepository(_context);

        public ISaldoConsolidadoRepository SaldosConsolidados =>
            _saldosConsolidados ??= new SaldoConsolidadoRepository(_context);

        public async Task<bool> CommitAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken) > 0;
        }

        /// <summary>
        /// Libera explicitamente o DbContext injetado, garantindo o fechamento 
        /// das conexões com o banco de dados assim que o ciclo de vida da operação termina.
        /// </summary>

        public void Dispose()
        {
            _context.Dispose(); // Fecha a conexão com o banco de dados
            GC.SuppressFinalize(this); // Avisa o Garbage Collector que a memória já foi limpa
        }
    }
}
