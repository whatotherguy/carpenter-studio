using System;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class EditorCanvasHost : IEditorCanvasHost
{
    private readonly EditorCanvas _canvas;

    public EditorCanvasHost(EditorCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    }

    public object View => _canvas;

    public bool IsCtrlHeld => false;

    public void UpdateScene(RenderSceneDto scene) => _canvas.UpdateScene(scene);

    public void UpdateViewport(ViewportTransform viewport) => _canvas.UpdateViewport(viewport);

    public void SetMouseDownHandler(Action<double, double> handler) { }

    public void SetMouseMoveHandler(Action<double, double> handler) { }

    public void SetMouseWheelHandler(Action<double, double, double> handler) { }

    public void SetMiddleButtonDragHandler(Action<double, double> onStart, Action<double, double> onMove, Action onEnd) { }
}
