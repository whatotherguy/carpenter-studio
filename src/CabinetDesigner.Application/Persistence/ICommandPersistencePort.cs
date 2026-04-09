using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Persistence;

public interface ICommandPersistencePort
{
    Task CommitCommandAsync(IDesignCommand command, CommandResult result, CancellationToken ct = default);
}
