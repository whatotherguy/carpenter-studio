using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Editor;
using Xunit;

namespace CabinetDesigner.Tests.Editor;

/// <summary>
/// Tests for viewport pan/zoom math in <see cref="ViewportTransform"/>.
/// </summary>
public sealed class ViewportTransformTests
{
    [Fact]
    public void Panned_ByDelta_UpdatesOffsets()
    {
        var viewport = ViewportTransform.Default; // scale=10, offsets=0

        var result = viewport.Panned(30m, -15m);

        Assert.Equal(30m, result.OffsetXPixels);
        Assert.Equal(-15m, result.OffsetYPixels);
        Assert.Equal(viewport.ScalePixelsPerInch, result.ScalePixelsPerInch);
    }

    [Fact]
    public void Panned_AccumulatesTwoDeltasCorrectly()
    {
        var viewport = ViewportTransform.Default;

        var result = viewport.Panned(10m, 20m).Panned(5m, -8m);

        Assert.Equal(15m, result.OffsetXPixels);
        Assert.Equal(12m, result.OffsetYPixels);
    }

    [Fact]
    public void ToWorld_RoundTripsScreenCoordinateAfterPan()
    {
        var viewport = ViewportTransform.Default.Panned(50m, 80m);

        var world = viewport.ToWorld(150.0, 180.0);
        var (screenX, screenY) = viewport.ToScreen(world);

        Assert.Equal(150.0, screenX, precision: 5);
        Assert.Equal(180.0, screenY, precision: 5);
    }

    [Fact]
    public void ToWorld_RoundTripsScreenCoordinateAfterZoom()
    {
        var viewport = new ViewportTransform(20m, 0m, 0m);

        var world = viewport.ToWorld(200.0, 400.0);
        var (screenX, screenY) = viewport.ToScreen(world);

        Assert.Equal(200.0, screenX, precision: 5);
        Assert.Equal(400.0, screenY, precision: 5);
    }
}
