using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline;

public sealed record StageResult
{
    public required int StageNumber { get; init; }

    public required bool Success { get; init; }

    /// <summary>
    /// <see langword="true"/> when the stage is a known skeleton that has not yet been
    /// implemented.  The pipeline continues normally, but the orchestrator will surface a
    /// diagnostic warning so developers and testers can identify which stages are still stubs.
    /// </summary>
    public bool IsNotImplemented { get; init; }

    public required IReadOnlyList<ValidationIssue> Issues { get; init; }

    public required IReadOnlyList<ExplanationNodeId> ExplanationNodeIds { get; init; }

    public static StageResult Succeeded(
        int stageNumber,
        IReadOnlyList<ExplanationNodeId>? explanationNodeIds = null,
        IReadOnlyList<ValidationIssue>? warnings = null) => new()
    {
        StageNumber = stageNumber,
        Success = true,
        Issues = warnings ?? [],
        ExplanationNodeIds = explanationNodeIds ?? []
    };

    public static StageResult Failed(
        int stageNumber,
        IReadOnlyList<ValidationIssue> issues,
        IReadOnlyList<ExplanationNodeId>? explanationNodeIds = null) => new()
    {
        StageNumber = stageNumber,
        Success = false,
        Issues = issues,
        ExplanationNodeIds = explanationNodeIds ?? []
    };

    /// <summary>
    /// Returns a successful result that is flagged as not yet implemented.  Use this in
    /// skeleton stage implementations so that the pipeline can continue while making it
    /// obvious in diagnostics that the stage is a stub.
    /// </summary>
    public static StageResult NotImplementedYet(
        int stageNumber,
        IReadOnlyList<ExplanationNodeId>? explanationNodeIds = null) => new()
    {
        StageNumber = stageNumber,
        Success = true,
        IsNotImplemented = true,
        Issues = [],
        ExplanationNodeIds = explanationNodeIds ?? []
    };
}
