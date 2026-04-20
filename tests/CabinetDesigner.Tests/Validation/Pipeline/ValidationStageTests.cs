using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.InstallContext;
using CabinetDesigner.Domain.ManufacturingContext;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Validation.Pipeline;

public sealed class ValidationStageTests
{
    [Fact]
    public void Execute_InFullMode_ShouldExecuteIsTrue()
    {
        var stage = new ValidationStage(new StubValidationEngine(_ => EmptyResult()));

        Assert.True(stage.ShouldExecute(ResolutionMode.Full));
    }

    [Fact]
    public void Execute_InPreviewMode_ShouldExecuteIsFalse()
    {
        var stage = new ValidationStage(new StubValidationEngine(_ => EmptyResult()));

        Assert.False(stage.ShouldExecute(ResolutionMode.Preview));
    }

    [Fact]
    public void Execute_WithPassingRules_SetsValidationResultOnContext()
    {
        var stage = new ValidationStage(new StubValidationEngine(_ => EmptyResult()));
        var context = CreateResolutionContext();

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.True(context.ValidationResult.IsValid);
        Assert.Empty(context.ValidationResult.Result.AllBaseIssues);
    }

    [Fact]
    public void Execute_WithBlockingRule_ReturnsFailed()
    {
        var stage = new ValidationStage(new StubValidationEngine(_ => new FullValidationResult
        {
            ContextualIssues = [],
            CrossCuttingIssues =
            [
                new ExtendedValidationIssue
                {
                    IssueId = new ValidationIssueId("run_integrity.over_capacity", ["run-1"]),
                    Issue = new ValidationIssue(
                        ValidationSeverity.Error,
                        "run_integrity.over_capacity",
                        "Run run-1 exceeds capacity.",
                        ["run-1"]),
                    RuleCode = "run_integrity.over_capacity",
                    Category = ValidationRuleCategory.RunIntegrity,
                    Scope = ValidationRuleScope.Run,
                    SuggestedFixes = []
                }
            ]
        }));
        var context = CreateResolutionContext();

        var result = stage.Execute(context);

        Assert.False(result.Success);
        Assert.Equal("run_integrity.over_capacity", Assert.Single(result.Issues).Code);
        Assert.Equal("run_integrity.over_capacity", Assert.Single(context.ValidationResult.Result.AllBaseIssues).Code);
    }

    [Fact]
    public void Execute_WithMaterialConstraintViolation_DefaultRules_BlockValidation()
    {
        var stage = new ValidationStage();
        var context = CreateResolutionContext();
        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations =
            [
                new ConstraintViolation(
                    "MATERIAL_UNRESOLVED",
                    "Part material could not be resolved.",
                    ValidationSeverity.Error,
                    ["part:1"])
            ]
        };

        var result = stage.Execute(context);

