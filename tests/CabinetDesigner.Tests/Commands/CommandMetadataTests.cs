using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Commands;

public sealed class CommandMetadataTests
{
    [Fact]
    public void Create_SetsAllRequiredFields()
    {
        var timestamp = new DateTimeOffset(2026, 4, 7, 12, 0, 0, TimeSpan.Zero);
        string[] affectedEntityIds = ["run-1", "cabinet-2"];

        var metadata = CommandMetadata.Create(
            timestamp,
            CommandOrigin.User,
            "Add cabinet to run",
            affectedEntityIds);

        Assert.Equal(timestamp, metadata.Timestamp);
        Assert.Equal(CommandOrigin.User, metadata.Origin);
        Assert.Equal("Add cabinet to run", metadata.IntentDescription);
        Assert.Same(affectedEntityIds, metadata.AffectedEntityIds);
    }

    [Fact]
    public void Create_GeneratesNonEmptyCommandId()
    {
        var metadata = CommandMetadata.Create(
            DateTimeOffset.UnixEpoch,
            CommandOrigin.System,
            "System command",
            []);

        Assert.NotEqual(Guid.Empty, metadata.CommandId.Value);
    }

    [Fact]
    public void Create_TwoCalls_GenerateDifferentCommandIds()
    {
        var first = CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "First", []);
        var second = CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Second", []);

        Assert.NotEqual(first.CommandId, second.CommandId);
    }

    [Fact]
    public void Create_ParentCommandId_DefaultsToNull()
    {
        var metadata = CommandMetadata.Create(
            DateTimeOffset.UnixEpoch,
            CommandOrigin.Editor,
            "Child command without parent",
            []);

        Assert.Null(metadata.ParentCommandId);
    }

    [Fact]
    public void Create_WithParentCommandId_SetsParentCommandId()
    {
        var parentCommandId = CommandId.New();

        var metadata = CommandMetadata.Create(
            DateTimeOffset.UnixEpoch,
            CommandOrigin.Editor,
            "Child command",
            [],
            parentCommandId);

        Assert.Equal(parentCommandId, metadata.ParentCommandId);
    }

    [Fact]
    public void Create_TimestampIsPreserved()
    {
        var timestamp = new DateTimeOffset(2024, 12, 1, 8, 30, 0, TimeSpan.FromHours(-7));

        var metadata = CommandMetadata.Create(
            timestamp,
            CommandOrigin.Template,
            "Apply template",
            []);

        Assert.Equal(timestamp, metadata.Timestamp);
    }

    [Fact]
    public void Create_AffectedEntityIdsIsPreserved()
    {
        string[] affectedEntityIds = ["entity-1", "entity-2"];

        var metadata = CommandMetadata.Create(
            DateTimeOffset.UnixEpoch,
            CommandOrigin.User,
            "Preserve affected ids",
            affectedEntityIds);

        Assert.Same(affectedEntityIds, metadata.AffectedEntityIds);
    }
}
