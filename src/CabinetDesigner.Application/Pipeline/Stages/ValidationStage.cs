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

    public ValidationStage(IValidationResultStore? resultStore = null)
        : this(CreateDefaultEngine(), resultStore)
    {
    }

    public ValidationStage(IValidationEngine engine, IValidationResultStore? resultStore = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _resultStore = resultStore;
    }

    public int StageNumber => 10;

    public string StageName => "Validation";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        var validationContext = BuildContext(context);
        var engineResult = _engine.Validate(validationContext);
        var result = engineResult with
        {
            ContextualIssues = context.AccumulatedIssues.ToArray()
        };

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
            .AddRule(new RunOverCapacityRule())
            .AddRule(new WorkflowUnapprovedChangesRule())
            .Build();

    private static ValidationContext BuildContext(ResolutionContext context) =>
        new()
        {
            Command = context.Command,
            Mode = context.Mode == ResolutionMode.Preview
                ? ValidationMode.Preview
                : ValidationMode.Full,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = BuildCabinetPositions(context.SpatialResult),
            RunSnapshots = BuildRunSnapshots(context.SpatialResult, context.EngineeringResult),
            WorkflowState = new WorkflowStateSnapshot(
                ApprovalState: "Draft",
                HasUnapprovedChanges: false,
                HasPendingManufactureBlockers: false)
        };

    private static IReadOnlyList<CabinetPositionSnapshot> BuildCabinetPositions(SpatialResolutionResult spatialResult) =>
        spatialResult.SlotPositionUpdates
            .Select(update => new CabinetPositionSnapshot(
                CabinetId: update.SlotId.ToString(),
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
}
