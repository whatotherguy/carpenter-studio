using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class PackagingStage : IResolutionStage
{
    private readonly IAppLogger? _logger;

    public PackagingStage(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public int StageNumber => 11;

    public string StageName => "Packaging";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        // NOT IMPLEMENTED YET - skeleton returns success with an empty result.
        context.PackagingResult = new PackagingResult
        {
            SnapshotId = string.Empty,
            RevisionId = default,
            CreatedAt = DateTimeOffset.UnixEpoch,
            ContentHash = string.Empty,
            Summary = new CabinetDesigner.Application.Pipeline.StageResults.SnapshotSummary(0, 0, 0, 0, 0m)
        };

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "PackagingStage",
            Message = $"Stage {StageNumber} ({StageName}) not yet implemented; returning skeleton result.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.NotImplementedYet(StageNumber);
    }
}
