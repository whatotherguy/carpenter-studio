using CabinetDesigner.Persistence.Models;

namespace CabinetDesigner.Persistence.Mapping;

internal static class CabinetMapper
{
    public static CabinetRow ToRow(
        Cabinet cabinet,
        RevisionId revisionId,
        RunId runId,
        int slotIndex,
        DateTimeOffset timestamp) => new()
    {
        Id = cabinet.Id.Value.ToString(),
        RevisionId = revisionId.Value.ToString(),
        RunId = runId.Value.ToString(),
        SlotIndex = slotIndex,
        CabinetTypeId = cabinet.CabinetTypeId,
        Category = cabinet.Category.ToString(),
        ConstructionMethod = cabinet.Construction.ToString(),
        NominalWidth = LengthText.FormatLength(cabinet.NominalWidth),
        NominalHeight = LengthText.FormatLength(cabinet.Height),
        NominalDepth = LengthText.FormatLength(cabinet.Depth),
        OverridesJson = JsonSerializer.Serialize(
            cabinet.Overrides.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            SqliteJson.Options),
        CreatedAt = timestamp.UtcDateTime.ToString("O"),
        UpdatedAt = timestamp.UtcDateTime.ToString("O")
    };

    public static Cabinet ToDomain(CabinetRow row)
    {
        var cabinet = new Cabinet(
            new CabinetId(Guid.Parse(row.Id)),
            new RevisionId(Guid.Parse(row.RevisionId)),
            row.CabinetTypeId,
            Enum.Parse<CabinetCategory>(row.Category, ignoreCase: true),
            Enum.Parse<ConstructionMethod>(row.ConstructionMethod, ignoreCase: true),
            LengthText.ParseLength(row.NominalWidth),
            LengthText.ParseLength(row.NominalDepth),
            LengthText.ParseLength(row.NominalHeight));

        var overrides = JsonSerializer.Deserialize<Dictionary<string, OverrideValue>>(row.OverridesJson ?? "{}", SqliteJson.Options)
            ?? [];
        foreach (var pair in overrides.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            cabinet.SetOverride(pair.Key, pair.Value);
        }

        return cabinet;
    }
}
