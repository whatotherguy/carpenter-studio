using CabinetDesigner.Application.Costing;
using System.Threading;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application;

public sealed class ResolutionOrchestrator : IResolutionOrchestrator
{
    private const int MaxRecursionDepth = 3;
    private const string StageNotImplementedCode = "STAGE_NOT_IMPLEMENTED";
    private static readonly IReadOnlyDictionary<string, DeltaValue> EmptyDeltaValues = new Dictionary<string, DeltaValue>();

    private readonly IDeltaTracker _deltaTracker;
    private readonly IWhyEngine _whyEngine;
    private readonly IUndoStack _undoStack;
    private readonly IStateManager _stateManager;
    private readonly IResolutionOrchestratorLogger _logger;
    private readonly IReadOnlyList<IResolutionStage> _stages;
    private int _currentRecursionDepth;

    public ResolutionOrchestrator(
        IDeltaTracker deltaTracker,
        IWhyEngine whyEngine,
        IUndoStack undoStack,
        IStateManager stateManager,
        IResolutionOrchestratorLogger? logger = null,
        IEnumerable<IResolutionStage>? stages = null,
        ICatalogService? catalogService = null,
        ICostingPolicy? costingPolicy = null,
        IPackagingResultStore? packagingResultStore = null)
        : this(deltaTracker, whyEngine, undoStack, stateManager, stateStore: null, logger, stages, validationResultStore: null, packagingResultStore, catalogService, costingPolicy, currentPersistedProjectState: null, previousCostLookup: null)
    {
    }

    public ResolutionOrchestrator(
        IDeltaTracker deltaTracker,
        IWhyEngine whyEngine,
        IUndoStack undoStack,
        IStateManager stateManager,
        IDesignStateStore? stateStore = null,
        IResolutionOrchestratorLogger? logger = null,
        IEnumerable<IResolutionStage>? stages = null,
        IValidationResultStore? validationResultStore = null,
        IPackagingResultStore? packagingResultStore = null,
        ICatalogService? catalogService = null,
        ICostingPolicy? costingPolicy = null,
        ICurrentPersistedProjectState? currentPersistedProjectState = null,
        IPreviousApprovedCostLookup? previousCostLookup = null)
    {
        _deltaTracker = deltaTracker ?? throw new ArgumentNullException(nameof(deltaTracker));
        _whyEngine = whyEngine ?? throw new ArgumentNullException(nameof(whyEngine));
        _undoStack = undoStack ?? throw new ArgumentNullException(nameof(undoStack));
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _logger = logger ?? new NullResolutionOrchestratorLogger();
        _stages = BuildStageList(stages ?? CreateDefaultStages(deltaTracker, stateStore, catalogService, costingPolicy, validationResultStore, packagingResultStore, currentPersistedProjectState, previousCostLookup));
    }

    public CommandResult Execute(IDesignCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var depth = Interlocked.Increment(ref _currentRecursionDepth);
        if (depth > MaxRecursionDepth)
        {
            Interlocked.Decrement(ref _currentRecursionDepth);
            return CommandResult.Failed(command.Metadata, [CreateRecursionIssue()]);
        }

        var structureIssues = command.ValidateStructure();
        if (HasBlockingIssues(structureIssues))
        {
            Interlocked.Decrement(ref _currentRecursionDepth);
            return CommandResult.Rejected(command.Metadata, structureIssues);
        }

        var context = CreateContext(command, ResolutionMode.Full, structureIssues);
        var deltaTrackingStarted = false;

        try
        {
            _deltaTracker.Begin();
            deltaTrackingStarted = true;

            if (!ExecuteStages(context))
            {
                DiscardTrackedDeltas(ref deltaTrackingStarted);
                return CommandResult.Failed(command.Metadata, context.AccumulatedIssues.ToArray());
            }

            var deltas = _deltaTracker.Finalize();
            deltaTrackingStarted = false;

            var explanationNodeIds = MergeExplanationNodeIds(
                context.ExplanationNodeIds,
                _whyEngine.RecordCommand(command, deltas));

            _undoStack.Push(new UndoEntry(command.Metadata, deltas, explanationNodeIds));

            return CommandResult.Succeeded(
                command.Metadata,
                deltas,
                explanationNodeIds,
                context.AccumulatedIssues.Count > 0 ? context.AccumulatedIssues.ToArray() : null);
        }
        catch (Exception exception)
        {
            _logger.LogUnhandledException(command, ResolutionMode.Full, exception);
            DiscardTrackedDeltas(ref deltaTrackingStarted);
            return CommandResult.Failed(command.Metadata, MergeIssues(context.AccumulatedIssues, CreateInternalErrorIssue()));
        }
        finally
        {
            Interlocked.Decrement(ref _currentRecursionDepth);
        }
    }

