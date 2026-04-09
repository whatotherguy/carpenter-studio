namespace CabinetDesigner.Application.Diagnostics;

public interface IAppLogger
{
    void Log(LogEntry entry);
}

public sealed record LogEntry
{
    public required LogLevel Level { get; init; }

    public required string Category { get; init; }

    public required string Message { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public string? CommandId { get; init; }

    public string? StageNumber { get; init; }

    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    public Exception? Exception { get; init; }
}

public enum LogLevel
{
    Trace,
    Debug,
    Info,
    Warning,
    Error,
    Fatal
}
