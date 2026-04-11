using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class CostingStage : IResolutionStage
{
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

        return StageResult.NotImplementedYet(StageNumber);
    }
}
