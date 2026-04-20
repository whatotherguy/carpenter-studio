using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class StageOrderingTests
{
    [Fact]
    public void AllStages_HaveUniqueStageNumbers()
    {
        var stages = CreateStages();

        Assert.Equal(stages.Count, stages.Select(stage => stage.StageNumber).Distinct().Count());
    }

    [Fact]
    public void AllStages_OrderedCorrectly()
    {
        var stageNumbers = CreateStages().Select(stage => stage.StageNumber).ToArray();

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, stageNumbers);
    }

    [Fact]
    public void PreviewMode_OnlyStages1Through3Execute()
    {
        var previewStages = CreateStages()
            .Where(stage => stage.ShouldExecute(ResolutionMode.Preview))
            .Select(stage => stage.StageNumber)
            .ToArray();

        Assert.Equal(new[] { 1, 2, 3 }, previewStages);
    }

    [Fact]
    public void FullMode_AllStagesExecute()
    {
        var fullStages = CreateStages()
            .Where(stage => stage.ShouldExecute(ResolutionMode.Full))
            .Select(stage => stage.StageNumber)
            .ToArray();

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, fullStages);
    }

    private static IReadOnlyList<IResolutionStage> CreateStages() =>
    [
        new InputCaptureStage(),
        new InteractionInterpretationStage(),
        new SpatialResolutionStage(),
        new EngineeringResolutionStage(new InMemoryDesignStateStore()),
        new ConstraintPropagationStage(new CatalogService(), new InMemoryDesignStateStore()),
        new PartGenerationStage(),
        new ManufacturingPlanningStage(),
        new InstallPlanningStage(),
        new CostingStage(),
        new ValidationStage(),
        new PackagingStage()
    ];
}
