using CabinetDesigner.Application;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class ResolutionOrchestratorTests
{
    [Fact]
    public void Execute_WithValidCommand_ReturnsSuccessResult()
    {
        var command = new TestDesignCommand([]);
        var deltaTracker = new RecordingDeltaTracker();
        var whyEngine = new RecordingWhyEngine();
        var undoStack = new RecordingUndoStack();
        var orchestrator = CreateOrchestrator(deltaTracker, whyEngine, undoStack, new RecordingStateManager(), CreatePipeline());

        var result = orchestrator.Execute(command);

        Assert.True(result.Success);
        Assert.Equal(1, deltaTracker.BeginCalls);
        Assert.Equal(1, deltaTracker.FinalizeCalls);
        Assert.Single(undoStack.PushedEntries);
        Assert.Equal(1, whyEngine.RecordCommandCalls);
    }

    [Fact]
    public void Execute_WithStructuralValidationError_ReturnsRejected()
    {
        var command = new TestDesignCommand([new ValidationIssue(ValidationSeverity.Error, "INVALID", "Invalid")]);
        var deltaTracker = new RecordingDeltaTracker();
        var undoStack = new RecordingUndoStack();
        var whyEngine = new RecordingWhyEngine();
        var stages = CreatePipeline();
        var orchestrator = CreateOrchestrator(deltaTracker, whyEngine, undoStack, new RecordingStateManager(), stages);

        var result = orchestrator.Execute(command);

        Assert.False(result.Success);
        Assert.Equal("INVALID", Assert.Single(result.Issues).Code);
        Assert.Equal(0, deltaTracker.BeginCalls);
        Assert.Empty(undoStack.PushedEntries);
        Assert.Empty(stages.SelectMany(stage => stage.ExecutionLog));
    }

    [Fact]
    public void Preview_WithStructuralValidationError_ReturnsFailed()
    {
        var command = new TestDesignCommand([new ValidationIssue(ValidationSeverity.Error, "INVALID", "Invalid")]);
        var deltaTracker = new RecordingDeltaTracker();
        var orchestrator = CreateOrchestrator(deltaTracker, new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), CreatePipeline());

        var result = orchestrator.Preview(command);

        Assert.False(result.Success);
        Assert.Equal("INVALID", Assert.Single(result.Issues).Code);
        Assert.Equal(0, deltaTracker.BeginCalls);
        Assert.Equal(0, deltaTracker.FinalizeCalls);
    }

    [Fact]
    public void Execute_WithStageFailing_HaltsAndReturnsFailed()
    {
        var executionLog = new List<int>();
        var stages = new IResolutionStage[]
        {
            new RecordingStage(1, executionLog),
            new FailingStage(2, executionLog),
            new RecordingStage(3, executionLog)
        };
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.False(result.Success);
        Assert.Equal([1, 2], executionLog);
        Assert.Equal("STAGE_FAIL", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Execute_WithStageFailing_DoesNotPushToUndoStack()
    {
        var undoStack = new RecordingUndoStack();
        var stages = new IResolutionStage[]
        {
            new RecordingStage(1, []),
            new FailingStage(2, [])
        };
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), undoStack, new RecordingStateManager(), stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.False(result.Success);
        Assert.Empty(undoStack.PushedEntries);
    }

    [Fact]
    public void Execute_DeltaTrackerCalledExactlyOnce_OnSuccess()
    {
        var deltaTracker = new RecordingDeltaTracker();
        var orchestrator = CreateOrchestrator(deltaTracker, new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), CreatePipeline());

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.Equal(1, deltaTracker.FinalizeCalls);
    }

    [Fact]
    public void Execute_DeltaTrackerFinalized_OnStageFailure()
    {
        var deltaTracker = new RecordingDeltaTracker();
        var stages = new IResolutionStage[]
        {
            new RecordingStage(1, []),
            new FailingStage(2, [])
        };
        var orchestrator = CreateOrchestrator(deltaTracker, new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.False(result.Success);
        Assert.Equal(1, deltaTracker.FinalizeCalls);
    }

    [Fact]
    public void Execute_SuccessfulCommand_PushesToUndoStack()
    {
        var undoStack = new RecordingUndoStack();
        StateDelta[] deltas = [new("cabinet-1", "Cabinet", DeltaOperation.Created)];
        var deltaTracker = new RecordingDeltaTracker(deltas);
        var orchestrator = CreateOrchestrator(deltaTracker, new RecordingWhyEngine(), undoStack, new RecordingStateManager(), CreatePipeline());

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.True(result.Success);
        var undoEntry = Assert.Single(undoStack.PushedEntries);
        Assert.Same(deltas, undoEntry.Deltas);
        Assert.Equal(result.CommandMetadata, undoEntry.CommandMetadata);
    }

    [Fact]
    public void Execute_SuccessfulCommand_RecordsInWhyEngine()
    {
        var command = new TestDesignCommand([]);
        StateDelta[] deltas = [new("cabinet-1", "Cabinet", DeltaOperation.Created)];
        var deltaTracker = new RecordingDeltaTracker(deltas);
        var whyEngine = new RecordingWhyEngine([ExplanationNodeId.New()]);
        var orchestrator = CreateOrchestrator(deltaTracker, whyEngine, new RecordingUndoStack(), new RecordingStateManager(), CreatePipeline());

        var result = orchestrator.Execute(command);

        Assert.True(result.Success);
        Assert.Equal(1, whyEngine.RecordCommandCalls);
        Assert.Same(command, whyEngine.LastCommand);
        Assert.Same(deltas, whyEngine.LastCommandDeltas);
    }

    [Fact]
    public void Execute_RecursionDepthExceeded_ReturnsFailure()
    {
        var command = new TestDesignCommand([]);
        ResolutionOrchestrator? orchestrator = null;
        var stages = new IResolutionStage[]
        {
            new RecursiveStage(1, () => orchestrator!, command)
        };
        orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), stages);

        var result = orchestrator.Execute(command);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "MAX_RECURSION");
    }

    [Fact]
    public void Execute_WhenStageThrows_ReturnsInternalErrorFailure()
    {
        var logger = new RecordingLogger();
        var orchestrator = CreateOrchestrator(
            new RecordingDeltaTracker(),
            new RecordingWhyEngine(),
            new RecordingUndoStack(),
            new RecordingStateManager(),
            [new ThrowingStage(1)],
            logger);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "INTERNAL_ERROR");
        Assert.Equal(1, logger.Calls);
        Assert.Equal(ResolutionMode.Full, logger.LastMode);
        Assert.IsType<InvalidOperationException>(logger.LastException);
    }

    [Fact]
    public void Execute_AccumulatesWarningsFromStages()
    {
        var warning = new ValidationIssue(ValidationSeverity.Warning, "WARN", "Warning");
        var stages = new IResolutionStage[]
        {
            new WarningStage(1, warning)
        };
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "WARN");
    }

    [Fact]
    public void Preview_RunsOnlyPreviewStages()
    {
        var executionLog = new List<int>();
        var stages = Enumerable.Range(1, 11)
            .Select(number => (IResolutionStage)new ModeAwareStage(number, executionLog))
            .ToArray();
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), stages);

        var result = orchestrator.Preview(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.Equal([1, 2, 3], executionLog);
    }

    [Fact]
    public void Preview_DoesNotTrackDeltas()
    {
        var deltaTracker = new RecordingDeltaTracker();
        var orchestrator = CreateOrchestrator(deltaTracker, new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), CreatePipeline());

        var result = orchestrator.Preview(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.Equal(0, deltaTracker.BeginCalls);
        Assert.Equal(0, deltaTracker.FinalizeCalls);
    }

    [Fact]
    public void Preview_DoesNotPushToUndoStack()
    {
        var undoStack = new RecordingUndoStack();
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), undoStack, new RecordingStateManager(), CreatePipeline());

        var result = orchestrator.Preview(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.Empty(undoStack.PushedEntries);
    }

    [Fact]
    public void Preview_ReturnsPreviewResult()
    {
        var stages = new IResolutionStage[]
        {
            new SpatialPreviewStage()
        };
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), stages);

        var result = orchestrator.Preview(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.NotNull(result.SpatialResult);
    }

    [Fact]
    public void Preview_WhenStageThrows_ReturnsInternalErrorFailure()
    {
        var logger = new RecordingLogger();
        var orchestrator = CreateOrchestrator(
            new RecordingDeltaTracker(),
            new RecordingWhyEngine(),
            new RecordingUndoStack(),
            new RecordingStateManager(),
            [new ThrowingStage(1)],
            logger);

        var result = orchestrator.Preview(new TestDesignCommand([]));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "INTERNAL_ERROR");
        Assert.Equal(1, logger.Calls);
        Assert.Equal(ResolutionMode.Preview, logger.LastMode);
        Assert.IsType<InvalidOperationException>(logger.LastException);
    }

    [Fact]
    public void Execute_WithDuplicateStageNumbers_ThrowsOnConstruction()
    {
        Assert.Throws<InvalidOperationException>(() => CreateOrchestrator(
            new RecordingDeltaTracker(),
            new RecordingWhyEngine(),
            new RecordingUndoStack(),
            new RecordingStateManager(),
            [new RecordingStage(1, []), new RecordingStage(1, [])]));
    }

    [Fact]
    public void Undo_WithHistory_AppliesReverseDeltas()
    {
        var stateManager = new RecordingStateManager();
        var undoStack = new RecordingUndoStack();
        undoStack.UndoResult = new UndoEntry(
            CreateMetadata(),
            [
                new StateDelta("created", "Cabinet", DeltaOperation.Created),
                new StateDelta("modified", "Cabinet", DeltaOperation.Modified, new Dictionary<string, DeltaValue> { ["Width"] = new DeltaValue.OfDecimal(1m) }),
                new StateDelta("removed", "Cabinet", DeltaOperation.Removed, new Dictionary<string, DeltaValue> { ["Width"] = new DeltaValue.OfDecimal(2m) })
            ],
            []);
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), undoStack, stateManager, CreatePipeline());

        var result = orchestrator.Undo();

        Assert.NotNull(result);
        Assert.Equal(
            ["RestoreEntity:removed:Cabinet", "RestoreValues:modified:Cabinet", "RemoveEntity:created:Cabinet"],
            stateManager.Actions);
    }

    [Fact]
    public void Undo_WithNoHistory_ReturnsNull()
    {
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), CreatePipeline());

        Assert.Null(orchestrator.Undo());
    }

    [Fact]
    public void Redo_WithUndoneEntry_AppliesForwardDeltas()
    {
        var stateManager = new RecordingStateManager();
        var undoStack = new RecordingUndoStack();
        undoStack.RedoResult = new UndoEntry(
            CreateMetadata(),
            [
                new StateDelta("created", "Cabinet", DeltaOperation.Created, null, new Dictionary<string, DeltaValue> { ["Width"] = new DeltaValue.OfDecimal(1m) }),
                new StateDelta("modified", "Cabinet", DeltaOperation.Modified, null, new Dictionary<string, DeltaValue> { ["Width"] = new DeltaValue.OfDecimal(2m) }),
                new StateDelta("removed", "Cabinet", DeltaOperation.Removed)
            ],
            []);
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), undoStack, stateManager, CreatePipeline());

        var result = orchestrator.Redo();

        Assert.NotNull(result);
        Assert.Equal(
            ["RestoreEntity:created:Cabinet", "RestoreValues:modified:Cabinet", "RemoveEntity:removed:Cabinet"],
            stateManager.Actions);
    }

    [Fact]
    public void Redo_WithNoHistory_ReturnsNull()
    {
        var orchestrator = CreateOrchestrator(new RecordingDeltaTracker(), new RecordingWhyEngine(), new RecordingUndoStack(), new RecordingStateManager(), CreatePipeline());

        Assert.Null(orchestrator.Redo());
    }

    private static ResolutionOrchestrator CreateOrchestrator(
        IDeltaTracker deltaTracker,
        IWhyEngine whyEngine,
        RecordingUndoStack undoStack,
        IStateManager stateManager,
        IEnumerable<IResolutionStage> stages,
        IResolutionOrchestratorLogger? logger = null) =>
        new(deltaTracker, whyEngine, undoStack, stateManager, logger, stages);

    private static RecordingStage[] CreatePipeline()
    {
        var executionLog = new List<int>();
        return Enumerable.Range(1, 11)
            .Select(number => new RecordingStage(number, executionLog))
            .ToArray();
    }

    private static CommandMetadata CreateMetadata() =>
        CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Undo", []);

    private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> Issues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test", []);

        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => Issues;
    }

    private sealed class RecordingDeltaTracker(params IReadOnlyList<StateDelta>[] finalizedResults) : IDeltaTracker
    {
        private readonly Queue<IReadOnlyList<StateDelta>> _finalizedResults = new(finalizedResults.Length > 0 ? finalizedResults : [[]]);

        public int BeginCalls { get; private set; }

        public int FinalizeCalls { get; private set; }

        public void Begin() => BeginCalls++;

        public void RecordDelta(StateDelta delta)
        {
        }

        public IReadOnlyList<StateDelta> Finalize()
        {
            FinalizeCalls++;
            return _finalizedResults.Count > 0 ? _finalizedResults.Dequeue() : [];
        }
    }

    private sealed class RecordingWhyEngine(params IReadOnlyList<ExplanationNodeId>[] commandResults) : IWhyEngine
    {
        private readonly IReadOnlyList<ExplanationNodeId> _commandResult = commandResults.Length > 0 ? commandResults[0] : [];

        public int RecordCommandCalls { get; private set; }

        public IDesignCommand? LastCommand { get; private set; }

        public IReadOnlyList<StateDelta>? LastCommandDeltas { get; private set; }

        public IReadOnlyList<ExplanationNodeId> RecordCommand(IDesignCommand command, IReadOnlyList<StateDelta> deltas)
        {
            RecordCommandCalls++;
            LastCommand = command;
            LastCommandDeltas = deltas;
            return _commandResult;
        }

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

    private sealed class RecordingLogger : IResolutionOrchestratorLogger
    {
        public int Calls { get; private set; }

        public ResolutionMode? LastMode { get; private set; }

        public Exception? LastException { get; private set; }

        public void LogUnhandledException(IDesignCommand command, ResolutionMode mode, Exception exception)
        {
            Calls++;
            LastMode = mode;
            LastException = exception;
        }
    }

    private sealed class RecordingUndoStack : IUndoStack
    {
        public List<UndoEntry> PushedEntries { get; } = [];

        public UndoEntry? UndoResult { get; set; }

        public UndoEntry? RedoResult { get; set; }

        public bool CanUndo => UndoResult is not null;

        public bool CanRedo => RedoResult is not null;

        public IReadOnlyList<UndoEntry> Journal => PushedEntries;

        public void Push(UndoEntry entry) => PushedEntries.Add(entry);

        public UndoEntry? Undo()
        {
            var result = UndoResult;
            UndoResult = null;
            return result;
        }

        public UndoEntry? Redo()
        {
            var result = RedoResult;
            RedoResult = null;
            return result;
        }

        public void Clear() => PushedEntries.Clear();
    }

    private sealed class RecordingStateManager : IStateManager
    {
        public List<string> Actions { get; } = [];

        public void RemoveEntity(string entityId, string entityType) => Actions.Add($"RemoveEntity:{entityId}:{entityType}");

        public void RestoreValues(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values) =>
            Actions.Add($"RestoreValues:{entityId}:{entityType}");

        public void RestoreEntity(string entityId, string entityType, IReadOnlyDictionary<string, DeltaValue> values) =>
            Actions.Add($"RestoreEntity:{entityId}:{entityType}");
    }

    private sealed class RecordingStage : IResolutionStage
    {
        private readonly int _stageNumber;
        private readonly List<int> _executionLog;

        public RecordingStage(int stageNumber, List<int> executionLog)
        {
            _stageNumber = stageNumber;
            _executionLog = executionLog;
            ExecutionLog = executionLog;
        }

        public int StageNumber => _stageNumber;

        public string StageName => $"Stage {_stageNumber}";

        public List<int> ExecutionLog { get; }

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context)
        {
            _executionLog.Add(_stageNumber);
            if (_stageNumber == 3)
            {
                context.SpatialResult = new SpatialResolutionResult
                {
                    SlotPositionUpdates = [],
                    AdjacencyChanges = [],
                    RunSummaries = [],
                    Placements = []
                };
            }

            return StageResult.Succeeded(_stageNumber);
        }
    }

    private sealed class FailingStage(int stageNumber, List<int> executionLog) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => $"Stage {stageNumber}";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context)
        {
            executionLog.Add(stageNumber);
            return StageResult.Failed(stageNumber, [new ValidationIssue(ValidationSeverity.Error, "STAGE_FAIL", "Stage failed")]);
        }
    }

    private sealed class WarningStage(int stageNumber, ValidationIssue warning) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => $"Stage {stageNumber}";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context)
        {
            if (stageNumber == 3)
            {
                context.SpatialResult = new SpatialResolutionResult
                {
                    SlotPositionUpdates = [],
                    AdjacencyChanges = [],
                    RunSummaries = [],
                    Placements = []
                };
            }

            return StageResult.Succeeded(stageNumber, warnings: [warning]);
        }
    }

    private sealed class ModeAwareStage(int stageNumber, List<int> executionLog) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => $"Stage {stageNumber}";

        public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full || stageNumber <= 3;

        public StageResult Execute(ResolutionContext context)
        {
            executionLog.Add(stageNumber);
            if (stageNumber == 3)
            {
                context.SpatialResult = new SpatialResolutionResult
                {
                    SlotPositionUpdates = [],
                    AdjacencyChanges = [],
                    RunSummaries = [],
                    Placements = []
                };
            }

            return StageResult.Succeeded(stageNumber);
        }
    }

    private sealed class SpatialPreviewStage : IResolutionStage
    {
        public int StageNumber => 3;

        public string StageName => "Spatial Preview";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context)
        {
            context.SpatialResult = new SpatialResolutionResult
            {
                SlotPositionUpdates = [],
                AdjacencyChanges = [],
                RunSummaries = [],
                Placements = [new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(
                    RunId.New(),
                    CabinetId.New(),
                    Point2D.Origin,
                    new Vector2D(1m, 0m),
                    new Rect2D(Point2D.Origin, Length.FromInches(1m), Length.FromInches(1m)),
                    Length.FromInches(1m))]
            };

            return StageResult.Succeeded(StageNumber);
        }
    }

    private sealed class RecursiveStage(int stageNumber, Func<ResolutionOrchestrator> orchestratorFactory, IDesignCommand command) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => "Recursive";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context)
        {
            var nestedResult = orchestratorFactory().Execute(command);
            if (!nestedResult.Success)
            {
                return StageResult.Failed(stageNumber, nestedResult.Issues);
            }

            return StageResult.Succeeded(stageNumber);
        }
    }

    private sealed class ThrowingStage(int stageNumber) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => "Throwing";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context) => throw new InvalidOperationException("boom");
    }

    private sealed class NotImplementedStage(int stageNumber) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => $"Stage {stageNumber}";

        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context) =>
            StageResult.NotImplementedYet(stageNumber);
    }

    private sealed class SkippableStage(int stageNumber, List<int> executionLog) : IResolutionStage
    {
        public int StageNumber => stageNumber;

        public string StageName => $"Stage {stageNumber}";

        // Only runs in Full mode.
        public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

        public StageResult Execute(ResolutionContext context)
        {
            executionLog.Add(stageNumber);
            return StageResult.Succeeded(stageNumber);
        }
    }
}

