using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class ResolutionContextTests
{
    [Fact]
    public void AccessingUnsetStageResult_ThrowsPipelineStageNotExecutedException()
    {
        var context = CreateContext();

        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.InputCapture);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.Interpretation);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.SpatialResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.EngineeringResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.ConstraintResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.PartResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.ManufacturingResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.InstallResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.CostingResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.ValidationResult);
        Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.PackagingResult);
    }

    [Fact]
    public void AccessingUnsetStageResult_IsAlsoInvalidOperationException()
    {
        // PipelineStageNotExecutedException extends InvalidOperationException so existing
        // catch (InvalidOperationException) handlers continue to work.
        var context = CreateContext();
        Assert.ThrowsAny<InvalidOperationException>(() => _ = context.InputCapture);
    }

    [Fact]
    public void AccessingUnsetStageResult_NeverInvoked_ExceptionHasCorrectProperties()
    {
        var context = CreateContext();

        var ex = Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.EngineeringResult);

        Assert.Equal(4, ex.StageNumber);
        Assert.Equal("Engineering Resolution", ex.StageName);
        Assert.False(ex.WasSkipped);
        Assert.Null(ex.PipelineMode);
        Assert.Contains("4", ex.Message);
        Assert.Contains("Engineering Resolution", ex.Message);
    }

    [Fact]
    public void SettingAndGettingStageResult_ReturnsCorrectValue()
    {
        var context = CreateContext();
        var inputCapture = new InputCaptureResult
        {
            ResolvedEntities = new Dictionary<string, IDomainEntity>(),
            NormalizedParameters = new Dictionary<string, CabinetDesigner.Domain.OverrideValue>(),
            TemplateExpansions = []
        };
        var interpretation = new InteractionInterpretationResult
        {
            Operations = [],
            InterpretedParameters = new Dictionary<string, CabinetDesigner.Domain.OverrideValue>()
        };
        var spatial = new SpatialResolutionResult
        {
            SlotPositionUpdates = [],
            AdjacencyChanges = [],
            RunSummaries = [],
            Placements = []
        };
        var engineering = new EngineeringResolutionResult
        {
            Assemblies = [],
            FillerRequirements = [],
            EndConditionUpdates = []
        };
        var constraint = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };
        var part = new PartGenerationResult { Parts = [] };
        var manufacturing = new ManufacturingPlanResult
        {
            Plan = new ManufacturingPlan
            {
                MaterialGroups = [],
                CutList = [],
                Operations = [],
                EdgeBandingRequirements = [],
                Readiness = new ManufacturingReadinessResult
                {
                    IsReady = true,
                    Blockers = []
                }
            }
        };
        var install = new InstallPlanResult
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
        var costing = new CostingResult
        {
            MaterialCost = 0m,
            HardwareCost = 0m,
            LaborCost = 0m,
            InstallCost = 0m,
            Subtotal = 0m,
            Markup = 0m,
            Tax = 0m,
            Total = 0m,
            CabinetBreakdowns = []
        };
        var validation = new ValidationResult
        {
            Result = new CabinetDesigner.Domain.Validation.FullValidationResult
            {
                CrossCuttingIssues = [],
                ContextualIssues = []
            }
        };
        var packaging = new PackagingResult
        {
            SnapshotId = "snapshot",
            RevisionId = default,
            CreatedAt = DateTimeOffset.UnixEpoch,
            ContentHash = "hash",
            Summary = new SnapshotSummary(0, 0, 0, 0, 0m),
            DesignBlob = "{}",
            PartsBlob = "{}",
            ManufacturingBlob = "{}",
            InstallBlob = "{}",
            EstimateBlob = "{}",
            ValidationBlob = "{}",
            ExplanationBlob = "{}"
        };

        context.InputCapture = inputCapture;
        context.Interpretation = interpretation;
        context.SpatialResult = spatial;
        context.EngineeringResult = engineering;
        context.ConstraintResult = constraint;
        context.PartResult = part;
        context.ManufacturingResult = manufacturing;
        context.InstallResult = install;
        context.CostingResult = costing;
        context.ValidationResult = validation;
        context.PackagingResult = packaging;

        Assert.Same(inputCapture, context.InputCapture);
        Assert.Same(interpretation, context.Interpretation);
        Assert.Same(spatial, context.SpatialResult);
        Assert.Same(engineering, context.EngineeringResult);
        Assert.Same(constraint, context.ConstraintResult);
        Assert.Same(part, context.PartResult);
        Assert.Same(manufacturing, context.ManufacturingResult);
        Assert.Same(install, context.InstallResult);
        Assert.Same(costing, context.CostingResult);
        Assert.Same(validation, context.ValidationResult);
        Assert.Same(packaging, context.PackagingResult);
    }

    [Fact]
    public void HasBlockingIssues_WithNoIssues_ReturnsFalse()
    {
        var context = CreateContext();

        Assert.False(context.HasBlockingIssues);
    }

    [Fact]
    public void HasBlockingIssues_WithErrorSeverity_ReturnsTrue()
    {
        var context = CreateContext();
        context.AccumulatedIssues.Add(new ValidationIssue(ValidationSeverity.Error, "ERR", "Error"));

        Assert.True(context.HasBlockingIssues);
    }

    [Fact]
    public void HasBlockingIssues_WithManufactureBlocker_ReturnsTrue()
    {
        var context = CreateContext();
        context.AccumulatedIssues.Add(new ValidationIssue(ValidationSeverity.ManufactureBlocker, "BLOCK", "Blocked"));

        Assert.True(context.HasBlockingIssues);
    }

    [Fact]
    public void HasBlockingIssues_WithWarningOnly_ReturnsFalse()
    {
        var context = CreateContext();
        context.AccumulatedIssues.Add(new ValidationIssue(ValidationSeverity.Warning, "WARN", "Warning"));

        Assert.False(context.HasBlockingIssues);
    }

    [Fact]
    public void MarkStageSkipped_ThenAccessResult_ThrowsSkippedException()
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand([]),
            Mode = ResolutionMode.Preview
        };
        context.MarkStageSkipped(4, "Engineering Resolution");

        var ex = Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.EngineeringResult);

        Assert.True(ex.WasSkipped);
        Assert.Equal(4, ex.StageNumber);
        Assert.Equal("Engineering Resolution", ex.StageName);
        Assert.Equal(ResolutionMode.Preview, ex.PipelineMode);
        Assert.Contains("skipped", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Preview", ex.Message);
    }

    [Fact]
    public void MarkStageSkipped_AllStages_ThrowSkippedExceptionsForEach()
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand([]),
            Mode = ResolutionMode.Preview
        };
        context.MarkStageSkipped(1, "Input Capture");
        context.MarkStageSkipped(2, "Interaction Interpretation");
        context.MarkStageSkipped(3, "Spatial Resolution");
        context.MarkStageSkipped(4, "Engineering Resolution");
        context.MarkStageSkipped(5, "Constraint Propagation");
        context.MarkStageSkipped(6, "Part Generation");
        context.MarkStageSkipped(7, "Manufacturing Planning");
        context.MarkStageSkipped(8, "Install Planning");
        context.MarkStageSkipped(9, "Costing");
        context.MarkStageSkipped(10, "Validation");
        context.MarkStageSkipped(11, "Packaging");

        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.InputCapture).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.Interpretation).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.SpatialResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.EngineeringResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.ConstraintResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.PartResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.ManufacturingResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.InstallResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.CostingResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.ValidationResult).WasSkipped);
        Assert.True(Assert.Throws<PipelineStageNotExecutedException>(() => _ = context.PackagingResult).WasSkipped);
    }

    [Fact]
    public void AccessingResult_AfterSet_ReturnValue_NotSkippedExceptionEvenIfMarkedSkipped()
    {
        // If a result is explicitly set, it takes precedence over the skipped marker.
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand([]),
            Mode = ResolutionMode.Full
        };
        context.MarkStageSkipped(6, "Part Generation");
        var partResult = new PartGenerationResult { Parts = [] };
        context.PartResult = partResult;

        Assert.Same(partResult, context.PartResult);
    }

    private static ResolutionContext CreateContext() =>
        new()
        {
            Command = new TestDesignCommand([]),
            Mode = ResolutionMode.Full
        };

    private sealed record TestDesignCommand(IReadOnlyList<ValidationIssue> Issues) : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test", []);

        public string CommandType => "test.command";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => Issues;
    }
}
