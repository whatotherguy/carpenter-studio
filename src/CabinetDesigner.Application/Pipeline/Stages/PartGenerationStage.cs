using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class PartGenerationStage : IResolutionStage
{
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

        return StageResult.NotImplementedYet(StageNumber);
    }
}
