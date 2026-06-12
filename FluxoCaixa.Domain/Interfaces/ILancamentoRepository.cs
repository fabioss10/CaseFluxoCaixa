using FluxoCaixa.Domain.Entities;

namespace FluxoCaixa.Domain.Interfaces
{
    /// <summary>
    /// Garanto que o Domain nao precise depender de detalhes de infraestrutura, como o Entity Framework, e possa ser facilmente testável e flexível para mudanças futuras.
    /// /// </summary>
    public interface ILancamentoRepository
    {
        Task AdicionarAsync(Lancamento lancamento);

        Task<Lancamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
