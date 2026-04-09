using CabinetDesigner.Persistence.UnitOfWork;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class AutosaveCheckpointRepository : SqliteRepositoryBase, IAutosaveCheckpointRepository
{
    public AutosaveCheckpointRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task<AutosaveCheckpoint?> FindByProjectAsync(ProjectId projectId, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                SELECT id, project_id, revision_id, saved_at, last_command_id, is_clean
                FROM autosave_checkpoints
                WHERE project_id = @projectId
                ORDER BY saved_at DESC
                LIMIT 1;
                """);
            command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return new AutosaveCheckpoint(
                reader.GetString(0),
                new ProjectId(Guid.Parse(reader.GetString(1))),
                new RevisionId(Guid.Parse(reader.GetString(2))),
                DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                reader.IsDBNull(4) ? null : new CommandId(Guid.Parse(reader.GetString(4))),
                reader.GetInt32(5) == 1);
        }, ct);

    public Task SaveAsync(AutosaveCheckpoint checkpoint, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                INSERT INTO autosave_checkpoints(id, project_id, revision_id, saved_at, last_command_id, is_clean)
                VALUES(@id, @projectId, @revisionId, @savedAt, @lastCommandId, @isClean)
                ON CONFLICT(id) DO UPDATE SET
                    revision_id = excluded.revision_id,
                    saved_at = excluded.saved_at,
                    last_command_id = excluded.last_command_id,
                    is_clean = excluded.is_clean;
                """);
            command.Parameters.AddWithValue("@id", checkpoint.Id);
            command.Parameters.AddWithValue("@projectId", checkpoint.ProjectId.Value.ToString());
            command.Parameters.AddWithValue("@revisionId", checkpoint.RevisionId.Value.ToString());
            command.Parameters.AddWithValue("@savedAt", checkpoint.SavedAt.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("@lastCommandId", (object?)checkpoint.LastCommandId?.Value.ToString() ?? DBNull.Value);
            command.Parameters.AddWithValue("@isClean", checkpoint.IsClean ? 1 : 0);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task MarkCleanAsync(ProjectId projectId, DateTimeOffset savedAt, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                UPDATE autosave_checkpoints
                SET saved_at = @savedAt,
                    is_clean = 1
                WHERE project_id = @projectId;
                """);
            command.Parameters.AddWithValue("@savedAt", savedAt.UtcDateTime.ToString("O"));
            command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
}
