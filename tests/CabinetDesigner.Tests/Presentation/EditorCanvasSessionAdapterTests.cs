using CabinetDesigner.Editor;
using CabinetDesigner.Presentation.ViewModels;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

/// <summary>
/// Tests for <see cref="EditorCanvasSessionAdapter"/> viewport zoom, pan, and reset math.
/// </summary>
public sealed class EditorCanvasSessionAdapterTests
{
    private static EditorCanvasSessionAdapter CreateAdapter() =>
        new(new EditorSession());

    // -----------------------------------------------------------------
    // ZoomAt
    // -----------------------------------------------------------------

    [Fact]
    public void ZoomAt_ZoomsIn_IncreasesScale()
    {
        var adapter = CreateAdapter();
        var initialScale = (double)adapter.Viewport.ScalePixelsPerInch;

        adapter.ZoomAt(0, 0, 1.5);

        Assert.True((double)adapter.Viewport.ScalePixelsPerInch > initialScale);
    }

    [Fact]
    public void ZoomAt_ZoomsOut_DecreasesScale()
    {
        var adapter = CreateAdapter();
        var initialScale = (double)adapter.Viewport.ScalePixelsPerInch;

        adapter.ZoomAt(0, 0, 0.5);

        Assert.True((double)adapter.Viewport.ScalePixelsPerInch < initialScale);
    }

    [Fact]
    public void ZoomAt_BelowMinScale_ClampsToMinimum()
    {
        var adapter = CreateAdapter();
        // Zoom out aggressively many times to push past the minimum.
        for (int i = 0; i < 30; i++)
        {
            adapter.ZoomAt(0, 0, 0.1);
        }

        Assert.Equal(2.0, (double)adapter.Viewport.ScalePixelsPerInch, precision: 5);
    }

    [Fact]
    public void ZoomAt_AboveMaxScale_ClampsToMaximum()
    {
        var adapter = CreateAdapter();
        // Zoom in aggressively many times to push past the maximum.
        for (int i = 0; i < 30; i++)
        {
            adapter.ZoomAt(0, 0, 10.0);
        }

        Assert.Equal(200.0, (double)adapter.Viewport.ScalePixelsPerInch, precision: 5);
    }

    [Fact]
    public void ZoomAt_PointUnderCursorRemainsFixed()
    {
        var adapter = CreateAdapter();
        const double cursorX = 300.0;
        const double cursorY = 200.0;

        // Zoom in 2× around the cursor.
        adapter.ZoomAt(cursorX, cursorY, 2.0);

        // The cursor position in screen space after zoom should still map to the same
        // world point: the fixed point of a cursor-centred zoom.
        var worldBefore = new CabinetDesigner.Domain.Geometry.Point2D(
            ((decimal)cursorX - ViewportTransform.Default.OffsetXPixels) / ViewportTransform.Default.ScalePixelsPerInch,
            ((decimal)cursorY - ViewportTransform.Default.OffsetYPixels) / ViewportTransform.Default.ScalePixelsPerInch);
        var worldAfter = adapter.Viewport.ToWorld(cursorX, cursorY);

        Assert.Equal((double)worldBefore.X, (double)worldAfter.X, precision: 5);
        Assert.Equal((double)worldBefore.Y, (double)worldAfter.Y, precision: 5);
    }

    // -----------------------------------------------------------------
    // PanBy
    // -----------------------------------------------------------------

    [Fact]
    public void PanBy_UpdatesViewportOffset()
    {
        var adapter = CreateAdapter();

        adapter.PanBy(40.0, -20.0);

        Assert.Equal(40m, adapter.Viewport.OffsetXPixels);
        Assert.Equal(-20m, adapter.Viewport.OffsetYPixels);
    }

    [Fact]
    public void PanBy_AccumulatesConsecutiveDeltas()
    {
        var adapter = CreateAdapter();

        adapter.PanBy(10.0, 5.0);
        adapter.PanBy(3.0, -8.0);

        Assert.Equal(13m, adapter.Viewport.OffsetXPixels);
        Assert.Equal(-3m, adapter.Viewport.OffsetYPixels);
    }

    // -----------------------------------------------------------------
    // BeginPan / EndPan
    // -----------------------------------------------------------------

    [Fact]
    public void BeginPan_SetsPanningViewportMode()
    {
        var adapter = CreateAdapter();

        adapter.BeginPan();

        Assert.Equal(EditorMode.PanningViewport, adapter.CurrentMode);
    }

    [Fact]
    public void EndPan_AfterBeginPan_RestoresIdleMode()
    {
        var adapter = CreateAdapter();
        adapter.BeginPan();

        adapter.EndPan();

        Assert.Equal(EditorMode.Idle, adapter.CurrentMode);
    }

    // -----------------------------------------------------------------
    // ResetViewport
    // -----------------------------------------------------------------

    [Fact]
    public void ResetViewport_ResetsToDefaultTransform()
    {
        var adapter = CreateAdapter();
        adapter.ZoomAt(100, 100, 3.0);
        adapter.PanBy(200.0, 150.0);

        adapter.ResetViewport();

        Assert.Equal(ViewportTransform.Default, adapter.Viewport);
    }

