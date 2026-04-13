using System;
using System.Windows.Input;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

/// <summary>
/// WPF-specific implementation of <see cref="IEditorCanvasHost"/>.  Bridges WPF routed
/// mouse events on the <see cref="EditorCanvas"/> control into the handler callbacks that
/// <see cref="EditorCanvasViewModel"/> registers via the <c>Set*Handler</c> methods.
/// </summary>
public class WpfEditorCanvasHost : IEditorCanvasHost, IDisposable
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
    private bool _isLeftDragActive;
    private bool _disposed;

    public WpfEditorCanvasHost(EditorCanvas canvas)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

        _canvas.MouseDown += OnCanvasMouseDown;
        _canvas.MouseMove += OnCanvasMouseMove;
        _canvas.MouseUp += OnCanvasMouseUp;
        _canvas.MouseWheel += OnCanvasMouseWheel;
    }

    public object View => _canvas;

    /// <inheritdoc />
    /// <remarks>
    /// Reads the physical keyboard state via <see cref="Keyboard.IsKeyDown"/> so that
    /// Ctrl+Click multi-select works regardless of which element currently has logical
    /// keyboard focus.
    /// </remarks>
    public bool IsCtrlHeld =>
        Keyboard.IsKeyDown(Key.LeftCtrl) ||
        Keyboard.IsKeyDown(Key.RightCtrl);

    public double CanvasWidth => _canvas.ActualWidth;

    public double CanvasHeight => _canvas.ActualHeight;

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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _isLeftDragActive = false;
        _canvas.MouseDown -= OnCanvasMouseDown;
        _canvas.MouseMove -= OnCanvasMouseMove;
        _canvas.MouseUp -= OnCanvasMouseUp;
        _canvas.MouseWheel -= OnCanvasMouseWheel;
    }

    private void OnCanvasMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        if (e.ChangedButton == MouseButton.Left)
        {
            var pos = e.GetPosition(_canvas);
            _canvas.Focus();
            _canvas.CaptureMouse();
            _isLeftDragActive = true;
            _mouseDownHandler?.Invoke(pos.X, pos.Y);
        }
        else if (e.ChangedButton == MouseButton.Middle && !_isLeftDragActive)
        {
            _middleDragOrigin = e.GetPosition(_canvas);
            _canvas.CaptureMouse();
            var pos = _middleDragOrigin.Value;
            _panStartHandler?.Invoke(pos.X, pos.Y);
        }
    }

    private void OnCanvasMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        var pos = e.GetPosition(_canvas);
        _mouseMoveHandler?.Invoke(pos.X, pos.Y);

        if (_middleDragOrigin.HasValue && e.MiddleButton == MouseButtonState.Pressed)
        {
            _panMoveHandler?.Invoke(pos.X, pos.Y);
        }
    }

    private void OnCanvasMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        if (e.ChangedButton == MouseButton.Left)
        {
            _isLeftDragActive = false;
            var pos = e.GetPosition(_canvas);
            if (!_middleDragOrigin.HasValue)
            {
                _canvas.ReleaseMouseCapture();
            }

            _mouseUpHandler?.Invoke(pos.X, pos.Y);
        }
        else if (e.ChangedButton == MouseButton.Middle && _middleDragOrigin.HasValue)
        {
            _middleDragOrigin = null;
            _canvas.ReleaseMouseCapture();
            _panEndHandler?.Invoke();
        }
    }

    private void OnCanvasMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (_disposed)
        {
            return;
        }
        if (_mouseWheelHandler is null)
        {
            return;
        }

        var pos = e.GetPosition(_canvas);
        _mouseWheelHandler(pos.X, pos.Y, e.Delta);
        e.Handled = true;
    }
}
