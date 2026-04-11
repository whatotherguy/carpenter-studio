using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Commands.Layout;
using CabinetDesigner.Domain.Commands.Modification;
using CabinetDesigner.Domain.Commands.Structural;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.RunContext;
using DomainRunPlacement = CabinetDesigner.Domain.Commands.Layout.RunPlacement;

namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed class InteractionInterpretationStage : IResolutionStage
{
    private readonly IDeltaTracker _deltaTracker;
    private readonly IDesignStateStore _stateStore;

    public InteractionInterpretationStage()
        : this(new InMemoryDeltaTracker(), new InMemoryDesignStateStore())
    {
    }

    public InteractionInterpretationStage(IDeltaTracker deltaTracker, IDesignStateStore stateStore)
    {
        _deltaTracker = deltaTracker ?? throw new ArgumentNullException(nameof(deltaTracker));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public int StageNumber => 2;

    public string StageName => "Interaction Interpretation";

    public bool ShouldExecute(ResolutionMode mode) => true;

    public StageResult Execute(ResolutionContext context)
    {
        try
        {
            var operations = context.Command switch
            {
                CreateRunCommand createRun => ExecuteCreateRun(createRun, context),
                AddCabinetToRunCommand addCabinet => ExecuteAddCabinet(addCabinet, context),
                MoveCabinetCommand moveCabinet => ExecuteMoveCabinet(moveCabinet, context),
                ResizeCabinetCommand resizeCabinet => ExecuteResizeCabinet(resizeCabinet, context),
                _ => [new DomainOperation.None()]
            };

            context.Interpretation = new InteractionInterpretationResult
            {
                Operations = operations,
                InterpretedParameters = new Dictionary<string, OverrideValue>()
            };

            return StageResult.Succeeded(StageNumber);
        }
        catch (InvalidOperationException exception)
        {
            return StageResult.Failed(StageNumber, [new ValidationIssue(ValidationSeverity.Error, "INTERACTION_FAILED", exception.Message)]);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return StageResult.Failed(StageNumber, [new ValidationIssue(ValidationSeverity.Error, "INTERACTION_FAILED", exception.Message)]);
        }
    }

    private IReadOnlyList<DomainOperation> ExecuteCreateRun(CreateRunCommand command, ResolutionContext context)
    {
        var wall = ((ResolvedWallEntity)context.InputCapture.ResolvedEntities["wall"]).Wall;
        var run = new CabinetRun(RunId.New(), wall.Id, command.StartPoint.DistanceTo(command.EndPoint));
        _stateStore.AddRun(run, command.StartPoint, command.EndPoint);

        _deltaTracker.RecordDelta(new StateDelta(
            run.Id.Value.ToString(),
            "CabinetRun",
            DeltaOperation.Created,
            null,
            _stateStore.CaptureRunValues(run)));

        return [new DomainOperation.UpdateRunCapacity(run.Id, run.Capacity)];
    }

    private IReadOnlyList<DomainOperation> ExecuteAddCabinet(AddCabinetToRunCommand command, ResolutionContext context)
    {
        var run = ((ResolvedRunEntity)context.InputCapture.ResolvedEntities["run"]).Run;
        var previousRunValues = _stateStore.CaptureRunValues(run);
        var cabinetId = CabinetId.New();
        var slot = command.Placement == DomainRunPlacement.AtIndex && command.InsertAtIndex is int insertIndex
            ? run.InsertCabinetAt(insertIndex, cabinetId, command.NominalWidth)
            : run.AppendCabinet(cabinetId, command.NominalWidth);
        var cabinet = new CabinetStateRecord(
            cabinetId,
            command.CabinetTypeId,
            command.NominalWidth,
            command.NominalDepth,
            run.Id,
            slot.Id);
        _stateStore.AddCabinet(cabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            run.Id.Value.ToString(),
            "CabinetRun",
            DeltaOperation.Modified,
            previousRunValues,
            _stateStore.CaptureRunValues(run)));
        _deltaTracker.RecordDelta(new StateDelta(
            cabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Created,
            null,
            _stateStore.CaptureCabinetValues(cabinet)));

        return [new DomainOperation.InsertSlot(run.Id, cabinetId, slot.SlotIndex)];
    }

    private IReadOnlyList<DomainOperation> ExecuteMoveCabinet(MoveCabinetCommand command, ResolutionContext context)
    {
        var sourceRun = ((ResolvedRunEntity)context.InputCapture.ResolvedEntities["sourceRun"]).Run;
        var targetRun = ((ResolvedRunEntity)context.InputCapture.ResolvedEntities["targetRun"]).Run;
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var sourceSlot = sourceRun.Slots.FirstOrDefault(slot => slot.CabinetId == cabinet.CabinetId)
            ?? throw new InvalidOperationException($"Cabinet {cabinet.CabinetId} is not in run {sourceRun.Id}.");
        var previousSourceValues = _stateStore.CaptureRunValues(sourceRun);
        var previousTargetValues = sourceRun.Id == targetRun.Id ? null : _stateStore.CaptureRunValues(targetRun);
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);

        var sourceIndex = sourceSlot.SlotIndex;
        sourceRun.RemoveSlot(sourceSlot.Id);
        var targetIndex = ResolveTargetIndex(command, targetRun, sourceRun.Id == targetRun.Id, sourceIndex);
        var insertedSlot = targetRun.InsertCabinetAt(targetIndex, cabinet.CabinetId, cabinet.NominalWidth);
        var updatedCabinet = cabinet with { RunId = targetRun.Id, SlotId = insertedSlot.Id };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            sourceRun.Id.Value.ToString(),
            "CabinetRun",
            DeltaOperation.Modified,
            previousSourceValues,
            _stateStore.CaptureRunValues(sourceRun)));

        if (sourceRun.Id != targetRun.Id)
        {
            _deltaTracker.RecordDelta(new StateDelta(
                targetRun.Id.Value.ToString(),
                "CabinetRun",
                DeltaOperation.Modified,
                previousTargetValues,
                _stateStore.CaptureRunValues(targetRun)));
        }

        _deltaTracker.RecordDelta(new StateDelta(
            cabinet.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.MoveSlot(sourceRun.Id, targetRun.Id, insertedSlot.Id, insertedSlot.SlotIndex)];
    }

    private IReadOnlyList<DomainOperation> ExecuteResizeCabinet(ResizeCabinetCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var run = _stateStore.GetRun(cabinet.RunId)
            ?? throw new InvalidOperationException($"Run {cabinet.RunId} for cabinet {cabinet.CabinetId} was not found.");

        var existingSlot = run.Slots.FirstOrDefault(slot => slot.CabinetId == command.CabinetId)
            ?? throw new InvalidOperationException($"Cabinet {command.CabinetId} has no slot in run {cabinet.RunId}.");

        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);

        // Remove the old slot and re-insert at the same index with the new width.
        var slotIndex = existingSlot.SlotIndex;
        run.RemoveSlot(existingSlot.Id);
        var newSlot = run.InsertCabinetAt(slotIndex, command.CabinetId, command.NewNominalWidth);

        // Preserve depth: only update NominalWidth.
        var updatedCabinet = cabinet with { NominalWidth = command.NewNominalWidth };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.ResizeCabinet(command.CabinetId, command.NewNominalWidth)];
    }

    private static int ResolveTargetIndex(MoveCabinetCommand command, CabinetRun targetRun, bool isSameRunMove, int sourceIndex)
    {
        var index = command.TargetPlacement switch
        {
            DomainRunPlacement.StartOfRun => 0,
            DomainRunPlacement.EndOfRun => targetRun.Slots.Count,
            DomainRunPlacement.AtIndex when command.TargetIndex is int targetIndex => targetIndex,
            _ => throw new InvalidOperationException("Move command is missing a target index.")
        };

        return index;
    }
}
