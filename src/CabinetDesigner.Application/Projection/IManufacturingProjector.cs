using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.ManufacturingContext;

namespace CabinetDesigner.Application.Projection;

public interface IManufacturingProjector
{
    ManufacturingPlan Project(
        PartGenerationResult partResult,
        ConstraintPropagationResult constraintResult);
}
