using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// A position in 2D space. Coordinates are raw decimal inches for performance and simplicity.
/// They are intentionally not wrapped in <see cref="Length"/> because spatial coordinates are signed.
/// </summary>
public readonly record struct Point2D
{
    public decimal X { get; }
    public decimal Y { get; }

    public Point2D(decimal x, decimal y)
    {
        X = x;
        Y = y;
    }

    public static readonly Point2D Origin = new(0m, 0m);

    public static Vector2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);
    public static Point2D operator +(Point2D p, Vector2D v) => new(p.X + v.Dx, p.Y + v.Dy);
    public static Point2D operator -(Point2D p, Vector2D v) => new(p.X - v.Dx, p.Y - v.Dy);

    /// <summary>
    /// Euclidean distance to another point. Uses double for sqrt as the sanctioned escape hatch;
    /// the result is immediately re-wrapped as <see cref="Length"/>.
    /// </summary>
    public Length DistanceTo(Point2D other)
    {
        var dx = (double)(X - other.X);
        var dy = (double)(Y - other.Y);
        return Length.FromInches((decimal)Math.Sqrt(dx * dx + dy * dy));
    }

    public Point2D MidpointTo(Point2D other) => new((X + other.X) / 2m, (Y + other.Y) / 2m);

    public override string ToString() => $"({X}, {Y})";
}
