#if WINDOWS
using System.Windows.Media;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering.Layers;

public sealed class WallLayer : IRenderLayer
{
    private static readonly Pen WallPen = CreateWallPen();
    private static readonly Pen HighlightedWallPen = CreateHighlightedWallPen();

    public void Draw(DrawingContext drawingContext, RenderSceneDto scene, ViewportTransform viewport)
    {
        for (var i = 0; i < scene.Walls.Count; i++)
        {
            var wall = scene.Walls[i];
            var line = ScreenProjection.ToScreen(wall.Segment, viewport);
            drawingContext.DrawLine(
                wall.IsHighlighted ? HighlightedWallPen : WallPen,
                new System.Windows.Point(line.Start.X, line.Start.Y),
                new System.Windows.Point(line.End.X, line.End.Y));
        }
    }

    private static Pen CreateWallPen()
    {
        var pen = new Pen(Brushes.DimGray, 3d);
        pen.Freeze();
        return pen;
    }

    private static Pen CreateHighlightedWallPen()
    {
        var pen = new Pen(Brushes.SteelBlue, 4d);
        pen.Freeze();
        return pen;
    }
}
#endif
