using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class Point2DTests
{
    private static Vector2D Vec(decimal dx, decimal dy) => new(dx, dy);

    [Fact]
    public void Origin_IsZeroZero()
    {
        Assert.Equal(0m, Point2D.Origin.X);
        Assert.Equal(0m, Point2D.Origin.Y);
    }

    [Fact]
    public void Constructor_StoresCoordinates()
    {
        var point = new Point2D(3m, 7m);
        Assert.Equal(3m, point.X);
        Assert.Equal(7m, point.Y);
    }

    [Fact]
    public void Point2D_AcceptsNegativeCoordinates()
    {
        var point = new Point2D(-5m, -10m);
        Assert.Equal(-5m, point.X);
        Assert.Equal(-10m, point.Y);
    }

    [Fact]
    public void Subtract_TwoPoints_ReturnsVector()
    {
        var a = new Point2D(5m, 3m);
        var b = new Point2D(2m, 1m);

        var vector = a - b;

        Assert.IsType<Vector2D>(vector);
        Assert.Equal(3m, vector.Dx);
        Assert.Equal(2m, vector.Dy);
    }

    [Fact]
    public void Add_PointPlusVector_ShiftsPoint()
    {
        var result = new Point2D(1m, 2m) + Vec(3m, 4m);
        Assert.Equal(4m, result.X);
        Assert.Equal(6m, result.Y);
    }

    [Fact]
    public void Subtract_VectorFromPoint_ShiftsPoint()
    {
        var result = new Point2D(5m, 6m) - Vec(2m, 3m);
        Assert.Equal(3m, result.X);
        Assert.Equal(3m, result.Y);
    }

    [Fact]
    public void DistanceTo_Self_IsZero()
    {
        var point = new Point2D(5m, 7m);
        Assert.Equal(Length.Zero, point.DistanceTo(point));
    }

    [Fact]
    public void DistanceTo_AxisAligned_Horizontal()
    {
        Assert.Equal(5m, new Point2D(0m, 0m).DistanceTo(new Point2D(5m, 0m)).Inches);
    }

    [Fact]
    public void DistanceTo_AxisAligned_Vertical()
    {
        Assert.Equal(12m, new Point2D(0m, 0m).DistanceTo(new Point2D(0m, 12m)).Inches);
    }

    [Fact]
    public void DistanceTo_ThreeFourFiveTriangle()
    {
        Assert.Equal(5m, new Point2D(0m, 0m).DistanceTo(new Point2D(3m, 4m)).Inches);
    }

    [Fact]
    public void DistanceTo_IsSymmetric()
    {
        var a = new Point2D(1m, 2m);
        var b = new Point2D(4m, 6m);
        Assert.Equal(a.DistanceTo(b).Inches, b.DistanceTo(a).Inches);
    }

    [Fact]
    public void DistanceTo_TriangleInequality()
    {
        var a = new Point2D(0m, 0m);
        var b = new Point2D(3m, 0m);
        var c = new Point2D(0m, 4m);
        Assert.True(a.DistanceTo(c) <= a.DistanceTo(b) + b.DistanceTo(c));
    }

    [Fact]
    public void MidpointTo_ReturnsCenter()
    {
        var midpoint = new Point2D(0m, 0m).MidpointTo(new Point2D(10m, 4m));
        Assert.Equal(5m, midpoint.X);
        Assert.Equal(2m, midpoint.Y);
    }

    [Fact]
    public void MidpointTo_Self_ReturnsSamePoint()
    {
        var point = new Point2D(3m, 7m);
        Assert.Equal(point, point.MidpointTo(point));
    }

    [Fact]
    public void Equality_SameCoordinates_AreEqual()
    {
        Assert.Equal(new Point2D(3m, 7m), new Point2D(3m, 7m));
    }

    [Fact]
    public void Equality_DifferentCoordinates_AreNotEqual()
    {
        Assert.NotEqual(new Point2D(1m, 2m), new Point2D(2m, 1m));
    }
}
