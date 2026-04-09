using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline;

public sealed record StageResult
{
    public required int StageNumber { get; init; }

    public required bool Success { get; init; }

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
}
