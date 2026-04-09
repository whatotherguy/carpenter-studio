namespace CabinetDesigner.Persistence.Snapshots;

public sealed class V1SnapshotDeserializer : ISnapshotDeserializer
{
    public int SchemaVersion => 1;

    public ApprovedSnapshot Deserialize(ApprovedSnapshot snapshot)
    {
        ValidateBlob(snapshot.DesignBlob);
        ValidateBlob(snapshot.PartsBlob);
        ValidateBlob(snapshot.ManufacturingBlob);
        ValidateBlob(snapshot.InstallBlob);
        ValidateBlob(snapshot.EstimateBlob);
        ValidateBlob(snapshot.ValidationBlob);
        ValidateBlob(snapshot.ExplanationBlob);
        return snapshot;
    }

    private static void ValidateBlob(string blob)
    {
        using var document = JsonDocument.Parse(blob);
        var root = document.RootElement;

        if (!root.TryGetProperty("schema_version", out var schemaVersion) || schemaVersion.GetInt32() != 1)
        {
            throw new FormatException("Snapshot blob schema_version must be 1.");
        }

        if (!root.TryGetProperty("revision_id", out _))
        {
            throw new FormatException("Snapshot blob is missing revision_id.");
        }
    }
}
