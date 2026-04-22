using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Application.Persistence;
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

public sealed class PackagingStageCostDecouplingTests
{
    [Fact]
    public void Execute_CostNotConfigured_PackagingStillSucceeds()
    {
        var revisionId = new RevisionId(Guid.Parse("60000000-0000-0000-0000-000000000001"));
        var stage = new PackagingStage(new RecordingWorkingRevisionSource(CreateState(revisionId)), new FixedClock(DateTimeOffset.Parse("2026-04-18T18:00:00Z")));
        var context = CreateContext(revisionId);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(context.PackagingResult.ContentHash));
        Assert.Contains("\"status\":\"not_configured\"", context.PackagingResult.EstimateBlob, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_ContentHash_DeterministicWhenCostNotConfigured()
    {
        var revisionId = new RevisionId(Guid.Parse("60000000-0000-0000-0000-000000000002"));
        var clock = new FixedClock(DateTimeOffset.Parse("2026-04-18T18:05:00Z"));
        var stage = new PackagingStage(new RecordingWorkingRevisionSource(CreateState(revisionId)), clock);
        var firstContext = CreateContext(revisionId);
        var secondContext = CreateContext(revisionId);

        var first = stage.Execute(firstContext);
        var second = stage.Execute(secondContext);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
        Assert.Equal(firstContext.PackagingResult.EstimateBlob, secondContext.PackagingResult.EstimateBlob);
    }

    private static PersistedProjectState CreateState(RevisionId revisionId)
    {
        var createdAt = DateTimeOffset.Parse("2026-04-18T12:00:00Z");
        var projectId = new ProjectId(Guid.Parse("61000000-0000-0000-0000-000000000001"));
        var project = new ProjectRecord(projectId, "Packaging Project", null, createdAt, createdAt, ApprovalState.Draft);
        var revision = new RevisionRecord(revisionId, projectId, 3, ApprovalState.Draft, createdAt, null, null, "Rev 3");
        var workingRevision = new WorkingRevision(revision, [], [], [], [], []);
        var checkpoint = new AutosaveCheckpoint("checkpoint", projectId, revisionId, createdAt, null, true);
        return new PersistedProjectState(project, revision, workingRevision, checkpoint);
    }

    private static ResolutionContext CreateContext(RevisionId revisionId)
    {
        var cabinetId = new CabinetId(Guid.Parse("62000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("63000000-0000-0000-0000-000000000001"));
        var part = new GeneratedPart
        {
            PartId = "part:1",
            CabinetId = cabinetId,
            PartType = "LeftSide",
            Width = Length.FromInches(24m),
            Height = Length.FromInches(30m),
            MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
            MaterialId = new MaterialId(Guid.Parse("64000000-0000-0000-0000-000000000001")),
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

        return new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full,
            SpatialResult = new SpatialResolutionResult
            {
                SlotPositionUpdates = [],
                AdjacencyChanges = [],
                RunSummaries = [new RunSummary(runId, Length.FromInches(96m), Length.FromInches(24m), Length.FromInches(72m), 1)],
                Placements = [new RunPlacement(runId, cabinetId, Point2D.Origin, new Vector2D(1m, 0m), new Rect2D(Point2D.Origin, Length.FromInches(24m), Length.FromInches(30m)), Length.FromInches(24m))]
            },
            EngineeringResult = new EngineeringResolutionResult
            {
                Assemblies = [],
                FillerRequirements = [],
                EndConditionUpdates = []
            },
            ConstraintResult = new ConstraintPropagationResult
            {
                MaterialAssignments = [],
                HardwareAssignments = [],
                Violations = []
            },
            PartResult = new PartGenerationResult { Parts = [part] },
            ManufacturingResult = new ManufacturingPlanResult
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
            },
            InstallResult = new InstallPlanResult
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
            },
            CostingResult = new CostingResult
            {
                Status = CostingStatus.NotConfigured,
                StatusReason = "Pricing catalog not configured. Cost calculation skipped. See docs/V2_enhancements.md.",
                MaterialCost = 0m,
                HardwareCost = 0m,
                LaborCost = 0m,
                InstallCost = 0m,
                Subtotal = 0m,
                Markup = 0m,
                Tax = 0m,
                Total = 0m,
                CabinetBreakdowns = []
            },
            ValidationResult = new ValidationResult
            {
                Result = new FullValidationResult
                {
                    CrossCuttingIssues = [],
                    ContextualIssues = []
                }
            }
        };
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
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Packaging Test", []);

        public string CommandType => "test.packaging";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}

