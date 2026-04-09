using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public static class RenderSceneComposer
{
    public static RenderSceneDto ApplyInteractionState(
        RenderSceneDto scene,
        IReadOnlyList<Guid> selectedCabinetIds,
        Guid? hoveredCabinetId)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(selectedCabinetIds);

        var selectedSet = selectedCabinetIds.ToHashSet();
        var cabinets = new CabinetRenderDto[scene.Cabinets.Count];

        for (var i = 0; i < scene.Cabinets.Count; i++)
        {
            var cabinet = scene.Cabinets[i];
            var state = cabinet.State;
            if (selectedSet.Contains(cabinet.CabinetId))
            {
                state = CabinetRenderState.Selected;
            }
            else if (hoveredCabinetId.HasValue && cabinet.CabinetId == hoveredCabinetId.Value)
            {
                state = CabinetRenderState.Hovered;
            }
            else if (state is CabinetRenderState.Selected or CabinetRenderState.Hovered)
            {
                state = CabinetRenderState.Normal;
            }

            cabinets[i] = cabinet with { State = state };
        }

        return scene with
        {
            Cabinets = cabinets,
            Selection = SelectionOverlayFactory.Create(cabinets, selectedCabinetIds)
        };
    }
}
