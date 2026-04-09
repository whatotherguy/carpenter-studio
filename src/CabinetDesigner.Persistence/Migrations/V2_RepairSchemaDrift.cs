namespace CabinetDesigner.Persistence.Migrations;

public sealed class V2_RepairSchemaDrift : ISchemaMigration
{
    public int Version => 2;

    public string Description => "Repairs schema drift for persistence columns and snapshot immutability guards.";

    public void Apply(IDbConnection connection, IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        EnsureColumn(connection, transaction, "projects", "file_path", "TEXT");
        EnsureColumn(connection, transaction, "revisions", "approval_notes", "TEXT");
        EnsureColumn(connection, transaction, "cabinets", "category", "TEXT NOT NULL DEFAULT 'Base'");
        EnsureColumn(connection, transaction, "cabinets", "construction_method", "TEXT NOT NULL DEFAULT 'Frameless'");

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TRIGGER IF NOT EXISTS trg_snapshots_no_delete
                BEFORE DELETE ON approved_snapshots
            BEGIN
                SELECT RAISE(ABORT, 'approved_snapshots rows are immutable and may not be deleted');
            END;
            """;
        command.ExecuteNonQuery();
    }

    private static void EnsureColumn(IDbConnection connection, IDbTransaction transaction, string tableName, string columnName, string columnDefinition)
    {
        using var schemaCommand = connection.CreateCommand();
        schemaCommand.Transaction = transaction;
        schemaCommand.CommandText = $"PRAGMA table_info({tableName});";

        using var reader = schemaCommand.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.Transaction = transaction;
        alter.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
        alter.ExecuteNonQuery();
    }
}
