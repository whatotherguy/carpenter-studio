using System.Data;

namespace CabinetDesigner.Application.Persistence;

public interface ISchemaMigration
{
    int Version { get; }

    string Description { get; }

    void Apply(IDbConnection connection, IDbTransaction transaction);
}
