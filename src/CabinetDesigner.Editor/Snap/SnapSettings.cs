using CabinetDesigner.Domain.Geometry;

namespace CabinetDesigner.Editor.Snap;

public sealed record SnapSettings(
    Length SnapRadius,
    Length HitTestRadius,
    Length GridSize,
    Length HysteresisDistance)
{
    public static readonly SnapSettings Default = new(
        Length.FromInches(6m),
        Length.FromInches(12m),
        Length.FromInches(3m),
        Length.FromInches(0.5m));
}