    [Fact]
    public void ResetViewport_AfterPanning_OffsetIsZero()
    {
        var adapter = CreateAdapter();
        adapter.PanBy(999.0, 888.0);

        adapter.ResetViewport();

        Assert.Equal(0m, adapter.Viewport.OffsetXPixels);
        Assert.Equal(0m, adapter.Viewport.OffsetYPixels);
    }

    [Fact]
    public void ResetViewport_AfterZooming_ScaleIsDefault()
    {
        var adapter = CreateAdapter();
        for (int i = 0; i < 10; i++)
        {
            adapter.ZoomAt(0, 0, 2.0);
        }

        adapter.ResetViewport();

        Assert.Equal(ViewportTransform.Default.ScalePixelsPerInch, adapter.Viewport.ScalePixelsPerInch);
    }

    // -----------------------------------------------------------------
    // FitViewport
    // -----------------------------------------------------------------

    [Fact]
    public void FitViewport_WithKnownBounds_CentresContentInCanvas()
    {
        var adapter = CreateAdapter();
        // Content: 20" × 10" block starting at (10", 5").
        var bounds = new ViewportBounds(10, 5, 30, 15);

        adapter.FitViewport(bounds, canvasWidth: 800, canvasHeight: 600);

        // Content centre in world: (20", 10").
        // After the fit, (20 * scale + offsetX) should equal canvasWidth / 2 = 400,
        // and (10 * scale + offsetY) should equal canvasHeight / 2 = 300.
        var scale = (double)adapter.Viewport.ScalePixelsPerInch;
        var offsetX = (double)adapter.Viewport.OffsetXPixels;
        var offsetY = (double)adapter.Viewport.OffsetYPixels;

        Assert.Equal(400.0, 20.0 * scale + offsetX, precision: 5);
        Assert.Equal(300.0, 10.0 * scale + offsetY, precision: 5);
    }

    [Fact]
    public void FitViewport_WithKnownBounds_ScaleFitsWidestDimension()
    {
        var adapter = CreateAdapter();
        // Wide content: 100" × 1".  Width dominates the fit.
        var bounds = new ViewportBounds(0, 0, 100, 1);

        adapter.FitViewport(bounds, canvasWidth: 800, canvasHeight: 600);

        // scaleX = 800 * 0.8 / 100 = 6.4; scaleY = 600 * 0.8 / 1 = 480; min(scaleX, scaleY) = 6.4
        Assert.Equal(6.4, (double)adapter.Viewport.ScalePixelsPerInch, precision: 5);
    }

    [Fact]
    public void FitViewport_ScaleIsClampedToMaximum()
    {
        var adapter = CreateAdapter();
        // Tiny content: 0.01" × 0.01" — would produce a huge scale without clamping.
        var bounds = new ViewportBounds(0, 0, 0.01, 0.01);

        adapter.FitViewport(bounds, canvasWidth: 800, canvasHeight: 600);

        Assert.Equal(200.0, (double)adapter.Viewport.ScalePixelsPerInch, precision: 5);
    }

    [Fact]
    public void FitViewport_WhenCanvasSizeIsZero_DoesNotChangeViewport()
    {
        var adapter = CreateAdapter();
        adapter.PanBy(50.0, 30.0);
        var viewport = adapter.Viewport;
        var bounds = new ViewportBounds(0, 0, 10, 10);

        adapter.FitViewport(bounds, canvasWidth: 0, canvasHeight: 0);

        Assert.Equal(viewport, adapter.Viewport);
    }

    [Fact]
    public void FitViewport_WithZeroWidthContent_FitsUsingHeightAxis()
    {
        // A vertical wall segment collapses to zero width — the fit should still work by
        // treating width as near-zero rather than aborting.
        var adapter = CreateAdapter();
        var bounds = new ViewportBounds(5, 0, 5, 20); // zero-width line

        adapter.FitViewport(bounds, canvasWidth: 800, canvasHeight: 600);

        // Scale must not remain at default; the viewport should have changed.
        Assert.NotEqual(ViewportTransform.Default, adapter.Viewport);
        // And still be within bounds.
        Assert.InRange((double)adapter.Viewport.ScalePixelsPerInch, 2.0, 200.0);
    }

    [Fact]
    public void ZoomAt_UsesSharedMinMax_MatchesFitViewportClampBounds()
    {
        // Regression guard: ZoomAt and FitViewport must both respect the same [2, 200] range.
        var adapter = CreateAdapter();

        for (var i = 0; i < 50; i++) adapter.ZoomAt(0, 0, 0.1); // force to minimum
        Assert.Equal(2.0, (double)adapter.Viewport.ScalePixelsPerInch, precision: 5);

        adapter.ResetViewport();
        for (var i = 0; i < 50; i++) adapter.ZoomAt(0, 0, 10.0); // force to maximum
        Assert.Equal(200.0, (double)adapter.Viewport.ScalePixelsPerInch, precision: 5);
    }
}
