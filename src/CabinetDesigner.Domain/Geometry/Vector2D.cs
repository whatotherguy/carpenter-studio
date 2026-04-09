using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// A direction and magnitude in 2D space. Components are stored as decimal inches.
/// </summary>
public readonly record struct Vector2D
{
    public decimal Dx { get; }
    public decimal Dy { get; }

    public Vector2D(decimal dx, decimal dy)
    {
        Dx = dx;
        Dy = dy;
    }

    public static readonly Vector2D Zero = new(0m, 0m);
    public static readonly Vector2D UnitX = new(1m, 0m);
    public static readonly Vector2D UnitY = new(0m, 1m);

    /// <summary>
    /// Euclidean magnitude. Uses double for sqrt as the sanctioned escape hatch;
    /// the result is immediately re-wrapped as <see cref="Length"/>.
    /// </summary>
    public Length Magnitude()
    {
        var dx = (double)Dx;
        var dy = (double)Dy;
        return Length.FromInches((decimal)Math.Sqrt(dx * dx + dy * dy));
    }

    public Vector2D Normalized()
    {
        var magnitude = (double)Magnitude().Inches;
        if (magnitude == 0.0)
            return Zero;

        return new((decimal)((double)Dx / magnitude), (decimal)((double)Dy / magnitude));
    }

    public static Vector2D operator +(Vector2D a, Vector2D b) => new(a.Dx + b.Dx, a.Dy + b.Dy);
    public static Vector2D operator -(Vector2D a, Vector2D b) => new(a.Dx - b.Dx, a.Dy - b.Dy);
    public static Vector2D operator -(Vector2D v) => new(-v.Dx, -v.Dy);
    public static Vector2D operator *(Vector2D v, decimal scalar) => new(v.Dx * scalar, v.Dy * scalar);
    public static Vector2D operator *(decimal scalar, Vector2D v) => v * scalar;

    public decimal Dot(Vector2D other) => Dx * other.Dx + Dy * other.Dy;
    public decimal Cross(Vector2D other) => Dx * other.Dy - Dy * other.Dx;

    /// <summary>
    /// Rotate by angle. Uses double for trig as the sanctioned escape hatch;
    /// the result is immediately re-wrapped in decimal.
    /// </summary>
    public Vector2D Rotate(Angle angle)
    {
        var radians = angle.ToRadians();
        var cos = (decimal)Math.Cos(radians);
        var sin = (decimal)Math.Sin(radians);

        return new(Dx * cos - Dy * sin, Dx * sin + Dy * cos);
    }

    public Vector2D PerpendicularCW() => new(Dy, -Dx);
    public Vector2D PerpendicularCCW() => new(-Dy, Dx);

    public override string ToString() => $"<{Dx}, {Dy}>";
}
