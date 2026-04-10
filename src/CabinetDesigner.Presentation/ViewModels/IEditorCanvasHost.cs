using System;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public interface IEditorCanvasHost
{
    object View { get; }

    bool IsCtrlHeld { get; }

    void UpdateScene(RenderSceneDto scene);

    void UpdateViewport(ViewportTransform viewport);

    void SetMouseDownHandler(Action<double, double> handler);

    void SetMouseMoveHandler(Action<double, double> handler);

    void SetMouseUpHandler(Action<double, double> handler);

    void SetMouseWheelHandler(Action<double, double, double> handler);

    void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd);
}