public sealed class ResolutionOrchestratorIncompleteStageTests
{
    [Fact]
    public void Execute_WithNotImplementedStage_SucceedsButAddsWarning()
    {
        var stages = new IResolutionStage[]
        {
            new NotImplementedStageHelper(1)
        };
        var orchestrator = CreateOrchestrator(stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "STAGE_NOT_IMPLEMENTED");
    }

    [Fact]
    public void Execute_WithNotImplementedStage_WarningMessageContainsStageName()
    {
        var stages = new IResolutionStage[]
        {
            new NotImplementedStageHelper(4, "Engineering Resolution")
        };
        var orchestrator = CreateOrchestrator(stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        var warning = Assert.Single(result.Issues, issue => issue.Code == "STAGE_NOT_IMPLEMENTED");
        Assert.Contains("4", warning.Message);
        Assert.Contains("Engineering Resolution", warning.Message);
    }

    [Fact]
    public void Execute_WithMultipleNotImplementedStages_AddsOneWarningPerStage()
    {
        var stages = new IResolutionStage[]
        {
            new NotImplementedStageHelper(4, "Engineering Resolution"),
            new NotImplementedStageHelper(5, "Constraint Propagation"),
            new NotImplementedStageHelper(6, "Part Generation")
        };
        var orchestrator = CreateOrchestrator(stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        var warnings = result.Issues.Where(issue => issue.Code == "STAGE_NOT_IMPLEMENTED").ToArray();
        Assert.Equal(3, warnings.Length);
    }

    [Fact]
    public void Preview_SkippedFullOnlyStage_ContextMarksItSkipped()
    {
        var executionLog = new List<int>();
        // Stage 1-3 run in Preview; stage 4 only runs in Full.
        var stages = new IResolutionStage[]
        {
            new ModeAwareStageHelper(1, executionLog),
            new ModeAwareStageHelper(2, executionLog),
            new SpatialHelper(3, executionLog),
            new FullOnlyHelper(4, executionLog)
        };
        var orchestrator = CreateOrchestrator(stages);

        var result = orchestrator.Preview(new TestDesignCommand([]));

        Assert.True(result.Success);
        // Stage 4 was skipped – confirm it didn't execute.
        Assert.Equal([1, 2, 3], executionLog);
    }

    [Fact]
    public void Execute_NotImplementedWarning_DoesNotBlockPipeline()
    {
        // Even if every stage is "not implemented yet", the pipeline should still succeed
        // (placeholder results are still set) and all warnings surface.
        var stages = Enumerable.Range(1, 5)
            .Select(n => (IResolutionStage)new NotImplementedStageHelper(n))
            .ToArray();
        var orchestrator = CreateOrchestrator(stages);

        var result = orchestrator.Execute(new TestDesignCommand([]));

        Assert.True(result.Success);
        Assert.Equal(5, result.Issues.Count(i => i.Code == "STAGE_NOT_IMPLEMENTED"));
    }

    private static ResolutionOrchestrator CreateOrchestrator(IEnumerable<IResolutionStage> stages) =>
        new(
            new SimpleRecordingDeltaTracker(),
            new SimpleRecordingWhyEngine(),
            new SimpleRecordingUndoStack(),
            new SimpleRecordingStateManager(),
            logger: null,
            stages);

    private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> Issues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test", []);

        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => Issues;
    }

    private sealed class NotImplementedStageHelper(int stageNumber, string stageName = "") : IResolutionStage
    {
        public int StageNumber => stageNumber;
        public string StageName => string.IsNullOrEmpty(stageName) ? $"Stage {stageNumber}" : stageName;
        public bool ShouldExecute(ResolutionMode mode) => true;
        public StageResult Execute(ResolutionContext context) => StageResult.NotImplementedYet(stageNumber);
    }

    private sealed class ModeAwareStageHelper(int stageNumber, List<int> log) : IResolutionStage
    {
        public int StageNumber => stageNumber;
        public string StageName => $"Stage {stageNumber}";
        public bool ShouldExecute(ResolutionMode mode) => true;
        public StageResult Execute(ResolutionContext context) { log.Add(stageNumber); return StageResult.Succeeded(stageNumber); }
    }

    private sealed class SpatialHelper(int stageNumber, List<int> log) : IResolutionStage
    {
        public int StageNumber => stageNumber;
        public string StageName => "Spatial Resolution";
        public bool ShouldExecute(ResolutionMode mode) => true;

        public StageResult Execute(ResolutionContext context)
        {
            log.Add(stageNumber);
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

    private sealed class FullOnlyHelper(int stageNumber, List<int> log) : IResolutionStage
    {
        public int StageNumber => stageNumber;
        public string StageName => $"Stage {stageNumber}";
        public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;
        public StageResult Execute(ResolutionContext context) { log.Add(stageNumber); return StageResult.Succeeded(stageNumber); }
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
        public ExplanationNodeId RecordDecision(CommandId commandId, int stageNumber, string decisionType, string description,
            IReadOnlyList<string>? affectedEntityIds = null, IReadOnlyDictionary<string, string>? context = null,
            ExplanationRuleRecord? rule = null) => ExplanationNodeId.New();
        public ExplanationNodeId RecordDecisionWithEdges(CommandId commandId, int stageNumber, string decisionType, string description,
            IReadOnlyList<ExplanationEdge> edges, IReadOnlyList<string>? affectedEntityIds = null,
            IReadOnlyDictionary<string, string>? context = null, ExplanationRuleRecord? rule = null) => ExplanationNodeId.New();
        public IReadOnlyList<ExplanationNodeId> RecordUndo(CommandMetadata commandMetadata, IReadOnlyList<StateDelta> deltas,
            IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds) => [];
        public IReadOnlyList<ExplanationNodeId> RecordRedo(CommandMetadata commandMetadata, IReadOnlyList<StateDelta> deltas,
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
