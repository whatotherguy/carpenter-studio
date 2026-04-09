using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Handlers;

public interface IDesignCommandHandler
{
    Task<CommandResultDto> ExecuteAsync(IDesignCommand command, CancellationToken ct = default);
}
