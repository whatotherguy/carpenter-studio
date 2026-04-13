using CabinetDesigner.Application.Explanation;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Explanation;

public sealed class WhyEngineTests
{
    [Fact]
    public void RecordCommand_CreatesCommandRootAndDeltaReferenceNodes()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Add cabinet", ["run-1"]);
        StateDelta[] deltas =
        [
            new("cabinet-1", "Cabinet", DeltaOperation.Created),
            new("run-1", "Run", DeltaOperation.Modified)
        ];

        var recordedNodeIds = engine.RecordCommand(command, deltas);

        var root = Assert.IsType<ExplanationNode>(engine.GetCommandRoot(command.Metadata.CommandId));
        Assert.Equal(ExplanationNodeType.CommandRoot, root.NodeType);
        Assert.Equal(command.Metadata.CommandId, root.CommandId);
        Assert.Equal(3, recordedNodeIds.Count);

        var commandNodes = engine.GetCommandExplanation(command.Metadata.CommandId);
        Assert.Equal([ExplanationNodeType.CommandRoot, ExplanationNodeType.DeltaReference, ExplanationNodeType.DeltaReference], commandNodes.Select(node => node.NodeType));
        Assert.All(commandNodes.Skip(1), node =>
            Assert.Contains(node.Edges, edge => edge.EdgeType == ExplanationEdgeType.ChildOf && edge.TargetNodeId == root.Id));
    }

    [Fact]
    public void RecordDecision_BeforeRecordCommand_IsIndexedByCommandId()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Resize cabinet", ["cabinet-1"]);

        var decisionId = engine.RecordDecision(
            command.Metadata.CommandId,
            4,
            "assembly_resolved",
            "Resolved cabinet assembly.",
            ["cabinet-1"]);

        var nodes = engine.GetCommandExplanation(command.Metadata.CommandId);

        Assert.Single(nodes);
        Assert.Equal(decisionId, nodes[0].Id);
    }

    [Fact]
    public void RecordDecision_AfterRecordCommand_HasChildOfEdgeToRoot()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Resize cabinet", ["cabinet-1"]);
        var rootId = engine.RecordCommand(command, []).Single();

        var decisionId = engine.RecordDecision(
            command.Metadata.CommandId,
            5,
            "material_assigned",
            "Assigned material.",
            ["cabinet-1"]);

        var decision = engine.GetAllNodes().Single(node => node.Id == decisionId);

        Assert.Contains(decision.Edges, edge => edge.EdgeType == ExplanationEdgeType.ChildOf && edge.TargetNodeId == rootId);
    }

    [Fact]
    public void RecordDecisionWithEdges_PreservesExplicitLinks()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Resize cabinet", ["cabinet-1"]);
        var causeId = engine.RecordDecision(
            command.Metadata.CommandId,
            4,
            "filler_required",
            "Filler required.",
            ["cabinet-1"]);

        var decisionId = engine.RecordDecisionWithEdges(
            command.Metadata.CommandId,
            5,
            "material_assigned",
            "Assigned filler material.",
            [
                new ExplanationEdge
                {
                    SourceNodeId = ExplanationNodeId.New(),
                    TargetNodeId = causeId,
                    EdgeType = ExplanationEdgeType.CausedBy,
                    Label = "filler_material"
                }
            ],
            ["cabinet-1"]);

        var decision = engine.GetAllNodes().Single(node => node.Id == decisionId);
        var edge = Assert.Single(decision.Edges);
        Assert.Equal(decisionId, edge.SourceNodeId);
        Assert.Equal(causeId, edge.TargetNodeId);
        Assert.Equal(ExplanationEdgeType.CausedBy, edge.EdgeType);
    }

    [Fact]
    public void Queries_AggregateByEntityStageRuleAndProperty()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Resize cabinet", ["cabinet-1", "run-1"]);
        var rule = new ExplanationRuleRecord
        {
            RuleId = "shop_standard.default_reveal",
            RuleName = "Default Reveal",
            Category = ExplanationRuleCategory.ShopStandard,
            Source = "shop_standard.default",
            Description = "Applies the default reveal."
        };

        engine.RecordDecision(
            command.Metadata.CommandId,
            4,
            "reveal_calculated",
            "Calculated reveal.",
            ["cabinet-1", "run-1"],
            new Dictionary<string, string> { ["property:NominalWidth"] = "36" },
            rule);

        var entityHistory = engine.GetEntityHistory("cabinet-1");
        var stageHistory = engine.GetStageDecisions(4);
        var ruleHistory = engine.GetDecisionsByRule("shop_standard.default_reveal");
        var propertyExplanation = engine.GetPropertyExplanation("cabinet-1", "NominalWidth");

        Assert.Single(entityHistory);
        Assert.Single(stageHistory);
        Assert.Single(ruleHistory);
        Assert.Same(entityHistory[0], stageHistory[0]);
        Assert.Same(entityHistory[0], ruleHistory[0]);
        Assert.Equal(entityHistory[0].Id, propertyExplanation?.Id);
    }

    [Fact]
    public void RecordUndoAndRedo_UpdateStatusProjection()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Resize cabinet", ["cabinet-1"]);
        var decisionId = engine.RecordDecision(
            command.Metadata.CommandId,
            4,
            "slot_placed",
            "Placed cabinet.",
            ["cabinet-1"]);

        engine.RecordUndo(command.Metadata, [], [decisionId]);
        var undone = engine.GetNodesByStatus(ExplanationNodeStatus.Undone);

        engine.RecordRedo(command.Metadata, [], [decisionId]);
        var redone = engine.GetNodesByStatus(ExplanationNodeStatus.Redone);

        Assert.Contains(undone, node => node.Id == decisionId);
        Assert.Contains(redone, node => node.Id == decisionId);
    }

    [Fact]
    public void GetDecisionTrail_FollowsCausalOrderDeterministically()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Resize cabinet", ["cabinet-1"]);
        var rootId = engine.RecordCommand(command, []).Single();
        var parentId = engine.RecordDecision(
            command.Metadata.CommandId,
            4,
            "assembly_resolved",
            "Resolved assembly.",
            ["cabinet-1"]);
        var childId = engine.RecordDecisionWithEdges(
            command.Metadata.CommandId,
            5,
            "material_assigned",
            "Assigned material.",
            [
                new ExplanationEdge
                {
                    SourceNodeId = ExplanationNodeId.New(),
                    TargetNodeId = parentId,
                    EdgeType = ExplanationEdgeType.CausedBy
                }
            ],
            ["cabinet-1"]);

        var trail = engine.GetDecisionTrail("cabinet-1");

        Assert.Equal([rootId, parentId, childId], trail.Select(node => node.Id));
    }

    [Fact]
    public void GetAllNodes_ReturnsAppendOnlyDeterministicOrder()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Resize cabinet", ["cabinet-1"]);
        var firstId = engine.RecordDecision(command.Metadata.CommandId, 1, "entity_resolved", "Resolved entity.", ["cabinet-1"]);
        var secondId = engine.RecordDecision(command.Metadata.CommandId, 2, "slot_placed", "Placed slot.", ["cabinet-1"]);
        var thirdId = engine.RecordCommand(command, []).Single();

        var nodes = engine.GetAllNodes();

        Assert.Equal([firstId, secondId, thirdId], nodes.Select(node => node.Id));
    }

    [Fact]
    public async Task ConcurrentWrites_NoExceptionThrown_AllNodesRecorded()
    {
        var engine = new WhyEngine();
        var tasks = new List<Task>();

        for (int i = 0; i < 4; i++)
        {
            int taskIndex = i;
            var task = Task.Run(() =>
            {
                var command = CreateCommand($"Command {taskIndex}", [$"entity-{taskIndex}"]);
                var deltas = new[]
                {
                    new StateDelta($"entity-{taskIndex}", "TestEntity", DeltaOperation.Created),
                    new StateDelta($"entity-{taskIndex}", "TestEntity", DeltaOperation.Modified)
                };

                engine.RecordCommand(command, deltas);
            });
            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        var allNodes = engine.GetAllNodes();
        // 4 tasks * (1 command root + 2 deltas) = 12 nodes total
        Assert.Equal(12, allNodes.Count);
    }

    [Fact]
    public async Task ConcurrentReadWrite_NoCollectionModifiedException_NoExceptionThrown()
    {
        var engine = new WhyEngine();
        var command = CreateCommand("Concurrent test", ["entity-1"]);
        var exceptionThrown = false;

        var writeTask = Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    var deltas = new[] { new StateDelta($"entity-{i}", "TestEntity", DeltaOperation.Created) };
                    engine.RecordCommand(command, deltas);
                }
                catch (InvalidOperationException)
                {
                    exceptionThrown = true;
                }
            }
        });

        var readTask = Task.Run(() =>
        {
            for (int i = 0; i < 50; i++)
            {
                try
                {
                    _ = engine.GetAllNodes();
                }
                catch (InvalidOperationException)
                {
                    exceptionThrown = true;
                }
            }
        });

        await Task.WhenAll(writeTask, readTask);

        Assert.False(exceptionThrown, "No InvalidOperationException should be thrown during concurrent read/write");
    }

    private static TestDesignCommand CreateCommand(string intentDescription, IReadOnlyList<string> affectedEntityIds) =>
        new(CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, intentDescription, affectedEntityIds));

    private sealed record TestDesignCommand(CommandMetadata Metadata) : IDesignCommand
    {
        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
