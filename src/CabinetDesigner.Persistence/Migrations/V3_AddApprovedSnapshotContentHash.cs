namespace CabinetDesigner.Persistence.Migrations;

public sealed class V3_AddApprovedSnapshotContentHash : ISchemaMigration
{
    public int Version => 3;

    public string Description => "Adds approved snapshot content hashes for deterministic package identity.";

    public void Apply(IDbConnection connection, IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        using var schemaCommand = connection.CreateCommand();
        schemaCommand.Transaction = transaction;
        schemaCommand.CommandText = "PRAGMA table_info(approved_snapshots);";

        using var reader = schemaCommand.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), "content_hash", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.Transaction = transaction;
        alter.CommandText = "ALTER TABLE approved_snapshots ADD COLUMN content_hash TEXT NOT NULL DEFAULT '';";
        alter.ExecuteNonQuery();
    }
}
