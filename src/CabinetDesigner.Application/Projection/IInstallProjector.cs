using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.InstallContext;

namespace CabinetDesigner.Application.Projection;

public interface IInstallProjector
{
    InstallPlan Project(
        SpatialResolutionResult spatialResult,
        EngineeringResolutionResult engineeringResult,
        ManufacturingPlanResult manufacturingResult);
}
