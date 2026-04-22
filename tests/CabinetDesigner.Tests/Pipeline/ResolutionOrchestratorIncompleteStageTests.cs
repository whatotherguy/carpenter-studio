using CabinetDesigner.Application;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class ResolutionOrchestratorIncompleteStageTests
{
    [Fact]
    public void Execute_NotImplementedStage_InFullMode_FailsPipeline()
    {
        var orchestrator = CreateOrchestrator([new NotImplementedStageHelper(1)]);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues, issue => issue.Code == "STAGE_NOT_IMPLEMENTED");
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("Stage 1 'Stage 1' is not implemented.", issue.Message);
    }

    [Fact]
    public void Preview_NotImplementedStage_StillWarnsOnly()
    {
        var orchestrator = CreateOrchestrator([new NotImplementedStageHelper(1), new SpatialStageHelper(3)]);

        var result = orchestrator.Preview(new TestDesignCommand([]));

        Assert.True(result.Success);
        var issue = Assert.Single(result.Issues, issue => issue.Code == "STAGE_NOT_IMPLEMENTED");
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("Stage 1 'Stage 1' is not implemented.", issue.Message);
    }

    private static ResolutionOrchestrator CreateOrchestrator(IEnumerable<IResolutionStage> stages) =>
        new(
            new SimpleRecordingDeltaTracker(),
            new SimpleRecordingWhyEngine(),
            new SimpleRecordingUndoStack(),
            new SimpleRecordingStateManager(),
            stateStore: null,
            logger: (IResolutionOrchestratorLogger?)null,
            stages: stages,
            validationResultStore: null,
            packagingResultStore: null,
            catalogService: null,
            costingPolicy: null,
            currentPersistedProjectState: null,
            previousCostLookup: null);

    private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> Issues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test", []);

        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => Issues;
    }

    private sealed class NotImplementedStageHelper(int stageNumber) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => $"Stage {stageNumber}";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context) => StageResult.NotImplementedYet(stageNumber);
    }

    private sealed class SpatialStageHelper(int stageNumber) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => "Spatial Resolution";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context)
        {
            context.SpatialResult = new SpatialResolutionResult
            {
                SlotPositionUpdates = [],
                AdjacencyChanges = [],
                RunSummaries = [],
                Placements = []
            };

            return StageResult.Succeeded(stageNumber);
        }
    }

    private sealed class SimpleRecordingDeltaTracker : IDeltaTracker
    {
        public void Begin() { }

        public void RecordDelta(StateDelta delta) { }

        public IReadOnlyList<StateDelta> Finalize() => [];
    }

    private sealed class SimpleRecordingWhyEngine : IWhyEngine
    {
        public IReadOnlyList<ExplanationNodeId> RecordCommand(IDesignCommand command, IReadOnlyList<StateDelta> deltas) => [];

        public ExplanationNodeId RecordDecision(
            CommandId commandId,
            int stageNumber,
            string decisionType,
            string description,
            IReadOnlyList<string>? affectedEntityIds = null,
            IReadOnlyDictionary<string, string>? context = null,
            ExplanationRuleRecord? rule = null) => ExplanationNodeId.New();

        public ExplanationNodeId RecordDecisionWithEdges(
            CommandId commandId,
            int stageNumber,
            string decisionType,
            string description,
            IReadOnlyList<ExplanationEdge> edges,
            IReadOnlyList<string>? affectedEntityIds = null,
            IReadOnlyDictionary<string, string>? context = null,
            ExplanationRuleRecord? rule = null) => ExplanationNodeId.New();

        public IReadOnlyList<ExplanationNodeId> RecordUndo(
            CommandMetadata commandMetadata,
            IReadOnlyList<StateDelta> deltas,
            IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds) => [];

        public IReadOnlyList<ExplanationNodeId> RecordRedo(
            CommandMetadata commandMetadata,
            IReadOnlyList<StateDelta> deltas,
            IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds) => [];

        public IReadOnlyList<ExplanationNode> GetEntityHistory(string entityId) => [];

        public IReadOnlyList<ExplanationNode> GetCommandExplanation(CommandId commandId) => [];

        public ExplanationNode? GetCommandRoot(CommandId commandId) => null;

        public IReadOnlyList<ExplanationNode> GetStageDecisions(int stageNumber) => [];

        public IReadOnlyList<ExplanationNode> GetDecisionsByRule(string ruleId) => [];

        public IReadOnlyList<ExplanationNode> GetDecisionTrail(string entityId) => [];

        public ExplanationNode? GetPropertyExplanation(string entityId, string propertyName) => null;

        public IReadOnlyList<ExplanationNode> GetNodesByStatus(ExplanationNodeStatus status) => [];

        public IReadOnlyList<ExplanationNode> GetAllNodes() => [];
    }

    private sealed class SimpleRecordingUndoStack : IUndoStack
    {
        public bool CanUndo => false;

        public bool CanRedo => false;

        public IReadOnlyList<UndoEntry> Journal => [];

        public void Push(UndoEntry entry) { }

        public UndoEntry? Undo() => null;

        public UndoEntry? Redo() => null;

        public void Clear() { }
    }

    private sealed class SimpleRecordingStateManager : IStateManager
    {
        public void RemoveEntity(string entityId, string entityType) { }

        public void RestoreValues(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values) { }

        public void RestoreEntity(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values) { }
    }
}
