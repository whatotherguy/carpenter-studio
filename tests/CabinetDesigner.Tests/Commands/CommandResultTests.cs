using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Commands;

public sealed class CommandResultTests
{
    [Fact]
    public void Succeeded_SetsSuccessTrue()
    {
        var metadata = CreateMetadata();

        var result = CommandResult.Succeeded(metadata, [], []);

        Assert.True(result.Success);
    }

    [Fact]
    public void Succeeded_SetsDeltasAndExplanationNodes()
    {
        var metadata = CreateMetadata();
        StateDelta[] deltas = [new("cabinet-1", "Cabinet", DeltaOperation.Created)];
        ExplanationNodeId[] explanationNodeIds = [ExplanationNodeId.New()];

        var result = CommandResult.Succeeded(metadata, deltas, explanationNodeIds);

        Assert.Same(deltas, result.Deltas);
        Assert.Same(explanationNodeIds, result.ExplanationNodeIds);
    }

    [Fact]
    public void Succeeded_WithWarnings_SetsIssues()
    {
        var metadata = CreateMetadata();
        ValidationIssue[] warnings =
        [
            new(ValidationSeverity.Warning, "NO_CHANGE", "No change.")
        ];

        var result = CommandResult.Succeeded(metadata, [], [], warnings);

        Assert.Same(warnings, result.Issues);
    }

    [Fact]
    public void Succeeded_WithNoWarnings_IssuesIsEmpty()
    {
        var metadata = CreateMetadata();

        var result = CommandResult.Succeeded(metadata, [], []);

        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Failed_SetsSuccessFalse()
    {
        var metadata = CreateMetadata();

        var result = CommandResult.Failed(metadata, []);

        Assert.False(result.Success);
    }

    [Fact]
    public void Failed_DeltasIsEmpty()
    {
        var metadata = CreateMetadata();

        var result = CommandResult.Failed(metadata, []);

        Assert.Empty(result.Deltas);
    }

    [Fact]
    public void Failed_ExplanationNodeIdsIsEmpty()
    {
        var metadata = CreateMetadata();

        var result = CommandResult.Failed(metadata, []);

        Assert.Empty(result.ExplanationNodeIds);
    }

    [Fact]
    public void Failed_SetsIssues()
    {
        var metadata = CreateMetadata();
        ValidationIssue[] issues =
        [
            new(ValidationSeverity.Error, "INVALID", "Invalid command.")
        ];

        var result = CommandResult.Failed(metadata, issues);

        Assert.Same(issues, result.Issues);
    }

    [Fact]
    public void Rejected_DelegatesToFailed()
    {
        var metadata = CreateMetadata();
        ValidationIssue[] issues =
        [
            new(ValidationSeverity.Error, "REJECTED", "Rejected.")
        ];

        var failed = CommandResult.Failed(metadata, issues);
        var rejected = CommandResult.Rejected(metadata, issues);

        Assert.Equal(failed, rejected);
    }

    [Fact]
    public void Rejected_SetsSuccessFalse()
    {
        var metadata = CreateMetadata();

        var result = CommandResult.Rejected(
            metadata,
            [new ValidationIssue(ValidationSeverity.Error, "REJECTED", "Rejected.")]);

        Assert.False(result.Success);
    }

    private static CommandMetadata CreateMetadata() =>
        CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Test command", []);
}
