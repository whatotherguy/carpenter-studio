namespace CabinetDesigner.Domain.Commands;

/// <summary>
/// Editor interaction that does not change design state.
/// Never submitted to the ResolutionOrchestrator.
/// </summary>
public interface IEditorCommand
{
    CommandMetadata Metadata { get; }

    string CommandType { get; }
}
