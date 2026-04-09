using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Validation;
using CabinetDesigner.Domain.Validation.Rules;
using Xunit;

namespace CabinetDesigner.Tests.Validation.Rules;

public sealed class RunOverCapacityRuleTests
{
    [Fact]
    public void Evaluate_RunWithinCapacity_ReturnsNoIssues()
    {
        var rule = new RunOverCapacityRule();

        var result = rule.Evaluate(CreateContext(
            new RunValidationSnapshot("run-1", Length.FromInches(120m), Length.FromInches(120m), 3, true, true)));

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_RunOverCapacity_ReturnsError()
    {
        var rule = new RunOverCapacityRule();

        var result = rule.Evaluate(CreateContext(
            new RunValidationSnapshot("run-1", Length.FromInches(120m), Length.FromInches(121m), 3, true, true)));

        var issue = Assert.Single(result);
        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("run_integrity.over_capacity", issue.Code);
        Assert.Equal(["run-1"], issue.AffectedEntityIds);
    }

    [Fact]
    public void Evaluate_MultipleRunsOverCapacity_ReturnsOneIssuePerRun()
    {
        var rule = new RunOverCapacityRule();

        var result = rule.Evaluate(CreateContext(
            new RunValidationSnapshot("run-1", Length.FromInches(120m), Length.FromInches(121m), 3, true, true),
            new RunValidationSnapshot("run-2", Length.FromInches(96m), Length.FromInches(100m), 2, true, true)));

        var affectedRuns = result.Select(issue => Assert.Single(issue.AffectedEntityIds!)).ToArray();

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { "run-1", "run-2" }, affectedRuns);
    }

    private static ValidationContext CreateContext(params RunValidationSnapshot[] runs) =>
        new()
        {
            Command = new TestDesignCommand(),
            Mode = ValidationMode.Full,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = [],
            RunSnapshots = runs,
            WorkflowState = new WorkflowStateSnapshot("Draft", false, false)
        };

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Run Rule Test", []);

        public string CommandType => "test.run_rule";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
