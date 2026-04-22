using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Rendering.DTOs;
using Xunit;

namespace CabinetDesigner.Tests.Application.Projection;

public sealed class SceneProjectorTests
{
    [Fact]
    public void Project_EmptyState_ReturnsEmptyScene()
    {
        var projector = new SceneProjector(new InMemoryDesignStateStore(), new TestEditorCanvasSession());

        var scene = projector.Project();

        Assert.NotNull(scene);
        Assert.Empty(scene.Walls);
        Assert.Empty(scene.Runs);
        Assert.Empty(scene.Cabinets);
    }

    [Fact]
    public void Project_OneRunOneCabinet_ReturnsCompleteRenderModel()
    {
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(96m, 0m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, Length.FromInches(96m));
        var cabinetId = CabinetId.New();
        var slot = run.AppendCabinet(cabinetId, Length.FromInches(30m));
        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        store.AddCabinet(new CabinetStateRecord(cabinetId, "base-30", Length.FromInches(30m), Length.FromInches(24m), run.Id, slot.Id, CabinetCategory.Base, ConstructionMethod.Frameless));

        var scene = new SceneProjector(store, new TestEditorCanvasSession()).Project();

        Assert.NotNull(scene);
        var projectedWall = Assert.Single(scene.Walls);
        var projectedRun = Assert.Single(scene.Runs);
        var cabinet = Assert.Single(scene.Cabinets);
        Assert.Equal(wall.Id.Value, projectedWall.WallId);
        Assert.Equal(run.Id.Value, projectedRun.RunId);
        Assert.Equal(cabinetId.Value, cabinet.CabinetId);
        Assert.Equal(CabinetRenderState.Normal, cabinet.State);
        Assert.Equal("base-30", cabinet.TypeDisplayName);
        Assert.Equal(2, cabinet.Handles.Count);
    }

    [Fact]
    public void Project_AngledWall_ProducesNonAxisAlignedProgression()
    {
        var store = new InMemoryDesignStateStore();
        var wall = new Wall(WallId.New(), RoomId.New(), Point2D.Origin, new Point2D(48m, 48m), Thickness.Exact(Length.FromInches(4m)));
        var run = new CabinetRun(RunId.New(), wall.Id, wall.Length);
        var firstCabinetId = CabinetId.New();
        var secondCabinetId = CabinetId.New();
        var firstSlot = run.AppendCabinet(firstCabinetId, Length.FromInches(24m));
        var secondSlot = run.AppendCabinet(secondCabinetId, Length.FromInches(24m));
        store.AddWall(wall);
        store.AddRun(run, wall.StartPoint, wall.EndPoint);
        store.AddCabinet(new CabinetStateRecord(firstCabinetId, "diag-1", Length.FromInches(24m), Length.FromInches(24m), run.Id, firstSlot.Id, CabinetCategory.Base, ConstructionMethod.Frameless));
        store.AddCabinet(new CabinetStateRecord(secondCabinetId, "diag-2", Length.FromInches(24m), Length.FromInches(24m), run.Id, secondSlot.Id, CabinetCategory.Base, ConstructionMethod.Frameless));

        var scene = new SceneProjector(store, new TestEditorCanvasSession()).Project();

        Assert.NotNull(scene);
        Assert.Equal(2, scene.Cabinets.Count);
        Assert.True(scene.Cabinets[1].WorldBounds.Origin.X > scene.Cabinets[0].WorldBounds.Origin.X);
        Assert.True(scene.Cabinets[1].WorldBounds.Origin.Y > scene.Cabinets[0].WorldBounds.Origin.Y);
}

    private sealed class TestEditorCanvasSession : CabinetDesigner.Presentation.ViewModels.IEditorCanvasSession
    {
        public CabinetDesigner.Editor.EditorMode CurrentMode => CabinetDesigner.Editor.EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds => [];

        public Guid? HoveredCabinetId => null;

        public Guid? ActiveRoomId => null;

        public CabinetDesigner.Editor.ViewportTransform Viewport => CabinetDesigner.Editor.ViewportTransform.Default;

        public CabinetDesigner.Editor.Snap.SnapSettings SnapSettings => CabinetDesigner.Editor.Snap.SnapSettings.Default;

        public void SetSelectedCabinetIds(IReadOnlyList<Guid> cabinetIds)
        {
        }

        public void SetHoveredCabinetId(Guid? cabinetId)
        {
        }

        public void SetActiveRoom(Guid? roomId)
        {
        }

        public void ZoomAt(double screenX, double screenY, double scaleFactor)
        {
        }

        public void PanBy(double dx, double dy)
        {
        }

        public void BeginPan()
        {
        }

        public void EndPan()
        {
        }

        public void ResetViewport()
        {
        }

        public void FitViewport(ViewportBounds contentBounds, double canvasWidth, double canvasHeight)
        {
        }
    }
}
