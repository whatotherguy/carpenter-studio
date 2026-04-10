#if WINDOWS
using System.Windows;
using System.Windows.Media;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering.Layers;

public sealed class BackgroundLayer : IRenderLayer
{
    private static readonly Pen MinorGridPen = CreateGridPen(Color.FromArgb(35, 120, 120, 120), 1d);
    private static readonly Pen MajorGridPen = CreateGridPen(Color.FromArgb(60, 100, 100, 100), 1d);

    public void Draw(DrawingContext drawingContext, RenderSceneDto scene, ViewportTransform viewport)
    {
        if (!scene.Grid.Visible || scene.Grid.MinorSpacing <= Length.Zero || scene.Grid.MajorSpacing <= Length.Zero)
        {
            return;
        }

        var bounds = RenderSceneBoundsCalculator.Calculate(scene);
        if (bounds is null)
        {
            return;
        }

        DrawGrid(drawingContext, bounds.Value, scene.Grid.MinorSpacing, viewport, MinorGridPen);
        DrawGrid(drawingContext, bounds.Value, scene.Grid.MajorSpacing, viewport, MajorGridPen);
    }

    private static void DrawGrid(
        DrawingContext drawingContext,
        Rect2D bounds,
        Length spacing,
        ViewportTransform viewport,
        Pen pen)
    {
        var spacingInches = spacing.Inches;
        if (spacingInches <= 0m)
        {
            return;
        }

        if (GridLinePlanner.ExceedsLimit(spacingInches, bounds.Min.X, bounds.Max.X, bounds.Min.Y, bounds.Max.Y))
        {
            return;
        }

        var startX = Math.Floor(bounds.Min.X / spacingInches) * spacingInches;
        var endX = Math.Ceiling(bounds.Max.X / spacingInches) * spacingInches;
        for (var x = startX; x <= endX; x += spacingInches)
        {
            var top = ScreenProjection.ToScreen(new Point2D(x, bounds.Min.Y), viewport);
            var bottom = ScreenProjection.ToScreen(new Point2D(x, bounds.Max.Y), viewport);
            drawingContext.DrawLine(pen, new Point(top.X, top.Y), new Point(bottom.X, bottom.Y));
        }

        var startY = Math.Floor(bounds.Min.Y / spacingInches) * spacingInches;
        var endY = Math.Ceiling(bounds.Max.Y / spacingInches) * spacingInches;
        for (var y = startY; y <= endY; y += spacingInches)
        {
            var left = ScreenProjection.ToScreen(new Point2D(bounds.Min.X, y), viewport);
            var right = ScreenProjection.ToScreen(new Point2D(bounds.Max.X, y), viewport);
            drawingContext.DrawLine(pen, new Point(left.X, left.Y), new Point(right.X, right.Y));
        }
    }

    private static Pen CreateGridPen(Color color, double thickness)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new Pen(brush, thickness);
        pen.Freeze();
        return pen;
    }
}
#endif
