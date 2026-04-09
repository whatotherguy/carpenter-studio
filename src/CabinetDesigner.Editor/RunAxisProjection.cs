using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Editor;

internal static class RunAxisProjection
{
    public static (decimal DistanceAlongAxis, Point2D ProjectedPoint) ProjectOntoAxis(
        Point2D point,
        Point2D axisOrigin,
        Vector2D axis)
    {
        var vector = point - axisOrigin;
        var distanceAlongAxis = vector.Dot(axis);
        return (distanceAlongAxis, axisOrigin + axis * distanceAlongAxis);
    }

    public static Point2D PointAtDistance(Point2D axisOrigin, Vector2D axis, decimal distanceAlongAxis) =>
        axisOrigin + axis * distanceAlongAxis;
}
