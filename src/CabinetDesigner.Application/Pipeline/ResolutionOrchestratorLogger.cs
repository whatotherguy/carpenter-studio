using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline;

public sealed class ResolutionOrchestratorLogger : IResolutionOrchestratorLogger
{
    private readonly IAppLogger _logger;

    public ResolutionOrchestratorLogger(IAppLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogUnhandledException(IDesignCommand command, ResolutionMode mode, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(exception);

        _logger.Log(new LogEntry
        {
            Level = LogLevel.Error,
            Category = "Pipeline",
            Message = "Unhandled exception during resolution.",
            Timestamp = DateTimeOffset.UtcNow,
            CommandId = command.Metadata.CommandId.Value.ToString(),
            Properties = new Dictionary<string, string>
            {
                ["commandType"] = command.CommandType,
                ["mode"] = mode.ToString(),
                ["origin"] = command.Metadata.Origin.ToString()
            },
            Exception = exception
        });
    }
}
