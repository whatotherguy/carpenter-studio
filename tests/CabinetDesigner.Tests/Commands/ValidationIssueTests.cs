using CabinetDesigner.Domain.Commands;
using Xunit;

namespace CabinetDesigner.Tests.Commands;

public sealed class ValidationIssueTests
{
    [Fact]
    public void Constructor_SetsAllFields()
    {
        string[] affectedEntityIds = ["cabinet-1"];
        var issue = new ValidationIssue(
            ValidationSeverity.Error,
            "INVALID_WIDTH",
            "Width must be positive.",
            affectedEntityIds);

        Assert.Equal(ValidationSeverity.Error, issue.Severity);
        Assert.Equal("INVALID_WIDTH", issue.Code);
        Assert.Equal("Width must be positive.", issue.Message);
        Assert.Same(affectedEntityIds, issue.AffectedEntityIds);
    }

    [Fact]
    public void AffectedEntityIds_DefaultsToNull()
    {
        var issue = new ValidationIssue(
            ValidationSeverity.Warning,
            "NO_CHANGE",
            "No changes detected.");

        Assert.Null(issue.AffectedEntityIds);
    }

    [Fact]
    public void AffectedEntityIds_CanBeProvided()
    {
        string[] affectedEntityIds = ["run-1", "run-2"];
        var issue = new ValidationIssue(
            ValidationSeverity.Info,
            "AFFECTED",
            "Entities affected.",
            affectedEntityIds);

        Assert.Same(affectedEntityIds, issue.AffectedEntityIds);
    }
}
