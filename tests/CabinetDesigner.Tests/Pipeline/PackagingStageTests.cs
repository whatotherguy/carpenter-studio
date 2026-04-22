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
    public void Execute_ProducesStableHash_ForIdenticalInputs()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000002"));
        var stage = CreateStage(revisionId);
        var firstContext = CreateContext(revisionId);
        var secondContext = CreateContext(revisionId);

        var first = stage.Execute(firstContext);
        var second = stage.Execute(secondContext);

        Assert.True(first.Success);
        Assert.True(second.Success);
        Assert.Equal(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
    }

    [Fact]
    public void Execute_HashChanges_WhenPartListChanges()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000003"));
        var stage = CreateStage(revisionId);
        var firstContext = CreateContext(revisionId);
        var secondContext = CreateContext(revisionId, partId: "part:2");

        stage.Execute(firstContext);
        stage.Execute(secondContext);

        Assert.NotEqual(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
    }

    [Fact]
    public void Execute_HashChanges_WhenMaterialChanges()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000004"));
        var stage = CreateStage(revisionId);
        var firstContext = CreateContext(revisionId);
        var secondContext = CreateContext(
            revisionId,
            materialId: new MaterialId(Guid.Parse("50000000-0000-0000-0000-000000000099")));

        stage.Execute(firstContext);
        stage.Execute(secondContext);

        Assert.NotEqual(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
    }

    [Fact]
    public void Execute_FailsStage_WhenValidationIsInvalid()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000005"));
        var stage = CreateStage(revisionId);
        var context = CreateContext(revisionId, isValid: false);

        var result = stage.Execute(context);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues);
        Assert.Equal("PACKAGING_INVALID_DESIGN", issue.Code);
    }

    [Fact]
    public void Execute_SnapshotSummary_ReflectsCounts()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000006"));
        var stage = CreateStage(revisionId);
        var context = CreateContext(revisionId, extraIssue: true);

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal(1, context.PackagingResult.Summary.CabinetCount);
        Assert.Equal(1, context.PackagingResult.Summary.RunCount);
        Assert.Equal(1, context.PackagingResult.Summary.PartCount);
        Assert.Equal(1, context.PackagingResult.Summary.ValidationIssueCount);
        Assert.Equal(CostingStatus.Calculated, context.PackagingResult.Summary.CostingStatus);
    }

    [Fact]
    public void Execute_CostingNotConfigured_ProducesStableHash()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000007"));
        var stage = CreateStage(revisionId);
        var firstContext = CreateContext(revisionId, costingStatus: CostingStatus.NotConfigured);
        var secondContext = CreateContext(revisionId, costingStatus: CostingStatus.NotConfigured);

        stage.Execute(firstContext);
        stage.Execute(secondContext);

        Assert.Equal(firstContext.PackagingResult.ContentHash, secondContext.PackagingResult.ContentHash);
        Assert.Contains("\"status\":\"not_configured\"", firstContext.PackagingResult.EstimateBlob, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_CostingCalculated_ProducesDifferentHashThanNotConfigured()
    {
        var revisionId = new RevisionId(Guid.Parse("10000000-0000-0000-0000-000000000008"));
        var stage = CreateStage(revisionId);
        var configuredContext = CreateContext(revisionId, costingStatus: CostingStatus.Calculated);
        var notConfiguredContext = CreateContext(revisionId, costingStatus: CostingStatus.NotConfigured);

        stage.Execute(configuredContext);
        stage.Execute(notConfiguredContext);

        Assert.NotEqual(configuredContext.PackagingResult.ContentHash, notConfiguredContext.PackagingResult.ContentHash);
    }

    private static PackagingStage CreateStage(RevisionId revisionId) =>
        new(new RecordingWorkingRevisionSource(CreateState(revisionId)), new FixedClock(DateTimeOffset.Parse("2026-04-18T18:00:00Z")));

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
        string partId = "part:1",
        MaterialId? materialId = null,
        CostingStatus costingStatus = CostingStatus.Calculated,
        bool isValid = true,
        bool extraIssue = false)
    {
        var cabinetId = new CabinetId(Guid.Parse("30000000-0000-0000-0000-000000000001"));
        var runId = new RunId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        var resolvedMaterialId = materialId ?? new MaterialId(Guid.Parse("50000000-0000-0000-0000-000000000001"));
        var part = new GeneratedPart
        {
            PartId = partId,
            CabinetId = cabinetId,
            PartType = "LeftSide",
            Width = Length.FromInches(24m),
            Height = Length.FromInches(30m),
            MaterialThickness = Thickness.Exact(Length.FromInches(0.75m)),
            MaterialId = resolvedMaterialId,
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

        var contextualIssues = new List<ValidationIssue>();
        if (!isValid)
        {
            contextualIssues.Add(new ValidationIssue(ValidationSeverity.Error, "validation.issue", "Blocking validation issue"));
        }

        if (extraIssue)
        {
            contextualIssues.Add(new ValidationIssue(ValidationSeverity.Warning, "validation.warning", "Non-blocking validation issue"));
        }

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
            PartResult = new PartGenerationResult
            {
                Parts = [part]
            },
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
            CostingResult = CreateCostingResult(cabinetId, costingStatus),
            ValidationResult = new ValidationResult
            {
                Result = new FullValidationResult
                {
                    CrossCuttingIssues = [],
                    ContextualIssues = contextualIssues
                }
            }
        };
    }

    private static CostingResult CreateCostingResult(CabinetId cabinetId, CostingStatus status) =>
        new()
        {
            Status = status,
            StatusReason = status == CostingStatus.NotConfigured ? "Pricing catalog not configured." : null,
            MaterialCost = status == CostingStatus.Calculated ? 42m : 0m,
            HardwareCost = 0m,
            LaborCost = 0m,
            InstallCost = 0m,
            Subtotal = status == CostingStatus.Calculated ? 42m : 0m,
            Markup = 0m,
            Tax = 0m,
            Total = status == CostingStatus.Calculated ? 42m : 0m,
            RevisionDelta = null,
            CabinetBreakdowns = status == CostingStatus.Calculated
                ? [new CabinetCostBreakdown(cabinetId.ToString(), 42m, 0m, 0m, 0m)]
                : []
        };

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
