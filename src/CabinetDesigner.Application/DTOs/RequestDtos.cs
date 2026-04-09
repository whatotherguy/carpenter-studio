namespace CabinetDesigner.Application.DTOs;

public sealed record CreateRunRequestDto(
    string WallId,
    decimal StartXInches,
    decimal StartYInches,
    decimal EndXInches,
    decimal EndYInches);

public sealed record AddCabinetRequestDto(
    Guid RunId,
    string CabinetTypeId,
    decimal NominalWidthInches,
    string Placement);

public sealed record InsertCabinetRequestDto(
    Guid RunId,
    string CabinetTypeId,
    decimal NominalWidthInches,
    int InsertAtIndex,
    Guid LeftNeighborId,
    Guid RightNeighborId);

public sealed record MoveCabinetRequestDto(
    Guid CabinetId,
    Guid SourceRunId,
    Guid TargetRunId,
    string TargetPlacement,
    int? TargetIndex);

public sealed record ResizeCabinetRequestDto(
    Guid CabinetId,
    decimal CurrentNominalWidthInches,
    decimal NewNominalWidthInches);

public abstract record OverrideValueDto
{
    public sealed record OfDecimalInches(decimal Inches) : OverrideValueDto;
    public sealed record OfString(string Value) : OverrideValueDto;
    public sealed record OfBool(bool Value) : OverrideValueDto;
    public sealed record OfInt(int Value) : OverrideValueDto;
    public sealed record OfMaterialId(Guid MaterialId) : OverrideValueDto;
    public sealed record OfHardwareItemId(Guid HardwareItemId) : OverrideValueDto;
}

public sealed record SetCabinetOverrideRequestDto(
    Guid CabinetId,
    string ParameterKey,
    OverrideValueDto Value);
