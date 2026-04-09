using System.Collections.Generic;

namespace CabinetDesigner.Domain.Commands;

/// <summary>
/// Immutable description of a user's design intent.
/// Does NOT execute itself. The ResolutionOrchestrator owns execution.
/// </summary>
public interface IDesignCommand
{
    CommandMetadata Metadata { get; }

    string CommandType { get; }

    /// <summary>
    /// Pure structural validation only.
    /// Contextual validation is performed by the orchestrator pipeline.
    /// </summary>
    IReadOnlyList<ValidationIssue> ValidateStructure();
}
