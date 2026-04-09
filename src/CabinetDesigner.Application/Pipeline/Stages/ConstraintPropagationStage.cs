using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class ConstraintPropagationStage : IResolutionStage
{
    public int StageNumber => 5;

    public string StageName => "Constraint Propagation";

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        // NOT IMPLEMENTED YET - skeleton returns success with an empty result.
        context.ConstraintResult = new ConstraintPropagationResult
        {
            MaterialAssignments = [],
            HardwareAssignments = [],
            Violations = []
        };

        return StageResult.Succeeded(StageNumber);
    }
}
