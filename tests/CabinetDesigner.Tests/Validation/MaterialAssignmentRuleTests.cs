using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using CabinetDesigner.Domain.Validation.Rules;
using Xunit;

namespace CabinetDesigner.Tests.Validation;

public sealed class MaterialAssignmentRuleTests
{
    [Fact]
    public void Evaluate_NoViolations_ReturnsEmpty()
    {
        var rule = new MaterialAssignmentRule();

        var result = rule.Evaluate(CreateContext([]));

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_MaterialUnresolved_EmitsError()
    {
        var rule = new MaterialAssignmentRule();

        var result = rule.Evaluate(CreateContext([
            new ConstraintViolationSnapshot(
                "MATERIAL_UNRESOLVED",
                "Material could not be resolved.",
                ValidationSeverity.Error,
                ["part:1"])
        ]));

        var issue = Assert.Single(result);
        Assert.Equal("constraint.material_unresolved", issue.Code);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal(["part:1"], issue.AffectedEntityIds);
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
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Material Assignment Rule Test", []);

        public string CommandType => "test.material_assignment_rule";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
