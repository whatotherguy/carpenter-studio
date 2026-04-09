using CabinetDesigner.Persistence.Snapshots;
using Xunit;

namespace CabinetDesigner.Persistence.Tests;

public sealed class BlobCorruptionTests
{
    [Fact]
    public void Read_MalformedJson_ReturnsUnreadableResult()
    {
        var snapshot = CreateSnapshot("{not-json");
        var reader = new SnapshotBlobReader([new V1SnapshotDeserializer()]);

        var result = reader.Read(snapshot);

        Assert.False(result.IsReadable);
        Assert.Equal(0, result.SchemaVersion);
        Assert.Contains("invalid", result.UnreadableReason ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_MissingRevisionId_ReturnsUnreadableResult()
    {
        var snapshot = CreateSnapshot("""{"schema_version":1,"payload":{"value":"ok"}}""");
        var reader = new SnapshotBlobReader([new V1SnapshotDeserializer()]);

        var result = reader.Read(snapshot);

        Assert.False(result.IsReadable);
        Assert.Equal(1, result.SchemaVersion);
        Assert.Contains("revision_id", result.UnreadableReason ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_SchemaVersionTypeMismatch_ReturnsUnreadableResult()
    {
        var snapshot = CreateSnapshot("""{"schema_version":"one","revision_id":"00000000-0000-0000-0000-000000000001"}""");
        var reader = new SnapshotBlobReader([new V1SnapshotDeserializer()]);

        var result = reader.Read(snapshot);

        Assert.False(result.IsReadable);
        Assert.Equal(0, result.SchemaVersion);
        Assert.NotNull(result.UnreadableReason);
    }

    private static ApprovedSnapshot CreateSnapshot(string blob)
    {
        var revisionId = RevisionId.New();
        return new ApprovedSnapshot(
            revisionId,
            ProjectId.New(),
            1,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"),
            "tester",
            "Rev 1",
            blob,
            blob,
            blob,
            blob,
            blob,
            blob,
            blob);
    }
}
