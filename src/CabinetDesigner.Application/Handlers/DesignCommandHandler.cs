using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Application.Persistence;

namespace CabinetDesigner.Application.Handlers;

public sealed class DesignCommandHandler : IDesignCommandHandler
{
    private readonly IResolutionOrchestrator _orchestrator;
    private readonly IApplicationEventBus _eventBus;
    private readonly ICommandPersistencePort _persistencePort;

    public DesignCommandHandler(
        IResolutionOrchestrator orchestrator,
        IApplicationEventBus eventBus,
        ICommandPersistencePort persistencePort)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _persistencePort = persistencePort ?? throw new ArgumentNullException(nameof(persistencePort));
    }

    public async Task<CommandResultDto> ExecuteAsync(IDesignCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var structureIssues = command.ValidateStructure();
        if (structureIssues.Any(issue => issue.Severity >= ValidationSeverity.Error))
        {
            return CommandResultDto.Rejected(command.Metadata, command.CommandType, structureIssues);
        }

        var result = _orchestrator.Execute(command);
        var dto = CommandResultDto.From(result, command.CommandType);

        if (result.Success)
        {
            await _persistencePort.CommitCommandAsync(command, result, ct).ConfigureAwait(false);
            _eventBus.Publish(new DesignChangedEvent(dto));
        }

        return dto;
    }
}
