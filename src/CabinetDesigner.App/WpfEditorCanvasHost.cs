using System;
using System.Windows.Input;
using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.App;

public sealed class WpfEditorCanvasHost : IEditorCanvasHost
{
    private readonly EditorCanvas _canvas;

    public WpfEditorCanvasHost(EditorCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
    }

    public object View => _canvas;

    public bool IsCtrlHeld =>
        Keyboard.IsKeyDown(Key.LeftCtrl) ||
        Keyboard.IsKeyDown(Key.RightCtrl);

    public void UpdateScene(RenderSceneDto scene) => _canvas.UpdateScene(scene);

    public void UpdateViewport(ViewportTransform viewport) => _canvas.UpdateViewport(viewport);

    public void SetMouseDownHandler(Action<double, double> handler)
    {
        _canvas.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            var pos = e.GetPosition(_canvas);
            handler(pos.X, pos.Y);
        };
    }

    public void SetMouseMoveHandler(Action<double, double> handler)
    {
        _canvas.MouseMove += (_, e) =>
        {
            var pos = e.GetPosition(_canvas);
            handler(pos.X, pos.Y);
        };
    }

    public void SetMouseWheelHandler(Action<double, double, double> handler)
    {
        _canvas.MouseWheel += (_, e) =>
        {
            var pos = e.GetPosition(_canvas);
            handler(pos.X, pos.Y, e.Delta);
            e.Handled = true;
        };
    }

    public void SetMiddleButtonDragHandler(
        Action<double, double> onStart,
        Action<double, double> onMove,
        Action onEnd)
    {
        System.Windows.Point? dragOrigin = null;

        _canvas.MouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Middle)
            {
                dragOrigin = e.GetPosition(_canvas);
                _canvas.CaptureMouse();
                var pos = dragOrigin.Value;
                onStart(pos.X, pos.Y);
            }
        };

        _canvas.MouseMove += (_, e) =>
        {
            if (dragOrigin.HasValue && e.MiddleButton == MouseButtonState.Pressed)
            {
                var pos = e.GetPosition(_canvas);
                onMove(pos.X, pos.Y);
            }
        };

        _canvas.MouseUp += (_, e) =>
        {
            if (dragOrigin.HasValue && e.ChangedButton == MouseButton.Middle)
            {
                dragOrigin = null;
                _canvas.ReleaseMouseCapture();
                onEnd();
            }
        };
    }
}
