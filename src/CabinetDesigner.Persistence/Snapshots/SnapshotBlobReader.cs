namespace CabinetDesigner.Persistence.Snapshots;

public sealed class SnapshotBlobReader
{
    private readonly IReadOnlyDictionary<int, ISnapshotDeserializer> _deserializers;

    public SnapshotBlobReader(IEnumerable<ISnapshotDeserializer> deserializers)
    {
        _deserializers = deserializers.ToDictionary(deserializer => deserializer.SchemaVersion);
    }

    public SnapshotReadResult Read(ApprovedSnapshot snapshot)
    {
        try
        {
            var version = ReadVersion(snapshot.DesignBlob);
            if (!_deserializers.TryGetValue(version, out var deserializer))
            {
                return SnapshotReadResult.Unreadable(version, $"No deserializer registered for schema version {version}.");
            }

            try
            {
                return SnapshotReadResult.Ok(version, deserializer.Deserialize(snapshot));
            }
            catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
            {
                return SnapshotReadResult.Unreadable(version, ex.Message);
            }
        }
        catch (Exception ex) when (ex is FormatException or JsonException or InvalidOperationException)
        {
            return SnapshotReadResult.Unreadable(0, ex.Message);
        }
    }

    private static int ReadVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("schema_version").GetInt32();
    }
}
