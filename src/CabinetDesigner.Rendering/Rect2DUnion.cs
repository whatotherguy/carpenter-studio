using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Rendering;

public static class Rect2DUnion
{
    public static Rect2D? Combine(IEnumerable<Rect2D> rects)
    {
        ArgumentNullException.ThrowIfNull(rects);

        using var enumerator = rects.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return null;
        }

        var current = enumerator.Current;
        var minX = current.Min.X;
        var minY = current.Min.Y;
        var maxX = current.Max.X;
        var maxY = current.Max.Y;

        while (enumerator.MoveNext())
        {
            var rect = enumerator.Current;
            minX = Math.Min(minX, rect.Min.X);
            minY = Math.Min(minY, rect.Min.Y);
            maxX = Math.Max(maxX, rect.Max.X);
            maxY = Math.Max(maxY, rect.Max.Y);
        }

        return Rect2D.FromCorners(new Point2D(minX, minY), new Point2D(maxX, maxY));
    }
}
