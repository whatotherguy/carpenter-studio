using System;

using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class LengthTests
{
    // ── Invariant ─────────────────────────────────────────────────────

    [Fact]
    public void FromInches_NegativeValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.FromInches(-0.001m));
    }

    [Fact]
    public void FromInches_Zero_Succeeds()
    {
        var l = Length.FromInches(0m);
        Assert.Equal(0m, l.Inches);
    }

    [Fact]
    public void FromFractionalInches_NegativeNumerator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.FromFractionalInches(36, -1, 4));
    }

    [Fact]
    public void FromFractionalInches_NumeratorNotLessThanDenominator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.FromFractionalInches(36, 4, 4));
    }

    [Fact]
    public void FromFractionalInches_ZeroDenominator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Length.FromFractionalInches(36, 1, 0));
    }

    // ── Factory round-trips ───────────────────────────────────────────

    [Fact]
    public void FromMillimeters_OneInch_RoundTrips()
    {
        var l = Length.FromMillimeters(25.4m);
        Assert.Equal(1m, l.Inches);
    }

    [Fact]
    public void FromFeet_OneFootIstwelveInches()
    {
        var l = Length.FromFeet(1m);
        Assert.Equal(12m, l.Inches);
    }

    [Fact]
    public void FromFractionalInches_ThirtySixAndHalf_IsCorrect()
    {
        var l = Length.FromFractionalInches(36, 1, 2);
        Assert.Equal(36.5m, l.Inches);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(36)]
    [InlineData(96)]
    public void FromInches_RoundTrip(decimal inches)
    {
        Assert.Equal(inches, Length.FromInches(inches).Inches);
    }

    [Theory]
    [InlineData(25.4)]
    [InlineData(304.8)]   // 12 inches
    [InlineData(914.4)]   // 36 inches
    public void FromMillimeters_ToMillimeters_RoundTrip(double mm)
    {
        var l = Length.FromMillimeters((decimal)mm);
        Assert.Equal((decimal)mm, l.ToMillimeters());
    }

    // ── Zero constant ─────────────────────────────────────────────────

    [Fact]
    public void Zero_HasZeroInches() => Assert.Equal(0m, Length.Zero.Inches);

    // ── Arithmetic ────────────────────────────────────────────────────

    [Fact]
    public void Add_Identity_AddZeroReturnsOriginal()
    {
        var a = Length.FromInches(36m);
        Assert.Equal(a, a + Length.Zero);
    }

    [Fact]
    public void Add_Commutativity()
    {
        var a = Length.FromInches(10m);
        var b = Length.FromInches(5m);
        Assert.Equal(a + b, b + a);
    }

    [Fact]
    public void Add_Associativity()
    {
        var a = Length.FromInches(3m);
        var b = Length.FromInches(5m);
        var c = Length.FromInches(7m);
        Assert.Equal((a + b) + c, a + (b + c));
    }

    [Fact]
    public void Subtract_LengthFromLength_ReturnsOffset()
    {
        var a = Length.FromInches(10m);
        var b = Length.FromInches(3m);
        var result = a - b;
        Assert.IsType<Offset>(result);
        Assert.Equal(7m, result.Inches);
    }

    [Fact]
    public void Subtract_EqualLengths_ReturnsZeroOffset()
    {
        var a = Length.FromInches(10m);
        Assert.Equal(Offset.Zero, a - a);
    }

    [Fact]
    public void Subtract_SmallerFromLarger_ReturnsNegativeOffset()
    {
        var a = Length.FromInches(3m);
        var b = Length.FromInches(10m);
        Assert.Equal(-7m, (a - b).Inches);
    }

    [Fact]
    public void Multiply_ByScalar()
    {
        var l = Length.FromInches(10m);
        Assert.Equal(30m, (l * 3m).Inches);
        Assert.Equal(30m, (3m * l).Inches);
    }

    [Fact]
    public void Divide_ByScalar()
    {
        var l = Length.FromInches(30m);
        Assert.Equal(10m, (l / 3m).Inches);
    }

    [Fact]
    public void Divide_ByLength_ReturnsRatio()
    {
        var a = Length.FromInches(30m);
        var b = Length.FromInches(10m);
        Assert.Equal(3m, a / b);
    }

    // ── Comparison ────────────────────────────────────────────────────

    [Fact]
    public void Comparison_Operators()
    {
        var small = Length.FromInches(5m);
        var large = Length.FromInches(10m);
        var sameAsSmall = Length.FromInches(5m);
        var sameAsLarge = Length.FromInches(10m);

        Assert.True(small < large);
        Assert.True(large > small);
        Assert.True(small <= sameAsSmall);
        Assert.True(large >= sameAsLarge);
        Assert.False(small > large);
    }

    [Fact]
    public void CompareTo_SmallerThanLarger_ReturnsNegative()
    {
        var a = Length.FromInches(5m);
        var b = Length.FromInches(10m);
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
        Assert.Equal(0, a.CompareTo(a));
    }

    // ── Utility ───────────────────────────────────────────────────────

    [Fact]
    public void Abs_ReturnsNonNegativeLength()
    {
        var l = Length.FromInches(5m);
        Assert.Equal(l, l.Abs());
    }

    [Fact]
    public void Min_ReturnsSmaller()
    {
        var a = Length.FromInches(3m);
        var b = Length.FromInches(7m);
        Assert.Equal(a, Length.Min(a, b));
        Assert.Equal(a, Length.Min(b, a));
    }

    [Fact]
    public void Max_ReturnsLarger()
    {
        var a = Length.FromInches(3m);
        var b = Length.FromInches(7m);
        Assert.Equal(b, Length.Max(a, b));
        Assert.Equal(b, Length.Max(b, a));
    }

    [Fact]
    public void ToString_ContainsInchSuffix()
    {
        Assert.Contains("in", Length.FromInches(36m).ToString());
    }
}
