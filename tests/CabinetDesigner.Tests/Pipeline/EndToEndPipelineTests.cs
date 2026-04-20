using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.MaterialContext;
using CabinetDesigner.Domain.ProjectContext;
using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class EndToEndPipelineTests
{
    private static readonly DateTimeOffset SampleClock = DateTimeOffset.Parse("2026-04-19T12:00:00Z");

    [Fact]
    public void FullPipeline_OnSampleProject_ProducesValidApprovedSnapshot()
    {
        var revisionId = new RevisionId(Guid.Parse("a0000000-0000-0000-0000-000000000001"));
        var stages = BuildLatePipelineStages(CreateState(revisionId));
        var context = BuildPreCostingContext(revisionId);

        foreach (var stage in stages)
        {
            var result = stage.Execute(context);
            Assert.True(result.Success, $"Stage {stage.StageNumber} ({stage.StageName}) failed: {string.Join("; ", result.Issues.Select(issue => issue.Code))}");
        }

        Assert.True(context.ValidationResult.Result.IsValid);
        Assert.NotNull(context.PackagingResult);
        Assert.False(string.IsNullOrWhiteSpace(context.PackagingResult.ContentHash));
        Assert.Equal(revisionId, context.PackagingResult.RevisionId);
        Assert.Contains("\"costing\"", context.PackagingResult.EstimateBlob, StringComparison.Ordinal);
        Assert.True(context.CostingResult.Total > 0m);
    }

    [Fact]
    public void FullPipeline_IsDeterministic()
    {
        var revisionId = new RevisionId(Guid.Parse("a0000000-0000-0000-0000-000000000002"));

        var firstStages = BuildLatePipelineStages(CreateState(revisionId));
        var firstContext = BuildPreCostingContext(revisionId);
        foreach (var stage in firstStages)
        {
            stage.Execute(firstContext);
        }

        var secondStages = BuildLatePipelineStages(CreateState(revisionId));
        var secondContext = BuildPreCostingContext(revisionId);
        foreach (var stage in secondStages)
        {
            stage.Execute(secondContext);
        }

        Assert.Equal(firstContext.CostingResult.Total, secondContext.CostingResult.Total);
        Assert.Equal(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
        Assert.Equal(firstContext.PackagingResult.PartsBlob, secondContext.PackagingResult.PartsBlob);
        Assert.Equal(firstContext.PackagingResult.ManufacturingBlob, secondContext.PackagingResult.ManufacturingBlob);
        Assert.Equal(firstContext.PackagingResult.EstimateBlob, secondContext.PackagingResult.EstimateBlob);
    }

    private static IReadOnlyList<IResolutionStage> BuildLatePipelineStages(PersistedProjectState state) =>
    [
        new CostingStage(new CatalogService(), new DefaultCostingPolicy()),
        new ValidationStage(),
        new PackagingStage(new RecordingWorkingRevisionSource(state), new FixedClock(SampleClock))
    ];

    private static PersistedProjectState CreateState(RevisionId revisionId)
    {
        var projectId = new ProjectId(Guid.Parse("b0000000-0000-0000-0000-000000000001"));
        var project = new ProjectRecord(projectId, "Sample E2E", null, SampleClock, SampleClock, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 1, ApprovalState.Draft, SampleClock, null, null, "Rev 1");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint("checkpoint", projectId, revisionId, SampleClock, null, true);
        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private static ResolutionContext BuildPreCostingContext(RevisionId revisionId)
    {
        var cabinetId = new CabinetId(Guid.Parse("c0000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("d0000000-0000-0000-0000-000000000001"));
        var catalog = new CatalogService();
        var materialId = catalog.ResolvePartMaterial("LeftSide", CabinetCategory.Base, ConstructionMethod.Frameless);
        var thickness = catalog.ResolvePartThickness("LeftSide", CabinetCategory.Base);

        var part = new GeneratedPart
        {
            PartId = "part:e2e:1",
            CabinetId = cabinetId,
            PartType = "LeftSide",
            Width = Length.FromInches(24m),
            Height = Length.FromInches(30m),
            MaterialThickness = thickness,
            MaterialId = materialId,
            GrainDirection = GrainDirection.LengthWise,
            Edges = new EdgeTreatment(null, null, null, null),
            Label = "E2E Cabinet-LeftSide"
        };
        var cutListItem = new CutListItem
        {
            PartId = part.PartId,
            CabinetId = cabinetId,
            PartType = part.PartType,
            Label = part.Label,
            CutWidth = part.Width,
            CutHeight = part.Height,
            MaterialThickness = part.MaterialThickness,
            MaterialId = part.MaterialId,
            GrainDirection = part.GrainDirection,
            EdgeTreatment = new ManufacturedEdgeTreatment(null, null, null, null)
        };

        var context = new ResolutionContext
        {
            Command = new SampleProjectCommand(),
            Mode = ResolutionMode.Full
        };

        context.SpatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [new RunSummary(runId, Length.FromInches(96m), Length.FromInches(24m), Length.FromInches(72m), 1)],
            Placements = [new RunPlacement(runId, cabinetId, Point2D.Origin, new Vector2D(1m, 0m), new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)), Length.FromInches(24m))]
        };
        context.EngineeringResult = new EngineeringResolutionResult
        {
            Assemblies = [],
            FillerRequirements = [],
            EndConditionUpdates = []
        };
        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };
        context.PartResult = new PartGenerationResult
        {
            Parts = [part]
        };
        context.ManufacturingResult = new ManufacturingPlanResult
        {
            Plan = new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = [cutListItem],
                Operations = [],
                EdgeBandingRequirements = [],
                Readiness = new ManufacturingReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            }
        };
        context.InstallResult = new InstallPlanResult
        {
            Plan = new InstallPlan
            {
                Steps = [],
                Dependencies = [],
                FasteningRequirements = [],
                Readiness = new InstallReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            }
        };

        return context;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class RecordingWorkingRevisionSource(PersistedProjectState state) : IWorkingRevisionSource
    {
        public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null) => state;
    }

    private sealed record SampleProjectCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "End-To-End Sample", []);

        public string CommandType => "test.end_to_end";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
