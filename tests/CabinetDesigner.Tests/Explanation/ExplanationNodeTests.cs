using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Explanation;

public sealed class ExplanationNodeTests
{
    [Fact]
    public void ExplanationNode_IsImmutableRecordValue()
    {
        var nodeId = ExplanationNodeId.New();
        var commandId = CommandId.New();
        var edge = new ExplanationEdge
        {
            SourceNodeId = nodeId,
            TargetNodeId = ExplanationNodeId.New(),
            EdgeType = ExplanationEdgeType.ChildOf
        };

        var first = new ExplanationNode
        {
            Id = nodeId,
            NodeType = ExplanationNodeType.CommandRoot,
            CommandId = commandId,
            Timestamp = DateTimeOffset.UnixEpoch,
            StageNumber = null,
            DecisionType = "layout.add_cabinet_to_run",
            Description = "Add cabinet.",
            AffectedEntityIds = ["run-1"],
            Edges = [edge],
            Context = new Dictionary<string, string> { ["intent"] = "Add cabinet." }
        };
        var second = first with { };

        Assert.Equal(first, second);
        Assert.Null(second.StageNumber);
    }

    [Fact]
    public void ExplanationNode_StageNumber_NullForCommandRoot()
    {
        var node = new ExplanationNode
        {
            Id = ExplanationNodeId.New(),
            NodeType = ExplanationNodeType.CommandRoot,
            CommandId = CommandId.New(),
            Timestamp = DateTimeOffset.UnixEpoch,
            StageNumber = null,
            DecisionType = "layout.add_cabinet_to_run",
            Description = "Add cabinet.",
            AffectedEntityIds = [],
            Edges = [],
            Context = null
        };

        Assert.Null(node.StageNumber);
    }
}
