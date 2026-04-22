using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;

namespace CabinetDesigner.Presentation.ViewModels;

public interface IEditorCanvasSession
{
    EditorMode CurrentMode { get; }

    IReadOnlyList<Guid> SelectedCabinetIds { get; }

    Guid? HoveredCabinetId { get; }

    Guid? ActiveRoomId { get; }

    ViewportTransform Viewport { get; }

    SnapSettings SnapSettings { get; }

    void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds);

    void SetHoveredCabinetId(Guid? cabinetId);

    void SetActiveRoom(Guid? roomId);

    void ZoomAt(double screenX, double screenY, double scaleFactor);

    void PanBy(double dx, double dy);

    void BeginPan();

    void EndPan();

    void ResetViewport();

    void FitViewport(ViewportBounds contentBounds, double canvasWidth, double canvasHeight);
}
