using CabinetDesigner.Persistence.Mapping;
using CabinetDesigner.Persistence.Models;
using CabinetDesigner.Persistence.UnitOfWork;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class SnapshotRepository : SqliteRepositoryBase, ISnapshotRepository
{
    public SnapshotRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task WriteAsync(ApprovedSnapshot snapshot, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            var row = SnapshotMapper.ToRow(snapshot);
            using var command = CreateCommand(connection, transaction, """
                INSERT OR IGNORE INTO approved_snapshots(
                    revision_id, snapshot_schema_ver, approved_at, approved_by,
                    design_blob, parts_blob, manufacturing_blob, install_blob,
                    estimate_blob, validation_blob, explanation_blob)
                VALUES(
                    @revisionId, @snapshotSchemaVer, @approvedAt, @approvedBy,
                    @designBlob, @partsBlob, @manufacturingBlob, @installBlob,
                    @estimateBlob, @validationBlob, @explanationBlob);
                """);
            command.Parameters.AddWithValue("@revisionId", row.RevisionId);
            command.Parameters.AddWithValue("@snapshotSchemaVer", row.SnapshotSchemaVer);
            command.Parameters.AddWithValue("@approvedAt", row.ApprovedAt);
            command.Parameters.AddWithValue("@approvedBy", row.ApprovedBy);
            command.Parameters.AddWithValue("@designBlob", row.DesignBlob);
            command.Parameters.AddWithValue("@partsBlob", row.PartsBlob);
            command.Parameters.AddWithValue("@manufacturingBlob", row.ManufacturingBlob);
            command.Parameters.AddWithValue("@installBlob", row.InstallBlob);
            command.Parameters.AddWithValue("@estimateBlob", row.EstimateBlob);
            command.Parameters.AddWithValue("@validationBlob", row.ValidationBlob);
            command.Parameters.AddWithValue("@explanationBlob", row.ExplanationBlob);
            var rowsAffected = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            if (rowsAffected == 0)
            {
                throw new InvalidOperationException($"Revision {snapshot.RevisionId} already has an approved snapshot.");
            }
        }, ct);

    public Task<ApprovedSnapshot?> ReadAsync(RevisionId revisionId, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                SELECT s.revision_id, s.snapshot_schema_ver, s.approved_at, s.approved_by, s.design_blob, s.parts_blob, s.manufacturing_blob, s.install_blob, s.estimate_blob, s.validation_blob, s.explanation_blob,
                       r.project_id, r.revision_number, COALESCE(r.label, '') AS label
                FROM approved_snapshots s
                INNER JOIN revisions r ON r.id = s.revision_id
                WHERE s.revision_id = @revisionId;
                """);
            command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                return null;
            }

            return SnapshotMapper.ToRecord(
                new ApprovedSnapshotRow
                {
                    RevisionId = reader.GetString(0),
                    SnapshotSchemaVer = reader.GetInt32(1),
                    ApprovedAt = reader.GetString(2),
                    ApprovedBy = reader.GetString(3),
                    DesignBlob = reader.GetString(4),
                    PartsBlob = reader.GetString(5),
                    ManufacturingBlob = reader.GetString(6),
                    InstallBlob = reader.GetString(7),
                    EstimateBlob = reader.GetString(8),
                    ValidationBlob = reader.GetString(9),
                    ExplanationBlob = reader.GetString(10)
                },
                new ProjectId(Guid.Parse(reader.GetString(11))),
                reader.GetInt32(12),
                reader.GetString(13));
        }, ct);

    public Task<IReadOnlyList<SnapshotSummary>> ListAsync(ProjectId projectId, CancellationToken ct = default) =>
        WithConnectionAsync<IReadOnlyList<SnapshotSummary>>(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                SELECT r.id, r.project_id, r.revision_number, COALESCE(r.label, ''), s.approved_at, s.approved_by
                FROM approved_snapshots s
                INNER JOIN revisions r ON r.id = s.revision_id
                WHERE r.project_id = @projectId
                ORDER BY r.revision_number DESC;
                """);
            command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());
            var results = new List<SnapshotSummary>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(new SnapshotSummary(
                    new RevisionId(Guid.Parse(reader.GetString(0))),
                    new ProjectId(Guid.Parse(reader.GetString(1))),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    reader.GetString(5)));
            }

            return results;
        }, ct);
}
