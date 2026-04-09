using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Commands;

public sealed record CommandResult
{
    public required CommandMetadata CommandMetadata { get; init; }
    public required bool Success { get; init; }
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    public required IReadOnlyList<StateDelta> Deltas { get; init; }
    public required IReadOnlyList<ExplanationNodeId> ExplanationNodeIds { get; init; }

    public static CommandResult Succeeded(
        CommandMetadata metadata,
        IReadOnlyList<StateDelta> deltas,
        IReadOnlyList<ExplanationNodeId> explanationNodeIds,
        IReadOnlyList<ValidationIssue>? warnings = null) => new()
    {
        CommandMetadata = metadata,
        Success = true,
        Deltas = deltas,
        ExplanationNodeIds = explanationNodeIds,
        Issues = warnings ?? []
    };

    public static CommandResult Failed(
        CommandMetadata metadata,
        IReadOnlyList<ValidationIssue> issues) => new()
    {
        CommandMetadata = metadata,
        Success = false,
        Deltas = [],
        ExplanationNodeIds = [],
        Issues = issues
    };

    public static CommandResult Rejected(
        CommandMetadata metadata,
        IReadOnlyList<ValidationIssue> issues) => Failed(metadata, issues);
}
