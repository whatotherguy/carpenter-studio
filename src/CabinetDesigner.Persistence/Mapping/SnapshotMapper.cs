using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class SnapshotMapper
{
    public static ApprovedSnapshotRow ToRow(ApprovedSnapshot snapshot) => new()
    {
        RevisionId = snapshot.RevisionId.Value.ToString(),
        SnapshotSchemaVer = ReadSchemaVersion(snapshot.DesignBlob),
        ApprovedAt = snapshot.ApprovedAt.UtcDateTime.ToString("O"),
        ApprovedBy = snapshot.ApprovedBy ?? string.Empty,
        DesignBlob = snapshot.DesignBlob,
        PartsBlob = snapshot.PartsBlob,
        ManufacturingBlob = snapshot.ManufacturingBlob,
        InstallBlob = snapshot.InstallBlob,
        EstimateBlob = snapshot.EstimateBlob,
        ValidationBlob = snapshot.ValidationBlob,
        ExplanationBlob = snapshot.ExplanationBlob
    };

    public static ApprovedSnapshot ToRecord(
        ApprovedSnapshotRow row,
        ProjectId projectId,
        int revisionNumber,
        string label) => new(
        new RevisionId(Guid.Parse(row.RevisionId)),
        projectId,
        revisionNumber,
        DateTimeOffset.Parse(row.ApprovedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
        string.IsNullOrWhiteSpace(row.ApprovedBy) ? null : row.ApprovedBy,
        label,
        row.DesignBlob,
        row.PartsBlob,
        row.ManufacturingBlob,
        row.InstallBlob,
        row.EstimateBlob,
        row.ValidationBlob,
        row.ExplanationBlob);

    private static int ReadSchemaVersion(string blob)
    {
        using var document = JsonDocument.Parse(blob);
        return document.RootElement.GetProperty("schema_version").GetInt32();
    }
}
