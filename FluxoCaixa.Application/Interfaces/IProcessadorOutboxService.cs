namespace FluxoCaixa.Application.Interfaces
{
    public interface IProcessadorOutboxService
    {
        Task ProcessarAsync(CancellationToken cancellationTokenm);
    }
}
