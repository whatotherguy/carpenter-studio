using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// An angle, normalized to [0, 360). Stored in degrees (decimal).
/// </summary>
public readonly record struct Angle : IComparable<Angle>
{
    public decimal Degrees { get; }

    private Angle(decimal degrees) => Degrees = Normalize(degrees);

    // ── Factory methods ───────────────────────────────────────────────

    public static Angle FromDegrees(decimal degrees) => new(degrees);

    /// <summary>Convert from radians. Uses double for transcendental math; result is re-wrapped.</summary>
    public static Angle FromRadians(double radians) => new((decimal)(radians * 180.0 / Math.PI));

    // ── Constants ─────────────────────────────────────────────────────

    public static readonly Angle Zero     = new(0m);
    public static readonly Angle Right    = new(90m);
    public static readonly Angle Straight = new(180m);

    // ── Conversion ────────────────────────────────────────────────────

    public double ToRadians() => (double)Degrees * Math.PI / 180.0;

    // ── Arithmetic ────────────────────────────────────────────────────

    public static Angle operator +(Angle a, Angle b) => new(a.Degrees + b.Degrees);
    public static Angle operator -(Angle a, Angle b) => new(a.Degrees - b.Degrees);
    public static Angle operator -(Angle a)           => new(-a.Degrees);

    // ── Comparison ────────────────────────────────────────────────────

    public int CompareTo(Angle other) => Degrees.CompareTo(other.Degrees);
    public static bool operator >(Angle a, Angle b)  => a.Degrees > b.Degrees;
    public static bool operator <(Angle a, Angle b)  => a.Degrees < b.Degrees;
    public static bool operator >=(Angle a, Angle b) => a.Degrees >= b.Degrees;
    public static bool operator <=(Angle a, Angle b) => a.Degrees <= b.Degrees;

    // ── Internals ─────────────────────────────────────────────────────

    private static decimal Normalize(decimal d)
    {
        d %= 360m;
        return d < 0m ? d + 360m : d;
    }

    // ── Display ───────────────────────────────────────────────────────

    public override string ToString() => $"{Degrees}°";
}
