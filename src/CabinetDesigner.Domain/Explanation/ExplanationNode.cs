using System;
using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Explanation;

public sealed record ExplanationNode
{
    public required ExplanationNodeId Id { get; init; }

    public required ExplanationNodeType NodeType { get; init; }

    public required CommandId CommandId { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    public int? StageNumber { get; init; }

    public required string DecisionType { get; init; }

    public required string Description { get; init; }

    public required IReadOnlyList<string> AffectedEntityIds { get; init; }

    public required IReadOnlyList<ExplanationEdge> Edges { get; init; }

    public IReadOnlyDictionary<string, string>? Context { get; init; }

    public ExplanationRuleRecord? Rule { get; init; }
}
