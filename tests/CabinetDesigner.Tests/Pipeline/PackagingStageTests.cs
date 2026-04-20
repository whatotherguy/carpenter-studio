using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain;
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

public sealed class PackagingStageTests
{
    [Fact]
    public void Execute_SuccessfulPackageCreation_PopulatesMetadataAndBlobs()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-18T18:00:00Z"));
        var stage = new PackagingStage(new RecordingWorkingRevisionSource(CreateState(revisionId)), clock);
        var context = CreateContext(revisionId);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(revisionId, context.PackagingResult.RevisionId);
        Assert.Equal(clock.Now, context.PackagingResult.CreatedAt);
        Assert.StartsWith($"snap:{revisionId.Value:D}:", context.PackagingResult.SnapshotId, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(context.PackagingResult.ContentHash));
        Assert.Contains("\"schema_version\":1", context.PackagingResult.PartsBlob, StringComparison.Ordinal);
        Assert.Contains("\"costing\"", context.PackagingResult.EstimateBlob, StringComparison.Ordinal);
        Assert.Equal(1, context.PackagingResult.Summary.CabinetCount);
        Assert.Equal(1, context.PackagingResult.Summary.RunCount);
        Assert.Equal(1, context.PackagingResult.Summary.PartCount);
    }

    [Fact]
    public void Execute_ProducesStableHash_ForIdenticalInputs()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-18T18:05:00Z"));
        var stage = new PackagingStage(new RecordingWorkingRevisionSource(CreateState(revisionId)), clock);
        var firstContext = CreateContext(revisionId);
        var secondContext = CreateContext(revisionId);

        var first = stage.Execute(firstContext);
        var second = stage.Execute(secondContext);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
        Assert.Equal(firstContext.PackagingResult.PartsBlob, secondContext.PackagingResult.PartsBlob);
        Assert.Equal(firstContext.PackagingResult.ManufacturingBlob, secondContext.PackagingResult.ManufacturingBlob);
        Assert.Equal(firstContext.PackagingResult.EstimateBlob, secondContext.PackagingResult.EstimateBlob);
    }

    [Fact]
    public void Execute_HashChanges_WhenPartListChanges()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000003"));
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-18T18:10:00Z"));
        var stage = new PackagingStage(new RecordingWorkingRevisionSource(CreateState(revisionId)), clock);
        var firstContext = CreateContext(revisionId);
        var secondContext = CreateContext(
            revisionId,
            cutWidth: Length.FromInches(25m));

        stage.Execute(firstContext);
        stage.Execute(secondContext);

        Assert.NotEqual(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
    }

    [Fact]
    public void Execute_BlockerOnMissingRequiredState()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000004"));
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-18T18:15:00Z"));
        var stage = new PackagingStage(new RecordingWorkingRevisionSource(CreateState(revisionId)), clock);
        var context = CreateContext(revisionId, includePart: false, includeCutList: false);

        var result = stage.Execute(context);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("PACKAGING_REQUIRED_STATE_MISSING", issue.Code);
    }

    private static PersistedProjectState CreateState(RevisionId revisionId)
    {
        var createdAt = DateTimeOffset.Parse("2026-04-18T12:00:00Z");
        var projectId = new ProjectId(Guid.Parse("20000000-0000-0000-0000-000000000001"));
        var project = new ProjectRecord(projectId, "Packaging Project", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 3, ApprovalState.Draft, createdAt, null, null, "Rev 3");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint("checkpoint", projectId, revisionId, createdAt, null, true);
        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private static ResolutionContext CreateContext(
        RevisionId revisionId,
        bool includePart = true,
        bool includeCutList = true,
        Length? cutWidth = null)
    {
        var cabinetId = new CabinetId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var width = cutWidth ?? Length.FromInches(24m);
        var part = new GeneratedPart
        {
            PartId = "part:1",
            CabinetId = cabinetId,
            PartType = "LeftSide",
            Width = width,
            Height = Length.FromInches(30m),
            MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
            MaterialId = new MaterialId(Guid.Parse("50000000-0000-0000-0000-000000000001")),
            GrainDirection = GrainDirection.LengthWise,
            Edges = new EdgeTreatment(null, null, null, null),
            Label = "Cabinet A-LeftSide"
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
            Command = new TestDesignCommand(),
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
            Parts = includePart ? [part] : []
        };
        context.ManufacturingResult = new ManufacturingPlanResult
        {
            Plan = new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = includeCutList ? [cutListItem] : [],
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
        context.CostingResult = new CostingResult
        {
            MaterialCost = includeCutList ? 42m : 0m,
            HardwareCost = 0m,
            LaborCost = 0m,
            InstallCost = 0m,
            Subtotal = includeCutList ? 42m : 0m,
            Markup = 0m,
            Tax = 0m,
            Total = includeCutList ? 42m : 0m,
            RevisionDelta = null,
            CabinetBreakdowns = [new CabinetCostBreakdown(cabinetId.ToString(), includeCutList ? 42m : 0m, 0m, 0m, 0m)]
        };
        context.ValidationResult = new ValidationResult
        {
            Result = new FullValidationResult
            {
                CrossCuttingIssues = [],
                ContextualIssues = []
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

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } = CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Packaging Test", []);
        public string CommandType => "test.packaging";
        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
