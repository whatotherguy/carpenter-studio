using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline.StageResults;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class ConstraintPropagationStage : IResolutionStage
{
    private readonly IAppLogger? _logger;

    public ConstraintPropagationStage(IAppLogger? logger = null)
    {
        _logger = logger;
    }

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

        _logger?.Log(new LogEntry
        {
            Level = LogLevel.Debug,
            Category = "ConstraintPropagationStage",
            Message = $"Stage {StageNumber} ({StageName}) not yet implemented; returning skeleton result.",
            Timestamp = DateTimeOffset.UtcNow,
            StageNumber = StageNumber.ToString()
        });

        return StageResult.NotImplementedYet(StageNumber);
    }
}
