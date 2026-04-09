using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Commands.Structural;

public sealed class CreateRunCommandTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var startPoint = new Point2D(0m, 0m);
        var endPoint = new Point2D(120m, 0m);

        var command = new CreateRunCommand(
            startPoint,
            endPoint,
            "wall-1",
            CommandOrigin.User,
            "Create run",
            DateTimeOffset.UnixEpoch);

        Assert.Equal(startPoint, command.StartPoint);
        Assert.Equal(endPoint, command.EndPoint);
        Assert.Equal("wall-1", command.WallId);
    }

    [Fact]
    public void CommandType_IsCorrectDiscriminator()
    {
        var command = CreateValidCommand();

        Assert.Equal("structural.create_run", command.CommandType);
    }

    [Fact]
    public void Metadata_AffectedEntityIds_ContainsWallId()
    {
        var command = CreateValidCommand(wallId: "wall-42");

        Assert.Contains("wall-42", command.Metadata.AffectedEntityIds);
    }

    [Fact]
    public void ValidateStructure_SameStartAndEnd_ReturnsError()
    {
        var point = new Point2D(12m, 0m);
        var command = new CreateRunCommand(
            point,
            point,
            "wall-1",
            CommandOrigin.User,
            "Create run",
            DateTimeOffset.UnixEpoch);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "ZERO_LENGTH_RUN");
    }

    [Fact]
    public void ValidateStructure_EmptyWallId_ReturnsError()
    {
        var command = CreateValidCommand(wallId: string.Empty);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "MISSING_WALL");
    }

    [Fact]
    public void ValidateStructure_ValidCommand_ReturnsEmptyList()
    {
        var command = CreateValidCommand();

        var issues = command.ValidateStructure();

        Assert.Empty(issues);
    }

    private static CreateRunCommand CreateValidCommand(
        Point2D? startPoint = null,
        Point2D? endPoint = null,
        string wallId = "wall-1") =>
        new(
            startPoint ?? new Point2D(0m, 0m),
            endPoint ?? new Point2D(120m, 0m),
            wallId,
            CommandOrigin.User,
            "Create run",
            DateTimeOffset.UnixEpoch);
}
