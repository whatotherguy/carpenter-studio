namespace CabinetDesigner.Application.Diagnostics;

public enum AlphaArea
{
    Editor,
    Catalog,
    Properties,
    Export,
    Persistence,
    RunPlanning,
    General
}

public sealed record AlphaLimitation(string Code, string Title, string UserFacingMessage, AlphaArea Area);

public static class AlphaLimitations
{
    private static readonly IReadOnlyList<AlphaLimitation> _all =
    [
        new(
            "ALPHA-EDITOR-FREEFORM-PLACEMENT",
            "Freeform cabinet placement",
            "Cabinets are still placed through runs and walls in this alpha build. Freeform placement on the canvas is not available yet, so keep using the catalog and run tools.",
            AlphaArea.Editor),
        new(
            "ALPHA-CATALOG-HARDWARE-PLACEMENT",
            "Hardware placement workflow",
            "Hardware items are shown in the catalog for reference, but hardware placement workflows are not wired up in alpha yet. The app can still use hardware data for costing and validation.",
            AlphaArea.Catalog),
        new(
            "ALPHA-PROPERTIES-NOOP-FALLBACK",
            "Property editor persistence fallback",
            "Some property editor paths still fall back to a non-persisting service in this alpha build. The panel stays usable, but those edits are not saved.",
            AlphaArea.Properties),
        new(
            "ALPHA-EXPORT-CUTLIST-ONLY",
            "Cut list export only",
            "Only the cut list export flow is available in alpha right now. Other export formats have not been wired up yet.",
            AlphaArea.Export),
        new(
            "ALPHA-PERSISTENCE-SINGLE-PROJECT-FILE",
            "Single-project cabinet file",
            "A cabinet file currently stores one project at a time. Multi-project file handling and merge behavior are not supported yet.",
            AlphaArea.Persistence),
        new(
            "ALPHA-RUN-INSERT-NOT-IMPLEMENTED",
            "Run insertion workflow",
            "Basic run planning is available, but insert-between and other advanced run planning paths are still under construction. Use the end-of-run placement flow for now.",
            AlphaArea.RunPlanning),
        new(
            "ALPHA-GENERAL-PARTIAL-ALPHA-IMPLEMENTATION",
            "Partial alpha implementation",
            "This build is intentionally alpha and some end-to-end flows are still incomplete. When that happens, the app should stay responsive and explain what is still in progress.",
            AlphaArea.General)
    ];

    public static IReadOnlyList<AlphaLimitation> All { get; } = _all
        .OrderBy(limitation => limitation.Area)
        .ThenBy(limitation => limitation.Code, StringComparer.Ordinal)
        .ToArray();

    public static IReadOnlyDictionary<string, AlphaLimitation> AllByCode { get; } =
        All.ToDictionary(limitation => limitation.Code, StringComparer.Ordinal);
}
