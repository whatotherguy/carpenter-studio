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
    public void AccessingUnsetStageResult_ThrowsInvalidOperationException()
    {
        var context = CreateContext();

        Assert.Throws<InvalidOperationException>(() => _ = context.InputCapture);
        Assert.Throws<InvalidOperationException>(() => _ = context.Interpretation);
        Assert.Throws<InvalidOperationException>(() => _ = context.SpatialResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.EngineeringResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.ConstraintResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.PartResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.ManufacturingResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.InstallResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.CostingResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.ValidationResult);
        Assert.Throws<InvalidOperationException>(() => _ = context.PackagingResult);
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
            Summary = new SnapshotSummary(0, 0, 0, 0, 0m)
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
