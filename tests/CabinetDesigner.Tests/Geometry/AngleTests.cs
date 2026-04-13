using System;

using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class AngleTests
{
    // ── Constants ─────────────────────────────────────────────────────

    [Fact]
    public void Zero_IsDegrees0() => Assert.Equal(0m, Angle.Zero.Degrees);

    [Fact]
    public void Right_IsDegrees90() => Assert.Equal(90m, Angle.Right.Degrees);

    [Fact]
    public void Straight_IsDegrees180() => Assert.Equal(180m, Angle.Straight.Degrees);

    [Fact]
    public void FromDegrees360EqualsZero() => Assert.Equal(Angle.Zero, Angle.FromDegrees(360m));

    [Fact]
    public void FromDegrees360_Degrees_IsZero() => Assert.Equal(0m, Angle.FromDegrees(360m).Degrees);

    // ── Normalization ─────────────────────────────────────────────────

    [Theory]
    [InlineData(-90, 270)]
    [InlineData(-180, 180)]
    [InlineData(-270, 90)]
    [InlineData(-360, 0)]
    [InlineData(360, 0)]
    [InlineData(450, 90)]
    [InlineData(720, 0)]
    [InlineData(0, 0)]
    [InlineData(359, 359)]
    public void FromDegrees_Normalization(double input, double expected)
    {
        var a = Angle.FromDegrees((decimal)input);
        Assert.Equal((decimal)expected, a.Degrees);
    }

    // ── Factory ───────────────────────────────────────────────────────

    [Fact]
    public void FromRadians_HalfPi_IsNinetyDegrees()
    {
        var a = Angle.FromRadians(Math.PI / 2.0);
        // floating-point conversion: allow small rounding
        Assert.True(Math.Abs((double)a.Degrees - 90.0) < 0.0001);
    }

    [Fact]
    public void FromRadians_Pi_IsOneEightyDegrees()
    {
        var a = Angle.FromRadians(Math.PI);
        Assert.True(Math.Abs((double)a.Degrees - 180.0) < 0.0001);
    }

    [Fact]
    public void ToRadians_NinetyDegrees_IsHalfPi()
    {
        Assert.True(Math.Abs(Angle.Right.ToRadians() - Math.PI / 2.0) < 1e-10);
    }

    // ── Arithmetic ────────────────────────────────────────────────────

    [Fact]
    public void Add_NinetyPlusNinety_IsOneEighty()
    {
        Assert.Equal(180m, (Angle.Right + Angle.Right).Degrees);
    }

    [Fact]
    public void Add_NinetyPlusThreeHundredSixty_IsNinety()
    {
        Assert.Equal(90m, (Angle.Right + Angle.FromDegrees(360m)).Degrees);
    }

    [Fact]
    public void Subtract_NinetyFromOneEighty_IsNinety()
    {
        Assert.Equal(90m, (Angle.Straight - Angle.Right).Degrees);
    }

    [Fact]
    public void Negate_NinetyDegrees_IsTwoSeventyDegrees()
    {
        // -90 normalizes to 270
        Assert.Equal(270m, (-Angle.Right).Degrees);
    }

    [Fact]
    public void Add_Identity() => Assert.Equal(Angle.Right, Angle.Right + Angle.Zero);

    // ── Comparison ────────────────────────────────────────────────────

    [Fact]
    public void Comparison_Ordering()
    {
        Assert.True(Angle.Zero < Angle.Right);
        Assert.True(Angle.Right < Angle.Straight);
        Assert.True(Angle.Straight > Angle.Zero);
        Assert.Equal(0, Angle.Right.CompareTo(Angle.Right));
    }

    // ── Display ───────────────────────────────────────────────────────

    [Fact]
    public void ToString_ContainsDegreeSymbol()
    {
        Assert.Contains("°", Angle.Right.ToString());
    }
}
