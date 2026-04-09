using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class CommandJournalMapper
{
    public static CommandJournalRow ToRow(CommandJournalEntry entry) => new()
    {
        Id = entry.Id.Value.ToString(),
        RevisionId = entry.RevisionId.Value.ToString(),
        SequenceNumber = entry.SequenceNumber,
        CommandType = entry.CommandType,
        Origin = entry.Origin.ToString(),
        IntentDescription = entry.IntentDescription,
        AffectedEntityIds = JsonSerializer.Serialize(entry.AffectedEntityIds, SqliteJson.Options),
        ParentCommandId = entry.ParentCommandId?.Value.ToString(),
        Timestamp = entry.Timestamp.UtcDateTime.ToString("O"),
        CommandJson = entry.CommandJson,
        DeltasJson = JsonSerializer.Serialize(entry.Deltas, SqliteJson.Options),
        Succeeded = entry.Succeeded ? 1 : 0
    };

    public static CommandJournalEntry ToRecord(CommandJournalRow row) => new(
        new CommandId(Guid.Parse(row.Id)),
        new RevisionId(Guid.Parse(row.RevisionId)),
        row.SequenceNumber,
        row.CommandType,
        Enum.Parse<CommandOrigin>(row.Origin, ignoreCase: true),
        row.IntentDescription,
        JsonSerializer.Deserialize<IReadOnlyList<string>>(row.AffectedEntityIds, SqliteJson.Options) ?? [],
        row.ParentCommandId is null ? null : new CommandId(Guid.Parse(row.ParentCommandId)),
        DateTimeOffset.Parse(row.Timestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        row.CommandJson,
        JsonSerializer.Deserialize<IReadOnlyList<StateDelta>>(row.DeltasJson, SqliteJson.Options) ?? [],
        row.Succeeded == 1);
}