    public PreviewResult Preview(IDesignCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        var depth = Interlocked.Increment(ref _currentRecursionDepth);
        if (depth > MaxRecursionDepth)
        {
            Interlocked.Decrement(ref _currentRecursionDepth);
            return PreviewResult.Failed(command.Metadata, [CreateRecursionIssue()]);
        }

        var structureIssues = command.ValidateStructure();
        if (HasBlockingIssues(structureIssues))
        {
            Interlocked.Decrement(ref _currentRecursionDepth);
            return PreviewResult.Failed(command.Metadata, structureIssues);
        }

        var context = CreateContext(command, ResolutionMode.Preview, structureIssues);

        try
        {
            if (!ExecuteStages(context))
            {
                return PreviewResult.Failed(command.Metadata, context.AccumulatedIssues.ToArray());
            }

            return PreviewResult.Succeeded(
                command.Metadata,
                context.SpatialResult,
                context.AccumulatedIssues.Count > 0 ? context.AccumulatedIssues.ToArray() : null);
        }
        catch (Exception exception)
        {
            _logger.LogUnhandledException(command, ResolutionMode.Preview, exception);
            return PreviewResult.Failed(command.Metadata, MergeIssues(context.AccumulatedIssues, CreateInternalErrorIssue()));
        }
        finally
        {
            Interlocked.Decrement(ref _currentRecursionDepth);
        }
    }

    public CommandResult? Undo()
    {
        var entry = _undoStack.Undo();
        if (entry is null)
        {
            return null;
        }

        ApplyReverseDeltas(entry.Deltas);
        var explanationNodeIds = _whyEngine.RecordUndo(entry.CommandMetadata, entry.Deltas, entry.ExplanationNodeIds);
        return CommandResult.Succeeded(entry.CommandMetadata, entry.Deltas, explanationNodeIds);
    }

    public CommandResult? Redo()
    {
        var entry = _undoStack.Redo();
        if (entry is null)
        {
            return null;
        }

        ApplyForwardDeltas(entry.Deltas);
        var explanationNodeIds = _whyEngine.RecordRedo(entry.CommandMetadata, entry.Deltas, entry.ExplanationNodeIds);
        return CommandResult.Succeeded(entry.CommandMetadata, entry.Deltas, explanationNodeIds);
    }

    private static IReadOnlyList<IResolutionStage> BuildStageList(IEnumerable<IResolutionStage> stages)
    {
        var orderedStages = stages.OrderBy(stage => stage.StageNumber).ToArray();
        var duplicateStageNumber = orderedStages
            .GroupBy(stage => stage.StageNumber)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateStageNumber is not null)
        {
            throw new InvalidOperationException($"Duplicate stage number detected: {duplicateStageNumber.Key}.");
        }

