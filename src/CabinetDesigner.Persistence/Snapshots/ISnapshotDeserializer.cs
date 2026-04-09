namespace CabinetDesigner.Persistence.Snapshots;

public interface ISnapshotDeserializer
{
    int SchemaVersion { get; }

    ApprovedSnapshot Deserialize(ApprovedSnapshot snapshot);
}
