namespace CabinetDesigner.Application.Pipeline;

public interface IResolutionStage
{
    int StageNumber { get; }

    string StageName { get; }

    StageResult Execute(ResolutionContext context);

    bool ShouldExecute(ResolutionMode mode);
}
