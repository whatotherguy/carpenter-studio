using CabinetDesigner.Domain.Validation;
using Xunit;

namespace CabinetDesigner.Tests.Validation;

public sealed class ValidationIssueIdTests
{
    [Fact]
    public void IssueId_SameRuleAndEntities_ProducesSameValue()
    {
        var first = new ValidationIssueId("rule.code", ["b", "a"]);
        var second = new ValidationIssueId("rule.code", ["a", "b"]);

        Assert.Equal(first, second);
        Assert.Equal("rule.code:a+b", first.Value);
    }

    [Fact]
    public void IssueId_EntitiesInDifferentOrder_ProducesSameValue()
    {
        var first = new ValidationIssueId("rule.code", ["run-2", "run-1", "run-3"]);
        var second = new ValidationIssueId("rule.code", ["run-3", "run-2", "run-1"]);

        Assert.Equal(first.Value, second.Value);
    }

    [Fact]
    public void IssueId_DifferentEntities_ProducesDifferentValue()
    {
        var first = new ValidationIssueId("rule.code", ["run-1"]);
        var second = new ValidationIssueId("rule.code", ["run-2"]);

        Assert.NotEqual(first, second);
    }
}
