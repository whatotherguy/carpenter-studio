using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.State;

public sealed class InMemoryPackagingResultStore : IPackagingResultStore
{
    private readonly IAppLogger? _logger;
    private volatile PackagingResult? _current;

    public InMemoryPackagingResultStore(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public PackagingResult? Current => _current;

    public void Update(PackagingResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        _current = result;

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "Packaging",
            Message = $"Updated packaged snapshot state for revision {result.RevisionId}.",
            Timestamp = result.CreatedAt,
            Properties = new Dictionary<string, string>
            {
                ["snapshotId"] = result.SnapshotId,
                ["contentHash"] = result.ContentHash
            }
        });
    }

    public void Clear() => _current = null;
}
