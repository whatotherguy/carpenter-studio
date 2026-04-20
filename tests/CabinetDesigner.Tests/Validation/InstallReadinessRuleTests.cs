using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using CabinetDesigner.Domain.Validation.Rules;
using Xunit;

namespace CabinetDesigner.Tests.Validation;

public sealed class InstallReadinessRuleTests
{
    [Fact]
    public void Evaluate_NoBlockers_ReturnsEmpty()
    {
        var rule = new InstallReadinessRule();

        var result = rule.Evaluate(CreateContext([]));

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_OneBlocker_EmitsErrorIssue()
    {
        var rule = new InstallReadinessRule();

        var result = rule.Evaluate(CreateContext([
            new InstallBlockerSnapshot(
                "ManufacturingNotReady",
                "Manufacturing is not ready, install cannot proceed.",
                ["project:default"])
        ]));

        var issue = Assert.Single(result);
        Assert.Equal("install.ManufacturingNotReady", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(["project:default"], issue.AffectedEntityIds);
    }

    [Fact]
    public void Evaluate_MultipleBlockers_EmitsOneIssuePerBlockerInOrder()
    {
        var rule = new InstallReadinessRule();

        var result = rule.Evaluate(CreateContext([
            new InstallBlockerSnapshot("MissingEngineeringAssembly", "Missing assembly on cabinet c1.", ["cabinet:c1"]),
            new InstallBlockerSnapshot("MissingRunEndConditions", "Run r1 missing end conditions.", ["run:r1"])
        ]));

        Assert.Collection(
            result,
            first =>
            {
                Assert.Equal("install.MissingEngineeringAssembly", first.Code);
                Assert.Equal(["cabinet:c1"], first.AffectedEntityIds);
            },
            second =>
            {
                Assert.Equal("install.MissingRunEndConditions", second.Code);
                Assert.Equal(["run:r1"], second.AffectedEntityIds);
            });
    }

    [Fact]
    public void RuleMetadata_IsStable()
    {
        var rule = new InstallReadinessRule();

        Assert.Equal("install.readiness", rule.RuleCode);
        Assert.Equal(ValidationRuleCategory.Installation, rule.Category);
        Assert.Equal(ValidationRuleScope.Project, rule.Scope);
        Assert.False(rule.PreviewSafe);
    }

    private static ValidationContext CreateContext(IReadOnlyList<InstallBlockerSnapshot> blockers) =>
        new()
        {
            Command = new TestDesignCommand(),
            Mode = ValidationMode.Full,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = [],
            RunSnapshots = [],
            WorkflowState = new WorkflowStateSnapshot("Draft", false, false),
            InstallBlockers = blockers
        };

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Install Readiness Rule Test", []);

        public string CommandType => "test.install_readiness_rule";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
