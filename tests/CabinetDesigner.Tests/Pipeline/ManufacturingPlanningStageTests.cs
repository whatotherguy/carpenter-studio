using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.MaterialContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class ManufacturingPlanningStageTests
{
    [Fact]
    public void Execute_WithValidParts_SetsManufacturingResultAndSucceeds()
    {
        var stage = new ManufacturingPlanningStage();
        var context = CreateContext(new PartGenerationResult
        {
            Parts =
            [
                new GeneratedPart
                {
                    PartId = "part-1",
                    CabinetId = CabinetId.New(),
                    PartType = "panel",
                    Width = Length.FromInches(18m),
                    Height = Length.FromInches(24m),
                    MaterialThickness = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.71m)),
                    MaterialId = MaterialId.New(),
                    GrainDirection = GrainDirection.None,
                    Edges = new EdgeTreatment(null, null, null, null),
                    Label = "Panel"
                }
            ]
        });

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.True(context.ManufacturingResult.Plan.Readiness.IsReady);
        Assert.Single(context.ManufacturingResult.Plan.CutList);
    }

    [Fact]
    public void Execute_WithManufactureBlocker_ReturnsFailed()
    {
        var stage = new ManufacturingPlanningStage();
        var context = CreateContext(new PartGenerationResult
        {
            Parts =
            [
                new GeneratedPart
                {
                    PartId = "part-1",
                    CabinetId = CabinetId.New(),
                    PartType = "panel",
                    Width = Length.FromInches(0.5m),
                    Height = Length.FromInches(24m),
                    MaterialThickness = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.71m)),
                    MaterialId = MaterialId.New(),
                    GrainDirection = GrainDirection.None,
                    Edges = new EdgeTreatment(null, null, null, null),
                    Label = "Tiny Panel"
                }
            ]
        });

        var result = stage.Execute(context);

        Assert.False(result.Success);
        Assert.Equal(ValidationSeverity.ManufactureBlocker, Assert.Single(result.Issues).Severity);
        Assert.False(context.ManufacturingResult.Plan.Readiness.IsReady);
    }

    private static ResolutionContext CreateContext(PartGenerationResult partResult)
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.PartResult = partResult;
        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };

        return context;
    }

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Manufacturing Stage Test", []);

        public string CommandType => "test.manufacturing_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
