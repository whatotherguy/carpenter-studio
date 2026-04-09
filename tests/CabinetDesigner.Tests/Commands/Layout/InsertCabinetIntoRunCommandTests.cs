using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Commands.Layout;

public sealed class InsertCabinetIntoRunCommandTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var runId = RunId.New();
        var leftNeighborId = CabinetId.New();
        var rightNeighborId = CabinetId.New();

        var command = new InsertCabinetIntoRunCommand(
            runId,
            "base-18",
            Length.FromInches(18m),
            1,
            leftNeighborId,
            rightNeighborId,
            CommandOrigin.User,
            "Insert cabinet",
            DateTimeOffset.UnixEpoch);

        Assert.Equal(runId, command.RunId);
        Assert.Equal("base-18", command.CabinetTypeId);
        Assert.Equal(Length.FromInches(18m), command.NominalWidth);
        Assert.Equal(1, command.InsertAtIndex);
        Assert.Equal(leftNeighborId, command.LeftNeighborId);
        Assert.Equal(rightNeighborId, command.RightNeighborId);
    }

    [Fact]
    public void CommandType_IsCorrectDiscriminator()
    {
        var command = CreateValidCommand();

        Assert.Equal("layout.insert_cabinet_into_run", command.CommandType);
    }

    [Fact]
    public void ValidateStructure_ZeroWidth_ReturnsError()
    {
        var command = CreateValidCommand(nominalWidth: Length.Zero);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "INVALID_WIDTH");
    }

    [Fact]
    public void ValidateStructure_EmptyCabinetTypeId_ReturnsError()
    {
        var command = CreateValidCommand(cabinetTypeId: string.Empty);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "MISSING_TYPE");
    }

    [Fact]
    public void ValidateStructure_NegativeIndex_ReturnsError()
    {
        var command = CreateValidCommand(insertAtIndex: -1);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "INVALID_INDEX");
    }

    [Fact]
    public void ValidateStructure_ValidCommand_ReturnsEmptyList()
    {
        var command = CreateValidCommand();

        var issues = command.ValidateStructure();

        Assert.Empty(issues);
    }

    private static InsertCabinetIntoRunCommand CreateValidCommand(
        RunId? runId = null,
        string cabinetTypeId = "base-18",
        Length? nominalWidth = null,
        int insertAtIndex = 1,
        CabinetId? leftNeighborId = null,
        CabinetId? rightNeighborId = null) =>
        new(
            runId ?? RunId.New(),
            cabinetTypeId,
            nominalWidth ?? Length.FromInches(18m),
            insertAtIndex,
            leftNeighborId ?? CabinetId.New(),
            rightNeighborId ?? CabinetId.New(),
            CommandOrigin.User,
            "Insert cabinet",
            DateTimeOffset.UnixEpoch);
}
