namespace CabinetDesigner.Application.Persistence;

public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);

    Task RollbackAsync(CancellationToken ct = default);
}
