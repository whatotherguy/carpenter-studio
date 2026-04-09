using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Validation;
using CabinetDesigner.Domain.Validation.Rules;
using Xunit;

namespace CabinetDesigner.Tests.Validation.Rules;

public sealed class WorkflowUnapprovedChangesRuleTests
{
    [Fact]
    public void Evaluate_ApprovedNoChanges_ReturnsNoIssues()
    {
        var rule = new WorkflowUnapprovedChangesRule();

        var result = rule.Evaluate(CreateContext(new WorkflowStateSnapshot("Approved", false, false)));

        Assert.Empty(result);
    }

    [Fact]
    public void Evaluate_HasUnapprovedChanges_ReturnsWarning()
    {
        var rule = new WorkflowUnapprovedChangesRule();

        var result = rule.Evaluate(CreateContext(new WorkflowStateSnapshot("Draft", true, false)));

        var issue = Assert.Single(result);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.Equal("workflow.unapproved_changes", issue.Code);
    }

    private static ValidationContext CreateContext(WorkflowStateSnapshot workflowState) =>
        new()
        {
            Command = new TestDesignCommand(),
            Mode = ValidationMode.Full,
            Strictness = ValidationStrictness.ReportOnly,
            CabinetPositions = [],
            RunSnapshots = [],
            WorkflowState = workflowState
        };

    private sealed record TestDesignCommand : IDesignCommand
    {
        public CommandMetadata Metadata { get; } =
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Workflow Rule Test", []);

        public string CommandType => "test.workflow_rule";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }
}
