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

    [Fact]
    public void NotImplementedYet_SetsPropertiesCorrectly()
    {
        ExplanationNodeId[] explanationNodeIds = [ExplanationNodeId.New()];

        var result = StageResult.NotImplementedYet(5, explanationNodeIds);

        Assert.Equal(5, result.StageNumber);
        Assert.True(result.Success);
        Assert.True(result.IsNotImplemented);
        Assert.Empty(result.Issues);
        Assert.Same(explanationNodeIds, result.ExplanationNodeIds);
    }

    [Fact]
    public void NotImplementedYet_WithNoExplanationNodeIds_DefaultsToEmpty()
    {
        var result = StageResult.NotImplementedYet(6);

        Assert.True(result.IsNotImplemented);
        Assert.Empty(result.ExplanationNodeIds);
    }

    [Fact]
    public void Succeeded_IsNotImplemented_IsFalse()
    {
        var result = StageResult.Succeeded(1);

        Assert.False(result.IsNotImplemented);
    }

    [Fact]
    public void Failed_IsNotImplemented_IsFalse()
    {
        var result = StageResult.Failed(1, [new ValidationIssue(ValidationSeverity.Error, "ERR", "Error")]);

        Assert.False(result.IsNotImplemented);
    }
}
