using System.Data;

namespace CabinetDesigner.Application.Persistence;

public interface IDbConnectionFactory
{
    Task<IDbConnection> OpenConnectionAsync(CancellationToken ct = default);
}
