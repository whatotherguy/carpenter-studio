using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Application.Projection;

public static class SceneProjectionGeometry
{
    public static Rect2D CreateWorldBounds(Point2D origin, Vector2D direction, Length width, Length depth)
    {
        var along = direction * width.Inches;
        var perpendicular = direction.PerpendicularCCW() * depth.Inches;
        var points = new[]
        {
            origin,
            origin + along,
            origin + perpendicular,
            origin + along + perpendicular
        };

        return Rect2D.FromCorners(
            new Point2D(points.Min(point => point.X), points.Min(point => point.Y)),
            new Point2D(points.Max(point => point.X), points.Max(point => point.Y)));
    }
}
