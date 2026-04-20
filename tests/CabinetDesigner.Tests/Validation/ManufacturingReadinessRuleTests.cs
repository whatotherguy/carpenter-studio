using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using CabinetDesigner.Domain.Validation.Rules;
using Xunit;

namespace CabinetDesigner.Tests.Validation;

public sealed class ManufacturingReadinessRuleTests
{
    [Fact]
    public void Evaluate_NoBlockers_ReturnsEmpty()
    {
        var rule = new ManufacturingReadinessRule();

        var result = rule.Evaluate(CreateContext([]));

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_OneBlocker_EmitsManufactureBlockerIssue()
    {
        var rule = new ManufacturingReadinessRule();

        var result = rule.Evaluate(CreateContext([
            new ManufacturingBlockerSnapshot(
                "NoPartsProduced",
                "No manufacturing parts were produced for the current design.",
                ["project:default"])
        ]));

        var issue = Assert.Single(result);
        Assert.Equal("manufacturing.NoPartsProduced", issue.Code);
        Assert.Equal(ValidationSeverity.ManufactureBlocker, issue.Severity);
        Assert.Equal(["project:default"], issue.AffectedEntityIds);
    }

    [Fact]
    public void Evaluate_MultipleBlockers_EmitsOneIssuePerBlockerInOrder()
    {
        var rule = new ManufacturingReadinessRule();

        var result = rule.Evaluate(CreateContext([
            new ManufacturingBlockerSnapshot("MissingMaterial", "Material missing for part p1.", ["part:p1"]),
            new ManufacturingBlockerSnapshot("InvalidThickness", "Thickness invalid for part p2.", ["part:p2"])
        ]));

        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("manufacturing.MissingMaterial", first.Code);
                Assert.Equal(["part:p1"], first.AffectedEntityIds);
            },
            second =>
            {
                Assert.Equal("manufacturing.InvalidThickness", second.Code);
                Assert.Equal(["part:p2"], second.AffectedEntityIds);
            });
    }

    [Fact]
    public void RuleMetadata_IsStable()
    {
        var rule = new ManufacturingReadinessRule();

        Assert.Equal("manufacturing.readiness", rule.RuleCode);
        Assert.Equal(ValidationRuleCategory.Manufacturing, rule.Category);
        Assert.Equal(ValidationRuleScope.Project, rule.Scope);
        Assert.False(rule.PreviewSafe);
    }

    private static ValidationContext CreateContext(IReadOnlyList<ManufacturingBlockerSnapshot> blockers) =>
        new()
        {
            Command = new TestDesignCommand(),
            Mode = ValidationMode.Full,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = [],
            RunSnapshots = [],
            WorkflowState = new WorkflowStateSnapshot("Draft", false, false),
            ManufacturingBlockers = blockers
        };

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Manufacturing Readiness Rule Test", []);

        public string CommandType => "test.manufacturing_readiness_rule";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
