using System;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Commands;

public sealed class UndoEntryTests
{
    [Fact]
    public void Constructor_SetsAllFields()
    {
        var metadata = CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Undoable", []);
        StateDelta[] deltas = [new("entity-1", "Cabinet", DeltaOperation.Created)];
        ExplanationNodeId[] explanationNodeIds = [ExplanationNodeId.New()];

        var entry = new UndoEntry(metadata, deltas, explanationNodeIds);

        Assert.Equal(metadata, entry.CommandMetadata);
        Assert.Same(deltas, entry.Deltas);
        Assert.Same(explanationNodeIds, entry.ExplanationNodeIds);
    }

    [Fact]
    public void Deltas_ExposedCorrectly()
    {
        StateDelta[] deltas = [new("entity-1", "Run", DeltaOperation.Modified)];
        var entry = new UndoEntry(
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Undoable", []),
            deltas,
            []);

        Assert.Same(deltas, entry.Deltas);
    }

    [Fact]
    public void ExplanationNodeIds_ExposedCorrectly()
    {
        ExplanationNodeId[] explanationNodeIds = [ExplanationNodeId.New()];
        var entry = new UndoEntry(
            CommandMetadata.Create(DateTimeOffset.UnixEpoch, CommandOrigin.User, "Undoable", []),
            [],
            explanationNodeIds);

        Assert.Same(explanationNodeIds, entry.ExplanationNodeIds);
    }
}
