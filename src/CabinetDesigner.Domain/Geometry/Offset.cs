using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// A signed dimensional quantity for directional differences, deltas, and adjustments.
/// Unlike <see cref="Length"/>, an Offset may be negative.
/// </summary>
public readonly record struct Offset : IComparable<Offset>
{
    public decimal Inches { get; }

    private Offset(decimal inches) => Inches = inches;

    // ── Factory methods ───────────────────────────────────────────────

    public static Offset FromInches(decimal inches) => new(inches);
    public static Offset FromMillimeters(decimal mm) => new(mm / 25.4m);

    public static readonly Offset Zero = new(0m);

    // ── Conversion ────────────────────────────────────────────────────

    /// <summary>Absolute magnitude of this offset as a non-negative <see cref="Length"/>.</summary>
    public Length Abs() => Length.FromInches(Math.Abs(Inches));

    /// <summary>Signed offset from a to b: positive when b > a.</summary>
    public static Offset Between(Length a, Length b) => new(b.Inches - a.Inches);

    // ── Arithmetic (Offset ± Offset) ──────────────────────────────────

    public static Offset operator +(Offset a, Offset b) => new(a.Inches + b.Inches);
    public static Offset operator -(Offset a, Offset b) => new(a.Inches - b.Inches);
    public static Offset operator -(Offset a) => new(-a.Inches);
    public static Offset operator *(Offset a, decimal scalar) => new(a.Inches * scalar);

    // ── Arithmetic (Length ± Offset → Length) ────────────────────────
    // Throws ArgumentOutOfRangeException via Length constructor if the result would be negative.

    public static Length operator +(Length l, Offset o) => Length.FromInches(l.Inches + o.Inches);
    public static Length operator -(Length l, Offset o) => Length.FromInches(l.Inches - o.Inches);

    // ── Comparison ────────────────────────────────────────────────────

    public int CompareTo(Offset other) => Inches.CompareTo(other.Inches);
    public static bool operator >(Offset a, Offset b) => a.Inches > b.Inches;
    public static bool operator <(Offset a, Offset b) => a.Inches < b.Inches;
    public static bool operator >=(Offset a, Offset b) => a.Inches >= b.Inches;
    public static bool operator <=(Offset a, Offset b) => a.Inches <= b.Inches;

    // ── Display ───────────────────────────────────────────────────────

    public override string ToString() => $"{Inches:+0.####;-0.####}in";
}
