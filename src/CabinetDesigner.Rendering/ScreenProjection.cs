using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;

namespace CabinetDesigner.Rendering;

public static class ScreenProjection
{
    public static ScreenPoint ToScreen(Point2D world, ViewportTransform viewport)
    {
        var (x, y) = viewport.ToScreen(world);
        return new ScreenPoint(x, y);
    }

    public static ScreenRect ToScreen(Rect2D rect, ViewportTransform viewport)
    {
        var min = ToScreen(rect.Min, viewport);
        var max = ToScreen(rect.Max, viewport);
        return new ScreenRect(min.X, min.Y, max.X - min.X, max.Y - min.Y);
    }

    public static ScreenLine ToScreen(LineSegment2D segment, ViewportTransform viewport) =>
        new(ToScreen(segment.Start, viewport), ToScreen(segment.End, viewport));

    public static double PixelsPerInch(ViewportTransform viewport) => (double)viewport.ScalePixelsPerInch;
}

public readonly record struct ScreenPoint(double X, double Y);

public readonly record struct ScreenRect(double X, double Y, double Width, double Height)
{
    public bool Contains(double x, double y) =>
        x >= X && x <= X + Width &&
        y >= Y && y <= Y + Height;
}

public readonly record struct ScreenLine(ScreenPoint Start, ScreenPoint End)
{
    public double DistanceTo(double x, double y)
    {
        var dx = End.X - Start.X;
        var dy = End.Y - Start.Y;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (lengthSquared <= 0d)
        {
            var pointDx = x - Start.X;
            var pointDy = y - Start.Y;
            return Math.Sqrt((pointDx * pointDx) + (pointDy * pointDy));
        }

        var t = ((x - Start.X) * dx + (y - Start.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);

        var closestX = Start.X + (dx * t);
        var closestY = Start.Y + (dy * t);
        var distanceX = x - closestX;
        var distanceY = y - closestY;
        return Math.Sqrt((distanceX * distanceX) + (distanceY * distanceY));
    }
}
