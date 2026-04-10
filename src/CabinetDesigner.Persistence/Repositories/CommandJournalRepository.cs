using CabinetDesigner.Persistence.Mapping;
using CabinetDesigner.Persistence.Models;
using CabinetDesigner.Persistence.UnitOfWork;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class CommandJournalRepository : SqliteRepositoryBase, ICommandJournalRepository
{
    public CommandJournalRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task AppendAsync(CommandJournalEntry entry, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            var row = CommandJournalMapper.ToRow(entry);
            using var command = CreateCommand(connection, transaction, """
                INSERT INTO command_journal(id, revision_id, sequence_number, command_type, origin, intent_description, affected_entity_ids, parent_command_id, timestamp, command_json, deltas_json, succeeded)
                VALUES(@id, @revisionId, (SELECT COALESCE(MAX(sequence_number), 0) + 1 FROM command_journal WHERE revision_id = @revisionId), @commandType, @origin, @intentDescription, @affectedEntityIds, @parentCommandId, @timestamp, @commandJson, @deltasJson, @succeeded);
                """);
            command.Parameters.AddWithValue("@id", row.Id);
            command.Parameters.AddWithValue("@revisionId", row.RevisionId);
            command.Parameters.AddWithValue("@commandType", row.CommandType);
            command.Parameters.AddWithValue("@origin", row.Origin);
            command.Parameters.AddWithValue("@intentDescription", row.IntentDescription);
            command.Parameters.AddWithValue("@affectedEntityIds", row.AffectedEntityIds);
            command.Parameters.AddWithValue("@parentCommandId", (object?)row.ParentCommandId ?? DBNull.Value);
            command.Parameters.AddWithValue("@timestamp", row.Timestamp);
            command.Parameters.AddWithValue("@commandJson", row.CommandJson);
            command.Parameters.AddWithValue("@deltasJson", row.DeltasJson);
            command.Parameters.AddWithValue("@succeeded", row.Succeeded);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<CommandJournalEntry>> LoadForRevisionAsync(RevisionId revisionId, CancellationToken ct = default) =>
        WithConnectionAsync<IReadOnlyList<CommandJournalEntry>>(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                SELECT id, revision_id, sequence_number, command_type, origin, intent_description, affected_entity_ids, parent_command_id, timestamp, command_json, deltas_json, succeeded
                FROM command_journal
                WHERE revision_id = @revisionId
                ORDER BY sequence_number;
                """);
            command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
            var entries = new List<CommandJournalEntry>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                entries.Add(CommandJournalMapper.ToRecord(new CommandJournalRow
                {
                    Id = reader.GetString(0),
                    RevisionId = reader.GetString(1),
                    SequenceNumber = reader.GetInt32(2),
                    CommandType = reader.GetString(3),
                    Origin = reader.GetString(4),
                    IntentDescription = reader.GetString(5),
                    AffectedEntityIds = reader.GetString(6),
                    ParentCommandId = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Timestamp = reader.GetString(8),
                    CommandJson = reader.GetString(9),
                    DeltasJson = reader.GetString(10),
                    Succeeded = reader.GetInt32(11)
                }));
            }

            return entries;
        }, ct);

}
