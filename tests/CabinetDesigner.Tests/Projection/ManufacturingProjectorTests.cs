using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Projection;
using CabinetDesigner.Domain.ManufacturingContext;
using Xunit;

namespace CabinetDesigner.Tests.Projection;

public sealed class ManufacturingProjectorTests
{
    [Fact]
    public void Project_WithNoParts_EmitsNoPartsProducedBlocker()
    {
        var projector = new ManufacturingProjector();

        var plan = projector.Project(
            new PartGenerationResult { Parts = [] },
            new ConstraintPropagationResult
            {
                MaterialAssignments = [],
                HardwareAssignments = [],
                Violations = []
            });

        Assert.False(plan.Readiness.IsReady);
        var blocker = Assert.Single(plan.Readiness.Blockers);
        Assert.Equal(ManufacturingBlockerCode.NoPartsProduced, blocker.Code);
        Assert.Equal("No parts were produced by part generation.", blocker.Message);
        Assert.Empty(plan.CutList);
        Assert.Empty(plan.MaterialGroups);
        Assert.Empty(plan.Operations);
    }
}
