using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Validation;
using CabinetDesigner.Domain.Validation.Rules;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class ValidationStage : IResolutionStage
{
    private readonly IValidationEngine _engine;
    private readonly IValidationResultStore? _resultStore;
    private readonly ICurrentPersistedProjectState? _projectState;

    public ValidationStage(IValidationResultStore? resultStore = null, ICurrentPersistedProjectState? projectState = null)
        : this(CreateDefaultEngine(), resultStore, projectState)
    {
    }

    public ValidationStage(IValidationEngine engine, IValidationResultStore? resultStore = null, ICurrentPersistedProjectState? projectState = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _resultStore = resultStore;
        _projectState = projectState;
    }

    public int StageNumber => 10;

    public string StageName => "Validation";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        var validationContext = BuildContext(context);
        var result = _engine.Validate(validationContext, context.AccumulatedIssues.ToArray());

        context.ValidationResult = new ValidationResult
        {
            Result = result
        };

        _resultStore?.Update(result);

        if (!result.IsValid)
        {
            return StageResult.Failed(StageNumber, result.AllBaseIssues);
        }

        return StageResult.Succeeded(StageNumber, warnings: result.AllBaseIssues);
    }

    private static IValidationEngine CreateDefaultEngine() =>
        new ValidationEngineBuilder()
            .AddRule(new MaterialAssignmentRule())
            .AddRule(new HardwareAssignmentRule())
            .AddRule(new ManufacturingReadinessRule())
            .AddRule(new InstallReadinessRule())
            .AddRule(new RunOverCapacityRule())
            .AddRule(new WorkflowUnapprovedChangesRule())
            .Build();

    private ValidationContext BuildContext(ResolutionContext context)
    {
        var currentState = _projectState?.CurrentState;
        return new ValidationContext
        {
            Command = context.Command,
            Mode = context.Mode == ResolutionMode.Preview
                ? ValidationMode.Preview
                : ValidationMode.Full,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = BuildCabinetPositions(context.SpatialResult),
            RunSnapshots = BuildRunSnapshots(context.SpatialResult, context.EngineeringResult),
            Constraints = BuildConstraintSnapshots(context.ConstraintResult),
            ManufacturingBlockers = BuildManufacturingBlockerSnapshots(context.ManufacturingResult),
            InstallBlockers = BuildInstallBlockerSnapshots(context.InstallResult),
            WorkflowState = new WorkflowStateSnapshot(
                ApprovalState: currentState?.Revision.State.ToString() ?? "Draft",
                HasUnapprovedChanges: currentState?.Checkpoint is { IsClean: false },
                HasPendingManufactureBlockers: context.ManufacturingResult.Plan.Readiness.Blockers.Count > 0)
        };
    }

    private static IReadOnlyList<CabinetPositionSnapshot> BuildCabinetPositions(SpatialResolutionResult spatialResult) =>
        spatialResult.SlotPositionUpdates
            .Select(update => new CabinetPositionSnapshot(
                CabinetId: update.CabinetId.ToString(),
                RunId: update.RunId.ToString(),
                BoundingBox: new Rect2D(update.WorldPosition, update.OccupiedWidth, Length.Zero),
                SlotIndex: update.NewIndex))
            .OrderBy(snapshot => snapshot.RunId, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.SlotIndex)
            .ToArray();

    private static IReadOnlyList<RunValidationSnapshot> BuildRunSnapshots(
        SpatialResolutionResult spatialResult,
        EngineeringResolutionResult engineeringResult)
    {
        var endConditionLookup = engineeringResult.EndConditionUpdates.ToDictionary(
            update => update.RunId.ToString(),
            update => update);

        return spatialResult.RunSummaries
            .Select(summary =>
            {
                var runId = summary.RunId.ToString();
                var hasEndConditions = endConditionLookup.TryGetValue(runId, out var endConditionUpdate);

                return new RunValidationSnapshot(
                    RunId: runId,
                    Capacity: summary.Capacity,
                    OccupiedLength: summary.OccupiedLength,
                    SlotCount: summary.SlotCount,
                    HasLeftEndCondition: hasEndConditions && endConditionUpdate!.LeftEndCondition is not null,
                    HasRightEndCondition: hasEndConditions && endConditionUpdate!.RightEndCondition is not null);
            })
            .OrderBy(snapshot => snapshot.RunId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<ConstraintViolationSnapshot> BuildConstraintSnapshots(
        ConstraintPropagationResult constraintResult) =>
        constraintResult.Violations
            .Select(violation => new ConstraintViolationSnapshot(
                violation.ConstraintCode,
                violation.Message,
                violation.Severity,
                violation.AffectedEntityIds
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(violation => violation.ConstraintCode, StringComparer.Ordinal)
            .ThenBy(violation => violation.Message, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<ManufacturingBlockerSnapshot> BuildManufacturingBlockerSnapshots(
        ManufacturingPlanResult manufacturingResult) =>
        manufacturingResult.Plan.Readiness.Blockers
            .Select(blocker => new ManufacturingBlockerSnapshot(
                blocker.Code.ToString(),
                blocker.Message,
                blocker.AffectedEntityIds
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(snapshot => snapshot.BlockerCode, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.Message, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<InstallBlockerSnapshot> BuildInstallBlockerSnapshots(
        InstallPlanResult installResult) =>
        installResult.Plan.Readiness.Blockers
            .Select(blocker => new InstallBlockerSnapshot(
                blocker.Code.ToString(),
                blocker.Message,
                blocker.AffectedEntityIds
                    .OrderBy(id => id, StringComparer.Ordinal)
                    .ToArray()))
            .OrderBy(snapshot => snapshot.BlockerCode, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.Message, StringComparer.Ordinal)
            .ToArray();
}
