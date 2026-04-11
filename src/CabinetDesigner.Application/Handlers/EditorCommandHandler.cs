using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Handlers;

public sealed class EditorCommandHandler : IEditorCommandHandler
{
    private readonly IEditorStateManager _editorStateManager;
    private readonly IAppLogger? _logger;

    public EditorCommandHandler(IEditorStateManager editorStateManager, IAppLogger? logger = null)
    {
        _editorStateManager = editorStateManager ?? throw new ArgumentNullException(nameof(editorStateManager));
        _logger = logger;
    }

    public void Execute(IEditorCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            _editorStateManager.Apply(command);
        }
        catch (Exception exception)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Error,
                Category = "Editor",
                Message = "Unhandled exception while applying editor command.",
                Timestamp = DateTimeOffset.UtcNow,
                CommandId = command.Metadata.CommandId.Value.ToString(),
                Properties = new Dictionary<string, string>
                {
                    ["commandType"] = command.CommandType
                },
                Exception = exception
            });
            throw;
        }
    }
}
