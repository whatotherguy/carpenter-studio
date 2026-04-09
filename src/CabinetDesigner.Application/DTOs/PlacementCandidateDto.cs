using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.DTOs;

public sealed record PlacementCandidateDto(
    Guid RunId,
    Guid CabinetId,
    decimal OriginXInches,
    decimal OriginYInches,
    decimal DirectionX,
    decimal DirectionY)
{
    public static PlacementCandidateDto From(RunPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        return new PlacementCandidateDto(
            placement.RunId.Value,
            placement.CabinetId.Value,
            placement.Origin.X,
            placement.Origin.Y,
            placement.Direction.Dx,
            placement.Direction.Dy);
    }
}
