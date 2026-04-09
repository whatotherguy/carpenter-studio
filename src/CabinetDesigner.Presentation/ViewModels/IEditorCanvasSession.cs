using CabinetDesigner.Editor;

namespace CabinetDesigner.Presentation.ViewModels;

public interface IEditorCanvasSession
{
    EditorMode CurrentMode { get; }

    IReadOnlyList<Guid> SelectedCabinetIds { get; }

    Guid? HoveredCabinetId { get; }

    ViewportTransform Viewport { get; }

    void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds);
}
