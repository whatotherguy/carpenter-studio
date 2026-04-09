using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// A directed line segment in 2D space.
/// </summary>
public readonly record struct LineSegment2D
{
    public Point2D Start { get; }
    public Point2D End   { get; }

    public LineSegment2D(Point2D start, Point2D end) { Start = start; End = end; }

    // ── Properties ───────────────────────────────────────────────────

    // Note: method name Length() intentionally mirrors the naming of the return type.
    // The C# compiler disambiguates return-type annotation from method identifier.
    public Length Length() => Start.DistanceTo(End);

    public Point2D Midpoint()  => Start.MidpointTo(End);
    public Vector2D Direction() => (End - Start).Normalized();

    // ── Spatial queries ───────────────────────────────────────────────

    /// <summary>
    /// The closest point on this segment to <paramref name="p"/>.
    /// Returns <see cref="Start"/> if the segment is degenerate (zero length).
    /// </summary>
    public Point2D ClosestPointTo(Point2D p)
    {
        var ab = End - Start;
        var ap = p - Start;
        var lengthSq = ab.Dot(ab);

        if (lengthSq == 0m)
            return Start;   // degenerate segment

        var t = Math.Clamp(ap.Dot(ab) / lengthSq, 0m, 1m);
        return Start + ab * t;
    }

    /// <summary>Perpendicular distance from <paramref name="p"/> to this segment.</summary>
    public Length DistanceTo(Point2D p) => ClosestPointTo(p).DistanceTo(p);

    // ── Display ───────────────────────────────────────────────────────

    public override string ToString() => $"Seg({Start} → {End})";
}
