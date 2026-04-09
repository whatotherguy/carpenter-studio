using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Pipeline;

public sealed class StageResultTests
{
    [Fact]
    public void Succeeded_SetsPropertiesCorrectly()
    {
        ValidationIssue[] warnings = [new(ValidationSeverity.Warning, "WARN", "Warning")];
        ExplanationNodeId[] explanationNodeIds = [ExplanationNodeId.New()];

        var result = StageResult.Succeeded(3, explanationNodeIds, warnings);

        Assert.Equal(3, result.StageNumber);
        Assert.True(result.Success);
        Assert.Same(warnings, result.Issues);
        Assert.Same(explanationNodeIds, result.ExplanationNodeIds);
    }

    [Fact]
    public void Failed_SetsPropertiesCorrectly()
    {
        ValidationIssue[] issues = [new(ValidationSeverity.Error, "FAIL", "Failure")];
        ExplanationNodeId[] explanationNodeIds = [ExplanationNodeId.New()];

        var result = StageResult.Failed(4, issues, explanationNodeIds);

        Assert.Equal(4, result.StageNumber);
        Assert.False(result.Success);
        Assert.Same(issues, result.Issues);
        Assert.Same(explanationNodeIds, result.ExplanationNodeIds);
    }
}
