using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Editor;

public enum DragType
{
    PlaceCabinet,
    MoveCabinet,
    ResizeCabinet
}

public sealed record DragContext(
    DragType DragType,
    Point2D CursorWorld,
    Vector2D GrabOffset,
    Length NominalWidth,
    Length NominalDepth,
    string? CabinetTypeId,
    CabinetId? SubjectCabinetId,
    RunId? SourceRunId,
    Point2D? FixedLeftEdgeWorld,
    RunId? TargetRunId)
{
    public Point2D CandidateRefPoint => CursorWorld - GrabOffset;
}

public enum DrawRunPhase
{
    SettingStart,
    SettingEnd
}

public sealed record DrawRunDragContext(
    DrawRunPhase Phase,
    Point2D? StartWorld,
    Point2D CurrentEndWorld,
    string? WallId);
