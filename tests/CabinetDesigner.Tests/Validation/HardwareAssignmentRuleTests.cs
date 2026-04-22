using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using CabinetDesigner.Domain.Validation.Rules;
using Xunit;

namespace CabinetDesigner.Tests.Validation;

public sealed class HardwareAssignmentRuleTests
{
    [Fact]
    public void Evaluate_NoViolations_ReturnsEmpty()
    {
        var rule = new HardwareAssignmentRule();

        var result = rule.Evaluate(CreateContext([]));

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_NoHardwareCatalog_EmitsWarning()
    {
        var rule = new HardwareAssignmentRule();

        var result = rule.Evaluate(CreateContext([
            new ConstraintViolationSnapshot(
                "NO_HARDWARE_CATALOG",
                "No hardware catalog configured for Base opening. V2 will integrate vendor hardware.",
                ValidationSeverity.Warning,
                ["cabinet:1"])
        ]));

        var issue = Assert.Single(result);
        Assert.Equal("constraint.hardware_missing", issue.Code);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    [Fact]
    public void Evaluate_HardwareMissing_EmitsWarning()
    {
        var rule = new HardwareAssignmentRule();

        var result = rule.Evaluate(CreateContext([
            new ConstraintViolationSnapshot(
                "NO_HARDWARE_CATALOG",
                "No hardware could be resolved.",
                ValidationSeverity.Warning,
                ["cabinet:1"])
        ]));

        var issue = Assert.Single(result);
        Assert.Equal("constraint.hardware_missing", issue.Code);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal(["cabinet:1"], issue.AffectedEntityIds);
    }

    private static ValidationContext CreateContext(IReadOnlyList<ConstraintViolationSnapshot> constraints) =>
        new()
        {
            Command = new TestDesignCommand(),
            Mode = ValidationMode.Full,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = [],
            RunSnapshots = [],
            WorkflowState = new WorkflowStateSnapshot("Draft", false, false),
            Constraints = constraints
        };

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Hardware Assignment Rule Test", []);

        public string CommandType => "test.hardware_assignment_rule";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
