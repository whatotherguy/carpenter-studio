using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class Rect2DTests
{
    private static Rect2D MakeRect(decimal x, decimal y, decimal w, decimal h) =>
        new(new Point2D(x, y), Length.FromInches(w), Length.FromInches(h));

    [Fact]
    public void Constructor_StoresValues()
    {
        var rect = MakeRect(1m, 2m, 10m, 5m);
        Assert.Equal(1m, rect.Origin.X);
        Assert.Equal(2m, rect.Origin.Y);
        Assert.Equal(10m, rect.Width.Inches);
        Assert.Equal(5m, rect.Height.Inches);
    }

    [Fact]
    public void FromCorners_BottomLeftToTopRight_IsCorrect()
    {
        var rect = Rect2D.FromCorners(new Point2D(0m, 0m), new Point2D(10m, 5m));
        Assert.Equal(0m, rect.Origin.X);
        Assert.Equal(0m, rect.Origin.Y);
        Assert.Equal(10m, rect.Width.Inches);
        Assert.Equal(5m, rect.Height.Inches);
    }

    [Fact]
    public void FromCorners_TopRightToBottomLeft_NormalizesToMinOrigin()
    {
        var rect = Rect2D.FromCorners(new Point2D(10m, 5m), new Point2D(0m, 0m));
        Assert.Equal(0m, rect.Origin.X);
        Assert.Equal(0m, rect.Origin.Y);
        Assert.Equal(10m, rect.Width.Inches);
        Assert.Equal(5m, rect.Height.Inches);
    }

    [Fact]
    public void FromCorners_SamePoint_IsDegenerate()
    {
        var point = new Point2D(3m, 4m);
        var rect = Rect2D.FromCorners(point, point);
        Assert.Equal(0m, rect.Width.Inches);
        Assert.Equal(0m, rect.Height.Inches);
    }

    [Fact]
    public void Min_IsOrigin()
    {
        var rect = MakeRect(2m, 3m, 10m, 5m);
        Assert.Equal(rect.Origin, rect.Min);
    }

    [Fact]
    public void Max_IsOppositeCorner()
    {
        var rect = MakeRect(2m, 3m, 10m, 5m);
        Assert.Equal(12m, rect.Max.X);
        Assert.Equal(8m, rect.Max.Y);
    }

    [Fact]
    public void Center_IsMiddleOfRect()
    {
        var rect = MakeRect(0m, 0m, 10m, 4m);
        Assert.Equal(5m, rect.Center.X);
        Assert.Equal(2m, rect.Center.Y);
    }

    [Fact]
    public void Area_IsWidthTimesHeight()
    {
        Assert.Equal(24m, MakeRect(0m, 0m, 6m, 4m).Area);
    }

    [Fact]
    public void Contains_Center_IsTrue()
    {
        var rect = MakeRect(0m, 0m, 10m, 10m);
        Assert.True(rect.Contains(rect.Center));
    }

    [Fact]
    public void Contains_PointInsideRect_IsTrue()
    {
        Assert.True(MakeRect(0m, 0m, 10m, 10m).Contains(new Point2D(5m, 5m)));
    }

    [Fact]
    public void Contains_PointOutsideRect_IsFalse()
    {
        var rect = MakeRect(0m, 0m, 10m, 10m);
        Assert.False(rect.Contains(new Point2D(11m, 5m)));
        Assert.False(rect.Contains(new Point2D(5m, -1m)));
    }

    [Fact]
    public void Contains_OriginCorner_IsTrue()
    {
        var rect = MakeRect(0m, 0m, 10m, 10m);
        Assert.True(rect.Contains(rect.Origin));
    }

    [Fact]
    public void Contains_MaxCorner_IsTrue()
    {
        var rect = MakeRect(0m, 0m, 10m, 10m);
        Assert.True(rect.Contains(rect.Max));
    }

    [Fact]
    public void Contains_PointOnEdge_IsTrue()
    {
        var rect = MakeRect(0m, 0m, 10m, 10m);
        Assert.True(rect.Contains(new Point2D(0m, 5m)));
        Assert.True(rect.Contains(new Point2D(10m, 5m)));
        Assert.True(rect.Contains(new Point2D(5m, 0m)));
        Assert.True(rect.Contains(new Point2D(5m, 10m)));
    }

    [Fact]
    public void Intersects_OverlappingRects_IsTrue()
    {
        var a = MakeRect(0m, 0m, 10m, 10m);
        var b = MakeRect(5m, 5m, 10m, 10m);
        Assert.True(a.Intersects(b));
        Assert.True(b.Intersects(a));
    }

    [Fact]
    public void Intersects_ContainedRect_IsTrue()
    {
        var outer = MakeRect(0m, 0m, 20m, 20m);
        var inner = MakeRect(5m, 5m, 5m, 5m);
        Assert.True(outer.Intersects(inner));
        Assert.True(inner.Intersects(outer));
    }

    [Fact]
    public void Intersects_NonOverlappingRects_IsFalse()
    {
        var a = MakeRect(0m, 0m, 5m, 5m);
        var b = MakeRect(10m, 10m, 5m, 5m);
        Assert.False(a.Intersects(b));
        Assert.False(b.Intersects(a));
    }

    [Fact]
    public void Intersects_TouchingEdges_IsFalse()
    {
        var a = MakeRect(0m, 0m, 5m, 5m);
        var b = MakeRect(5m, 0m, 5m, 5m);
        Assert.False(a.Intersects(b));
    }

    [Fact]
    public void Intersects_Self_IsTrue()
    {
        var rect = MakeRect(0m, 0m, 10m, 10m);
        Assert.True(rect.Intersects(rect));
    }
}