        Assert.False(result.Success);
        var issue = Assert.Single(result.Issues.Where(x => x.Code == "constraint.material_unresolved"));
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("constraint.material_unresolved", Assert.Single(context.ValidationResult.Result.CrossCuttingIssues).RuleCode);
    }

    [Fact]
    public void Execute_ThreadsAccumulatedIssuesIntoValidationResult()
    {
        var stage = new ValidationStage(new StubValidationEngine(_ => EmptyResult()));
        var context = CreateResolutionContext();
        context.AccumulatedIssues.Add(new ValidationIssue(ValidationSeverity.Warning, "existing.warning", "Existing"));

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.Equal("existing.warning", Assert.Single(context.ValidationResult.Result.ContextualIssues).Code);
        Assert.Equal("existing.warning", Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void BuildCabinetPositions_UsesCabinetIdNotSlotId()
    {
        // Regression test: ensure CabinetPositionSnapshot.CabinetId contains the cabinet ID, not the slot ID
        var cabinetId = CabinetId.New();
        var slotId = RunSlotId.New();
        var runId = RunId.New();

        var spatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates =
            [
                new SlotPositionUpdate(
                    slotId,
                    cabinetId,
                    runId,
                    0,
                    Point2D.Origin,
                    Length.FromInches(24m))
            ],
            AdjacencyChanges = [],
            RunSummaries =
            [
                new RunSummary(
                    runId,
                    Length.FromInches(120m),
                    Length.FromInches(24m),
                    Length.FromInches(96m),
                    1)
            ],
            Placements = []
        };

        ValidationContext? capturedContext = null;
        var engine = new StubValidationEngine(ctx =>
        {
            capturedContext = ctx;
            return EmptyResult();
        });

        var stage = new ValidationStage(engine);
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full,
            SpatialResult = spatialResult,
            EngineeringResult = new EngineeringResolutionResult
            {
                Assemblies = [],
                FillerRequirements = [],
                EndConditionUpdates = [new EndConditionUpdate(runId, EndCondition.Open(), EndCondition.AgainstWall())]
            },
            ConstraintResult = new ConstraintPropagationResult { MaterialAssignments = [], HardwareAssignments = [], Violations = [] },
            PartResult = new PartGenerationResult { Parts = [] },
            ManufacturingResult = new ManufacturingPlanResult
            {
                Plan = new ManufacturingPlan
                {
                    MaterialGroups = [],
                    CutList = [],
                    Operations = [],
                    EdgeBandingRequirements = [],
                    Readiness = new ManufacturingReadinessResult { IsReady = true, Blockers = [] }
                }
            },
            InstallResult = new InstallPlanResult
            {
                Plan = new InstallPlan
                {
                    Steps = [],
                    Dependencies = [],
                    FasteningRequirements = [],
                    Readiness = new InstallReadinessResult { IsReady = true, Blockers = [] }
                }
            },
            CostingResult = new CostingResult
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
            }
        };

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.NotNull(capturedContext);
        var cabinetSnapshot = Assert.Single(capturedContext.CabinetPositions);
        Assert.Equal(cabinetId.ToString(), cabinetSnapshot.CabinetId);
    }

    [Fact]
    public void BuildCabinetPositions_CabinetIdDifferentFromSlotId()
    {
        // Distinction test: explicitly verify that CabinetId and SlotId are different
        var cabinetId = CabinetId.New();
        var slotId = RunSlotId.New();
        var runId = RunId.New();

        // Ensure they are actually different
        Assert.NotEqual(cabinetId.ToString(), slotId.ToString());

        var spatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates =
            [
                new SlotPositionUpdate(
                    slotId,
                    cabinetId,
                    runId,
                    0,
                    Point2D.Origin,
                    Length.FromInches(24m))
            ],
            AdjacencyChanges = [],
            RunSummaries =
            [
                new RunSummary(
                    runId,
                    Length.FromInches(120m),
                    Length.FromInches(24m),
                    Length.FromInches(96m),
                    1)
            ],
            Placements = []
        };

        ValidationContext? capturedContext = null;
        var engine = new StubValidationEngine(ctx =>
        {
            capturedContext = ctx;
            return EmptyResult();
        });

        var stage = new ValidationStage(engine);
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full,
            SpatialResult = spatialResult,
            EngineeringResult = new EngineeringResolutionResult
            {
                Assemblies = [],
                FillerRequirements = [],
                EndConditionUpdates = [new EndConditionUpdate(runId, EndCondition.Open(), EndCondition.AgainstWall())]
            },
            ConstraintResult = new ConstraintPropagationResult { MaterialAssignments = [], HardwareAssignments = [], Violations = [] },
            PartResult = new PartGenerationResult { Parts = [] },
            ManufacturingResult = new ManufacturingPlanResult
            {
                Plan = new ManufacturingPlan
                {
                    MaterialGroups = [],
                    CutList = [],
                    Operations = [],
                    EdgeBandingRequirements = [],
                    Readiness = new ManufacturingReadinessResult { IsReady = true, Blockers = [] }
                }
            },
            InstallResult = new InstallPlanResult
            {
                Plan = new InstallPlan
                {
                    Steps = [],
                    Dependencies = [],
                    FasteningRequirements = [],
                    Readiness = new InstallReadinessResult { IsReady = true, Blockers = [] }
                }
            },
            CostingResult = new CostingResult
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
            }
        };

        var result = stage.Execute(context);

        Assert.True(result.Success);
        Assert.NotNull(capturedContext);
        var cabinetSnapshot = Assert.Single(capturedContext.CabinetPositions);
        Assert.NotEqual(slotId.ToString(), cabinetSnapshot.CabinetId);
        Assert.Equal(cabinetId.ToString(), cabinetSnapshot.CabinetId);
    }

    private static FullValidationResult EmptyResult() =>
        new()
        {
            ContextualIssues = [],
            CrossCuttingIssues = []
        };

    private static ResolutionContext CreateResolutionContext()
    {
        var context = new ResolutionContext
        {
            Command = new TestDesignCommand(),
            Mode = ResolutionMode.Full
        };

        context.SpatialResult = new SpatialResolutionResult
        {
            SlotPositionUpdates =
            [
                new SlotPositionUpdate(
                    RunSlotId.New(),
                    CabinetId.New(),
                    RunId.New(),
                    0,
                    Point2D.Origin,
                    Length.FromInches(24m))
            ],
            AdjacencyChanges = [],
            RunSummaries =
            [
                new RunSummary(
                    RunId.New(),
                    Length.FromInches(120m),
                    Length.FromInches(24m),
                    Length.FromInches(96m),
                    1)
            ],
            Placements = []
        };
        context.EngineeringResult = new EngineeringResolutionResult
        {
            Assemblies = [],
            FillerRequirements = [],
            EndConditionUpdates =
            [
                new EndConditionUpdate(
                    context.SpatialResult.RunSummaries[0].RunId,
                    EndCondition.Open(),
                    EndCondition.AgainstWall())
            ]
        };
        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };
        context.PartResult = new PartGenerationResult { Parts = [] };
        context.ManufacturingResult = new ManufacturingPlanResult
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

        return context;
    }

    private sealed class StubValidationEngine(Func<ValidationContext, FullValidationResult> validate) : IValidationEngine
    {
        public IReadOnlyList<IValidationRule> RegisteredRules => [];

        public FullValidationResult Validate(ValidationContext context, IReadOnlyList<ValidationIssue>? contextualIssues = null)
        {
            var result = validate(context);
            return contextualIssues is not null && contextualIssues.Count > 0
                ? result with { ContextualIssues = contextualIssues }
                : result;
        }

        public IReadOnlyList<ValidationIssue> ValidatePreview(ValidationContext context) => [];

        public IReadOnlyList<ExtendedValidationIssue> ValidateCategory(
            ValidationContext context,
            ValidationRuleCategory category) => [];
    }

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Validation Stage Test", []);

        public string CommandType => "test.validation_stage";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
