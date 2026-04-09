using CabinetDesigner.Domain.InstallContext;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record InstallPlanResult
{
    public required InstallPlan Plan { get; init; }
}
