using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record ManufacturingPlanResult
{
    public required ManufacturingPlan Plan { get; init; }
}
