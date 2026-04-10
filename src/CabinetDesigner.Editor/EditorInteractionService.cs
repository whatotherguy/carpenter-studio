using System.Threading;
using System.Threading.Tasks;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Editor.Snap;

namespace CabinetDesigner.Editor;

public sealed class EditorInteractionService : IEditorInteractionService
{
    private readonly EditorSession _session;
    private readonly IEditorSceneGraph _sceneGraph;
    private readonly ISnapResolver _snapResolver;
    private readonly IPreviewCommandExecutor _previewCommandExecutor;
    private readonly ICommitCommandExecutor _commitCommandExecutor;
    private readonly IClock _clock;

    public EditorInteractionService(
        EditorSession session,
        IEditorSceneGraph sceneGraph,
        ISnapResolver snapResolver,
        IPreviewCommandExecutor previewCommandExecutor,
        ICommitCommandExecutor commitCommandExecutor,
        IClock clock)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _sceneGraph = sceneGraph ?? throw new ArgumentNullException(nameof(sceneGraph));
        _snapResolver = snapResolver ?? throw new ArgumentNullException(nameof(snapResolver));
        _previewCommandExecutor = previewCommandExecutor ?? throw new ArgumentNullException(nameof(previewCommandExecutor));
        _commitCommandExecutor = commitCommandExecutor ?? throw new ArgumentNullException(nameof(commitCommandExecutor));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public void BeginPlaceCabinet(string cabinetTypeId, Length nominalWidth, Length nominalDepth, double screenX, double screenY)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cabinetTypeId);

        var cursorWorld = _session.Viewport.ToWorld(screenX, screenY);
        var targetRunId = _sceneGraph.HitTestRun(cursorWorld, _session.SnapSettings.HitTestRadius);
        _session.BeginCatalogDrag(new DragContext(
            DragType.PlaceCabinet,
            cursorWorld,
            Vector2D.Zero,
            nominalWidth,
            nominalDepth,
            cabinetTypeId,
            null,
            null,
            null,
            targetRunId));
    }

    public void BeginMoveCabinet(CabinetId cabinetId, double screenX, double screenY)
    {
        var scene = _sceneGraph.Capture();
        var cabinet = scene.FindCabinet(cabinetId)
            ?? throw new InvalidOperationException($"Cabinet {cabinetId} was not found in the editor scene.");

        var cursorWorld = _session.Viewport.ToWorld(screenX, screenY);
        var targetRunId = _sceneGraph.HitTestRun(cursorWorld, _session.SnapSettings.HitTestRadius) ?? cabinet.RunId;
        _session.BeginMoveDrag(new DragContext(
            DragType.MoveCabinet,
            cursorWorld,
            cursorWorld - cabinet.LeftFaceWorld,
            cabinet.Width,
            cabinet.Depth,
            null,
            cabinetId,
            cabinet.RunId,
            null,
            targetRunId));
    }

    public void BeginResizeCabinet(CabinetId cabinetId, double screenX, double screenY)
    {
        var scene = _sceneGraph.Capture();
        var cabinet = scene.FindCabinet(cabinetId)
            ?? throw new InvalidOperationException($"Cabinet {cabinetId} was not found in the editor scene.");

        var cursorWorld = _session.Viewport.ToWorld(screenX, screenY);
        _session.BeginResizeDrag(new DragContext(
            DragType.ResizeCabinet,
            cursorWorld,
            Vector2D.Zero,
            cabinet.Width,
            cabinet.Depth,
            null,
            cabinetId,
            cabinet.RunId,
            cabinet.LeftFaceWorld,
            cabinet.RunId));
    }

    public DragPreviewResult OnDragMoved(double screenX, double screenY)
    {
        if (_session.ActiveDrag is null)
        {
            return DragPreviewResult.Invalid("No active drag.");
        }

        var cursorWorld = _session.Viewport.ToWorld(screenX, screenY);
        var targetRunId = _sceneGraph.HitTestRun(cursorWorld, _session.SnapSettings.HitTestRadius)
            ?? (_session.ActiveDrag.DragType == DragType.ResizeCabinet ? _session.ActiveDrag.TargetRunId : null);

        _session.UpdateDragCursor(cursorWorld, targetRunId);
        var drag = _session.ActiveDrag ?? throw new InvalidOperationException("Active drag unexpectedly missing.");
        var resolution = Resolve(drag);
        _session.RecordSnapWinner(resolution.Winner);

        var command = BuildCommand(drag, resolution.Winner);
        if (command is null)
        {
            return DragPreviewResult.Invalid("Drag is not currently over a valid target.");
        }

        return _previewCommandExecutor.Preview(command);
    }

    public async Task<DragCommitResult> OnDragCommittedAsync(CancellationToken ct = default)
    {
        if (_session.ActiveDrag is null)
        {
            return DragCommitResult.Failed("No active drag.");
        }

        try
        {
            var drag = _session.ActiveDrag;
            var resolution = Resolve(drag);
            var command = BuildCommand(drag, resolution.Winner);
            if (command is null)
            {
                return DragCommitResult.Failed("Drag is not currently over a valid target.");
            }

            return await _commitCommandExecutor.ExecuteAsync(command, ct).ConfigureAwait(false);
        }
        finally
        {
            _session.EndDrag();
        }
    }

    public void OnDragAborted() => _session.AbortDrag();

    private SnapResolution Resolve(DragContext drag)
    {
        var request = new SnapRequest(_sceneGraph.Capture(), drag, _session.SnapSettings);
        return _snapResolver.Resolve(request, _session.PreviousSnapWinner);
    }

    private IDesignCommand? BuildCommand(DragContext drag, SnapCandidate? winner)
    {
        return drag.DragType switch
        {
            DragType.PlaceCabinet => BuildPlaceCommand(drag, winner),
            DragType.MoveCabinet => BuildMoveCommand(drag, winner),
            DragType.ResizeCabinet => BuildResizeCommand(drag, winner),
            _ => null
        };
    }

    private IDesignCommand? BuildPlaceCommand(DragContext drag, SnapCandidate? winner)
    {
        if (drag.TargetRunId is null || string.IsNullOrWhiteSpace(drag.CabinetTypeId))
        {
            return null;
        }

        var scene = _sceneGraph.Capture();
        var run = scene.FindRun(drag.TargetRunId.Value);
        if (run is null)
        {
            return null;
        }

        var insertIndex = DetermineInsertIndex(run, winner?.SnapPoint ?? drag.CandidateRefPoint, drag.SubjectCabinetId);
        return new AddCabinetToRunCommand(
            drag.TargetRunId.Value,
            drag.CabinetTypeId,
            drag.NominalWidth,
            RunPlacement.AtIndex,
            CommandOrigin.User,
            CreateIntentDescription("Place cabinet", winner),
            _clock.Now,
            insertIndex);
    }

    private IDesignCommand? BuildMoveCommand(DragContext drag, SnapCandidate? winner)
    {
        if (drag.TargetRunId is null || drag.SourceRunId is null || drag.SubjectCabinetId is null)
        {
            return null;
        }

        var scene = _sceneGraph.Capture();
        var run = scene.FindRun(drag.TargetRunId.Value);
        if (run is null)
        {
            return null;
        }

        var insertIndex = DetermineInsertIndex(run, winner?.SnapPoint ?? drag.CandidateRefPoint, drag.SubjectCabinetId);
        return new MoveCabinetCommand(
            drag.SubjectCabinetId.Value,
            drag.SourceRunId.Value,
            drag.TargetRunId.Value,
            RunPlacement.AtIndex,
            CommandOrigin.User,
            CreateIntentDescription("Move cabinet", winner),
            _clock.Now,
            insertIndex);
    }

    private IDesignCommand? BuildResizeCommand(DragContext drag, SnapCandidate? winner)
    {
        if (drag.SubjectCabinetId is null || drag.FixedLeftEdgeWorld is null || drag.TargetRunId is null)
        {
            return null;
        }

        var scene = _sceneGraph.Capture();
        var cabinet = scene.FindCabinet(drag.SubjectCabinetId.Value);
        var run = scene.FindRun(drag.TargetRunId.Value);
        if (cabinet is null || run is null)
        {
            return null;
        }

        var rightEdge = winner?.SnapPoint ?? drag.CandidateRefPoint;
        var (distanceAlongAxis, _) = RunAxisProjection.ProjectOntoAxis(rightEdge, drag.FixedLeftEdgeWorld.Value, run.Axis);
        var newWidth = Length.FromInches(Math.Max(0m, distanceAlongAxis));

        return new ResizeCabinetCommand(
            drag.SubjectCabinetId.Value,
            cabinet.Width,
            newWidth,
            CommandOrigin.User,
            CreateIntentDescription("Resize cabinet", winner),
            _clock.Now);
    }

    private static int DetermineInsertIndex(RunSceneView run, Point2D referencePoint, CabinetId? subjectCabinetId)
    {
        var (distanceAlongAxis, _) = RunAxisProjection.ProjectOntoAxis(referencePoint, run.StartWorld, run.Axis);
        if (distanceAlongAxis <= 0m)
        {
            return 0;
        }

        var offset = 0m;
        var index = 0;
        foreach (var cabinet in run.Cabinets.OrderBy(cabinet => cabinet.SlotIndex))
        {
            if (cabinet.CabinetId == subjectCabinetId)
            {
                continue;
            }

            var midpoint = offset + (cabinet.Width.Inches / 2m);
            if (distanceAlongAxis < midpoint)
            {
                return index;
            }

            offset += cabinet.Width.Inches;
            index++;
        }

        return index;
    }

    private static string CreateIntentDescription(string action, SnapCandidate? winner) =>
        winner is null
            ? $"{action} freeform"
            : $"{action} snapped to {winner.Label}";
}
