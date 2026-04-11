using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Application.Persistence;

namespace CabinetDesigner.Application.Handlers;

public sealed class DesignCommandHandler : IDesignCommandHandler
{
    private readonly IResolutionOrchestrator _orchestrator;
    private readonly IApplicationEventBus _eventBus;
    private readonly ICommandPersistencePort _persistencePort;
    private readonly IAppLogger? _logger;

    public DesignCommandHandler(
        IResolutionOrchestrator orchestrator,
        IApplicationEventBus eventBus,
        ICommandPersistencePort persistencePort,
        IAppLogger? logger = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _persistencePort = persistencePort ?? throw new ArgumentNullException(nameof(persistencePort));
        _logger = logger;
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
            try
            {
                await _persistencePort.CommitCommandAsync(command, result, ct).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger?.Log(new LogEntry
                {
                    Level = LogLevel.Error,
                    Category = "Persistence",
                    Message = "Failed to persist command result; in-memory state may be ahead of the database.",
                    Timestamp = DateTimeOffset.UtcNow,
                    CommandId = command.Metadata.CommandId.Value.ToString(),
                    Properties = new Dictionary<string, string>
                    {
                        ["commandType"] = command.CommandType,
                        ["origin"] = command.Metadata.Origin.ToString()
                    },
                    Exception = exception
                });
                throw;
            }

            _eventBus.Publish(new DesignChangedEvent(dto));
        }

        return dto;
    }
}
