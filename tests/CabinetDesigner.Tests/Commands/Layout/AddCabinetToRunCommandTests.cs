using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Commands.Layout;

public sealed class AddCabinetToRunCommandTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var runId = RunId.New();
        var timestamp = new DateTimeOffset(2026, 4, 7, 12, 0, 0, TimeSpan.Zero);

        var command = new AddCabinetToRunCommand(
            runId,
            "base-36",
            Length.FromInches(36m),
            RunPlacement.EndOfRun,
            CommandOrigin.User,
            "Add cabinet",
            timestamp);

        Assert.Equal(runId, command.RunId);
        Assert.Equal("base-36", command.CabinetTypeId);
        Assert.Equal(Length.FromInches(36m), command.NominalWidth);
        Assert.Equal(RunPlacement.EndOfRun, command.Placement);
        Assert.Null(command.InsertAtIndex);
    }

    [Fact]
    public void CommandType_IsCorrectDiscriminator()
    {
        var command = CreateValidCommand();

        Assert.Equal("layout.add_cabinet_to_run", command.CommandType);
    }

    [Fact]
    public void Metadata_Origin_IsSet()
    {
        var command = CreateValidCommand(origin: CommandOrigin.Template);

        Assert.Equal(CommandOrigin.Template, command.Metadata.Origin);
    }

    [Fact]
    public void Metadata_AffectedEntityIds_ContainsRunId()
    {
        var runId = RunId.New();
        var command = CreateValidCommand(runId: runId);

        Assert.Contains(runId.Value.ToString(), command.Metadata.AffectedEntityIds);
    }

    [Fact]
    public void ValidateStructure_ZeroWidth_ReturnsError()
    {
        var command = CreateValidCommand(nominalWidth: Length.Zero);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "INVALID_WIDTH" && issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateStructure_NegativeWidth_ReturnsError()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.FromInches(-1m));
    }

    [Fact]
    public void ValidateStructure_EmptyCabinetTypeId_ReturnsError()
    {
        var command = CreateValidCommand(cabinetTypeId: string.Empty);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "MISSING_TYPE");
    }

    [Fact]
    public void ValidateStructure_WhitespaceCabinetTypeId_ReturnsError()
    {
        var command = CreateValidCommand(cabinetTypeId: "   ");

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "MISSING_TYPE");
    }

    [Fact]
    public void ValidateStructure_AtIndexPlacement_WithNoIndex_ReturnsError()
    {
        var command = CreateValidCommand(placement: RunPlacement.AtIndex, insertAtIndex: null);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "MISSING_INDEX");
    }

    [Fact]
    public void ValidateStructure_AtIndexPlacement_WithIndex_ReturnsNoErrors()
    {
        var command = CreateValidCommand(placement: RunPlacement.AtIndex, insertAtIndex: 2);

        var issues = command.ValidateStructure();

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateStructure_ValidCommand_ReturnsEmptyList()
    {
        var command = CreateValidCommand();

        var issues = command.ValidateStructure();

        Assert.Empty(issues);
    }

    private static AddCabinetToRunCommand CreateValidCommand(
        RunId? runId = null,
        string cabinetTypeId = "base-36",
        Length? nominalWidth = null,
        RunPlacement placement = RunPlacement.EndOfRun,
        CommandOrigin origin = CommandOrigin.User,
        int? insertAtIndex = null) =>
        new(
            runId ?? RunId.New(),
            cabinetTypeId,
            nominalWidth ?? Length.FromInches(36m),
            placement,
            origin,
            "Add cabinet",
            DateTimeOffset.UnixEpoch,
            insertAtIndex);
}
