using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class ThicknessTests
{
    // ── Construction ──────────────────────────────────────────────────

    [Fact]
    public void Constructor_StoresNominalAndActual()
    {
        var nominal = Length.FromInches(0.75m);
        var actual  = Length.FromInches(0.72m);
        var t = new Thickness(nominal, actual);
        Assert.Equal(nominal, t.Nominal);
        Assert.Equal(actual,  t.Actual);
    }

    // ── Exact factory ─────────────────────────────────────────────────

    [Fact]
    public void Exact_NominalEqualsActual()
    {
        var value = Length.FromInches(0.75m);
        var t = Thickness.Exact(value);
        Assert.Equal(value, t.Nominal);
        Assert.Equal(value, t.Actual);
    }

    // ── Variance ──────────────────────────────────────────────────────

    [Fact]
    public void Variance_ActualThinnerThanNominal_IsPositive()
    {
        // nominal = 3/4", actual = 0.72" (actual is thinner)
        var t = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        var variance = t.Variance;
        Assert.True(variance.Inches > 0m);
        Assert.Equal(0.75m - 0.72m, variance.Inches);
    }

    [Fact]
    public void Variance_ActualThickerThanNominal_IsNegative()
    {
        // nominal = 3/4", actual = 0.76" (actual is thicker)
        var t = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.76m));
        var variance = t.Variance;
        Assert.True(variance.Inches < 0m);
    }

    [Fact]
    public void Variance_ExactThickness_IsZero()
    {
        var t = Thickness.Exact(Length.FromInches(0.75m));
        Assert.Equal(Offset.Zero, t.Variance);
    }

    [Fact]
    public void Variance_IsNominalMinusActual()
    {
        var nominal = Length.FromInches(0.75m);
        var actual  = Length.FromInches(0.72m);
        var t = new Thickness(nominal, actual);
        Assert.Equal(Offset.Between(actual, nominal), t.Variance);
    }

    // ── Equality ─────────────────────────────────────────────────────

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        var b = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentActual_AreNotEqual()
    {
        var a = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        var b = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.75m));
        Assert.NotEqual(a, b);
    }

    // ── Display ───────────────────────────────────────────────────────

    [Fact]
    public void ToString_ExactThickness_DoesNotMentionActual()
    {
        var t = Thickness.Exact(Length.FromInches(0.75m));
        Assert.DoesNotContain("actual", t.ToString());
    }

    [Fact]
    public void ToString_DifferentNominalAndActual_MentionsActual()
    {
        var t = new Thickness(Length.FromInches(0.75m), Length.FromInches(0.72m));
        Assert.Contains("actual", t.ToString());
    }
}
