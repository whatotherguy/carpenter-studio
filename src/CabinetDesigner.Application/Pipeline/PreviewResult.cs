using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.Pipeline;

public sealed record PreviewResult
{
    public required CommandMetadata CommandMetadata { get; init; }

    public required bool Success { get; init; }

    public required IReadOnlyList<ValidationIssue> Issues { get; init; }

    public SpatialResolutionResult? SpatialResult { get; init; }

    public static PreviewResult Succeeded(
        CommandMetadata metadata,
        SpatialResolutionResult spatialResult,
        IReadOnlyList<ValidationIssue>? warnings = null) => new()
    {
        CommandMetadata = metadata,
        Success = true,
        Issues = warnings ?? [],
        SpatialResult = spatialResult
    };

    public static PreviewResult Failed(
        CommandMetadata metadata,
        IReadOnlyList<ValidationIssue> issues) => new()
    {
        CommandMetadata = metadata,
        Success = false,
        Issues = issues,
        SpatialResult = null
    };
}
