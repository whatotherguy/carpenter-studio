using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class PartGenerationStage : IResolutionStage
{
    private readonly IAppLogger? _logger;

    public PartGenerationStage(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public int StageNumber => 6;

    public string StageName => "Part Generation";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        // NOT IMPLEMENTED YET - skeleton returns success with an empty result.
        context.PartResult = new PartGenerationResult
        {
            Parts = []
        };

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "PartGenerationStage",
            Message = $"Stage {StageNumber} ({StageName}) not yet implemented; returning skeleton result.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.NotImplementedYet(StageNumber);
    }
}
