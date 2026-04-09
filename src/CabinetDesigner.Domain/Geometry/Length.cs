using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// The fundamental dimensional unit. Non-negative. Stored canonically in inches (decimal).
/// Signed differences use <see cref="Offset"/>.
/// </summary>
public readonly record struct Length : IComparable<Length>
{
    public decimal Inches { get; }

    private Length(decimal inches)
    {
        if (inches < 0)
            throw new ArgumentOutOfRangeException(nameof(inches), inches,
                "Length cannot be negative. Use Offset for signed dimensional quantities.");
        Inches = inches;
    }

    // ── Factory methods ───────────────────────────────────────────────

    public static Length FromInches(decimal inches) => new(inches);
    public static Length FromFeet(decimal feet) => new(feet * 12m);
    public static Length FromMillimeters(decimal mm) => new(mm / 25.4m);

    /// <summary>
    /// Construct from whole inches plus a proper fraction.
    /// Example: 36 1/2" = FromFractionalInches(36, 1, 2)
    /// </summary>
    public static Length FromFractionalInches(int whole, int numerator, int denominator)
    {
        if (denominator <= 0)
            throw new ArgumentOutOfRangeException(nameof(denominator),
                "Denominator must be positive.");
        if (numerator < 0)
            throw new ArgumentOutOfRangeException(nameof(numerator),
                "Numerator must be non-negative.");
        if (numerator >= denominator)
            throw new ArgumentOutOfRangeException(nameof(numerator),
                "Numerator must be less than denominator (use whole inches for full units).");
        if (whole < 0)
            throw new ArgumentOutOfRangeException(nameof(whole),
                "Whole inches must be non-negative.");
        return new(whole + (decimal)numerator / denominator);
    }

    public static readonly Length Zero = new(0m);

    // ── Conversions ───────────────────────────────────────────────────

    public decimal ToMillimeters() => Inches * 25.4m;
    public decimal ToFeet() => Inches / 12m;

    // ── Arithmetic ────────────────────────────────────────────────────

    public static Length operator +(Length a, Length b) => new(a.Inches + b.Inches);

    /// <summary>Length minus Length yields a signed <see cref="Offset"/>, not a Length.</summary>
    public static Offset operator -(Length a, Length b) => Offset.FromInches(a.Inches - b.Inches);

    public static Length operator *(Length a, decimal scalar) => new(a.Inches * scalar);
    public static Length operator *(decimal scalar, Length a) => new(a.Inches * scalar);
    public static Length operator /(Length a, decimal scalar) => new(a.Inches / scalar);

    /// <summary>Ratio of two lengths — dimensionless scalar.</summary>
    public static decimal operator /(Length a, Length b) => a.Inches / b.Inches;

    // ── Comparison ────────────────────────────────────────────────────

    public int CompareTo(Length other) => Inches.CompareTo(other.Inches);
    public static bool operator >(Length a, Length b) => a.Inches > b.Inches;
    public static bool operator <(Length a, Length b) => a.Inches < b.Inches;
    public static bool operator >=(Length a, Length b) => a.Inches >= b.Inches;
    public static bool operator <=(Length a, Length b) => a.Inches <= b.Inches;

    // ── Utility ───────────────────────────────────────────────────────

    public Length Abs() => new(Math.Abs(Inches));
    public static Length Min(Length a, Length b) => a <= b ? a : b;
    public static Length Max(Length a, Length b) => a >= b ? a : b;

    // ── Display ───────────────────────────────────────────────────────

    /// <summary>
    /// Internal debug representation. Display formatting for UI lives in IDimensionFormatter
    /// (Application/Infrastructure) — never here.
    /// </summary>
    public override string ToString() => $"{Inches}in";
}
