using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class ProjectMapper
{
    public static ProjectRow ToRow(ProjectRecord record) => new()
    {
        Id = record.Id.Value.ToString(),
        Name = record.Name,
        Description = record.Description,
        CreatedAt = record.CreatedAt.UtcDateTime.ToString("O"),
        UpdatedAt = record.UpdatedAt.UtcDateTime.ToString("O"),
        CurrentState = record.CurrentState.ToString(),
        FilePath = record.FilePath
    };

    public static ProjectRecord ToRecord(ProjectRow row) => new(
        new ProjectId(Guid.Parse(row.Id)),
        row.Name,
        row.Description,
        DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        DateTimeOffset.Parse(row.UpdatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        Enum.Parse<ApprovalState>(row.CurrentState, ignoreCase: true),
        row.FilePath);
}
