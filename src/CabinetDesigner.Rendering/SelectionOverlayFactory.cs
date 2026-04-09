using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public static class SelectionOverlayFactory
{
    public static SelectionOverlayDto? Create(
        IReadOnlyList<CabinetRenderDto> cabinets,
        IReadOnlyList<Guid> selectedCabinetIds)
    {
        ArgumentNullException.ThrowIfNull(cabinets);
        ArgumentNullException.ThrowIfNull(selectedCabinetIds);

        if (selectedCabinetIds.Count == 0)
        {
            return null;
        }

        var selectedSet = selectedCabinetIds.ToHashSet();
        var selectedCabinets = cabinets
            .Where(cabinet => selectedSet.Contains(cabinet.CabinetId))
            .ToArray();

        if (selectedCabinets.Length == 0)
        {
            return null;
        }

        var handles = selectedCabinets
            .SelectMany(cabinet => cabinet.Handles)
            .ToArray();

        var multiSelectionBounds = selectedCabinets.Length > 1
            ? Rect2DUnion.Combine(selectedCabinets.Select(cabinet => cabinet.WorldBounds))
            : null;

        return new SelectionOverlayDto(
            selectedCabinetIds.Where(selectedSet.Contains).ToArray(),
            multiSelectionBounds,
            handles);
    }
}
