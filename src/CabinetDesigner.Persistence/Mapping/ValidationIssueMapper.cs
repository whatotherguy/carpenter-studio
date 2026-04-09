using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class ValidationIssueMapper
{
    public static ValidationIssueRow ToRow(ValidationIssueRecord record) => new()
    {
        Id = record.Id.Value,
        RevisionId = record.RevisionId.Value.ToString(),
        RunAt = record.RunAt.UtcDateTime.ToString("O"),
        Severity = record.Severity.ToString(),
        RuleCode = record.RuleCode,
        Message = record.Message,
        AffectedEntityIds = JsonSerializer.Serialize(record.AffectedEntityIds, SqliteJson.Options),
        SuggestedFixJson = record.SuggestedFixJson
    };

    public static ValidationIssueRecord ToRecord(ValidationIssueRow row) => new(
        new ValidationIssueId(row.RuleCode, JsonSerializer.Deserialize<IReadOnlyList<string>>(row.AffectedEntityIds, SqliteJson.Options) ?? []),
        new RevisionId(Guid.Parse(row.RevisionId)),
        DateTimeOffset.Parse(row.RunAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        Enum.Parse<ValidationSeverity>(row.Severity, ignoreCase: true),
        row.RuleCode,
        row.Message,
        JsonSerializer.Deserialize<IReadOnlyList<string>>(row.AffectedEntityIds, SqliteJson.Options) ?? [],
        row.SuggestedFixJson);
}
