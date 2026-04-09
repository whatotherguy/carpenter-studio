using System;

namespace CabinetDesigner.Domain.Geometry;

/// <summary>
/// Material thickness as a first-class domain concept: nominal (labelled) vs actual (measured).
/// Nominal and actual are kept separate because cabinet engineering depends on actual thickness
/// for filler math and opening widths, while BOMs and purchasing use nominal.
/// </summary>
public readonly record struct Thickness
{
    public Length Nominal { get; }
    public Length Actual  { get; }

    public Thickness(Length nominal, Length actual)
    {
        Nominal = nominal;
        Actual  = actual;
    }

    // ── Factory ───────────────────────────────────────────────────────

    /// <summary>Creates a thickness where nominal and actual are identical.</summary>
    public static Thickness Exact(Length value) => new(value, value);

    // ── Derived ───────────────────────────────────────────────────────

    /// <summary>
    /// Difference between nominal and actual (Nominal − Actual).
    /// Positive variance means actual material is thinner than labelled.
    /// </summary>
    public Offset Variance => Offset.Between(Actual, Nominal);

    // ── Display ───────────────────────────────────────────────────────

    public override string ToString() => Nominal == Actual
        ? $"{Nominal}"
        : $"{Nominal} (actual: {Actual})";
}
