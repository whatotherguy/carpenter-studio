namespace CabinetDesigner.Application.Persistence;

public sealed record SnapshotReadResult(
    bool IsReadable,
    int SchemaVersion,
    ApprovedSnapshot? Snapshot,
    string? UnreadableReason)
{
    public static SnapshotReadResult Ok(int schemaVersion, ApprovedSnapshot snapshot) =>
        new(true, schemaVersion, snapshot, null);

    public static SnapshotReadResult Unreadable(int schemaVersion, string reason) =>
        new(false, schemaVersion, null, reason);
}
