using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Domain.Commands;
using Xunit;

namespace CabinetDesigner.Tests.Application.Handlers;

public sealed class EditorCommandHandlerTests
{
    [Fact]
    public void Execute_AppliesCommandToEditorStateOnly()
    {
        var editorState = new RecordingEditorStateManager();
        var handler = new EditorCommandHandler(editorState);
        var command = new TestEditorCommand();

        handler.Execute(command);

        Assert.Same(command, editorState.AppliedCommand);
    }

    [Fact]
    public void Execute_WhenEditorStateManagerThrows_LogsErrorAndRethrows()
    {
        var editorState = new ThrowingEditorStateManager();
        var logger = new CapturingLogger();
        var handler = new EditorCommandHandler(editorState, logger);
        var command = new TestEditorCommand();

        var ex = Assert.Throws<InvalidOperationException>(() => handler.Execute(command));

        Assert.Equal("simulated editor failure", ex.Message);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Editor", entry.Category);
        Assert.NotNull(entry.Exception);
    }

    private sealed class RecordingEditorStateManager : IEditorStateManager
    {
        public IEditorCommand? AppliedCommand { get; private set; }

        public void Apply(IEditorCommand command) => AppliedCommand = command;
    }

    private sealed class ThrowingEditorStateManager : IEditorStateManager
    {
        public void Apply(IEditorCommand command) =>
            throw new InvalidOperationException("simulated editor failure");
    }

    private sealed class CapturingLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }

    private sealed record TestEditorCommand : IEditorCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Editor command", []);

        public string CommandType => "editor.test";
    }
}
