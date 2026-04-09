using System;

using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class OffsetTests
{
    // ── Factory & Zero ────────────────────────────────────────────────

    [Fact]
    public void FromInches_PositiveAndNegative_AreAllowed()
    {
        Assert.Equal(5m,  Offset.FromInches(5m).Inches);
        Assert.Equal(-5m, Offset.FromInches(-5m).Inches);
        Assert.Equal(0m,  Offset.FromInches(0m).Inches);
    }

    [Fact]
    public void Zero_HasZeroInches() => Assert.Equal(0m, Offset.Zero.Inches);

    [Fact]
    public void FromMillimeters_25_4mm_IsOneInch()
    {
        Assert.Equal(1m, Offset.FromMillimeters(25.4m).Inches);
    }

    // ── Abs ───────────────────────────────────────────────────────────

    [Fact]
    public void Abs_NegativeOffset_ReturnsPositiveLength()
    {
        var offset = Offset.FromInches(-5m);
        Assert.Equal(Length.FromInches(5m), offset.Abs());
    }

    [Fact]
    public void Abs_PositiveOffset_ReturnsSameLength()
    {
        var offset = Offset.FromInches(5m);
        Assert.Equal(Length.FromInches(5m), offset.Abs());
    }

    // ── Between ───────────────────────────────────────────────────────

    [Fact]
    public void Between_bGreaterThanA_ReturnsPositive()
    {
        var a = Length.FromInches(3m);
        var b = Length.FromInches(10m);
        Assert.Equal(7m, Offset.Between(a, b).Inches);
    }

    [Fact]
    public void Between_aGreaterThanB_ReturnsNegative()
    {
        var a = Length.FromInches(10m);
        var b = Length.FromInches(3m);
        Assert.Equal(-7m, Offset.Between(a, b).Inches);
    }

    [Fact]
    public void Between_Equal_ReturnsZero()
    {
        var a = Length.FromInches(5m);
        Assert.Equal(0m, Offset.Between(a, a).Inches);
    }

    // ── Arithmetic (Offset ± Offset) ──────────────────────────────────

    [Fact]
    public void Add_Identity() => Assert.Equal(Offset.FromInches(5m), Offset.FromInches(5m) + Offset.Zero);

    [Fact]
    public void Add_NegativeAndPositive_Cancels()
    {
        var a = Offset.FromInches(5m);
        var b = Offset.FromInches(-5m);
        Assert.Equal(Offset.Zero, a + b);
    }

    [Fact]
    public void Subtract_Self_IsZero()
    {
        var a = Offset.FromInches(7m);
        Assert.Equal(Offset.Zero, a - a);
    }

    [Fact]
    public void Negate_FlipsSign()
    {
        var a = Offset.FromInches(5m);
        Assert.Equal(-5m, (-a).Inches);
        Assert.Equal(5m, (-(-a)).Inches);
    }

    [Fact]
    public void Multiply_ByScalar()
    {
        var a = Offset.FromInches(3m);
        Assert.Equal(9m, (a * 3m).Inches);
        Assert.Equal(-6m, (a * -2m).Inches);
    }

    // ── Arithmetic (Length ± Offset) ──────────────────────────────────

    [Fact]
    public void Length_PlusPositiveOffset_Increases()
    {
        var l = Length.FromInches(10m);
        var o = Offset.FromInches(5m);
        Assert.Equal(15m, (l + o).Inches);
    }

    [Fact]
    public void Length_MinusPositiveOffset_Decreases()
    {
        var l = Length.FromInches(10m);
        var o = Offset.FromInches(5m);
        Assert.Equal(5m, (l - o).Inches);
    }

    [Fact]
    public void Length_PlusNegativeOffset_Decreases()
    {
        var l = Length.FromInches(10m);
        var o = Offset.FromInches(-3m);
        Assert.Equal(7m, (l + o).Inches);
    }

    [Fact]
    public void Length_PlusOffset_WouldGoNegative_Throws()
    {
        var l = Length.FromInches(5m);
        var o = Offset.FromInches(-10m);
        Assert.Throws<ArgumentOutOfRangeException>(() => l + o);
    }

    [Fact]
    public void Length_MinusOffset_WouldGoNegative_Throws()
    {
        var l = Length.FromInches(5m);
        var o = Offset.FromInches(10m);
        Assert.Throws<ArgumentOutOfRangeException>(() => l - o);
    }

    // ── Comparison ────────────────────────────────────────────────────

    [Fact]
    public void Comparison_SignedOrdering()
    {
        var neg = Offset.FromInches(-5m);
        var pos = Offset.FromInches(5m);
        var sameAsNeg = Offset.FromInches(-5m);
        var sameAsPos = Offset.FromInches(5m);
        Assert.True(neg < pos);
        Assert.True(pos > neg);
        Assert.True(neg <= sameAsNeg);
        Assert.True(pos >= sameAsPos);
    }
}
