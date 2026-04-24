using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Tests.Presentation;
using Xunit;

namespace CabinetDesigner.Tests.App;

public sealed class AppUnhandledExceptionTests
{
    [Fact]
    public void AppUnhandledException_IsLoggedAtError_WithAppCategory()
    {
        var logger = new CapturingAppLogger();
        var eventBus = new ApplicationEventBus();
        CommandExecutionFailedEvent? publishedEvent = null;
        eventBus.Subscribe<CommandExecutionFailedEvent>(@event => publishedEvent = @event);

        var exception = new InvalidOperationException("boom");

        var correlationId = UserActionErrorReporter.Report(
            logger,
            eventBus,
            "App",
            "app.unhandled",
            "Unhandled exception in application.",
            exception);

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Equal("App", logEntry.Category);
        Assert.Equal("Unhandled exception in application.", logEntry.Message);
        Assert.Equal("app.unhandled", logEntry.Properties!["commandName"]);
        Assert.Equal(correlationId.ToString("N"), logEntry.Properties!["correlationId"]);
        Assert.Same(exception, logEntry.Exception);

        Assert.NotNull(publishedEvent);
        Assert.Equal("app.unhandled", publishedEvent!.CommandName);
        Assert.Equal(exception.Message, publishedEvent.Message);
        Assert.Equal(correlationId, publishedEvent.CorrelationId);
    }
}
