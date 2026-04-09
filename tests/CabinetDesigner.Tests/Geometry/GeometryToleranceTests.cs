using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class GeometryToleranceTests
{
    private static Length In(decimal inches) => Length.FromInches(inches);

    private static readonly Length ShopTol = GeometryTolerance.DefaultShopTolerance;

    [Fact]
    public void DefaultShopTolerance_IsOneSixtyFourth()
    {
        Assert.Equal(1m / 64m, ShopTol.Inches);
    }

    [Fact]
    public void ApproximatelyEqual_EqualLengths_IsTrue()
    {
        var length = In(36m);
        Assert.True(GeometryTolerance.ApproximatelyEqual(length, length, ShopTol));
    }

    [Fact]
    public void ApproximatelyEqual_WithinTolerance_IsTrue()
    {
        Assert.True(GeometryTolerance.ApproximatelyEqual(In(36m), In(36m + (1m / 64m)), ShopTol));
    }

    [Fact]
    public void ApproximatelyEqual_ExceedsTolerance_IsFalse()
    {
        Assert.False(GeometryTolerance.ApproximatelyEqual(In(36m), In(36m + (2m / 64m)), ShopTol));
    }

    [Fact]
    public void ApproximatelyEqual_IsSymmetric()
    {
        var a = In(36m);
        var b = In(36m + (1m / 128m));

        Assert.Equal(
            GeometryTolerance.ApproximatelyEqual(a, b, ShopTol),
            GeometryTolerance.ApproximatelyEqual(b, a, ShopTol));
    }

    [Fact]
    public void ApproximatelyEqual_ZeroTolerance_OnlyTrueForIdenticalValues()
    {
        Assert.True(GeometryTolerance.ApproximatelyEqual(In(36m), In(36m), Length.Zero));
        Assert.False(GeometryTolerance.ApproximatelyEqual(In(36m), In(36.001m), Length.Zero));
    }

    [Theory]
    [InlineData(36.0, 36.0, true)]
    [InlineData(36.0, 36.01, true)]
    [InlineData(36.0, 36.02, false)]
    [InlineData(36.0, 35.99, true)]
    [InlineData(0.0, 0.0, true)]
    public void ApproximatelyEqual_TableDriven(double aIn, double bIn, bool expected)
    {
        var result = GeometryTolerance.ApproximatelyEqual(In((decimal)aIn), In((decimal)bIn), ShopTol);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApproximatelyEqual_SamePoint_IsTrue()
    {
        var point = new Point2D(10m, 20m);
        Assert.True(GeometryTolerance.ApproximatelyEqual(point, point, ShopTol));
    }

    [Fact]
    public void ApproximatelyEqual_NearbyPoints_IsTrue()
    {
        Assert.True(GeometryTolerance.ApproximatelyEqual(new Point2D(0m, 0m), new Point2D(0.01m, 0m), ShopTol));
    }

    [Fact]
    public void ApproximatelyEqual_FarPoints_IsFalse()
    {
        Assert.False(GeometryTolerance.ApproximatelyEqual(new Point2D(0m, 0m), new Point2D(1m, 0m), ShopTol));
    }

    [Fact]
    public void ApproximatelyEqual_Points_IsSymmetric()
    {
        var a = new Point2D(0m, 0m);
        var b = new Point2D(0.005m, 0.005m);

        Assert.Equal(
            GeometryTolerance.ApproximatelyEqual(a, b, ShopTol),
            GeometryTolerance.ApproximatelyEqual(b, a, ShopTol));
    }

    [Fact]
    public void IsEffectivelyZero_ZeroLength_IsTrue()
    {
        Assert.True(GeometryTolerance.IsEffectivelyZero(Length.Zero, ShopTol));
    }

    [Fact]
    public void IsEffectivelyZero_WithinTolerance_IsTrue()
    {
        Assert.True(GeometryTolerance.IsEffectivelyZero(In(1m / 128m), ShopTol));
    }

    [Fact]
    public void IsEffectivelyZero_ExactlyAtTolerance_IsTrue()
    {
        Assert.True(GeometryTolerance.IsEffectivelyZero(ShopTol, ShopTol));
    }

    [Fact]
    public void IsEffectivelyZero_LargerThanTolerance_IsFalse()
    {
        Assert.False(GeometryTolerance.IsEffectivelyZero(In(1m), ShopTol));
    }
}
