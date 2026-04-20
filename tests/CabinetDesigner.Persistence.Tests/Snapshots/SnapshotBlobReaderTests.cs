using CabinetDesigner.Persistence.Snapshots;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Snapshots;

public sealed class SnapshotBlobReaderTests
{
    [Fact]
    public void Read_ReturnsReadableSnapshot_ForVersion1Blob()
    {
        var snapshot = CreateSnapshot(1);
        var reader = new SnapshotBlobReader([new V1SnapshotDeserializer()]);

        var result = reader.Read(snapshot);

        Assert.True(result.IsReadable);
        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal(snapshot, result.Snapshot);
        Assert.Null(result.UnreadableReason);
    }

    [Fact]
    public void Read_ReturnsUnreadableResult_ForUnknownVersion()
    {
        var snapshot = CreateSnapshot(99);
        var reader = new SnapshotBlobReader([new V1SnapshotDeserializer()]);

        var result = reader.Read(snapshot);

        Assert.False(result.IsReadable);
        Assert.Equal(99, result.SchemaVersion);
        Assert.Null(result.Snapshot);
        Assert.Contains("No deserializer", result.UnreadableReason, StringComparison.Ordinal);
    }

    private static ApprovedSnapshot CreateSnapshot(int schemaVersion)
    {
        var revisionId = RevisionId.New();
        return new ApprovedSnapshot(
            revisionId,
            ProjectId.New(),
            1,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"),
            "tester",
            "Rev 1",
            "hash",
            CreateBlob(schemaVersion, revisionId),
            CreateBlob(schemaVersion, revisionId),
            CreateBlob(schemaVersion, revisionId),
            CreateBlob(schemaVersion, revisionId),
            CreateBlob(schemaVersion, revisionId),
            CreateBlob(schemaVersion, revisionId),
            CreateBlob(schemaVersion, revisionId));
    }

    private static string CreateBlob(int schemaVersion, RevisionId revisionId) =>
        JsonSerializer.Serialize(new
        {
            schema_version = schemaVersion,
            revision_id = revisionId.Value,
            payload = new { value = "ok" }
        });
}
