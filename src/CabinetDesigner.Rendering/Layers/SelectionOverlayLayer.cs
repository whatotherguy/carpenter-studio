#if WINDOWS
using System.Windows;
using System.Windows.Media;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering.Layers;

public sealed class SelectionOverlayLayer : IRenderLayer
{
    private const double HandleSizePixels = 10d;
    private static readonly Pen SelectionPen = CreateSelectionPen();
    private static readonly Pen MultiSelectionPen = CreateMultiSelectionPen();
    private static readonly Brush HandleFill = CreateHandleFill();

    public void Draw(DrawingContext drawingContext, RenderSceneDto scene, ViewportTransform viewport)
    {
        if (scene.Selection is null)
        {
            return;
        }

        var selectedIds = scene.Selection.SelectedCabinetIds.ToHashSet();
        for (var i = 0; i < scene.Cabinets.Count; i++)
        {
            var cabinet = scene.Cabinets[i];
            if (!selectedIds.Contains(cabinet.CabinetId))
            {
                continue;
            }

            var rect = ScreenProjection.ToScreen(cabinet.WorldBounds, viewport);
            drawingContext.DrawRectangle(null, SelectionPen, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        if (scene.Selection.MultiSelectionBounds is Rect2D multiSelectionBounds)
        {
            var rect = ScreenProjection.ToScreen(multiSelectionBounds, viewport);
            drawingContext.DrawRectangle(null, MultiSelectionPen, new Rect(rect.X, rect.Y, rect.Width, rect.Height));
        }

        for (var i = 0; i < scene.Selection.Handles.Count; i++)
        {
            var handle = scene.Selection.Handles[i];
            var center = ScreenProjection.ToScreen(handle.WorldPosition, viewport);
            var rect = new Rect(
                center.X - (HandleSizePixels / 2d),
                center.Y - (HandleSizePixels / 2d),
                HandleSizePixels,
                HandleSizePixels);
            drawingContext.DrawRectangle(HandleFill, SelectionPen, rect);
        }
    }

    private static Pen CreateSelectionPen()
    {
        var pen = new Pen(Brushes.DodgerBlue, 2d);
        pen.Freeze();
        return pen;
    }

    private static Pen CreateMultiSelectionPen()
    {
        var pen = new Pen(Brushes.DodgerBlue, 1d)
        {
            DashStyle = DashStyles.Dash
        };
        pen.Freeze();
        return pen;
    }

    private static Brush CreateHandleFill()
    {
        var brush = Brushes.White;
        brush.Freeze();
        return brush;
    }
}
#endif
