using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// An axis-aligned rectangle defined by its origin, width, and height.
/// </summary>
public readonly record struct Rect2D
{
    public Point2D Origin { get; }
    public Length Width { get; }
    public Length Height { get; }

    public Rect2D(Point2D origin, Length width, Length height)
    {
        Origin = origin;
        Width = width;
        Height = height;
    }

    public static Rect2D FromCorners(Point2D a, Point2D b)
    {
        var minX = Math.Min(a.X, b.X);
        var minY = Math.Min(a.Y, b.Y);
        var maxX = Math.Max(a.X, b.X);
        var maxY = Math.Max(a.Y, b.Y);

        return new(
            new Point2D(minX, minY),
            Length.FromInches(maxX - minX),
            Length.FromInches(maxY - minY));
    }

    public Point2D Min => Origin;
    public Point2D Max => new(Origin.X + Width.Inches, Origin.Y + Height.Inches);
    public Point2D Center => Origin.MidpointTo(Max);

    public decimal Area => Width.Inches * Height.Inches;

    public bool Contains(Point2D p) =>
        p.X >= Origin.X && p.X <= Origin.X + Width.Inches &&
        p.Y >= Origin.Y && p.Y <= Origin.Y + Height.Inches;

    public bool Intersects(Rect2D other) =>
        Origin.X < other.Origin.X + other.Width.Inches &&
        Origin.X + Width.Inches > other.Origin.X &&
        Origin.Y < other.Origin.Y + other.Height.Inches &&
        Origin.Y + Height.Inches > other.Origin.Y;

    public override string ToString() => $"Rect({Origin}, {Width}x{Height})";
}
