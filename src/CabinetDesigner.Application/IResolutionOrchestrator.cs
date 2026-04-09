using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Application.Pipeline;

namespace CabinetDesigner.Application;

/// <summary>
/// The single execution choke point for all design state changes.
/// Every IDesignCommand flows through here.
/// </summary>
public interface IResolutionOrchestrator
{
    /// <summary>
    /// Execute a design command through the full resolution pipeline.
    /// Returns a failure result for business logic failures instead of throwing.
    /// </summary>
    CommandResult Execute(IDesignCommand command);

    /// <summary>
    /// Execute a design command through the preview pipeline.
    /// Preview is read-only and runs only the fast-path stages.
    /// </summary>
    PreviewResult Preview(IDesignCommand command);

    /// <summary>Undo the most recent command. Returns null when nothing can be undone.</summary>
    CommandResult? Undo();

    /// <summary>Redo the most recently undone command. Returns null when nothing can be redone.</summary>
    CommandResult? Redo();
}
