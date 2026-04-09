using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering;
using Xunit;

namespace CabinetDesigner.Tests.Rendering;

public sealed class ScreenProjectionTests
{
    [Fact]
    public void ToScreen_Point_UsesScaleAndOffset()
    {
        var viewport = new ViewportTransform(8m, 10m, -6m);

        var point = ScreenProjection.ToScreen(new Point2D(4m, 3m), viewport);

        Assert.Equal(42d, point.X);
        Assert.Equal(18d, point.Y);
    }

    [Fact]
    public void ToScreen_Rect_ProjectsCornersDeterministically()
    {
        var viewport = new ViewportTransform(5m, 2m, 4m);
        var rect = new Rect2D(new Point2D(1m, 2m), Length.FromInches(3m), Length.FromInches(4m));

        var projected = ScreenProjection.ToScreen(rect, viewport);

        Assert.Equal(7d, projected.X);
        Assert.Equal(14d, projected.Y);
        Assert.Equal(15d, projected.Width);
        Assert.Equal(20d, projected.Height);
    }

    [Fact]
    public void ToScreen_Line_ProducesExpectedPixelDistance()
    {
        var viewport = new ViewportTransform(10m, 0m, 0m);
        var line = ScreenProjection.ToScreen(new LineSegment2D(Point2D.Origin, new Point2D(3m, 4m)), viewport);

        var distance = line.DistanceTo(30d, 40d);

        Assert.Equal(0d, distance, 6);
        Assert.Equal(10d, ScreenProjection.PixelsPerInch(viewport));
    }
}
