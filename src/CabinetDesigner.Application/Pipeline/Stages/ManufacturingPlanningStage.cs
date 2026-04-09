using CabinetDesigner.Application.Projection;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class ManufacturingPlanningStage : IResolutionStage
{
    private readonly IManufacturingProjector _projector;

    public ManufacturingPlanningStage()
        : this(new ManufacturingProjector())
    {
    }

    public ManufacturingPlanningStage(IManufacturingProjector projector)
    {
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    public int StageNumber => 7;

    public string StageName => "Manufacturing Planning";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        var plan = _projector.Project(context.PartResult, context.ConstraintResult);
        context.ManufacturingResult = new ManufacturingPlanResult
        {
            Plan = plan
        };

        var issues = plan.Readiness.Blockers
            .Select(blocker => blocker.ToValidationIssue())
            .ToArray();

        if (issues.Length > 0)
        {
            return StageResult.Failed(StageNumber, issues);
        }

        return StageResult.Succeeded(StageNumber);
    }
}
