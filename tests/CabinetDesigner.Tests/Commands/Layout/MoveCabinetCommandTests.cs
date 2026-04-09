using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Commands.Layout;

public sealed class MoveCabinetCommandTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var cabinetId = CabinetId.New();
        var sourceRunId = RunId.New();
        var targetRunId = RunId.New();

        var command = new MoveCabinetCommand(
            cabinetId,
            sourceRunId,
            targetRunId,
            RunPlacement.EndOfRun,
            CommandOrigin.User,
            "Move cabinet",
            DateTimeOffset.UnixEpoch,
            3);

        Assert.Equal(cabinetId, command.CabinetId);
        Assert.Equal(sourceRunId, command.SourceRunId);
        Assert.Equal(targetRunId, command.TargetRunId);
        Assert.Equal(RunPlacement.EndOfRun, command.TargetPlacement);
        Assert.Equal(3, command.TargetIndex);
    }

    [Fact]
    public void CommandType_IsCorrectDiscriminator()
    {
        var command = CreateValidCommand();

        Assert.Equal("layout.move_cabinet", command.CommandType);
    }

    [Fact]
    public void Metadata_AffectedEntityIds_ContainsCabinetAndBothRunIds()
    {
        var cabinetId = CabinetId.New();
        var sourceRunId = RunId.New();
        var targetRunId = RunId.New();
        var command = CreateValidCommand(cabinetId, sourceRunId, targetRunId);

        Assert.Contains(cabinetId.Value.ToString(), command.Metadata.AffectedEntityIds);
        Assert.Contains(sourceRunId.Value.ToString(), command.Metadata.AffectedEntityIds);
        Assert.Contains(targetRunId.Value.ToString(), command.Metadata.AffectedEntityIds);
    }

    [Fact]
    public void ValidateStructure_AtIndexPlacement_WithNoIndex_ReturnsError()
    {
        var command = CreateValidCommand(targetPlacement: RunPlacement.AtIndex, targetIndex: null);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "MISSING_INDEX");
    }

    [Fact]
    public void ValidateStructure_ValidCommand_ReturnsEmptyList()
    {
        var command = CreateValidCommand();

        var issues = command.ValidateStructure();

        Assert.Empty(issues);
    }

    private static MoveCabinetCommand CreateValidCommand(
        CabinetId? cabinetId = null,
        RunId? sourceRunId = null,
        RunId? targetRunId = null,
        RunPlacement targetPlacement = RunPlacement.EndOfRun,
        int? targetIndex = null) =>
        new(
            cabinetId ?? CabinetId.New(),
            sourceRunId ?? RunId.New(),
            targetRunId ?? RunId.New(),
            targetPlacement,
            CommandOrigin.User,
            "Move cabinet",
            DateTimeOffset.UnixEpoch,
            targetIndex);
}
