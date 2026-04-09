using System;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class Vector2DTests
{
    private static readonly decimal Epsilon = 0.0001m;

    private static Vector2D Vec(decimal dx, decimal dy) => new(dx, dy);

    private static bool ApproxEqual(decimal a, decimal b) => Math.Abs(a - b) < Epsilon;

    [Fact]
    public void Zero_HasZeroComponents()
    {
        Assert.Equal(0m, Vector2D.Zero.Dx);
        Assert.Equal(0m, Vector2D.Zero.Dy);
    }

    [Fact]
    public void UnitX_HasComponentsOneZero()
    {
        Assert.Equal(1m, Vector2D.UnitX.Dx);
        Assert.Equal(0m, Vector2D.UnitX.Dy);
    }

    [Fact]
    public void UnitY_HasComponentsZeroOne()
    {
        Assert.Equal(0m, Vector2D.UnitY.Dx);
        Assert.Equal(1m, Vector2D.UnitY.Dy);
    }

    [Fact]
    public void Magnitude_UnitX_IsOne() => Assert.Equal(1m, Vector2D.UnitX.Magnitude().Inches);

    [Fact]
    public void Magnitude_UnitY_IsOne() => Assert.Equal(1m, Vector2D.UnitY.Magnitude().Inches);

    [Fact]
    public void Magnitude_Zero_IsZero() => Assert.Equal(0m, Vector2D.Zero.Magnitude().Inches);

    [Fact]
    public void Magnitude_ThreeFourFive()
    {
        Assert.Equal(5m, Vec(3m, 4m).Magnitude().Inches);
    }

    [Fact]
    public void Normalized_Zero_ReturnsZero()
    {
        Assert.Equal(Vector2D.Zero, Vector2D.Zero.Normalized());
    }

    [Fact]
    public void Normalized_UnitX_ReturnsSelf()
    {
        var normalized = Vector2D.UnitX.Normalized();
        Assert.True(ApproxEqual(1m, normalized.Dx));
        Assert.True(ApproxEqual(0m, normalized.Dy));
    }

    [Fact]
    public void Normalized_ArbitraryVector_HasMagnitudeOne()
    {
        Assert.True(ApproxEqual(1m, Vec(3m, 4m).Normalized().Magnitude().Inches));
    }

    [Fact]
    public void Add_Identity()
    {
        Assert.Equal(Vector2D.UnitX, Vector2D.UnitX + Vector2D.Zero);
    }

    [Fact]
    public void Add_Commutativity()
    {
        Assert.Equal(Vec(1m, 2m) + Vec(3m, 4m), Vec(3m, 4m) + Vec(1m, 2m));
    }

    [Fact]
    public void Subtract_Self_IsZero()
    {
        var vector = Vec(3m, 7m);
        Assert.Equal(Vector2D.Zero, vector - vector);
    }

    [Fact]
    public void Negate_FlipsComponents()
    {
        Assert.Equal(Vec(-3m, 4m), -Vec(3m, -4m));
    }

    [Fact]
    public void Multiply_Scalar_BothOrders()
    {
        var vector = Vec(2m, 3m);
        Assert.Equal(Vec(6m, 9m), vector * 3m);
        Assert.Equal(Vec(6m, 9m), 3m * vector);
    }

    [Fact]
    public void Dot_PerpendicularVectors_IsZero()
    {
        Assert.Equal(0m, Vector2D.UnitX.Dot(Vector2D.UnitY));
    }

    [Fact]
    public void Dot_ParallelVectors_IsProductOfMagnitudes()
    {
        Assert.Equal(12m, Vec(3m, 0m).Dot(Vec(4m, 0m)));
    }

    [Fact]
    public void Cross_UnitXAndUnitY_IsOne()
    {
        Assert.Equal(1m, Vector2D.UnitX.Cross(Vector2D.UnitY));
    }

    [Fact]
    public void Cross_UnitYAndUnitX_IsNegativeOne()
    {
        Assert.Equal(-1m, Vector2D.UnitY.Cross(Vector2D.UnitX));
    }

    [Fact]
    public void PerpendicularCW_UnitX_IsNegativeUnitY()
    {
        Assert.Equal(Vec(0m, -1m), Vector2D.UnitX.PerpendicularCW());
    }

    [Fact]
    public void PerpendicularCCW_UnitX_IsUnitY()
    {
        Assert.Equal(Vector2D.UnitY, Vector2D.UnitX.PerpendicularCCW());
    }

    [Fact]
    public void PerpendicularCW_And_CCW_ArePerpendicular()
    {
        var vector = Vec(3m, 4m);
        Assert.Equal(0m, vector.PerpendicularCW().Dot(vector));
        Assert.Equal(0m, vector.PerpendicularCCW().Dot(vector));
    }

    [Fact]
    public void Rotate_UnitX_By90CCW_IsApproxUnitY()
    {
        var result = Vector2D.UnitX.Rotate(Angle.Right);
        Assert.True(ApproxEqual(0m, result.Dx));
        Assert.True(ApproxEqual(1m, result.Dy));
    }

    [Fact]
    public void Rotate_By360_ReturnsSameVector()
    {
        var vector = Vec(3m, 4m);
        var rotated = vector.Rotate(Angle.FromDegrees(360m));
        Assert.True(ApproxEqual(vector.Dx, rotated.Dx));
        Assert.True(ApproxEqual(vector.Dy, rotated.Dy));
    }

    [Fact]
    public void Rotate_By180_FlipsDirection()
    {
        var rotated = Vector2D.UnitX.Rotate(Angle.Straight);
        Assert.True(ApproxEqual(-1m, rotated.Dx));
        Assert.True(ApproxEqual(0m, rotated.Dy));
    }
}
