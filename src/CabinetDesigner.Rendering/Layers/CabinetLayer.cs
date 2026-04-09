#if WINDOWS
using System.Windows;
using System.Windows.Media;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering.Layers;

public sealed class CabinetLayer : IRenderLayer
{
    private static readonly Brush NormalFill = CreateBrush(233, 214, 179);
    private static readonly Brush HoverFill = CreateBrush(210, 227, 242);
    private static readonly Brush SelectedFill = CreateBrush(188, 221, 255);
    private static readonly Brush InvalidFill = CreateBrush(244, 204, 204);
    private static readonly Brush GhostFill = CreateGhostBrush();
    private static readonly Pen NormalPen = CreatePen(Brushes.SaddleBrown, 1d);
    private static readonly Pen HighlightPen = CreatePen(Brushes.DodgerBlue, 2d);
    private static readonly Pen InvalidPen = CreatePen(Brushes.Firebrick, 2d);

    public void Draw(DrawingContext drawingContext, RenderSceneDto scene, ViewportTransform viewport)
    {
        for (var i = 0; i < scene.Cabinets.Count; i++)
        {
            var cabinet = scene.Cabinets[i];
            var rect = ScreenProjection.ToScreen(cabinet.WorldBounds, viewport);
            var brush = GetFill(cabinet.State);
            var pen = GetPen(cabinet.State);
            drawingContext.DrawRectangle(brush, pen, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }
    }

    private static Brush GetFill(CabinetRenderState state) => state switch
    {
        CabinetRenderState.Hovered => HoverFill,
        CabinetRenderState.Selected => SelectedFill,
        CabinetRenderState.Invalid => InvalidFill,
        CabinetRenderState.Ghost => GhostFill,
        _ => NormalFill
    };

    private static Pen GetPen(CabinetRenderState state) => state switch
    {
        CabinetRenderState.Hovered => HighlightPen,
        CabinetRenderState.Selected => HighlightPen,
        CabinetRenderState.Invalid => InvalidPen,
        _ => NormalPen
    };

    private static Brush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateGhostBrush()
    {
        var brush = new SolidColorBrush(Color.FromArgb(128, 210, 210, 210));
        brush.Freeze();
        return brush;
    }

    private static Pen CreatePen(Brush brush, double thickness)
    {
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
#endif
