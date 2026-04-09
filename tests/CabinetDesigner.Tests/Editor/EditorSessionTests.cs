using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
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
