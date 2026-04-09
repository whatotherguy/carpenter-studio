using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Rendering;

public static class RenderSceneBoundsCalculator
{
    public static Rect2D? Calculate(RenderSceneDto scene)
    {
        ArgumentNullException.ThrowIfNull(scene);

        var rects = new List<Rect2D>(scene.Runs.Count + scene.Cabinets.Count + scene.Walls.Count);
        rects.AddRange(scene.Runs.Select(run => run.BoundingRect));
        rects.AddRange(scene.Cabinets.Select(cabinet => cabinet.WorldBounds));
        rects.AddRange(scene.Walls.Select(wall => Rect2D.FromCorners(wall.Segment.Start, wall.Segment.End)));
        return Rect2DUnion.Combine(rects);
    }
}
