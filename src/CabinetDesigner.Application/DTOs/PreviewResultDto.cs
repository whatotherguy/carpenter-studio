using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;

namespace CabinetDesigner.Application.DTOs;

public sealed record PreviewResultDto(
    bool IsValid,
    string? RejectionReason,
    IReadOnlyList<PlacementCandidateDto> Candidates,
    IReadOnlyList<ValidationIssueSummaryDto> Warnings)
{
    public static PreviewResultDto From(PreviewResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var warnings = result.Issues
            .Where(issue => issue.Severity < ValidationSeverity.Error)
            .Select(ValidationIssueSummaryDto.From)
            .ToArray();

        if (!result.Success)
        {
            var rejectionReason = result.Issues.FirstOrDefault(issue => issue.Severity >= ValidationSeverity.Error)?.Message
                ?? result.Issues.FirstOrDefault()?.Message
                ?? "Preview failed.";

            return new PreviewResultDto(false, rejectionReason, [], warnings);
        }

        var candidates = result.SpatialResult?.Placements
            .Select(PlacementCandidateDto.From)
            .ToArray()
            ?? [];

        return new PreviewResultDto(true, null, candidates, warnings);
    }

    public static PreviewResultDto Invalid(string rejectionReason) =>
        new(false, rejectionReason, [], []);
}
