#if WINDOWS
using System.Windows;
using System.Windows.Media;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering.Layers;

public sealed class RunLayer : IRenderLayer
{
    private static readonly Brush RunFill = CreateRunFill();
    private static readonly Brush ActiveRunFill = CreateActiveRunFill();
    private static readonly Pen RunBorderPen = CreateRunBorderPen();
    private static readonly Pen RunAxisPen = CreateRunAxisPen();

    public void Draw(DrawingContext drawingContext, RenderSceneDto scene, ViewportTransform viewport)
    {
        for (var i = 0; i < scene.Runs.Count; i++)
        {
            var run = scene.Runs[i];
            var rect = ScreenProjection.ToScreen(run.BoundingRect, viewport);
            drawingContext.DrawRectangle(
                run.IsActive ? ActiveRunFill : RunFill,
                RunBorderPen,
                new Rect(rect.X, rect.Y, rect.Width, rect.Height));

            var axis = ScreenProjection.ToScreen(run.AxisSegment, viewport);
            drawingContext.DrawLine(
                RunAxisPen,
                new Point(axis.Start.X, axis.Start.Y),
                new Point(axis.End.X, axis.End.Y));
        }
    }

    private static Brush CreateRunFill()
    {
        var brush = new SolidColorBrush(Color.FromArgb(28, 70, 90, 110));
        brush.Freeze();
        return brush;
    }

    private static Brush CreateActiveRunFill()
    {
        var brush = new SolidColorBrush(Color.FromArgb(42, 70, 130, 180));
        brush.Freeze();
        return brush;
    }

    private static Pen CreateRunBorderPen()
    {
        var pen = new Pen(Brushes.SlateGray, 1d);
        pen.Freeze();
        return pen;
    }

    private static Pen CreateRunAxisPen()
    {
        var pen = new Pen(Brushes.SlateBlue, 1d)
        {
            DashStyle = DashStyles.Dash
        };
        pen.Freeze();
        return pen;
    }
}
#endif
