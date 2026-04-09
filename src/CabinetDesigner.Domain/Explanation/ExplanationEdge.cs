using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Explanation;

public sealed record ExplanationEdge
{
    public required ExplanationNodeId SourceNodeId { get; init; }

    public required ExplanationNodeId TargetNodeId { get; init; }

    public required ExplanationEdgeType EdgeType { get; init; }

    public string? Label { get; init; }
}
