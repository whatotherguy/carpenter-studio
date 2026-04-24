using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Presentation.Commands;
using CabinetDesigner.Tests.Presentation;
using Xunit;

namespace CabinetDesigner.Tests.Presentation.Commands;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_ExceptionThrown_IsLoggedAndSurfacedOnEventBus()
    {
        var logger = new CapturingAppLogger();
        var eventBus = new ApplicationEventBus();
        CommandExecutionFailedEvent? capturedEvent = null;
        eventBus.Subscribe<CommandExecutionFailedEvent>(@event => capturedEvent = @event);
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("boom"),
            "test.command",
            logger,
            eventBus);

        await command.ExecuteAsync();

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Equal("Presentation", logEntry.Category);
        Assert.Equal("Async command execution failed.", logEntry.Message);
        Assert.Equal("test.command", logEntry.Properties!["commandName"]);
        Assert.False(string.IsNullOrWhiteSpace(logEntry.Properties!["correlationId"]));
        Assert.IsType<InvalidOperationException>(logEntry.Exception);
        Assert.Equal("boom", logEntry.Exception!.Message);
        Assert.NotNull(capturedEvent);
        Assert.Equal("test.command", capturedEvent!.CommandName);
        Assert.Equal("boom", capturedEvent!.Message);
        Assert.IsType<InvalidOperationException>(capturedEvent.Exception);
        Assert.NotEqual(Guid.Empty, capturedEvent.CorrelationId);
        Assert.Equal(logEntry.Properties["correlationId"], capturedEvent.CorrelationId.ToString("N"));
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public void Constructor_RequiresCommandName()
    {
        var constructor = Assert.Single(typeof(AsyncRelayCommand).GetConstructors());
        Assert.Contains(constructor.GetParameters(), parameter =>
            parameter.ParameterType == typeof(string) &&
            string.Equals(parameter.Name, "commandName", StringComparison.Ordinal));

        var eventBus = new ApplicationEventBus();

        Assert.Throws<ArgumentException>(() => new AsyncRelayCommand(
            () => Task.CompletedTask,
            string.Empty,
            new CapturingAppLogger(),
            eventBus));
    }

    [Fact]
    public void Constructor_RequiresLogger()
    {
        var eventBus = new ApplicationEventBus();

        Assert.Throws<ArgumentNullException>(() => new AsyncRelayCommand(
            () => Task.CompletedTask,
            "test.command",
            null!,
            eventBus));
    }
}
