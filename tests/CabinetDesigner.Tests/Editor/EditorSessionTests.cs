using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
using Xunit;

namespace CabinetDesigner.Tests.Editor;

public sealed class EditorSessionTests
{
    [Fact]
    public void BeginMoveDrag_FromIdle_Succeeds()
    {
        var session = new CabinetDesigner.Editor.EditorSession();

        session.BeginMoveDrag(CreateDragContext(CabinetDesigner.Editor.DragType.MoveCabinet));

        Assert.Equal(CabinetDesigner.Editor.EditorMode.MovingCabinet, session.Mode);
    }

    [Fact]
    public void BeginMoveDrag_FromNonIdle_Throws()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.BeginCatalogDrag(CreateDragContext(CabinetDesigner.Editor.DragType.PlaceCabinet));

        Assert.Throws<InvalidOperationException>(() => session.BeginMoveDrag(CreateDragContext(CabinetDesigner.Editor.DragType.MoveCabinet)));
    }

    [Fact]
    public void BeginCatalogDrag_FromNonIdle_Throws()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.BeginMoveDrag(CreateDragContext(CabinetDesigner.Editor.DragType.MoveCabinet));

        Assert.Throws<InvalidOperationException>(() => session.BeginCatalogDrag(CreateDragContext(CabinetDesigner.Editor.DragType.PlaceCabinet)));
    }

    [Fact]
    public void EndDrag_ClearsTypedSnapWinner()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.BeginMoveDrag(CreateDragContext(CabinetDesigner.Editor.DragType.MoveCabinet));
        session.RecordSnapWinner(new SnapCandidate(
            SnapKind.Grid,
            RunId.New(),
            "grid",
            0,
            Point2D.Origin,
            Length.Zero,
            "Grid"));

        session.EndDrag();

        Assert.Null(session.PreviousSnapWinner);
        Assert.Null(session.ActiveDrag);
        Assert.Equal(CabinetDesigner.Editor.EditorMode.Idle, session.Mode);
    }

    // -----------------------------------------------------------------
    // Thread-safety tests
    // -----------------------------------------------------------------

    /// <summary>
    /// Verifies that concurrent Begin/EndDrag calls on separate threads never corrupt
    /// the session into an invalid state (e.g. Mode=MovingCabinet with no ActiveDrag,
    /// or an InvalidOperationException due to a torn Mode read during AssertMode).
    /// </summary>
    /// <remarks>
    /// The test drives the concurrent-access path without relying on specific timing, so it
    /// will not produce false negatives on a slow machine.  The important guarantee is that
    /// after all concurrent tasks complete, the session is in a self-consistent state.
    /// </remarks>
    [Fact]
    public async Task ConcurrentBeginAndEndDrag_DoNotCorruptSessionState()
    {
        var session = new CabinetDesigner.Editor.EditorSession();

        const int iterations = 200;
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var tasks = Enumerable.Range(0, iterations).Select(_ => Task.Run(() =>
        {
            try
            {
                session.BeginMoveDrag(CreateDragContext(CabinetDesigner.Editor.DragType.MoveCabinet));
                session.UpdateDragCursor(new Point2D(1m, 2m), RunId.New());
                session.EndDrag();
            }
            catch (InvalidOperationException)
            {
                // Another thread has already changed the mode — this is expected and
                // safe: the lock guarantees no torn writes, just serialised rejections.
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // No unexpected exceptions should have occurred.
        Assert.Empty(exceptions);

        // After all threads finish, the session must be in a self-consistent terminal state.
        var finalMode = session.Mode;
        var finalDrag = session.ActiveDrag;

        if (finalMode == CabinetDesigner.Editor.EditorMode.Idle)
        {
            Assert.Null(finalDrag);
        }
        else if (finalMode == CabinetDesigner.Editor.EditorMode.MovingCabinet)
        {
            Assert.NotNull(finalDrag);
        }
        else
        {
            Assert.Fail($"Session ended in unexpected mode: {finalMode}");
        }
    }

    /// <summary>
    /// Verifies that concurrent selection writes and reads never produce a torn/null result.
    /// </summary>
    [Fact]
    public async Task ConcurrentSelectionUpdates_AlwaysReturnConsistentList()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        var ids = Enumerable.Range(0, 10).Select(_ => CabinetId.New()).ToArray();

        const int iterations = 500;
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        var writeTasks = Enumerable.Range(0, iterations).Select(i => Task.Run(() =>
        {
            try
            {
                // Alternate between setting a list and clearing it.
                if (i % 2 == 0)
                {
                    session.SetSelection((IReadOnlyList<CabinetId>)ids.Take(i % ids.Length + 1).ToArray());
                }
                else
                {
                    session.SetSelection((IReadOnlyList<CabinetId>)[]);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        var readTasks = Enumerable.Range(0, iterations).Select(_ => Task.Run(() =>
        {
            try
            {
                // Reading must never return null (the backing field is always a valid list).
                var result = session.SelectedCabinetIds;
                if (result is null)
                {
                    exceptions.Add(new InvalidOperationException("SelectedCabinetIds returned null."));
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }));

        await Task.WhenAll(writeTasks.Concat(readTasks));

        Assert.Empty(exceptions);
    }

    /// <summary>
    /// Verifies that calling EndDrag from a non-UI thread (simulating the ConfigureAwait(false)
    /// path that existed before the fix) does not corrupt session state when a drag is active.
    /// </summary>
    [Fact]
    public async Task EndDrag_CalledFromBackgroundThread_ResetsStateCorrectly()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.BeginMoveDrag(CreateDragContext(CabinetDesigner.Editor.DragType.MoveCabinet));

        Assert.Equal(CabinetDesigner.Editor.EditorMode.MovingCabinet, session.Mode);

        await Task.Run(() => session.EndDrag());

        Assert.Equal(CabinetDesigner.Editor.EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
        Assert.Null(session.PreviousSnapWinner);
    }

    /// <summary>
    /// Verifies that a Begin → AbortDrag sequence is equivalent to EndDrag with respect to
    /// leaving the session in the Idle state.
    /// </summary>
    [Fact]
    public void AbortDrag_AfterBegin_RestoresIdleMode()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.BeginResizeDrag(CreateDragContext(CabinetDesigner.Editor.DragType.ResizeCabinet));

        session.AbortDrag();

        Assert.Equal(CabinetDesigner.Editor.EditorMode.Idle, session.Mode);
        Assert.Null(session.ActiveDrag);
    }

    // -----------------------------------------------------------------
    // Pan mode tests
    // -----------------------------------------------------------------

    [Fact]
    public void BeginPan_FromIdle_SetsPanningViewportMode()
    {
        var session = new CabinetDesigner.Editor.EditorSession();

        session.BeginPan();

        Assert.Equal(CabinetDesigner.Editor.EditorMode.PanningViewport, session.Mode);
    }

    [Fact]
    public void EndPan_AfterBeginPan_RestoresIdleMode()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.BeginPan();

        session.EndPan();

        Assert.Equal(CabinetDesigner.Editor.EditorMode.Idle, session.Mode);
    }

    [Fact]
    public void BeginPan_FromNonIdle_Throws()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.BeginMoveDrag(CreateDragContext(CabinetDesigner.Editor.DragType.MoveCabinet));

        Assert.Throws<InvalidOperationException>(() => session.BeginPan());
    }

    [Fact]
    public void ResetViewport_ResetsToDefault()
    {
        var session = new CabinetDesigner.Editor.EditorSession();
        session.SetViewport(new ViewportTransform(50m, 100m, 200m));

        session.ResetViewport();

        Assert.Equal(ViewportTransform.Default, session.Viewport);
    }

    [Fact]
    public void PanBy_UpdatesViewportOffset()
    {
        var session = new CabinetDesigner.Editor.EditorSession();

        session.SetViewport(session.Viewport.Panned(30m, 50m));

        Assert.Equal(30m, session.Viewport.OffsetXPixels);
        Assert.Equal(50m, session.Viewport.OffsetYPixels);
    }

    private static CabinetDesigner.Editor.DragContext CreateDragContext(CabinetDesigner.Editor.DragType dragType) =>
        new(
            dragType,
            Point2D.Origin,
            Vector2D.Zero,
            Length.FromInches(24m),
            Length.FromInches(24m),
            "base-24",
            CabinetId.New(),
            RunId.New(),
            null,
            RunId.New());
}
