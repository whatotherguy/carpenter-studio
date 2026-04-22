using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CabinetDesigner.Application.Pipeline.StageResults;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
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
    private readonly ICatalogService? _catalogService;

    public InteractionInterpretationStage()
        : this(new InMemoryDeltaTracker(), new InMemoryDesignStateStore(), null)
    {
    }

    public InteractionInterpretationStage(IDeltaTracker deltaTracker, IDesignStateStore stateStore, ICatalogService? catalogService = null)
    {
        _deltaTracker = deltaTracker ?? throw new ArgumentNullException(nameof(deltaTracker));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _catalogService = catalogService;
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
                InsertCabinetIntoRunCommand insertCabinet => ExecuteInsertCabinet(insertCabinet, context),
                MoveCabinetCommand moveCabinet => ExecuteMoveCabinet(moveCabinet, context),
                ResizeCabinetCommand resizeCabinet => ExecuteResizeCabinet(resizeCabinet, context),
                SetCabinetConstructionCommand setCabinetConstruction => ExecuteSetCabinetConstruction(setCabinetConstruction, context),
                SetCabinetCategoryCommand setCabinetCategory => ExecuteSetCabinetCategory(setCabinetCategory, context),
                AddOpeningCommand addOpening => ExecuteAddOpening(addOpening, context),
                RemoveOpeningCommand removeOpening => ExecuteRemoveOpening(removeOpening, context),
                ReorderOpeningCommand reorderOpening => ExecuteReorderOpening(reorderOpening, context),
                DeleteRunCommand deleteRun => ExecuteDeleteRun(deleteRun, context),
                SetCabinetOverrideCommand setCabinetOverride => ExecuteSetCabinetOverride(setCabinetOverride, context),
                RemoveCabinetOverrideCommand removeCabinetOverride => ExecuteRemoveCabinetOverride(removeCabinetOverride, context),
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
        catch (InteractionFailureException exception)
        {
            return StageResult.Failed(StageNumber, [new ValidationIssue(ValidationSeverity.Error, exception.Code, exception.Message)]);
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
            slot.Id,
            command.Category,
            command.Construction);
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

    private IReadOnlyList<DomainOperation> ExecuteInsertCabinet(InsertCabinetIntoRunCommand command, ResolutionContext context)
    {
        var run = _stateStore.GetRun(command.RunId)
            ?? throw new InvalidOperationException($"Run {command.RunId} not found.");

        var previousRunValues = _stateStore.CaptureRunValues(run);
        var cabinetId = CabinetId.New();
        var slot = run.InsertCabinetAt(command.InsertAtIndex, cabinetId, command.NominalWidth);

        var cabinet = new CabinetStateRecord(
            cabinetId,
            command.CabinetTypeId,
            command.NominalWidth,
            command.NominalDepth,
            run.Id,
            slot.Id,
            command.Category,
            command.Construction);
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

        var previousRunValues = _stateStore.CaptureRunValues(run);
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);

        // Remove the old slot and re-insert at the same index with the new width.
        var slotIndex = existingSlot.SlotIndex;
        run.RemoveSlot(existingSlot.Id);
        var newSlot = run.InsertCabinetAt(slotIndex, command.CabinetId, command.NewWidth, CreateStableRunSlotId(command.CabinetId, run.Id, slotIndex, command.NewWidth));

        // Preserve depth while updating the cabinet to reference the replacement slot.
        var updatedCabinet = command.HasExplicitDimensions
            ? cabinet with { NominalWidth = command.NewWidth, NominalDepth = command.NewDepth, NominalHeight = command.NewHeight, SlotId = newSlot.Id }
            : cabinet with { NominalWidth = command.NewWidth, SlotId = newSlot.Id };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            run.Id.Value.ToString(),
            "CabinetRun",
            DeltaOperation.Modified,
            previousRunValues,
            _stateStore.CaptureRunValues(run)));

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.ResizeCabinet(command.CabinetId, command.NewWidth)];
    }

    private IReadOnlyList<DomainOperation> ExecuteSetCabinetConstruction(SetCabinetConstructionCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);
        var updatedCabinet = ApplyCatalogMatch(cabinet, cabinet.Category, command.Construction)
            ?? cabinet with
        {
            Construction = command.Construction
        };

        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.None()];
    }

    private IReadOnlyList<DomainOperation> ExecuteSetCabinetCategory(SetCabinetCategoryCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);
        var updatedCabinet = ApplyCatalogMatch(cabinet, command.Category, cabinet.Construction);
        if (updatedCabinet is null)
        {
            throw new InteractionFailureException("CABINET_CATEGORY_INVALID", $"Cabinet {cabinet.CabinetId} could not be rebuilt as {command.Category} with its current dimensions.");
        }

        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.None()];
    }

    private IReadOnlyList<DomainOperation> ExecuteAddOpening(AddOpeningCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);
        var openings = cabinet.EffectiveOpenings.ToList();
        var insertIndex = command.InsertIndex is null ? openings.Count : Math.Clamp(command.InsertIndex.Value, 0, openings.Count);
        openings.Insert(insertIndex, new CabinetOpeningStateRecord(CreateStableOpeningId(cabinet.CabinetId, insertIndex).Value, insertIndex, command.OpeningType, command.Width, command.Height));
        var updatedCabinet = cabinet with { Openings = ReindexOpenings(openings) };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.None()];
    }

    private IReadOnlyList<DomainOperation> ExecuteRemoveOpening(RemoveOpeningCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);
        var openings = cabinet.EffectiveOpenings.Where(opening => opening.OpeningId != command.OpeningId.Value).ToList();
        var updatedCabinet = cabinet with { Openings = ReindexOpenings(openings) };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.None()];
    }

    private IReadOnlyList<DomainOperation> ExecuteReorderOpening(ReorderOpeningCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);
        var openings = cabinet.EffectiveOpenings.OrderBy(opening => opening.Index).ToList();
        var opening = openings.FirstOrDefault(candidate => candidate.OpeningId == command.OpeningId.Value)
            ?? throw new InteractionFailureException("OPENING_NOT_FOUND", $"Opening {command.OpeningId} was not found.");
        openings.Remove(opening);
        openings.Insert(Math.Clamp(command.NewIndex, 0, openings.Count), opening);
        var updatedCabinet = cabinet with { Openings = ReindexOpenings(openings) };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.None()];
    }

    private IReadOnlyList<DomainOperation> ExecuteDeleteRun(DeleteRunCommand command, ResolutionContext context)
    {
        var run = ((ResolvedRunEntity)context.InputCapture.ResolvedEntities["run"]).Run;
        if (run.Slots.Count > 0)
        {
            throw new InteractionFailureException("RUN_NOT_EMPTY", $"Run {command.RunId} cannot be deleted because it is not empty.");
        }

        var previousRunValues = _stateStore.CaptureRunValues(run);
        _stateStore.RemoveRun(run.Id);

        _deltaTracker.RecordDelta(new StateDelta(
            run.Id.Value.ToString(),
            "CabinetRun",
            DeltaOperation.Removed,
            previousRunValues));

        return [new DomainOperation.DeleteRun(run.Id)];
    }

    private IReadOnlyList<DomainOperation> ExecuteSetCabinetOverride(SetCabinetOverrideCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);
        var updatedOverrides = new Dictionary<string, OverrideValue>(cabinet.EffectiveOverrides, StringComparer.Ordinal)
        {
            [command.OverrideKey] = command.Value
        };
        var updatedCabinet = cabinet with { Overrides = updatedOverrides };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.SetCabinetOverride(command.CabinetId, command.OverrideKey)];
    }

    private IReadOnlyList<DomainOperation> ExecuteRemoveCabinetOverride(RemoveCabinetOverrideCommand command, ResolutionContext context)
    {
        var cabinet = ((ResolvedCabinetEntity)context.InputCapture.ResolvedEntities["cabinet"]).Cabinet;
        var previousCabinetValues = _stateStore.CaptureCabinetValues(cabinet);
        var updatedOverrides = new Dictionary<string, OverrideValue>(cabinet.EffectiveOverrides, StringComparer.Ordinal);
        updatedOverrides.Remove(command.OverrideKey);
        var updatedCabinet = cabinet with { Overrides = updatedOverrides };
        _stateStore.UpdateCabinet(updatedCabinet);

        _deltaTracker.RecordDelta(new StateDelta(
            command.CabinetId.Value.ToString(),
            "Cabinet",
            DeltaOperation.Modified,
            previousCabinetValues,
            _stateStore.CaptureCabinetValues(updatedCabinet)));

        return [new DomainOperation.None()];
    }

    private CabinetStateRecord? ApplyCatalogMatch(CabinetStateRecord cabinet, CabinetCategory category, ConstructionMethod construction)
    {
        if (_catalogService is null)
        {
            return cabinet with { Category = category, Construction = construction };
        }

        var match = _catalogService.GetAllItems().FirstOrDefault(item =>
            item.Category == category.ToString() &&
            item.ConstructionMethod == construction &&
            item.NominalWidth == cabinet.NominalWidth &&
            item.Depth == cabinet.NominalDepth &&
            item.Height == cabinet.EffectiveNominalHeight);

        return match is null
            ? null
            : cabinet with
            {
                CabinetTypeId = match.TypeId,
                Category = category,
                Construction = construction,
                DefaultOpeningCount = match.DefaultOpenings
            };
    }

    private static IReadOnlyList<CabinetOpeningStateRecord> ReindexOpenings(IReadOnlyList<CabinetOpeningStateRecord> openings) =>
        openings.Select((opening, index) => opening with { Index = index }).ToArray();

    private static OpeningId CreateStableOpeningId(CabinetId cabinetId, int ordinal)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"opening:{cabinetId.Value:D}:{ordinal}");
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return new OpeningId(new Guid(hashBytes.AsSpan(0, 16)));
    }

    private static RunSlotId CreateStableRunSlotId(CabinetId cabinetId, RunId runId, int slotIndex, Length width)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"slot:{cabinetId.Value:D}:{runId.Value:D}:{slotIndex}:{width.Inches:0.###}");
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return new RunSlotId(new Guid(hashBytes.AsSpan(0, 16)));
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

    private sealed class InteractionFailureException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
