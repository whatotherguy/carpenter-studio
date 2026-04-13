using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Validation;

public sealed class ValidationEngineTests
{
    [Fact]
    public void Validate_WithNoRules_ReturnsEmptyResult()
    {
        var engine = new ValidationEngineBuilder().Build();

        var result = engine.Validate(CreateContext());

        Assert.Empty(result.CrossCuttingIssues);
        Assert.Empty(result.AllBaseIssues);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithPassingRule_ReturnsNoIssues()
    {
        var engine = new ValidationEngineBuilder()
            .AddRule(new TestRule("pass.rule", []))
            .Build();

        var result = engine.Validate(CreateContext());

        Assert.Empty(result.CrossCuttingIssues);
    }

    [Fact]
    public void Validate_WithFailingRule_ReturnsIssue()
    {
        var engine = new ValidationEngineBuilder()
            .AddRule(new TestRule("fail.rule", [CreateIssue(ValidationSeverity.Error, "fail.rule", "Failed", ["run-1"])]))
            .Build();

        var result = engine.Validate(CreateContext());

        var issue = Assert.Single(result.CrossCuttingIssues);
        Assert.Equal("fail.rule", issue.RuleCode);
        Assert.Equal("fail.rule:run-1", issue.IssueId.Value);
    }

    [Fact]
    public void Validate_DuplicateIssueIds_DeduplicatesInResult()
    {
        var duplicateIssue = CreateIssue(ValidationSeverity.Error, "dup.rule", "Duplicate", ["run-1"]);
        var engine = new ValidationEngineBuilder()
            .AddRule(new TestRule("dup.rule", [duplicateIssue, duplicateIssue]))
            .Build();

        var result = engine.Validate(CreateContext());

        Assert.Single(result.CrossCuttingIssues);
    }

    [Fact]
    public void ValidatePreview_OnlyRunsPreviewSafeRules()
    {
        var engine = new ValidationEngineBuilder()
            .AddRule(new TestRule("preview.rule", [CreateIssue(ValidationSeverity.Warning, "preview.rule", "Preview", ["run-1"])], previewSafe: true))
            .AddRule(new TestRule("full.rule", [CreateIssue(ValidationSeverity.Warning, "full.rule", "Full", ["run-2"])]))
            .Build();

        var result = engine.ValidatePreview(CreateContext(ValidationMode.Preview));

        var issue = Assert.Single(result);
        Assert.Equal("preview.rule", issue.Code);
    }

    [Fact]
    public void ValidateCategory_OnlyRunsRulesInCategory()
    {
        var engine = new ValidationEngineBuilder()
            .AddRule(new TestRule(
                "workflow.rule",
                [CreateIssue(ValidationSeverity.Warning, "workflow.rule", "Workflow", [])],
                category: ValidationRuleCategory.Workflow))
            .AddRule(new TestRule(
                "run.rule",
                [CreateIssue(ValidationSeverity.Error, "run.rule", "Run", ["run-1"])],
                category: ValidationRuleCategory.RunIntegrity))
            .Build();

        var result = engine.ValidateCategory(CreateContext(), ValidationRuleCategory.Workflow);

        var issue = Assert.Single(result);
        Assert.Equal("workflow.rule", issue.RuleCode);
    }

    [Fact]
    public void Builder_DuplicateRuleCode_Throws()
    {
        var builder = new ValidationEngineBuilder()
            .AddRule(new TestRule("duplicate.rule", []));

        Assert.Throws<InvalidOperationException>(() => builder.AddRule(new TestRule("duplicate.rule", [])));
    }

    [Fact]
    public void FullValidationResult_SeverityAggregationCountsAllIssues()
    {
        var result = new FullValidationResult
        {
            ContextualIssues =
            [
                CreateIssue(ValidationSeverity.Info, "context.info", "Info", []),
                CreateIssue(ValidationSeverity.Warning, "context.warning", "Warning", [])
            ],
            CrossCuttingIssues =
            [
                CreateExtendedIssue(ValidationSeverity.Error, "cross.error", "Error", ["run-1"]),
                CreateExtendedIssue(ValidationSeverity.ManufactureBlocker, "cross.blocker", "Blocker", ["run-2"])
            ]
        };

        Assert.Equal(1, result.SeverityCounts.Info);
        Assert.Equal(1, result.SeverityCounts.Warnings);
        Assert.Equal(1, result.SeverityCounts.Errors);
        Assert.Equal(1, result.SeverityCounts.ManufactureBlockers);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FullValidationResult_MultiRuleAggregation_ReturnsDeterministicSortedBaseIssues()
    {
        var result = new FullValidationResult
        {
            ContextualIssues =
            [
                CreateIssue(ValidationSeverity.Warning, "context.warning", "Context warning", [])
            ],
            CrossCuttingIssues =
            [
                CreateExtendedIssue(ValidationSeverity.ManufactureBlocker, "z.rule", "Blocker", ["run-2"]),
                CreateExtendedIssue(ValidationSeverity.Error, "a.rule", "Error", ["run-1"])
            ]
        };

        var codes = result.AllBaseIssues.Select(issue => issue.Code).ToArray();

        Assert.Equal(new[] { "z.rule", "a.rule", "context.warning" }, codes);
    }

    [Fact]
    public void Validate_WithContextualIssuesParameter_IncludesThemInResult()
    {
        var engine = new ValidationEngineBuilder().Build();
        var contextualIssues = new[]
        {
            CreateIssue(ValidationSeverity.Warning, "context.issue.1", "First contextual issue", []),
            CreateIssue(ValidationSeverity.Error, "context.issue.2", "Second contextual issue", [])
        };

        var result = engine.Validate(CreateContext(), contextualIssues);

        Assert.Equal(2, result.ContextualIssues.Count);
        Assert.Equal("context.issue.1", result.ContextualIssues[0].Code);
        Assert.Equal("context.issue.2", result.ContextualIssues[1].Code);
    }

    [Fact]
    public void Validate_WithBlockerContextualIssue_MakesResultInvalid()
    {
        var engine = new ValidationEngineBuilder().Build();
        var blockerIssue = new[] { CreateIssue(ValidationSeverity.ManufactureBlocker, "blocker", "Manufacture blocker", []) };

        var result = engine.Validate(CreateContext(), blockerIssue);

        Assert.False(result.IsValid);
        Assert.Single(result.ContextualIssues);
        Assert.Equal(ValidationSeverity.ManufactureBlocker, result.ContextualIssues[0].Severity);
    }

    [Fact]
    public void Validate_WithoutContextualIssuesParameter_DefaultsToEmpty()
    {
        var engine = new ValidationEngineBuilder().Build();

        var result = engine.Validate(CreateContext());

        Assert.Empty(result.ContextualIssues);
    }

    private static ValidationContext CreateContext(ValidationMode mode = ValidationMode.Full) =>
        new()
        {
            Command = new TestDesignCommand(),
            Mode = mode,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = [],
            RunSnapshots = [],
            WorkflowState = new WorkflowStateSnapshot("Draft", false, false)
        };

    private static ValidationIssue CreateIssue(
        ValidationSeverity severity,
        string code,
        string message,
        IReadOnlyList<string> affectedEntityIds) =>
        new(severity, code, message, affectedEntityIds);

    private static ExtendedValidationIssue CreateExtendedIssue(
        ValidationSeverity severity,
        string code,
        string message,
        IReadOnlyList<string> affectedEntityIds) =>
        new()
        {
            IssueId = new ValidationIssueId(code, affectedEntityIds),
            Issue = CreateIssue(severity, code, message, affectedEntityIds),
            RuleCode = code,
            Category = ValidationRuleCategory.RunIntegrity,
            Scope = ValidationRuleScope.Run,
            SuggestedFixes = []
        };

    private sealed class TestRule(
        string ruleCode,
        IReadOnlyList<ValidationIssue> issues,
        bool previewSafe = false,
        ValidationRuleCategory category = ValidationRuleCategory.RunIntegrity) : IValidationRule
    {
        public string RuleCode => ruleCode;

        public string RuleName => ruleCode;

        public string Description => ruleCode;

        public ValidationRuleCategory Category => category;

        public ValidationRuleScope Scope => ValidationRuleScope.Run;

        public bool PreviewSafe => previewSafe;

        public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context) => issues;
    }

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Validation Engine Test", []);

        public string CommandType => "test.validation_engine";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
