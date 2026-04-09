using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class PartMapper
{
    public static PartRow ToRow(GeneratedPart part, RevisionId revisionId, DateTimeOffset timestamp) => new()
    {
        Id = part.PartId,
        RevisionId = revisionId.Value.ToString(),
        CabinetId = part.CabinetId.Value.ToString(),
        PartType = part.PartType,
        Label = part.Label,
        MaterialId = part.MaterialId.Value.ToString(),
        Length = LengthText.FormatLength(part.Height),
        Width = LengthText.FormatLength(part.Width),
        Thickness = LengthText.FormatThickness(part.MaterialThickness),
        GrainDirection = part.GrainDirection.ToString(),
        EdgeTreatmentJson = JsonSerializer.Serialize(part.Edges, SqliteJson.Options),
        CreatedAt = timestamp.UtcDateTime.ToString("O"),
        UpdatedAt = timestamp.UtcDateTime.ToString("O")
    };

    public static GeneratedPart ToDomain(PartRow row) => new()
    {
        PartId = row.Id,
        CabinetId = new CabinetId(Guid.Parse(row.CabinetId)),
        PartType = row.PartType,
        Label = row.Label,
        MaterialId = new MaterialId(Guid.Parse(row.MaterialId)),
        Height = LengthText.ParseLength(row.Length),
        Width = LengthText.ParseLength(row.Width),
        MaterialThickness = LengthText.ParseThickness(row.Thickness),
        GrainDirection = string.IsNullOrWhiteSpace(row.GrainDirection)
            ? GrainDirection.None
            : Enum.Parse<GrainDirection>(row.GrainDirection, ignoreCase: true),
        Edges = JsonSerializer.Deserialize<EdgeTreatment>(row.EdgeTreatmentJson ?? "{}", SqliteJson.Options)
            ?? new EdgeTreatment(null, null, null, null)
    };
}
