using CabinetDesigner.Application.Projection;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class InstallPlanningStage : IResolutionStage
{
    private readonly IInstallProjector _projector;

    public InstallPlanningStage()
        : this(new InstallProjector())
    {
    }

    public InstallPlanningStage(IInstallProjector projector)
    {
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
    }

    public int StageNumber => 8;

    public string StageName => "Install Planning";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        var plan = _projector.Project(
            context.SpatialResult,
            context.EngineeringResult,
            context.ManufacturingResult);

        context.InstallResult = new InstallPlanResult
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
