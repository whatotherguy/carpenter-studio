using CabinetDesigner.Persistence.Mapping;
using CabinetDesigner.Persistence.Models;
using CabinetDesigner.Persistence.UnitOfWork;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class ExplanationRepository : SqliteRepositoryBase, IExplanationRepository
{
    public ExplanationRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task AppendNodeAsync(ExplanationNodeRecord node, CancellationToken ct = default) =>
        WithConnectionAsync(async (connection, transaction) =>
        {
            var row = ExplanationNodeMapper.ToRow(node);
            using var command = CreateCommand(connection, transaction, """
                INSERT INTO explanation_nodes(id, revision_id, command_id, stage_number, node_type, decision_type, description, affected_entity_ids, parent_node_id, edge_type, status, created_at)
                VALUES(@id, @revisionId, @commandId, @stageNumber, @nodeType, @decisionType, @description, @affectedEntityIds, @parentNodeId, @edgeType, @status, @createdAt);
                """);
            command.Parameters.AddWithValue("@id", row.Id);
            command.Parameters.AddWithValue("@revisionId", row.RevisionId);
            command.Parameters.AddWithValue("@commandId", (object?)row.CommandId ?? DBNull.Value);
            command.Parameters.AddWithValue("@stageNumber", (object?)row.StageNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@nodeType", row.NodeType);
            command.Parameters.AddWithValue("@decisionType", row.DecisionType);
            command.Parameters.AddWithValue("@description", row.Description);
            command.Parameters.AddWithValue("@affectedEntityIds", row.AffectedEntityIds);
            command.Parameters.AddWithValue("@parentNodeId", (object?)row.ParentNodeId ?? DBNull.Value);
            command.Parameters.AddWithValue("@edgeType", (object?)row.EdgeType ?? DBNull.Value);
            command.Parameters.AddWithValue("@status", row.Status);
            command.Parameters.AddWithValue("@createdAt", row.CreatedAt);
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            foreach (var entityId in node.AffectedEntityIds.Distinct(StringComparer.Ordinal))
            {
                using var indexCommand = CreateCommand(connection, transaction, "INSERT INTO explanation_entity_index(node_id, entity_id) VALUES(@nodeId, @entityId);");
                indexCommand.Parameters.AddWithValue("@nodeId", row.Id);
                indexCommand.Parameters.AddWithValue("@entityId", entityId);
                await indexCommand.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }, ct);

    public Task<IReadOnlyList<ExplanationNodeRecord>> LoadForEntityAsync(string entityId, RevisionId revisionId, CancellationToken ct = default) =>
        LoadAsync("""
            SELECT n.id, n.revision_id, n.command_id, n.stage_number, n.node_type, n.decision_type, n.description, n.affected_entity_ids, n.parent_node_id, n.edge_type, n.status, n.created_at
            FROM explanation_nodes n
            INNER JOIN explanation_entity_index e ON e.node_id = n.id
            WHERE e.entity_id = @entityId AND n.revision_id = @revisionId
            ORDER BY n.created_at ASC, n.id ASC;
            """,
            ct,
            ("@entityId", entityId),
            ("@revisionId", revisionId.Value.ToString()));

    public Task<IReadOnlyList<ExplanationNodeRecord>> LoadForCommandAsync(CommandId commandId, CancellationToken ct = default) =>
        LoadAsync("""
            SELECT id, revision_id, command_id, stage_number, node_type, decision_type, description, affected_entity_ids, parent_node_id, edge_type, status, created_at
            FROM explanation_nodes
            WHERE command_id = @commandId
            ORDER BY created_at ASC, id ASC;
            """,
            ct,
            ("@commandId", commandId.Value.ToString()));

    private Task<IReadOnlyList<ExplanationNodeRecord>> LoadAsync(string sql, CancellationToken ct, params (string Name, object Value)[] parameters) =>
        WithConnectionAsync<IReadOnlyList<ExplanationNodeRecord>>(async (connection, transaction) =>
        {
            using var command = CreateCommand(connection, transaction, sql);
            foreach (var parameter in parameters)
            {
                command.Parameters.AddWithValue(parameter.Name, parameter.Value);
            }

            var nodes = new List<ExplanationNodeRecord>();
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                nodes.Add(ExplanationNodeMapper.ToRecord(new ExplanationNodeRow
                {
                    Id = reader.GetString(0),
                    RevisionId = reader.GetString(1),
                    CommandId = reader.IsDBNull(2) ? null : reader.GetString(2),
                    StageNumber = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    NodeType = reader.GetString(4),
                    DecisionType = reader.GetString(5),
                    Description = reader.GetString(6),
                    AffectedEntityIds = reader.GetString(7),
                    ParentNodeId = reader.IsDBNull(8) ? null : reader.GetString(8),
                    EdgeType = reader.IsDBNull(9) ? null : reader.GetString(9),
                    Status = reader.GetString(10),
                    CreatedAt = reader.GetString(11)
                }));
            }

            return nodes;
        }, ct);
}
