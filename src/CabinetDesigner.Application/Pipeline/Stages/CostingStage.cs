using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class CostingStage : IResolutionStage
{
    private readonly IAppLogger? _logger;

    public CostingStage(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public int StageNumber => 9;

    public string StageName => "Costing";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        // NOT IMPLEMENTED YET - skeleton returns success with an empty result.
        context.CostingResult = new CostingResult
        {
            MaterialCost = 0m,
            HardwareCost = 0m,
            LaborCost = 0m,
            InstallCost = 0m,
            Subtotal = 0m,
            Markup = 0m,
            Tax = 0m,
            Total = 0m,
            CabinetBreakdowns = []
        };

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "CostingStage",
            Message = $"Stage {StageNumber} ({StageName}) not yet implemented; returning skeleton result.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.NotImplementedYet(StageNumber);
    }
}