        return orderedStages;
    }

    private static IEnumerable<IResolutionStage> CreateDefaultStages(
        IDeltaTracker deltaTracker,
        IDesignStateStore? stateStore,
        ICatalogService? catalogService,
        ICostingPolicy? costingPolicy,
        IValidationResultStore? validationResultStore = null,
        IPackagingResultStore? packagingResultStore = null,
        ICurrentPersistedProjectState? currentPersistedProjectState = null,
        IPreviousApprovedCostLookup? previousCostLookup = null)
    {
        if (stateStore is null)
        {
            throw new InvalidOperationException("Default resolution stages require an IDesignStateStore instance.");
        }

        if (catalogService is null)
        {
            throw new InvalidOperationException("Default resolution stages require an ICatalogService instance.");
        }

        yield return new InputCaptureStage(stateStore);
        yield return new InteractionInterpretationStage(deltaTracker, stateStore, catalogService);
        yield return new SpatialResolutionStage(stateStore);
        yield return new EngineeringResolutionStage(stateStore);
        yield return new PartGenerationStage(stateStore);
        yield return new ConstraintPropagationStage(catalogService, stateStore);
        yield return new ManufacturingPlanningStage();
        yield return new InstallPlanningStage();
        yield return new CostingStage(
            catalogService,
            costingPolicy ?? new DefaultCostingPolicy(),
            previousCostLookup: previousCostLookup);
        yield return new ValidationStage(resultStore: validationResultStore, projectState: currentPersistedProjectState);
        yield return new PackagingStage(
            currentPersistedProjectState as IWorkingRevisionSource ?? throw new InvalidOperationException("Default packaging stage requires an IWorkingRevisionSource instance."),
            new SystemClock(),
            packagingResultStore);
    }

    private static bool HasBlockingIssues(IEnumerable<ValidationIssue> issues) =>
        issues.Any(issue => issue.Severity >= ValidationSeverity.Error);

    private static ResolutionContext CreateContext(
        IDesignCommand command,
        ResolutionMode mode,
        IEnumerable<ValidationIssue> structureIssues)
    {
        var context = new ResolutionContext
        {
            Command = command,
            Mode = mode
        };

        foreach (var issue in structureIssues)
        {
            if (issue.Severity < ValidationSeverity.Error)
            {
                context.AccumulatedIssues.Add(issue);
            }
        }

        return context;
    }

    private static IReadOnlyList<ExplanationNodeId> MergeExplanationNodeIds(
        IEnumerable<ExplanationNodeId> stageExplanationNodeIds,
        IEnumerable<ExplanationNodeId> recordedExplanationNodeIds) =>
        stageExplanationNodeIds.Concat(recordedExplanationNodeIds).ToArray();

    private static IReadOnlyList<ValidationIssue> MergeIssues(
        IEnumerable<ValidationIssue> issues,
        ValidationIssue additionalIssue) =>
        issues.Concat([additionalIssue]).ToArray();

    private static ValidationIssue CreateRecursionIssue() =>
        new(ValidationSeverity.Error, "MAX_RECURSION", "Resolution recursion depth exceeded the maximum allowed depth.");

    private static ValidationIssue CreateInternalErrorIssue() =>
        new(ValidationSeverity.Error, "INTERNAL_ERROR", "Resolution failed due to an unexpected internal error.");

    private bool ExecuteStages(ResolutionContext context)
    {
        foreach (var stage in _stages)
        {
            if (!stage.ShouldExecute(context.Mode))
            {
                context.MarkStageSkipped(stage.StageNumber, stage.StageName);
                continue;
            }

            var result = stage.Execute(context);
            context.AccumulatedIssues.AddRange(result.Issues);
            context.ExplanationNodeIds.AddRange(result.ExplanationNodeIds);

            if (result.IsNotImplemented)
            {
                context.AccumulatedIssues.Add(new ValidationIssue(
                    context.Mode == ResolutionMode.Full ? ValidationSeverity.Error : ValidationSeverity.Warning,
                    StageNotImplementedCode,
                    $"Stage {stage.StageNumber} '{stage.StageName}' is not implemented."));

                if (context.Mode == ResolutionMode.Full)
                {
                    return false;
                }
            }

            if (!result.Success || context.HasBlockingIssues)
            {
                return false;
            }
        }

        return true;
    }

    private void DiscardTrackedDeltas(ref bool deltaTrackingStarted)
    {
        if (!deltaTrackingStarted)
        {
            return;
        }

        _deltaTracker.Finalize();
        deltaTrackingStarted = false;
    }

    private void ApplyReverseDeltas(IReadOnlyList<StateDelta> deltas)
    {
        for (var index = deltas.Count - 1; index >= 0; index--)
        {
            var delta = deltas[index];

            switch (delta.Operation)
            {
                case DeltaOperation.Created:
                    _stateManager.RemoveEntity(delta.EntityId, delta.EntityType);
                    break;
                case DeltaOperation.Modified:
                    _stateManager.RestoreValues(delta.EntityId, delta.EntityType, delta.PreviousValues ?? EmptyDeltaValues);
                    break;
                case DeltaOperation.Removed:
                    _stateManager.RestoreEntity(delta.EntityId, delta.EntityType, delta.PreviousValues ?? EmptyDeltaValues);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported delta operation: {delta.Operation}.");
            }
        }
    }

    private void ApplyForwardDeltas(IReadOnlyList<StateDelta> deltas)
    {
        foreach (var delta in deltas)
        {
            switch (delta.Operation)
            {
                case DeltaOperation.Created:
                    _stateManager.RestoreEntity(delta.EntityId, delta.EntityType, delta.NewValues ?? EmptyDeltaValues);
                    break;
                case DeltaOperation.Modified:
                    _stateManager.RestoreValues(delta.EntityId, delta.EntityType, delta.NewValues ?? EmptyDeltaValues);
                    break;
                case DeltaOperation.Removed:
                    _stateManager.RemoveEntity(delta.EntityId, delta.EntityType);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported delta operation: {delta.Operation}.");
            }
        }
    }

    private sealed class NullResolutionOrchestratorLogger : IResolutionOrchestratorLogger
    {
        public void LogUnhandledException(IDesignCommand command, ResolutionMode mode, Exception exception)
        {
        }
    }
}
