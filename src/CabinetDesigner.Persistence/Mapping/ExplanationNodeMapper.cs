using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class ExplanationNodeMapper
{
    public static ExplanationNodeRow ToRow(ExplanationNodeRecord record) => new()
    {
        Id = record.Id.Value.ToString(),
        RevisionId = record.RevisionId.Value.ToString(),
        CommandId = record.CommandId?.Value.ToString(),
        StageNumber = record.StageNumber,
        NodeType = record.NodeType.ToString(),
        DecisionType = record.DecisionType,
        Description = record.Description,
        AffectedEntityIds = JsonSerializer.Serialize(record.AffectedEntityIds, SqliteJson.Options),
        ParentNodeId = record.ParentNodeId?.Value.ToString(),
        EdgeType = record.EdgeType,
        Status = record.Status.ToString(),
        CreatedAt = record.CreatedAt.UtcDateTime.ToString("O")
    };

    public static ExplanationNodeRecord ToRecord(ExplanationNodeRow row) => new(
        new ExplanationNodeId(Guid.Parse(row.Id)),
        new RevisionId(Guid.Parse(row.RevisionId)),
        row.CommandId is null ? null : new CommandId(Guid.Parse(row.CommandId)),
        row.StageNumber,
        Enum.Parse<ExplanationNodeType>(row.NodeType, ignoreCase: true),
        row.DecisionType,
        row.Description,
        JsonSerializer.Deserialize<IReadOnlyList<string>>(row.AffectedEntityIds, SqliteJson.Options) ?? [],
        row.ParentNodeId is null ? null : new ExplanationNodeId(Guid.Parse(row.ParentNodeId)),
        row.EdgeType,
        Enum.Parse<ExplanationNodeStatus>(row.Status, ignoreCase: true),
        DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));
}
