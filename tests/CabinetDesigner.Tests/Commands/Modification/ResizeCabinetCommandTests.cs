using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Commands.Modification;

public sealed class ResizeCabinetCommandTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var cabinetId = CabinetId.New();

        var command = new ResizeCabinetCommand(
            cabinetId,
            Length.FromInches(30m),
            Length.FromInches(36m),
            CommandOrigin.User,
            "Resize cabinet",
            DateTimeOffset.UnixEpoch);

        Assert.Equal(cabinetId, command.CabinetId);
        Assert.Equal(Length.FromInches(30m), command.PreviousNominalWidth);
        Assert.Equal(Length.FromInches(36m), command.NewNominalWidth);
    }

    [Fact]
    public void CommandType_IsCorrectDiscriminator()
    {
        var command = CreateValidCommand();

        Assert.Equal("modification.resize_cabinet", command.CommandType);
    }

    [Fact]
    public void ValidateStructure_ZeroNewWidth_ReturnsError()
    {
        var command = CreateValidCommand(newNominalWidth: Length.Zero);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "INVALID_WIDTH" && issue.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ValidateStructure_SameWidthAsExisting_ReturnsWarning()
    {
        var width = Length.FromInches(30m);
        var command = CreateValidCommand(previousNominalWidth: width, newNominalWidth: width);

        var issues = command.ValidateStructure();

        Assert.Contains(issues, issue => issue.Code == "NO_CHANGE");
    }

    [Fact]
    public void ValidateStructure_ValidCommand_ReturnsEmptyList()
    {
        var command = CreateValidCommand();

        var issues = command.ValidateStructure();

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateStructure_SameWidth_SeverityIsWarning_NotError()
    {
        var width = Length.FromInches(30m);
        var command = CreateValidCommand(previousNominalWidth: width, newNominalWidth: width);

        var issues = command.ValidateStructure();

        var issue = Assert.Single(issues);
        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
    }

    private static ResizeCabinetCommand CreateValidCommand(
        CabinetId? cabinetId = null,
        Length? previousNominalWidth = null,
        Length? newNominalWidth = null) =>
        new(
            cabinetId ?? CabinetId.New(),
            previousNominalWidth ?? Length.FromInches(30m),
            newNominalWidth ?? Length.FromInches(36m),
            CommandOrigin.User,
            "Resize cabinet",
            DateTimeOffset.UnixEpoch);
}
