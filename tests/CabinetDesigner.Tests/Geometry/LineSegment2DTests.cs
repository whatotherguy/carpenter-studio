using System;
using CabinetDesigner.Domain.Geometry;
using Xunit;

namespace CabinetDesigner.Tests.Geometry;

public sealed class LineSegment2DTests
{
    private static readonly decimal Epsilon = 0.0001m;

    private static bool ApproxEqual(decimal a, decimal b) => Math.Abs(a - b) < Epsilon;

    private static LineSegment2D Seg(decimal x1, decimal y1, decimal x2, decimal y2) =>
        new(new Point2D(x1, y1), new Point2D(x2, y2));

    [Fact]
    public void Length_HorizontalSegment_IsCorrect()
    {
        Assert.Equal(10m, Seg(0m, 0m, 10m, 0m).Length().Inches);
    }

    [Fact]
    public void Length_VerticalSegment_IsCorrect()
    {
        Assert.Equal(7m, Seg(0m, 0m, 0m, 7m).Length().Inches);
    }

    [Fact]
    public void Length_ThreeFourFiveTriangle()
    {
        Assert.Equal(5m, Seg(0m, 0m, 3m, 4m).Length().Inches);
    }

    [Fact]
    public void Length_DegenerateSegment_IsZero()
    {
        Assert.Equal(0m, Seg(3m, 4m, 3m, 4m).Length().Inches);
    }

    [Fact]
    public void Midpoint_HorizontalSegment_IsCenter()
    {
        var midpoint = Seg(0m, 0m, 10m, 0m).Midpoint();
        Assert.Equal(5m, midpoint.X);
        Assert.Equal(0m, midpoint.Y);
    }

    [Fact]
    public void Midpoint_ArbitrarySegment_IsCenter()
    {
        var midpoint = Seg(2m, 4m, 8m, 10m).Midpoint();
        Assert.Equal(5m, midpoint.X);
        Assert.Equal(7m, midpoint.Y);
    }

    [Fact]
    public void Direction_HorizontalRight_IsUnitX()
    {
        var direction = Seg(0m, 0m, 10m, 0m).Direction();
        Assert.True(ApproxEqual(1m, direction.Dx));
        Assert.True(ApproxEqual(0m, direction.Dy));
    }

    [Fact]
    public void Direction_VerticalUp_IsUnitY()
    {
        var direction = Seg(0m, 0m, 0m, 5m).Direction();
        Assert.True(ApproxEqual(0m, direction.Dx));
        Assert.True(ApproxEqual(1m, direction.Dy));
    }

    [Fact]
    public void ClosestPointTo_PointOnSegment_ReturnsThatPoint()
    {
        var closest = Seg(0m, 0m, 10m, 0m).ClosestPointTo(new Point2D(5m, 0m));
        Assert.True(ApproxEqual(5m, closest.X));
        Assert.True(ApproxEqual(0m, closest.Y));
    }

    [Fact]
    public void ClosestPointTo_PointAboveMiddle_ReturnsFootOnSegment()
    {
        var closest = Seg(0m, 0m, 10m, 0m).ClosestPointTo(new Point2D(5m, 4m));
        Assert.True(ApproxEqual(5m, closest.X));
        Assert.True(ApproxEqual(0m, closest.Y));
    }

    [Fact]
    public void ClosestPointTo_PointBeyondStart_ClampsToStart()
    {
        var closest = Seg(0m, 0m, 10m, 0m).ClosestPointTo(new Point2D(-3m, 0m));
        Assert.True(ApproxEqual(0m, closest.X));
        Assert.True(ApproxEqual(0m, closest.Y));
    }

    [Fact]
    public void ClosestPointTo_PointBeyondEnd_ClampsToEnd()
    {
        var closest = Seg(0m, 0m, 10m, 0m).ClosestPointTo(new Point2D(15m, 0m));
        Assert.True(ApproxEqual(10m, closest.X));
        Assert.True(ApproxEqual(0m, closest.Y));
    }

    [Fact]
    public void ClosestPointTo_DegenerateSegment_ReturnsStart()
    {
        var segment = Seg(3m, 4m, 3m, 4m);
        Assert.Equal(segment.Start, segment.ClosestPointTo(new Point2D(10m, 10m)));
    }

    [Fact]
    public void DistanceTo_Start_IsZero()
    {
        var segment = Seg(0m, 0m, 10m, 0m);
        Assert.True(ApproxEqual(0m, segment.DistanceTo(segment.Start).Inches));
    }

    [Fact]
    public void DistanceTo_End_IsZero()
    {
        var segment = Seg(0m, 0m, 10m, 0m);
        Assert.True(ApproxEqual(0m, segment.DistanceTo(segment.End).Inches));
    }

    [Fact]
    public void DistanceTo_PointAboveMiddle_IsPerpendicular()
    {
        Assert.True(ApproxEqual(4m, Seg(0m, 0m, 10m, 0m).DistanceTo(new Point2D(5m, 4m)).Inches));
    }

    [Fact]
    public void DistanceTo_PointBeyondEnd_IsDistanceToEndpoint()
    {
        Assert.True(ApproxEqual(5m, Seg(0m, 0m, 10m, 0m).DistanceTo(new Point2D(13m, 4m)).Inches));
    }
}
