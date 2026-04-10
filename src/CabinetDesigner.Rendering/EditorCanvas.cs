#if WINDOWS
using System.Windows;
using System.Windows.Media;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;
using CabinetDesigner.Rendering.Layers;

namespace CabinetDesigner.Rendering;

public sealed class EditorCanvas : FrameworkElement
{
    private readonly IReadOnlyList<IRenderLayer> _layers;
    private RenderSceneDto? _scene;
    private ViewportTransform _viewport = ViewportTransform.Default;

    public EditorCanvas()
        : this([new BackgroundLayer(), new WallLayer(), new RunLayer(), new CabinetLayer(), new SelectionOverlayLayer()])
    {
    }

    public EditorCanvas(IReadOnlyList<IRenderLayer> layers)
    {
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
        Focusable = true;
    }

    public void UpdateScene(RenderSceneDto scene)
    {
        _scene = scene;
        InvalidateVisual();
    }

    public void UpdateViewport(ViewportTransform viewport)
    {
        _viewport = viewport;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_scene is null)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var viewportWithDpi = _viewport with { PixelsPerDip = dpi.PixelsPerDip };
        foreach (var layer in _layers)
        {
            layer.Draw(drawingContext, _scene, viewportWithDpi);
        }
    }
}
#else
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public sealed class EditorCanvas
{
    public RenderSceneDto? Scene { get; private set; }

    public ViewportTransform Viewport { get; private set; } = ViewportTransform.Default;

    public void UpdateScene(RenderSceneDto scene) => Scene = scene;

    public void UpdateViewport(ViewportTransform viewport) => Viewport = viewport;
}
#endif
