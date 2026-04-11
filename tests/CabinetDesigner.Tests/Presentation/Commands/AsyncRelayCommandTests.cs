using CabinetDesigner.Presentation.Commands;
using Xunit;

namespace CabinetDesigner.Tests.Presentation.Commands;

public sealed class AsyncRelayCommandTests
{
    [Fact]
    public async Task ExecuteAsync_WhenDelegateThrows_CallsExceptionHandler()
    {
        var capturedEx = (Exception?)null;
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("boom"),
            onException: ex => capturedEx = ex);

        await command.ExecuteAsync();

        Assert.NotNull(capturedEx);
        Assert.Equal("boom", capturedEx!.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDelegateThrows_ResetsIsExecutingToFalse()
    {
        var command = new AsyncRelayCommand(
            () => throw new InvalidOperationException("boom"),
            onException: _ => { });

        await command.ExecuteAsync();

        Assert.False(command.IsExecuting);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoExceptionHandler_DoesNotPropagateExceptionToCaller()
    {
        var command = new AsyncRelayCommand(() => throw new InvalidOperationException("silent"));

        // Should not throw — exception is swallowed when no handler is provided.
        var ex = await Record.ExceptionAsync(() => command.ExecuteAsync());
        Assert.Null(ex);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDelegateSucceeds_ExceptionHandlerIsNotCalled()
    {
        var handlerCalled = false;
        var command = new AsyncRelayCommand(
            () => Task.CompletedTask,
            onException: _ => handlerCalled = true);

        await command.ExecuteAsync();

        Assert.False(handlerCalled);
    }
}
