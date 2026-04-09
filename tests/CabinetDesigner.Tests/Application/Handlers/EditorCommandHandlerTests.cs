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

    private sealed class RecordingEditorStateManager : IEditorStateManager
    {
        public IEditorCommand? AppliedCommand { get; private set; }

        public void Apply(IEditorCommand command) => AppliedCommand = command;
    }

    private sealed record TestEditorCommand : IEditorCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Editor command", []);

        public string CommandType => "editor.test";
    }
}
