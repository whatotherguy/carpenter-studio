using CabinetDesigner.Application;
using CabinetDesigner.Application.Handlers;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Application.Handlers;

public sealed class PreviewCommandHandlerTests
{
    [Fact]
    public void Preview_WithBlockingStructuralIssue_ReturnsInvalidDto_WithoutCallingOrchestrator()
    {
        var orchestrator = new RecordingOrchestrator();
        var handler = new PreviewCommandHandler(orchestrator);
        var command = new TestDesignCommand(
            [new ValidationIssue(ValidationSeverity.Error, "INVALID", "Invalid preview.")]);

        var result = handler.Preview(command);

        Assert.False(result.IsValid);
        Assert.Equal("Structural validation failed.", result.RejectionReason);
        Assert.Equal(0, orchestrator.PreviewCalls);
        Assert.Equal(0, orchestrator.ExecuteCalls);
    }

    [Fact]
    public void Preview_WhenOrchestratorPreviewSucceeds_ReturnsValidDto()
    {
        var orchestrator = new RecordingOrchestrator
        {
            PreviewResult = PreviewResult.Succeeded(
                CreateMetadata(),
                new SpatialResolutionResult
                {
                    SlotPositionUpdates = [],
                    AdjacencyChanges = [],
                    RunSummaries = [],
                    Placements =
                    [
                        new CabinetDesigner.Application.Pipeline.StageResults.RunPlacement(
                            RunId.New(),
                            CabinetId.New(),
                            new Point2D(10m, 20m),
                            new Vector2D(1m, 0m),
                            new Rect2D(new Point2D(10m, 20m), Length.FromInches(12m), Length.FromInches(24m)),
                            Length.FromInches(12m))
                    ]
                })
        };
        var handler = new PreviewCommandHandler(orchestrator);

        var result = handler.Preview(new TestDesignCommand([]));

        Assert.True(result.IsValid);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(10m, candidate.OriginXInches);
        Assert.Equal(20m, candidate.OriginYInches);
        Assert.Equal(1, orchestrator.PreviewCalls);
        Assert.Equal(0, orchestrator.ExecuteCalls);
    }

    [Fact]
    public void Preview_NeverPublishesEvents()
    {
        var orchestrator = new RecordingOrchestrator();
        var handler = new PreviewCommandHandler(orchestrator);

        var result = handler.Preview(new TestDesignCommand([]));

        Assert.True(result.IsValid);
        Assert.Equal(1, orchestrator.PreviewCalls);
        Assert.Equal(0, orchestrator.ExecuteCalls);
    }

    private static CommandMetadata CreateMetadata() =>
        CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Preview", []);

    private sealed class RecordingOrchestrator : IResolutionOrchestrator
    {
        public int ExecuteCalls { get; private set; }

        public int PreviewCalls { get; private set; }

        public PreviewResult PreviewResult { get; set; } = PreviewResult.Succeeded(
            CreateMetadata(),
            new SpatialResolutionResult
            {
                SlotPositionUpdates = [],
                AdjacencyChanges = [],
                RunSummaries = [],
                Placements = []
            });

        public CommandResult Execute(IDesignCommand command)
        {
            ExecuteCalls++;
            return CommandResult.Succeeded(CreateMetadata(), [], []);
        }

        public PreviewResult Preview(IDesignCommand command)
        {
            PreviewCalls++;
            return PreviewResult;
        }

        public CommandResult? Undo() => null;

        public CommandResult? Redo() => null;
    }

    private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> StructuralIssues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } = CreateMetadata();

        public string CommandType => "preview.test";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => StructuralIssues;
    }
}
