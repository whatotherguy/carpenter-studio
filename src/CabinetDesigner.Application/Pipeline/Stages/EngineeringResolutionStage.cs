using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class EngineeringResolutionStage : IResolutionStage
{
    public int StageNumber => 4;

    public string StageName => "Engineering Resolution";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        // NOT IMPLEMENTED YET - skeleton returns success with an empty result.
        context.EngineeringResult = new EngineeringResolutionResult
        {
            Assemblies = [],
            FillerRequirements = [],
            EndConditionUpdates = []
        };

        return StageResult.Succeeded(StageNumber);
    }
}
