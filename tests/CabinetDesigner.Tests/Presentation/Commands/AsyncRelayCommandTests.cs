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
            logger,
            eventBus);

        await command.ExecuteAsync();

        var logEntry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logEntry.Level);
        Assert.Equal("Presentation", logEntry.Category);
        Assert.Equal("Async command execution failed.", logEntry.Message);
        Assert.IsType<InvalidOperationException>(logEntry.Exception);
        Assert.Equal("boom", logEntry.Exception!.Message);
        Assert.NotNull(capturedEvent);
        Assert.Equal("boom", capturedEvent!.Message);
        Assert.IsType<InvalidOperationException>(capturedEvent.Exception);
        Assert.False(command.IsExecuting);
    }

    [Fact]
    public void Constructor_RequiresLogger()
    {
        var eventBus = new ApplicationEventBus();

        Assert.Throws<ArgumentNullException>(() => new AsyncRelayCommand(
            () => Task.CompletedTask,
            null!,
            eventBus));
    }
}
