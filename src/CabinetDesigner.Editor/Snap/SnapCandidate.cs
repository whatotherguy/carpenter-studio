using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Editor.Snap;

public sealed record SnapCandidate(
    SnapKind Kind,
    RunId RunId,
    string SourceId,
    int SourceIndex,
    Point2D SnapPoint,
    Length Distance,
    string Label);
