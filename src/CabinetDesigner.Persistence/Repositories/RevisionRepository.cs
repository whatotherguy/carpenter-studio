using CabinetDesigner.Persistence.Mapping;
using CabinetDesigner.Persistence.Models;
using CabinetDesigner.Persistence.UnitOfWork;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class RevisionRepository : SqliteRepositoryBase, IRevisionRepository
{
    public RevisionRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task<RevisionRecord?> FindAsync(RevisionId id, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, "SELECT id, project_id, revision_number, state, created_at, approved_at, approved_by, label, approval_notes FROM revisions WHERE id = @id;");
            command.Parameters.AddWithValue("@id", id.Value.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return RevisionMapper.ToRecord(ReadRow(reader));
        }, ct);

    public Task<RevisionRecord?> FindWorkingAsync(ProjectId projectId, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                SELECT id, project_id, revision_number, state, created_at, approved_at, approved_by, label, approval_notes
                FROM revisions
                WHERE project_id = @projectId AND state IN ('Draft', 'UnderReview')
                ORDER BY revision_number DESC
                LIMIT 1;
                """);
            command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return RevisionMapper.ToRecord(ReadRow(reader));
        }, ct);

    public Task SaveAsync(RevisionRecord revision, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            var row = RevisionMapper.ToRow(revision);
            using var command = CreateCommand(connection, transaction, """
                INSERT INTO revisions(id, project_id, revision_number, state, created_at, approved_at, approved_by, label, approval_notes)
                VALUES(@id, @projectId, @revisionNumber, @state, @createdAt, @approvedAt, @approvedBy, @label, @approvalNotes)
                ON CONFLICT(id) DO UPDATE SET
                    state = excluded.state,
                    approved_at = excluded.approved_at,
                    approved_by = excluded.approved_by,
                    label = excluded.label,
                    approval_notes = excluded.approval_notes;
                """);
            command.Parameters.AddWithValue("@id", row.Id);
            command.Parameters.AddWithValue("@projectId", row.ProjectId);
            command.Parameters.AddWithValue("@revisionNumber", row.RevisionNumber);
            command.Parameters.AddWithValue("@state", row.State);
            command.Parameters.AddWithValue("@createdAt", row.CreatedAt);
            command.Parameters.AddWithValue("@approvedAt", (object?)row.ApprovedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("@approvedBy", (object?)row.ApprovedBy ?? DBNull.Value);
            command.Parameters.AddWithValue("@label", (object?)row.Label ?? DBNull.Value);
            command.Parameters.AddWithValue("@approvalNotes", (object?)row.ApprovalNotes ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);

    public Task<IReadOnlyList<RevisionRecord>> ListAsync(ProjectId projectId, CancellationToken ct = default) =>
        WithConnectionAsync<IReadOnlyList<RevisionRecord>>(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                SELECT id, project_id, revision_number, state, created_at, approved_at, approved_by, label, approval_notes
                FROM revisions
                WHERE project_id = @projectId
                ORDER BY revision_number DESC;
                """);
            command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());
            var revisions = new List<RevisionRecord>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                revisions.Add(RevisionMapper.ToRecord(ReadRow(reader)));
            }

            return revisions;
        }, ct);

    private static RevisionRow ReadRow(Microsoft.Data.Sqlite.SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        ProjectId = reader.GetString(1),
        RevisionNumber = reader.GetInt32(2),
        State = reader.GetString(3),
        CreatedAt = reader.GetString(4),
        ApprovedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
        ApprovedBy = reader.IsDBNull(6) ? null : reader.GetString(6),
        Label = reader.IsDBNull(7) ? null : reader.GetString(7),
        ApprovalNotes = reader.IsDBNull(8) ? null : reader.GetString(8)
    };
}
