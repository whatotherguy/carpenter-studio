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
}
