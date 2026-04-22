using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class InputCaptureStage : IResolutionStage
{
    private readonly IDesignStateStore _stateStore;

    public InputCaptureStage()
        : this(new InMemoryDesignStateStore())
    {
    }

    public InputCaptureStage(IDesignStateStore stateStore)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public int StageNumber => 1;

    public string StageName => "Input Capture";

    public bool ShouldExecute(ResolutionMode mode) => true;

    public StageResult Execute(ResolutionContext context)
    {
        var resolvedEntities = new Dictionary<string, IDomainEntity>(StringComparer.Ordinal);
        context.InputCapture = new InputCaptureResult
        {
            ResolvedEntities = resolvedEntities,
            NormalizedParameters = new Dictionary<string, OverrideValue>(),
            TemplateExpansions = []
        };

        switch (context.Command)
        {
            case AddCabinetToRunCommand addCabinet:
                if (_stateStore.GetRun(addCabinet.RunId) is not { } run)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("RUN_NOT_FOUND", $"Run {addCabinet.RunId} was not found.")]);
                }

                resolvedEntities["run"] = new ResolvedRunEntity(run);
                break;

            case MoveCabinetCommand moveCabinet:
                if (_stateStore.GetRun(moveCabinet.SourceRunId) is not { } sourceRun)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("SOURCE_RUN_NOT_FOUND", $"Run {moveCabinet.SourceRunId} was not found.")]);
                }

                if (_stateStore.GetRun(moveCabinet.TargetRunId) is not { } targetRun)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("TARGET_RUN_NOT_FOUND", $"Run {moveCabinet.TargetRunId} was not found.")]);
                }

                if (_stateStore.GetCabinet(moveCabinet.CabinetId) is not { } cabinet)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {moveCabinet.CabinetId} was not found.")]);
                }

                resolvedEntities["sourceRun"] = new ResolvedRunEntity(sourceRun);
                resolvedEntities["targetRun"] = new ResolvedRunEntity(targetRun);
                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinet);
                break;

            case DeleteRunCommand deleteRun:
                if (_stateStore.GetRun(deleteRun.RunId) is not { } runToDelete)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("RUN_NOT_FOUND", $"Run {deleteRun.RunId} was not found.")]);
                }

                resolvedEntities["run"] = new ResolvedRunEntity(runToDelete);
                break;

            case CreateRunCommand createRun:
                if (!Guid.TryParse(createRun.WallId, out var wallGuid))
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("INVALID_WALL_ID", $"Wall id '{createRun.WallId}' is not a valid GUID.")]);
                }

                if (_stateStore.GetWall(new WallId(wallGuid)) is not { } wall)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("WALL_NOT_FOUND", $"Wall {createRun.WallId} was not found.")]);
                }

                resolvedEntities["wall"] = new ResolvedWallEntity(wall);
                break;

            case ResizeCabinetCommand resizeCabinet:
                if (_stateStore.GetCabinet(resizeCabinet.CabinetId) is not { } cabinetToResize)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {resizeCabinet.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToResize);
                break;

            case SetCabinetConstructionCommand setCabinetConstruction:
                if (_stateStore.GetCabinet(setCabinetConstruction.CabinetId) is not { } cabinetToReconstruct)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {setCabinetConstruction.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToReconstruct);
                break;

            case SetCabinetCategoryCommand setCabinetCategory:
                if (_stateStore.GetCabinet(setCabinetCategory.CabinetId) is not { } cabinetToRetag)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {setCabinetCategory.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToRetag);
                break;

            case AddOpeningCommand addOpening:
                if (_stateStore.GetCabinet(addOpening.CabinetId) is not { } cabinetToOpen)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {addOpening.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToOpen);
                break;

            case RemoveOpeningCommand removeOpening:
                if (_stateStore.GetCabinet(removeOpening.CabinetId) is not { } cabinetToRemoveOpening)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {removeOpening.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToRemoveOpening);
                break;

            case ReorderOpeningCommand reorderOpening:
                if (_stateStore.GetCabinet(reorderOpening.CabinetId) is not { } cabinetToReorder)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {reorderOpening.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToReorder);
                break;

            case SetCabinetOverrideCommand setCabinetOverride:
                if (_stateStore.GetCabinet(setCabinetOverride.CabinetId) is not { } cabinetToOverride)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {setCabinetOverride.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToOverride);
                break;

            case RemoveCabinetOverrideCommand removeCabinetOverride:
                if (_stateStore.GetCabinet(removeCabinetOverride.CabinetId) is not { } cabinetToClearOverride)
                {
                    return StageResult.Failed(StageNumber, [CreateIssue("CABINET_NOT_FOUND", $"Cabinet {removeCabinetOverride.CabinetId} was not found.")]);
                }

                resolvedEntities["cabinet"] = new ResolvedCabinetEntity(cabinetToClearOverride);
                break;
        }

        return StageResult.Succeeded(StageNumber);
    }

    private static ValidationIssue CreateIssue(string code, string message) =>
        new(ValidationSeverity.Error, code, message);
}
