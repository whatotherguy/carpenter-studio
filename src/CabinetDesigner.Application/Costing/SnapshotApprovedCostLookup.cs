using System.Text.Json;
using CabinetDesigner.Application.Persistence;

namespace CabinetDesigner.Application.Costing;

public sealed class SnapshotApprovedCostLookup : IPreviousApprovedCostLookup
{
    private readonly ISnapshotRepository _snapshots;
    private readonly ICurrentPersistedProjectState _projectState;

    public SnapshotApprovedCostLookup(
        ISnapshotRepository snapshots,
        ICurrentPersistedProjectState projectState)
    {
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _projectState = projectState ?? throw new ArgumentNullException(nameof(projectState));
    }

    public decimal? GetMostRecentApprovedTotal()
    {
        var current = _projectState.CurrentState;
        if (current is null)
        {
            return null;
        }

        var summaries = _snapshots
            .ListAsync(current.Project.Id)
            .GetAwaiter()
            .GetResult();

        var mostRecent = summaries
            .Where(summary => summary.RevisionId != current.Revision.Id)
            .OrderByDescending(summary => summary.ApprovedAt)
            .ThenByDescending(summary => summary.RevisionNumber)
            .FirstOrDefault();

        if (mostRecent is null)
        {
            return null;
        }

        var snapshot = _snapshots
            .ReadAsync(mostRecent.RevisionId)
            .GetAwaiter()
            .GetResult();

        if (snapshot is null || string.IsNullOrEmpty(snapshot.EstimateBlob))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(snapshot.EstimateBlob);
            if (!document.RootElement.TryGetProperty("costing", out var costing))
            {
                return null;
            }

            if (!costing.TryGetProperty("total", out var totalElement))
            {
                return null;
            }

            return totalElement.GetDecimal();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
