namespace FluxoCaixa.Domain.Interfaces
{
    /// <summary>
    /// Herda de IDisposable para garantir a liberação segura dos recursos de conexão do DbContext.
    /// Embora o container de DI descarte escopos Scoped automaticamente em requisições HTTP,
    /// a herança protege a aplicação contra Memory Leaks (vazamento de memória) e Connection Starvation
    /// caso o Unit of Work seja consumido manualmente em Background Services, filas ou testes.
    /// </summary>
    public interface IUnitOfWorkRepository : IDisposable
    {
        // 1. Expõe os repositórios 
        ILancamentoRepository Lancamentos { get; }
        IOutboxEventRepository OutboxEvents { get; }
        ISaldoConsolidadoRepository SaldosConsolidados { get; }

        // 2. Método de Commit com suporte a cancelamento, padrão do EF Core
        Task<bool> CommitAsync(CancellationToken cancellationToken = default);
    }

}
