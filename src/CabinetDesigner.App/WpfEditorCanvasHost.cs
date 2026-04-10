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
    private Action<double, double>? _mouseDownHandler;
    private Action<double, double>? _mouseMoveHandler;
    private Action<double, double>? _mouseUpHandler;
    private Action<double, double, double>? _mouseWheelHandler;
    private Action<double, double>? _panStartHandler;
    private Action<double, double>? _panMoveHandler;
    private Action? _panEndHandler;
    private System.Windows.Point? _middleDragOrigin;

    public WpfEditorCanvasHost(EditorCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

        _canvas.MouseDown += OnCanvasMouseDown;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseUp += OnCanvasMouseUp;
        _canvas.MouseWheel += OnCanvasMouseWheel;
    }

    public object View => _canvas;

    public bool IsCtrlHeld =>
        Keyboard.IsKeyDown(Key.LeftCtrl) ||
        Keyboard.IsKeyDown(Key.RightCtrl);

    public void UpdateScene(RenderSceneDto scene) => _canvas.UpdateScene(scene);

    public void UpdateViewport(ViewportTransform viewport) => _canvas.UpdateViewport(viewport);

    public void SetMouseDownHandler(Action<double, double> handler) => _mouseDownHandler = handler;

    public void SetMouseMoveHandler(Action<double, double> handler) => _mouseMoveHandler = handler;

    public void SetMouseUpHandler(Action<double, double> handler) => _mouseUpHandler = handler;

    public void SetMouseWheelHandler(Action<double, double, double> handler) => _mouseWheelHandler = handler;

    public void SetMiddleButtonDragHandler(
        Action<double, double> onStart,
        Action<double, double> onMove,
        Action onEnd)
    {
        _panStartHandler = onStart;
        _panMoveHandler = onMove;
        _panEndHandler = onEnd;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            var pos = e.GetPosition(_canvas);
            _mouseDownHandler?.Invoke(pos.X, pos.Y);
        }
        else if (e.ChangedButton == MouseButton.Middle)
        {
            _middleDragOrigin = e.GetPosition(_canvas);
            _canvas.CaptureMouse();
            var pos = _middleDragOrigin.Value;
            _panStartHandler?.Invoke(pos.X, pos.Y);
        }
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(_canvas);
        _mouseMoveHandler?.Invoke(pos.X, pos.Y);

        if (_middleDragOrigin.HasValue && e.MiddleButton == MouseButtonState.Pressed)
        {
            _panMoveHandler?.Invoke(pos.X, pos.Y);
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            var pos = e.GetPosition(_canvas);
            _mouseUpHandler?.Invoke(pos.X, pos.Y);
        }
        else if (e.ChangedButton == MouseButton.Middle && _middleDragOrigin.HasValue)
        {
            _middleDragOrigin = null;
            _canvas.ReleaseMouseCapture();
            _panEndHandler?.Invoke();
        }
    }

    private void OnCanvasMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var pos = e.GetPosition(_canvas);
        _mouseWheelHandler?.Invoke(pos.X, pos.Y, e.Delta);
        e.Handled = true;
    }
}
