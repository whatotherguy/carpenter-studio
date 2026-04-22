using CabinetDesigner.Application.Projection;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Rendering;
using CabinetDesigner.Rendering.DTOs;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;

namespace CabinetDesigner.Presentation.Projection;

public sealed class SceneProjector : ISceneProjector
{
    private static readonly Length DefaultCabinetDepth = Length.FromInches(24m);
    private readonly IDesignStateStore _stateStore;
    private readonly IEditorCanvasSession _editorSession;

    public SceneProjector(IDesignStateStore stateStore)
        : this(stateStore, new NoOpEditorCanvasSession())
    {
    }

    public SceneProjector(IDesignStateStore stateStore, IEditorCanvasSession editorSession)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _editorSession = editorSession ?? throw new ArgumentNullException(nameof(editorSession));
    }

    public RenderSceneDto? Project()
    {
        var activeRoomId = _editorSession.ActiveRoomId;
        var walls = new List<WallRenderDto>();
        var runs = new List<RunRenderDto>();
        var cabinets = new List<CabinetRenderDto>();

        foreach (var wall in _stateStore.GetAllWalls())
        {
            if (activeRoomId is not null && wall.RoomId.Value != activeRoomId.Value)
            {
                continue;
            }

            walls.Add(new WallRenderDto(
                wall.Id.Value,
                wall.Segment,
                false));
        }

        foreach (var run in _stateStore.GetAllRuns())
        {
            var wall = _stateStore.GetWall(run.WallId);
            var spatialInfo = _stateStore.GetRunSpatialInfo(run.Id);
            if (wall is null || spatialInfo is null)
            {
                continue;
            }

            if (activeRoomId is not null && wall.RoomId.Value != activeRoomId.Value)
            {
                continue;
            }

            runs.Add(new RunRenderDto(
                run.Id.Value,
                new LineSegment2D(spatialInfo.StartWorld, spatialInfo.EndWorld),
                SceneProjectionGeometry.CreateWorldBounds(spatialInfo.StartWorld, wall.Direction, run.Capacity, DefaultCabinetDepth),
                false));

            foreach (var slot in run.Slots.Where(slot => slot.CabinetId is not null))
            {
                var cabinet = _stateStore.GetCabinet(slot.CabinetId!.Value);
                if (cabinet is null)
                {
                    continue;
                }

                var leftEdge = spatialInfo.StartWorld + wall.Direction * run.SlotOffset(slot.SlotIndex).Inches;
                var bounds = SceneProjectionGeometry.CreateWorldBounds(leftEdge, wall.Direction, slot.OccupiedWidth, cabinet.NominalDepth);
                cabinets.Add(new CabinetRenderDto(
                    cabinet.CabinetId.Value,
                    run.Id.Value,
                    bounds,
                    cabinet.CabinetTypeId,
                    cabinet.CabinetTypeId,
                    CabinetRenderState.Normal,
                    CabinetRenderModelFactory.CreateHandles(cabinet.CabinetId.Value, bounds)));
            }
        }

        return new RenderSceneDto(
            walls
                .GroupBy(wall => wall.WallId)
                .Select(group => group.First())
                .OrderBy(wall => wall.WallId)
                .ToArray(),
            runs.OrderBy(run => run.RunId).ToArray(),
            cabinets,
            null,
            new GridSettingsDto(false, Length.FromInches(12m), Length.FromInches(3m)));
    }

    private sealed class NoOpEditorCanvasSession : IEditorCanvasSession
    {
        public EditorMode CurrentMode => EditorMode.Idle;

        public IReadOnlyList<Guid> SelectedCabinetIds => [];

        public Guid? HoveredCabinetId => null;

        public Guid? ActiveRoomId => null;

        public ViewportTransform Viewport => ViewportTransform.Default;

        public SnapSettings SnapSettings => SnapSettings.Default;

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
