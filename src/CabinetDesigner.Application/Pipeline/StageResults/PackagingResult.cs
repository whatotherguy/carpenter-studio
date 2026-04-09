using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline.StageResults;

public sealed record PackagingResult
{
    public required string SnapshotId { get; init; }

    public required RevisionId RevisionId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string ContentHash { get; init; }

    public required SnapshotSummary Summary { get; init; }
}

public sealed record SnapshotSummary(
    int CabinetCount,
    int RunCount,
    int PartCount,
    int ValidationIssueCount,
    decimal TotalCost);
