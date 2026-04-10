using CabinetDesigner.Persistence.Mapping;
using CabinetDesigner.Persistence.Models;
using CabinetDesigner.Persistence.UnitOfWork;
using Microsoft.Data.Sqlite;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class ValidationHistoryRepository : SqliteRepositoryBase, IValidationHistoryRepository
{
    public ValidationHistoryRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task SaveIssuesAsync(RevisionId revisionId, IReadOnlyList<ValidationIssueRecord> issues, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            if (transaction is not null)
            {
                await SaveIssuesCoreAsync(connection, transaction, revisionId, issues, ct).ConfigureAwait(false);
                return;
            }

            await using var localTransaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            try
            {
                await SaveIssuesCoreAsync(connection, localTransaction, revisionId, issues, ct).ConfigureAwait(false);
                await localTransaction.CommitAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await localTransaction.RollbackAsync(ct).ConfigureAwait(false);
                throw;
            }
        }, ct);

    private static async Task SaveIssuesCoreAsync(SqliteConnection connection, SqliteTransaction transaction, RevisionId revisionId, IReadOnlyList<ValidationIssueRecord> issues, CancellationToken ct)
    {
        using var deleteIndex = CreateCommand(connection, transaction, """
            DELETE FROM validation_entity_index
            WHERE issue_id IN (SELECT id FROM validation_issues WHERE revision_id = @revisionId);
            """);
        deleteIndex.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        await deleteIndex.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        using var deleteIssues = CreateCommand(connection, transaction, "DELETE FROM validation_issues WHERE revision_id = @revisionId;");
        deleteIssues.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        await deleteIssues.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

        foreach (var issue in issues)
        {
            var row = ValidationIssueMapper.ToRow(issue);
            using var insert = CreateCommand(connection, transaction, """
                INSERT INTO validation_issues(id, revision_id, run_at, severity, rule_code, message, affected_entity_ids, suggested_fix_json)
                VALUES(@id, @revisionId, @runAt, @severity, @ruleCode, @message, @affectedEntityIds, @suggestedFixJson);
                """);
            insert.Parameters.AddWithValue("@id", row.Id);
            insert.Parameters.AddWithValue("@revisionId", row.RevisionId);
            insert.Parameters.AddWithValue("@runAt", row.RunAt);
            insert.Parameters.AddWithValue("@severity", row.Severity);
            insert.Parameters.AddWithValue("@ruleCode", row.RuleCode);
            insert.Parameters.AddWithValue("@message", row.Message);
            insert.Parameters.AddWithValue("@affectedEntityIds", row.AffectedEntityIds);
            insert.Parameters.AddWithValue("@suggestedFixJson", (object?)row.SuggestedFixJson ?? DBNull.Value);
            await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            foreach (var entityId in issue.AffectedEntityIds.Distinct(StringComparer.Ordinal))
            {
                using var index = CreateCommand(connection, transaction, "INSERT INTO validation_entity_index(issue_id, entity_id) VALUES(@issueId, @entityId);");
                index.Parameters.AddWithValue("@issueId", row.Id);
                index.Parameters.AddWithValue("@entityId", entityId);
                await index.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
    }

    public Task<IReadOnlyList<ValidationIssueRecord>> LoadAsync(RevisionId revisionId, CancellationToken ct = default) =>
        WithConnectionAsync<IReadOnlyList<ValidationIssueRecord>>(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, """
                SELECT id, revision_id, run_at, severity, rule_code, message, affected_entity_ids, suggested_fix_json
                FROM validation_issues
                WHERE revision_id = @revisionId
                ORDER BY severity DESC, rule_code, message;
                """);
            command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
            var issues = new List<ValidationIssueRecord>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                issues.Add(ValidationIssueMapper.ToRecord(new ValidationIssueRow
                {
                    Id = reader.GetString(0),
                    RevisionId = reader.GetString(1),
                    RunAt = reader.GetString(2),
                    Severity = reader.GetString(3),
                    RuleCode = reader.GetString(4),
                    Message = reader.GetString(5),
                    AffectedEntityIds = reader.GetString(6),
                    SuggestedFixJson = reader.IsDBNull(7) ? null : reader.GetString(7)
                }));
            }

            return issues;
        }, ct);
}
