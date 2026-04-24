using CabinetDesigner.Application.Events;

namespace CabinetDesigner.Application.Diagnostics;

public static class UserActionErrorReporter
{
    public static Guid Report(
        IAppLogger logger,
        IApplicationEventBus eventBus,
        string category,
        string commandName,
        string logMessage,
        Exception exception,
        Guid? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(eventBus);
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        ArgumentException.ThrowIfNullOrWhiteSpace(logMessage);
        ArgumentNullException.ThrowIfNull(exception);

        var id = correlationId ?? Guid.NewGuid();
        logger.Log(new LogEntry
        {
            Level = LogLevel.Error,
            Category = category,
            Message = logMessage,
            Timestamp = DateTimeOffset.UtcNow,
            Properties = new Dictionary<string, string>
            {
                ["commandName"] = commandName,
                ["correlationId"] = id.ToString("N")
            },
            Exception = exception
        });

        eventBus.Publish(new CommandExecutionFailedEvent(commandName, exception.Message, exception, id));
        return id;
    }

    public static bool IsFatal(Exception exception) =>
        exception is StackOverflowException or OutOfMemoryException or AccessViolationException;
}
