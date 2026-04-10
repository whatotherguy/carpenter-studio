using CabinetDesigner.Editor;

namespace CabinetDesigner.Presentation.ViewModels;

public interface IEditorCanvasSession
{
    EditorMode CurrentMode { get; }

    IReadOnlyList<Guid> SelectedCabinetIds { get; }

    Guid? HoveredCabinetId { get; }

    ViewportTransform Viewport { get; }

    void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds);

    void SetHoveredCabinetId(Guid? cabinetId);

    void ZoomAt(double screenX, double screenY, double scaleFactor);

    void PanBy(double dx, double dy);

    void BeginPan();

    void EndPan();

    void ResetViewport();
}
