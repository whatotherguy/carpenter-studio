using System;
using System.Threading.Tasks;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public interface IEditorCanvasHost
{
    object View { get; }

    bool IsCtrlHeld { get; }

    double CanvasWidth { get; }

    double CanvasHeight { get; }

    void UpdateScene(RenderSceneDto scene);

    void UpdateViewport(ViewportTransform viewport);

    void SetMouseDownHandler(Action<double, double> handler);

    void SetMouseMoveHandler(Action<double, double> handler);

    void SetMouseUpHandler(Action<double, double> handler);

    void SetMouseWheelHandler(Action<double, double, double> handler);

    void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd);

    void SetDragOverHandler(Func<double, double, object?, System.Windows.DragDropEffects> handler) { }

    void SetDropHandler(Func<double, double, object?, Task<System.Windows.DragDropEffects>> handler) { }
}
