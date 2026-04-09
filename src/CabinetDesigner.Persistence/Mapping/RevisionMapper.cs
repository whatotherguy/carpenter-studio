using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class RevisionMapper
{
    public static RevisionRow ToRow(RevisionRecord record) => new()
    {
        Id = record.Id.Value.ToString(),
        ProjectId = record.ProjectId.Value.ToString(),
        RevisionNumber = record.RevisionNumber,
        State = record.State.ToString(),
        CreatedAt = record.CreatedAt.UtcDateTime.ToString("O"),
        ApprovedAt = record.ApprovedAt?.UtcDateTime.ToString("O"),
        ApprovedBy = record.ApprovedBy,
        Label = record.Label,
        ApprovalNotes = record.ApprovalNotes
    };

    public static RevisionRecord ToRecord(RevisionRow row) => new(
        new RevisionId(Guid.Parse(row.Id)),
        new ProjectId(Guid.Parse(row.ProjectId)),
        row.RevisionNumber,
        Enum.Parse<ApprovalState>(row.State, ignoreCase: true),
        DateTimeOffset.Parse(row.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        row.ApprovedAt is null ? null : DateTimeOffset.Parse(row.ApprovedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        row.ApprovedBy,
        row.Label,
        row.ApprovalNotes);
}
