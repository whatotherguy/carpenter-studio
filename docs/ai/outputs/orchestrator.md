# P4 — Resolution Orchestrator Design

Source: `cabinet_ai_prompt_pack_v3_final.md` (Phase 4)
Context: `commands.md`, `domain_model.md`

---

## 1. Goals

- Define the `ResolutionOrchestrator` as the **single choke point** for all design state changes
- Specify the 11-stage resolution pipeline with clear inputs, outputs, and responsibilities per stage
- Integrate the command system — commands in, results out, no exceptions
- Wire the Why Engine into every stage so that every decision is traceable
- Maintain deterministic, repeatable execution — same command + same state = same result
- Support both the **fast path** (drag-time preview) and **deep path** (commit-time full resolution)
- Keep the orchestrator UI-independent and fully testable

---

## 2. Design Decisions

| Decision | Rationale |
|---|---|
| Orchestrator owns execution, commands are data | Commands carry intent but no behavior. The orchestrator interprets, validates, and applies. This keeps the pipeline centralized and auditable |
| Pipeline stages are ordered and non-skippable | Every command traverses all 11 stages. Early stages may no-op for irrelevant commands, but they still run — ensures invariants are checked globally |
| Each stage receives a `ResolutionContext` and returns a `StageResult` | Uniform interface enables composition, testing, and explanation capture at every boundary |
| Fast path runs stages 1–3 only | During drag, only input capture, interaction interpretation, and spatial resolution execute. No engineering, parts, or manufacturing — just enough for preview |
| Deep path runs all 11 stages | After commit (drop, enter, menu confirm), the full pipeline executes deterministically |
| Explanation nodes are emitted per-stage, not post-hoc | Each stage records its decisions as they happen. Post-hoc explanation is fragile and drifts from reality |
| Pipeline is synchronous and single-threaded | Desktop app, single user. No concurrent command execution. Simplifies state management and eliminates race conditions |
| Stage failures halt the pipeline | If any stage produces an `Error` or `ManufactureBlocker` severity issue, subsequent stages do not execute. Partial results are never committed |
| Delta tracking wraps the entire pipeline | A `DeltaTracker` is initialized before stage 1 and finalized after stage 11. All entity mutations within the pipeline are captured automatically |

---

## 3. Architecture Overview

```
┌────────────────────────────────────────────────────────────────────┐
│  UI / Editor Layer                                                 │
│  (drag, drop, click, menu, keyboard, numeric entry)                │
└──────────────┬─────────────────────────────────────────────────────┘
               │ IDesignCommand
               ▼
┌────────────────────────────────────────────────────────────────────┐
│  ResolutionOrchestrator                                            │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  Pre-flight Validation                                       │  │
│  │  command.Validate() → reject if Error/ManufactureBlocker     │  │
│  └──────────────┬───────────────────────────────────────────────┘  │
│                 │                                                   │
│  ┌──────────────▼───────────────────────────────────────────────┐  │
│  │  DeltaTracker.Begin()                                        │  │
│  └──────────────┬───────────────────────────────────────────────┘  │
│                 │                                                   │
│  ┌──────────────▼───────────────────────────────────────────────┐  │
│  │  Stage 1:  Input Capture                                     │  │
│  │  Stage 2:  Interaction Interpretation                        │  │
│  │  Stage 3:  Spatial Resolution                                │  │
│  │  ─── fast path returns here ───                              │  │
│  │  Stage 4:  Engineering Resolution                            │  │
│  │  Stage 5:  Constraint Propagation                            │  │
│  │  Stage 6:  Part Generation                                   │  │
│  │  Stage 7:  Manufacturing Planning                            │  │
│  │  Stage 8:  Install Planning                                  │  │
│  │  Stage 9:  Costing                                           │  │
│  │  Stage 10: Validation                                        │  │
│  │  Stage 11: Packaging / Snapshot                              │  │
│  └──────────────┬───────────────────────────────────────────────┘  │
│                 │                                                   │
│  ┌──────────────▼───────────────────────────────────────────────┐  │
│  │  DeltaTracker.Finalize()                                     │  │
│  │  WhyEngine.RecordCommand()                                   │  │
│  │  UndoStack.Push()                                            │  │
│  └──────────────┬───────────────────────────────────────────────┘  │
│                 │                                                   │
│                 ▼                                                   │
│  CommandResult (success/failure, deltas, explanation IDs, issues)   │
└────────────────────────────────────────────────────────────────────┘
```

---

## 4. Core Interfaces (C#)

### 4.1 Resolution Context

The shared context object that flows through every pipeline stage. Accumulates state as stages execute.

```csharp
namespace CabinetDesigner.Application.Pipeline;

using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Mutable context passed through all pipeline stages.
/// Each stage reads from prior stages' outputs and writes its own.
/// Stage result properties throw InvalidOperationException if accessed before the
/// producing stage has executed — this catches ordering bugs at runtime rather than
/// silently operating on null data.
/// </summary>
public sealed class ResolutionContext
{
    // --- Input (immutable after construction) ---
    public required IDesignCommand Command { get; init; }
    public required ResolutionMode Mode { get; init; }

    // --- Stage results with guarded accessors ---
    private InputCaptureResult? _inputCapture;
    private InteractionInterpretationResult? _interpretation;
    private SpatialResolutionResult? _spatialResult;
    private EngineeringResolutionResult? _engineeringResult;
    private ConstraintPropagationResult? _constraintResult;
    private PartGenerationResult? _partResult;
    private ManufacturingPlanResult? _manufacturingResult;
    private InstallPlanResult? _installResult;
    private CostingResult? _costingResult;
    private ValidationResult? _validationResult;
    private PackagingResult? _packagingResult;

    public InputCaptureResult InputCapture
    {
        get => _inputCapture
            ?? throw new InvalidOperationException("Stage 1 (Input Capture) has not executed.");
        set => _inputCapture = value;
    }

    public InteractionInterpretationResult Interpretation
    {
        get => _interpretation
            ?? throw new InvalidOperationException("Stage 2 (Interaction Interpretation) has not executed.");
        set => _interpretation = value;
    }

    public SpatialResolutionResult SpatialResult
    {
        get => _spatialResult
            ?? throw new InvalidOperationException("Stage 3 (Spatial Resolution) has not executed.");
        set => _spatialResult = value;
    }

    public EngineeringResolutionResult EngineeringResult
    {
        get => _engineeringResult
            ?? throw new InvalidOperationException("Stage 4 (Engineering Resolution) has not executed.");
        set => _engineeringResult = value;
    }

    public ConstraintPropagationResult ConstraintResult
    {
        get => _constraintResult
            ?? throw new InvalidOperationException("Stage 5 (Constraint Propagation) has not executed.");
        set => _constraintResult = value;
    }

    public PartGenerationResult PartResult
    {
        get => _partResult
            ?? throw new InvalidOperationException("Stage 6 (Part Generation) has not executed.");
        set => _partResult = value;
    }

    public ManufacturingPlanResult ManufacturingResult
    {
        get => _manufacturingResult
            ?? throw new InvalidOperationException("Stage 7 (Manufacturing Planning) has not executed.");
        set => _manufacturingResult = value;
    }

    public InstallPlanResult InstallResult
    {
        get => _installResult
            ?? throw new InvalidOperationException("Stage 8 (Install Planning) has not executed.");
        set => _installResult = value;
    }

    public CostingResult CostingResult
    {
        get => _costingResult
            ?? throw new InvalidOperationException("Stage 9 (Costing) has not executed.");
        set => _costingResult = value;
    }

    public ValidationResult ValidationResult
    {
        get => _validationResult
            ?? throw new InvalidOperationException("Stage 10 (Validation) has not executed.");
        set => _validationResult = value;
    }

    public PackagingResult PackagingResult
    {
        get => _packagingResult
            ?? throw new InvalidOperationException("Stage 11 (Packaging) has not executed.");
        set => _packagingResult = value;
    }

    // --- Cross-cutting (mutable, accumulated) ---
    public List<ValidationIssue> AccumulatedIssues { get; } = [];
    public List<ExplanationNodeId> ExplanationNodeIds { get; } = [];

    /// <summary>True if any accumulated issue is Error or ManufactureBlocker.</summary>
    public bool HasBlockingIssues =>
        AccumulatedIssues.Any(i => i.Severity >= ValidationSeverity.Error);
}

public enum ResolutionMode
{
    /// <summary>Fast path: stages 1-3 only, for drag-time preview.</summary>
    Preview,

    /// <summary>Deep path: all 11 stages, for commit-time full resolution.</summary>
    Full
}
```

### 4.2 Pipeline Stage Interface

```csharp
namespace CabinetDesigner.Application.Pipeline;

/// <summary>
/// A single stage in the resolution pipeline.
/// Stages are executed in order. Each stage may read from prior stage results
/// on the context and must write its own result.
/// </summary>
public interface IResolutionStage
{
    /// <summary>Stage number (1-11). Used for ordering and logging.</summary>
    int StageNumber { get; }

    /// <summary>Human-readable stage name.</summary>
    string StageName { get; }

    /// <summary>
    /// Execute this stage against the given context.
    /// Must populate its result on the context.
    /// Returns a StageResult indicating success or failure.
    /// </summary>
    StageResult Execute(ResolutionContext context);

    /// <summary>
    /// Whether this stage should run in the given mode.
    /// Preview mode skips stages 4-11.
    /// </summary>
    bool ShouldExecute(ResolutionMode mode);
}
```

### 4.3 Stage Result

```csharp
namespace CabinetDesigner.Application.Pipeline;

using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

public sealed record StageResult
{
    public required int StageNumber { get; init; }
    public required bool Success { get; init; }
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    public required IReadOnlyList<ExplanationNodeId> ExplanationNodeIds { get; init; }

    public static StageResult Succeeded(
        int stageNumber,
        IReadOnlyList<ExplanationNodeId>? explanationNodeIds = null,
        IReadOnlyList<ValidationIssue>? warnings = null) => new()
    {
        StageNumber = stageNumber,
        Success = true,
        Issues = warnings ?? [],
        ExplanationNodeIds = explanationNodeIds ?? []
    };

    public static StageResult Failed(
        int stageNumber,
        IReadOnlyList<ValidationIssue> issues,
        IReadOnlyList<ExplanationNodeId>? explanationNodeIds = null) => new()
    {
        StageNumber = stageNumber,
        Success = false,
        Issues = issues,
        ExplanationNodeIds = explanationNodeIds ?? []
    };
}
```

### 4.4 Delta Tracker

```csharp
namespace CabinetDesigner.Application.Pipeline;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Tracks all entity mutations during a pipeline execution.
/// Wraps the entire pipeline — Begin() before stage 1, Finalize() after stage 11.
/// </summary>
public interface IDeltaTracker
{
    /// <summary>Begin tracking. Takes a snapshot of relevant state.</summary>
    void Begin();

    /// <summary>Record a mutation as it happens.</summary>
    void RecordDelta(StateDelta delta);

    /// <summary>Finalize tracking. Returns all captured deltas.</summary>
    IReadOnlyList<StateDelta> Finalize();
}
```

---

## 5. Resolution Orchestrator Implementation

### 5.1 Interface (from commands.md, expanded)

```csharp
namespace CabinetDesigner.Application;

using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;

/// <summary>
/// The single choke point for all design state changes.
/// Every IDesignCommand flows through here — no exceptions.
/// </summary>
public interface IResolutionOrchestrator
{
    /// <summary>
    /// Execute a design command through the full resolution pipeline (deep path).
    /// </summary>
    CommandResult Execute(IDesignCommand command);

    /// <summary>
    /// Execute a design command through the preview pipeline (fast path, stages 1-3).
    /// Used during drag operations for lightweight preview.
    /// Does NOT capture deltas, does NOT update undo stack.
    /// </summary>
    PreviewResult Preview(IDesignCommand command);

    /// <summary>Undo the most recent command.</summary>
    CommandResult? Undo();

    /// <summary>Redo the most recently undone command.</summary>
    CommandResult? Redo();
}
```

### 5.2 Preview Result

```csharp
namespace CabinetDesigner.Application.Pipeline;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Lightweight result from fast-path preview execution.
/// Contains only spatial layout information — no parts, manufacturing, or cost data.
/// </summary>
public sealed record PreviewResult
{
    public required bool Success { get; init; }
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    public required SpatialResolutionResult? SpatialResult { get; init; }

    public static PreviewResult Succeeded(SpatialResolutionResult spatialResult,
        IReadOnlyList<ValidationIssue>? warnings = null) => new()
    {
        Success = true,
        Issues = warnings ?? [],
        SpatialResult = spatialResult
    };

    public static PreviewResult Failed(IReadOnlyList<ValidationIssue> issues) => new()
    {
        Success = false,
        Issues = issues,
        SpatialResult = null
    };
}
```

### 5.3 Orchestrator Implementation

```csharp
namespace CabinetDesigner.Application;

using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Identifiers;

public sealed class ResolutionOrchestrator : IResolutionOrchestrator
{
    private readonly IReadOnlyList<IResolutionStage> _stages;
    private readonly IDeltaTracker _deltaTracker;
    private readonly IWhyEngine _whyEngine;
    private readonly IUndoStack _undoStack;

    /// <summary>
    /// Maximum recursion depth for system-generated commands (e.g., auto-filler
    /// triggers during resolution). Prevents circular command chains.
    /// </summary>
    private const int MaxRecursionDepth = 3;
    private int _currentRecursionDepth;

    public ResolutionOrchestrator(
        IReadOnlyList<IResolutionStage> stages,
        IDeltaTracker deltaTracker,
        IWhyEngine whyEngine,
        IUndoStack undoStack)
    {
        _stages = stages.OrderBy(s => s.StageNumber).ToList();
        _deltaTracker = deltaTracker;
        _whyEngine = whyEngine;
        _undoStack = undoStack;
    }

    public CommandResult Execute(IDesignCommand command)
    {
        // --- Guard: recursion depth ---
        if (_currentRecursionDepth >= MaxRecursionDepth)
            return CommandResult.Failed(command.Metadata, [
                new ValidationIssue(
                    ValidationSeverity.Error,
                    "MAX_RECURSION",
                    $"Command recursion depth exceeded ({MaxRecursionDepth}). " +
                    "Circular or deeply nested system commands detected.")
            ]);

        // --- 1. Pre-flight validation ---
        var preflightIssues = command.ValidateStructure();
        if (preflightIssues.Any(i => i.Severity >= ValidationSeverity.Error))
            return CommandResult.Rejected(command.Metadata, preflightIssues);

        // --- 2. Build context ---
        var context = new ResolutionContext
        {
            Command = command,
            Mode = ResolutionMode.Full
        };

        // Add any pre-flight warnings to accumulated issues
        context.AccumulatedIssues.AddRange(
            preflightIssues.Where(i => i.Severity < ValidationSeverity.Error));

        // --- 3. Begin delta tracking ---
        _deltaTracker.Begin();

        try
        {
            _currentRecursionDepth++;

            // --- 4. Execute pipeline stages ---
            foreach (var stage in _stages)
            {
                if (!stage.ShouldExecute(context.Mode))
                    continue;

                var stageResult = stage.Execute(context);

                // Accumulate issues and explanation nodes
                context.AccumulatedIssues.AddRange(stageResult.Issues);
                context.ExplanationNodeIds.AddRange(stageResult.ExplanationNodeIds);

                // Halt on failure
                if (!stageResult.Success || context.HasBlockingIssues)
                {
                    _deltaTracker.Finalize(); // discard deltas
                    return CommandResult.Failed(command.Metadata, context.AccumulatedIssues);
                }
            }

            // --- 5. Finalize deltas ---
            var deltas = _deltaTracker.Finalize();

            // --- 6. Record in Why Engine ---
            var explanationNodes = _whyEngine.RecordCommand(command, deltas);
            context.ExplanationNodeIds.AddRange(explanationNodes);

            // --- 7. Push to undo stack ---
            var undoEntry = new UndoEntry(
                command.Metadata, deltas, context.ExplanationNodeIds);
            _undoStack.Push(undoEntry);

            // --- 8. Return success ---
            return CommandResult.Succeeded(
                command.Metadata,
                deltas,
                context.ExplanationNodeIds,
                context.AccumulatedIssues.Count > 0 ? context.AccumulatedIssues : null);
        }
        finally
        {
            _currentRecursionDepth--;
        }
    }

    public PreviewResult Preview(IDesignCommand command)
    {
        // Pre-flight validation
        var preflightIssues = command.ValidateStructure();
        if (preflightIssues.Any(i => i.Severity >= ValidationSeverity.Error))
            return PreviewResult.Failed(preflightIssues);

        // Build context in preview mode
        var context = new ResolutionContext
        {
            Command = command,
            Mode = ResolutionMode.Preview
        };

        // Execute only stages that opt in to preview mode (stages 1-3)
        foreach (var stage in _stages)
        {
            if (!stage.ShouldExecute(context.Mode))
                continue;

            var stageResult = stage.Execute(context);
            context.AccumulatedIssues.AddRange(stageResult.Issues);

            if (!stageResult.Success || context.HasBlockingIssues)
                return PreviewResult.Failed(context.AccumulatedIssues);
        }

        // No delta tracking, no undo, no Why Engine for preview
        return PreviewResult.Succeeded(
            context.SpatialResult,
            context.AccumulatedIssues.Count > 0 ? context.AccumulatedIssues : null);
    }

    public CommandResult? Undo()
    {
        var entry = _undoStack.Undo();
        if (entry is null) return null;

        // Apply reverse deltas
        ApplyReverseDeltas(entry.Deltas);

        // Record undo in Why Engine
        _whyEngine.RecordUndo(entry.CommandMetadata, entry.Deltas);

        return CommandResult.Succeeded(
            entry.CommandMetadata,
            entry.Deltas,
            entry.ExplanationNodeIds);
    }

    public CommandResult? Redo()
    {
        var entry = _undoStack.Redo();
        if (entry is null) return null;

        // Re-apply forward deltas
        ApplyForwardDeltas(entry.Deltas);

        // Record redo in Why Engine
        _whyEngine.RecordRedo(entry.CommandMetadata, entry.Deltas);

        return CommandResult.Succeeded(
            entry.CommandMetadata,
            entry.Deltas,
            entry.ExplanationNodeIds);
    }

    // --- Delta application (undo/redo) ---

    private void ApplyReverseDeltas(IReadOnlyList<StateDelta> deltas)
    {
        // Process in reverse order to maintain referential integrity
        foreach (var delta in deltas.Reverse())
        {
            switch (delta.Operation)
            {
                case DeltaOperation.Created:
                    // Reverse of create = remove
                    _stateManager.RemoveEntity(delta.EntityId, delta.EntityType);
                    break;
                case DeltaOperation.Modified:
                    // Reverse of modify = restore previous values
                    _stateManager.RestoreValues(
                        delta.EntityId, delta.EntityType, delta.PreviousValues!);
                    break;
                case DeltaOperation.Removed:
                    // Reverse of remove = re-create
                    _stateManager.RestoreEntity(
                        delta.EntityId, delta.EntityType, delta.PreviousValues!);
                    break;
            }
        }
    }

    private void ApplyForwardDeltas(IReadOnlyList<StateDelta> deltas)
    {
        // Process in forward order
        foreach (var delta in deltas)
        {
            switch (delta.Operation)
            {
                case DeltaOperation.Created:
                    _stateManager.RestoreEntity(
                        delta.EntityId, delta.EntityType, delta.NewValues!);
                    break;
                case DeltaOperation.Modified:
                    _stateManager.RestoreValues(
                        delta.EntityId, delta.EntityType, delta.NewValues!);
                    break;
                case DeltaOperation.Removed:
                    _stateManager.RemoveEntity(delta.EntityId, delta.EntityType);
                    break;
            }
        }
    }
}
```

---

## 6. The 11 Pipeline Stages

### Stage 1: Input Capture

**Number:** 1
**Runs in:** Preview, Full
**Reads:** `IDesignCommand`
**Writes:** `InputCaptureResult` on context

Normalizes command parameters into a canonical form the pipeline can process. Resolves entity references (IDs → loaded entities), expands template references, and ensures all geometry values are in internal units.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed record InputCaptureResult
{
    /// <summary>
    /// Entities loaded by typed ID. Key is entity ID string; value is the loaded domain entity.
    /// All entries are non-null — missing entities cause Stage 1 to fail before this is populated.
    /// </summary>
    public required IReadOnlyDictionary<string, IDomainEntity> ResolvedEntities { get; init; }

    /// <summary>
    /// Normalized geometry parameters. Key is parameter name; value is an OverrideValue.
    /// All geometry is in internal units (inches) at this point.
    /// </summary>
    public required IReadOnlyDictionary<string, OverrideValue> NormalizedParameters { get; init; }

    /// <summary>Template expansions, if the command references templates.</summary>
    public IReadOnlyList<TemplateExpansion>? TemplateExpansions { get; init; }
}

/// <summary>Marker interface for domain entities surfaced to the pipeline.</summary>
public interface IDomainEntity
{
    string EntityId { get; }
    string EntityType { get; }
}

public sealed record TemplateExpansion(
    string TemplateId,
    IReadOnlyDictionary<string, OverrideValue> ExpandedParameters);
```

**Responsibilities:**
- Load entities by typed ID from the current state (e.g., resolve `RunId` → `CabinetRun`)
- Validate that referenced entities exist (contextual validation)
- Normalize geometry values to internal representation
- Expand template references into concrete parameters
- Reject the command if referenced entities are missing or state is inconsistent

**Explanation integration:**
- Emits `ExplanationNode` for any template expansion (what template → what parameters)
- Emits `ExplanationNode` if entity resolution required fallback or defaulting

---

### Stage 2: Interaction Interpretation

**Number:** 2
**Runs in:** Preview, Full
**Reads:** `InputCaptureResult`
**Writes:** `InteractionInterpretationResult` on context

Translates the abstract command intent into concrete operations on the domain model. This is where "add cabinet to end of run" becomes "insert at slot index 5 in run R-123."

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed record InteractionInterpretationResult
{
    /// <summary>Concrete domain operations to perform, in order.</summary>
    public required IReadOnlyList<DomainOperation> Operations { get; init; }
}

/// <summary>
/// A concrete domain operation produced by stage 2 interpretation.
/// Sealed hierarchy — no raw object parameters.
/// </summary>
public abstract record DomainOperation
{
    /// <summary>Insert a cabinet into a run slot at a specific index.</summary>
    public sealed record InsertSlot(
        RunId RunId,
        CabinetId CabinetId,
        Length Width,
        int SlotIndex) : DomainOperation;

    /// <summary>Remove a slot from a run.</summary>
    public sealed record RemoveSlot(
        RunId RunId,
        RunSlotId SlotId) : DomainOperation;

    /// <summary>Move a slot from one run to another.</summary>
    public sealed record MoveSlot(
        RunId SourceRunId,
        RunId TargetRunId,
        RunSlotId SlotId,
        int TargetIndex) : DomainOperation;

    /// <summary>Resize a cabinet already placed in a run.</summary>
    public sealed record ResizeCabinet(
        RunId RunId,
        CabinetId CabinetId,
        Length NewWidth) : DomainOperation;

    /// <summary>Insert a filler slot at a specific index.</summary>
    public sealed record InsertFiller(
        RunId RunId,
        Length Width,
        int SlotIndex) : DomainOperation;

    /// <summary>Update a run's capacity (e.g., after wall geometry changes).</summary>
    public sealed record UpdateRunCapacity(
        RunId RunId,
        Length NewCapacity) : DomainOperation;
}
```

**Responsibilities:**
- Map `RunPlacement.EndOfRun` → concrete slot index
- Resolve `MoveCabinetCommand` source/target positions
- Determine if the operation requires filler adjustments
- Identify affected neighbors for adjacency updates
- Produce an ordered list of `DomainOperation` objects that stages 3+ will apply

**Explanation integration:**
- Emits `ExplanationNode` for every interpretation decision (why this slot index, why this position)

---

### Stage 3: Spatial Resolution

**Number:** 3
**Runs in:** Preview, Full
**Reads:** `InteractionInterpretationResult`, domain state
**Writes:** `SpatialResolutionResult` on context

Applies the domain operations to the spatial model. Updates run slots, positions cabinets, resolves adjacency, and updates the layout graph.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record SpatialResolutionResult
{
    /// <summary>Updated slot positions within affected runs.</summary>
    public required IReadOnlyList<SlotPositionUpdate> SlotUpdates { get; init; }

    /// <summary>Adjacency relationships that changed.</summary>
    public required IReadOnlyList<AdjacencyChange> AdjacencyChanges { get; init; }

    /// <summary>Run-level summaries (remaining length, slot count, etc.).</summary>
    public required IReadOnlyList<RunSummary> AffectedRuns { get; init; }
}

/// <summary>
/// World-space placement of a run. Owned by the spatial layer.
/// CabinetRun itself does NOT store StartPoint/EndPoint — this is the canonical source.
/// </summary>
public sealed record RunPlacement(
    RunId RunId,
    Point2D StartPoint,
    Point2D EndPoint)
{
    public Length Length => StartPoint.DistanceTo(EndPoint);
    public Vector2D Direction => (EndPoint - StartPoint).Normalized();

    /// <summary>Compute world-space position from a run-local offset.</summary>
    public Point2D PositionAtOffset(Length offset) =>
        StartPoint + Direction * offset.Inches;
}

public sealed record SlotPositionUpdate(
    RunSlotId SlotId,
    RunId RunId,
    int NewIndex,
    Point2D WorldPosition,
    Length OccupiedWidth);

public sealed record AdjacencyChange(
    CabinetId CabinetId,
    CabinetId? LeftNeighborId,
    CabinetId? RightNeighborId);

public sealed record RunSummary(
    RunId RunId,
    Length Capacity,
    Length OccupiedLength,
    Length RemainingLength,
    int SlotCount);
```

**Responsibilities:**
- Execute slot insertions, removals, and reordering on `CabinetRun`
- Compute world-space positions for all affected slots
- Update adjacency graph (left/right neighbors per cabinet)
- Validate that slots fit within run length (invariant enforcement)
- Produce spatial preview data (used by fast path for drag feedback)

**Explanation integration:**
- Emits `ExplanationNode` for slot placement decisions
- Emits `ExplanationNode` for adjacency changes and why they occurred

**This is where the fast path (Preview mode) returns.** Stages 4-11 only execute in Full mode.

---

### Stage 4: Engineering Resolution

**Number:** 4
**Runs in:** Full only
**Reads:** `SpatialResolutionResult`, cabinet types, domain state
**Writes:** `EngineeringResolutionResult` on context

Resolves the constructible assembly for each affected cabinet. Determines openings, door/drawer assignments, filler requirements, reveal calculations, and construction method details.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record EngineeringResolutionResult
{
    /// <summary>Assembly resolutions for affected cabinets.</summary>
    public required IReadOnlyList<AssemblyResolution> Assemblies { get; init; }

    /// <summary>Filler requirements identified by the run engine.</summary>
    public required IReadOnlyList<FillerRequirement> Fillers { get; init; }

    /// <summary>End condition updates for affected runs.</summary>
    public required IReadOnlyList<EndConditionUpdate> EndConditionUpdates { get; init; }
}

public sealed record AssemblyResolution(
    CabinetId CabinetId,
    IReadOnlyList<OpeningResolution> Openings,
    Length ResolvedWidth,
    Length ResolvedDepth,
    Length ResolvedHeight);

public sealed record OpeningResolution(
    OpeningId OpeningId,
    OpeningType ResolvedType,
    Length Width,
    Length Height);

public sealed record FillerRequirement(
    RunId RunId,
    int SlotIndex,
    Length RequiredWidth,
    string Reason);

public sealed record EndConditionUpdate(
    RunId RunId,
    EndConditionType LeftType,
    Length? LeftFillerWidth,
    EndConditionType RightType,
    Length? RightFillerWidth);
```

**Responsibilities:**
- Resolve cabinet openings based on type, width, and construction method
- Calculate reveals (overlay amounts) based on construction method (frameless vs face-frame)
- Determine filler requirements where cabinets don't fill the run exactly
- Update run end conditions based on wall proximity and adjacent runs
- Apply shared-stile logic for adjacent frameless cabinets

**Explanation integration:**
- Emits `ExplanationNode` for every assembly decision (why this opening layout, why this reveal)
- Emits `ExplanationNode` for filler insertion rationale
- Emits `ExplanationNode` for end condition determination

---

### Stage 5: Constraint Propagation

**Number:** 5
**Runs in:** Full only
**Reads:** `EngineeringResolutionResult`, material catalog, hardware catalog
**Writes:** `ConstraintPropagationResult` on context

Propagates material and hardware constraints across the resolved assemblies. Ensures material thicknesses, grain directions, hardware clearances, and compatibility rules are all satisfied.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record ConstraintPropagationResult
{
    /// <summary>Material assignments for all parts.</summary>
    public required IReadOnlyList<MaterialAssignment> MaterialAssignments { get; init; }

    /// <summary>Hardware assignments for all openings.</summary>
    public required IReadOnlyList<HardwareAssignment> HardwareAssignments { get; init; }

    /// <summary>Constraint violations that are warnings (not blocking).</summary>
    public required IReadOnlyList<ConstraintViolation> Violations { get; init; }
}

public sealed record MaterialAssignment(
    string PartId,
    MaterialId MaterialId,
    Thickness ResolvedThickness,
    GrainDirection GrainDirection);

public sealed record HardwareAssignment(
    OpeningId OpeningId,
    IReadOnlyList<HardwareItemId> HardwareIds,
    BoringPattern? BoringPattern);

public sealed record ConstraintViolation(
    string ConstraintCode,
    string Message,
    ValidationSeverity Severity,
    IReadOnlyList<string> AffectedEntityIds);
```

**Responsibilities:**
- Assign materials to cabinet parts based on style presets, shop standards, and overrides
- Resolve actual thickness from nominal (material catalog lookup)
- Validate grain direction compatibility
- Assign hardware to openings based on type, size, and compatibility rules
- Propagate boring patterns from hardware to parts
- Detect and report constraint violations (e.g., hinge incompatible with door width)

**Explanation integration:**
- Emits `ExplanationNode` for every material assignment (why this material for this part)
- Emits `ExplanationNode` for hardware selection rationale
- Emits `ExplanationNode` for constraint violations with suggested remediation

---

### Stage 6: Part Generation

**Number:** 6
**Runs in:** Full only
**Reads:** `EngineeringResolutionResult`, `ConstraintPropagationResult`
**Writes:** `PartGenerationResult` on context

Generates the concrete part list for each affected assembly. Each part has geometry, material, edge treatment, and labeling.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record PartGenerationResult
{
    /// <summary>All generated parts for affected assemblies.</summary>
    public required IReadOnlyList<GeneratedPart> Parts { get; init; }
}

public sealed record GeneratedPart
{
    public required string PartId { get; init; }
    public required CabinetId CabinetId { get; init; }
    public required string PartType { get; init; }  // e.g., "left_side", "bottom", "back", "shelf"
    public required Length Width { get; init; }
    public required Length Height { get; init; }
    public required Thickness MaterialThickness { get; init; }
    public required MaterialId MaterialId { get; init; }
    public required EdgeTreatment Edges { get; init; }
    public required string Label { get; init; }
}

public sealed record EdgeTreatment(
    string? TopEdgeBandingId,
    string? BottomEdgeBandingId,
    string? LeftEdgeBandingId,
    string? RightEdgeBandingId);
```

**Responsibilities:**
- Generate parts (sides, top, bottom, back, shelves, doors, drawer fronts) from assembly resolution
- Apply material assignments from constraint propagation
- Determine edge treatment per edge (exposed edges get banding)
- Generate part labels (cabinet ID + part type + index)
- Account for material thickness in joinery dimensions (dado depths, rabbet widths)

**Explanation integration:**
- Emits `ExplanationNode` for edge treatment decisions (why this edge gets banding)
- Emits `ExplanationNode` for dimension adjustments due to material thickness

---

### Stage 7: Manufacturing Planning

**Number:** 7
**Runs in:** Full only
**Reads:** `PartGenerationResult`, material catalog
**Writes:** `ManufacturingPlanResult` on context

Generates manufacturing artifacts: cut lists, CNC operations, nesting layouts, and workflow assignments.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Geometry;

public sealed record ManufacturingPlanResult
{
    /// <summary>Cut list entries grouped by material.</summary>
    public required IReadOnlyList<CutListEntry> CutList { get; init; }

    /// <summary>CNC machining operations (boring, routing, edging).</summary>
    public required IReadOnlyList<MachiningOperation> MachiningOps { get; init; }

    /// <summary>Sheet nesting results (for panel optimization).</summary>
    public required IReadOnlyList<NestingResult> Nesting { get; init; }
}

public sealed record CutListEntry(
    string PartId,
    string MaterialName,
    Length CutWidth,
    Length CutHeight,
    Length Kerf,
    string GrainNote);

public sealed record MachiningOperation(
    string PartId,
    string OperationType,   // "bore", "route", "edge_band"
    IReadOnlyDictionary<string, OverrideValue> Parameters);

public sealed record NestingResult(
    string MaterialName,
    Length SheetWidth,
    Length SheetHeight,
    IReadOnlyList<string> PartIds,
    decimal YieldPercentage);
```

**Responsibilities:**
- Generate cut list with kerf allowance from part dimensions
- Plan CNC operations (hinge boring, shelf pin holes, edge routing)
- Optimize sheet nesting for material yield
- Assign grain direction constraints to nesting
- Report material waste and yield metrics

**Explanation integration:**
- Emits `ExplanationNode` for kerf adjustments
- Emits `ExplanationNode` for nesting decisions and material yield

---

### Stage 8: Install Planning

**Number:** 8
**Runs in:** Full only
**Reads:** `SpatialResolutionResult`, `EngineeringResolutionResult`
**Writes:** `InstallPlanResult` on context

Plans the installation sequence: dependency ordering, fastening requirements, stud/blocking locations, access clearances.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Identifiers;

public sealed record InstallPlanResult
{
    /// <summary>Cabinets in install order (dependency-sorted).</summary>
    public required IReadOnlyList<InstallStep> InstallSequence { get; init; }

    /// <summary>Fastening requirements per cabinet.</summary>
    public required IReadOnlyList<FasteningRequirement> FasteningRequirements { get; init; }
}

public sealed record InstallStep(
    int Order,
    CabinetId CabinetId,
    IReadOnlyList<CabinetId> DependsOn,
    string InstallNotes);

public sealed record FasteningRequirement(
    CabinetId CabinetId,
    string FasteningType,   // "wall_screw", "cabinet_to_cabinet", "floor_anchor"
    string Location,
    string Requirements);   // e.g., "must hit stud or use toggle bolt"
```

**Responsibilities:**
- Topological sort of cabinets by install dependency (e.g., wall uppers before lowers in some workflows)
- Identify fastening points and requirements
- Check stud/blocking alignment for wall-mounted cabinets
- Verify access clearances (can installers reach fastening points?)
- Generate install notes per cabinet

**Explanation integration:**
- Emits `ExplanationNode` for install order decisions
- Emits `ExplanationNode` for fastening requirement rationale

---

### Stage 9: Costing

**Number:** 9
**Runs in:** Full only
**Reads:** `PartGenerationResult`, `ManufacturingPlanResult`, material/hardware catalogs
**Writes:** `CostingResult` on context

Calculates cost estimates: material costs, labor, hardware, installation, taxes, markup, and revision deltas.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

public sealed record CostingResult
{
    public required decimal MaterialCost { get; init; }
    public required decimal HardwareCost { get; init; }
    public required decimal LaborCost { get; init; }
    public required decimal InstallCost { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Markup { get; init; }
    public required decimal Tax { get; init; }
    public required decimal Total { get; init; }

    /// <summary>Cost delta from previous revision (if applicable).</summary>
    public CostDelta? RevisionDelta { get; init; }

    /// <summary>Per-cabinet cost breakdown.</summary>
    public required IReadOnlyList<CabinetCostBreakdown> CabinetBreakdowns { get; init; }
}

public sealed record CostDelta(
    decimal PreviousTotal,
    decimal CurrentTotal,
    decimal Difference,
    string Summary);

public sealed record CabinetCostBreakdown(
    string CabinetId,
    decimal MaterialCost,
    decimal HardwareCost,
    decimal LaborCost);
```

**Responsibilities:**
- Calculate material costs from part list + material catalog pricing
- Calculate hardware costs from assignments
- Estimate labor hours based on assembly complexity
- Estimate installation cost
- Apply markup and tax rules from shop standards
- Compute revision delta (how cost changed from the previous snapshot)

**Explanation integration:**
- Emits `ExplanationNode` for cost calculation methodology
- Emits `ExplanationNode` for revision delta breakdown

---

### Stage 10: Validation

**Number:** 10
**Runs in:** Full only
**Reads:** All prior stage results
**Writes:** `ValidationResult` on context

Cross-cutting validation across all resolved layers. Checks for collisions, compatibility issues, manufacturability problems, and installability concerns.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Commands;

public sealed record ValidationResult
{
    /// <summary>All validation issues found across all layers.</summary>
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }

    /// <summary>True if no Error or ManufactureBlocker issues exist.</summary>
    public bool IsValid => !Issues.Any(i => i.Severity >= ValidationSeverity.Error);
}
```

**Validation checks include:**

| Category | Example Checks |
|---|---|
| **Collision** | Cabinet-to-cabinet overlap, cabinet-to-opening conflict, cabinet-to-obstacle interference |
| **Compatibility** | Hardware fits opening dimensions, material thickness supports construction method |
| **Manufacturability** | Part dimensions within CNC capacity, grain direction possible for material, kerf doesn't consume part |
| **Installability** | Fastening possible at planned locations, access clearance sufficient, weight within mounting capacity |
| **Completeness** | All openings have hardware assigned, all exposed edges have treatment specified |

**Responsibilities:**
- Run all validation rules against the fully resolved state
- Classify each issue by severity (`Info`, `Warning`, `Error`, `ManufactureBlocker`)
- Provide fix suggestions where possible
- Report affected entity IDs per issue for UI highlighting

**Explanation integration:**
- Emits `ExplanationNode` for each validation failure with the rule that triggered it
- Links validation issues back to the stage that produced the problematic state

---

### Stage 11: Packaging / Snapshot

**Number:** 11
**Runs in:** Full only
**Reads:** All prior stage results, `ValidationResult`
**Writes:** `PackagingResult` on context

Freezes the fully resolved state into an immutable snapshot and binds it to the current revision.

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Identifiers;

public sealed record PackagingResult
{
    /// <summary>The immutable snapshot ID for this resolution.</summary>
    public required string SnapshotId { get; init; }

    /// <summary>The revision this snapshot is bound to.</summary>
    public required RevisionId RevisionId { get; init; }

    /// <summary>Timestamp of snapshot creation.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Hash of the snapshot contents for integrity verification.</summary>
    public required string ContentHash { get; init; }

    /// <summary>Summary statistics for the snapshot.</summary>
    public required SnapshotSummary Summary { get; init; }
}

public sealed record SnapshotSummary(
    int CabinetCount,
    int RunCount,
    int PartCount,
    int ValidationIssueCount,
    decimal TotalCost);
```

**Responsibilities:**
- Serialize the complete resolved state (spatial, engineering, parts, manufacturing, install, cost)
- Compute a content hash for integrity verification
- Bind the snapshot to the revision ID
- If the revision is being approved, mark the snapshot as the approved state
- Produce summary statistics for quick display

**Explanation integration:**
- Emits `ExplanationNode` linking the snapshot to the command that produced it
- Records the full lineage: command → stages → decisions → snapshot

---

## 7. Stage Execution Summary

| # | Stage | Preview | Full | Input | Output |
|---|---|---|---|---|---|
| 1 | Input Capture | Yes | Yes | `IDesignCommand` | `InputCaptureResult` |
| 2 | Interaction Interpretation | Yes | Yes | `InputCaptureResult` | `InteractionInterpretationResult` |
| 3 | Spatial Resolution | Yes | Yes | `InteractionInterpretationResult` | `SpatialResolutionResult` |
| 4 | Engineering Resolution | No | Yes | `SpatialResolutionResult` | `EngineeringResolutionResult` |
| 5 | Constraint Propagation | No | Yes | `EngineeringResolutionResult` | `ConstraintPropagationResult` |
| 6 | Part Generation | No | Yes | Engineering + Constraints | `PartGenerationResult` |
| 7 | Manufacturing Planning | No | Yes | `PartGenerationResult` | `ManufacturingPlanResult` |
| 8 | Install Planning | No | Yes | Spatial + Engineering | `InstallPlanResult` |
| 9 | Costing | No | Yes | Parts + Manufacturing | `CostingResult` |
| 10 | Validation | No | Yes | All prior results | `ValidationResult` |
| 11 | Packaging / Snapshot | No | Yes | All prior results | `PackagingResult` |

---

## 8. Why Engine Integration Contract

The orchestrator integrates with the Why Engine at three levels.

> **Interface definition:** `IWhyEngine` is fully specified in `why_engine.md` section 5.
> Do not duplicate it here. The orchestrator consumes the interface; `why_engine.md` owns it.

### 8.1 Per-Stage Explanation

After all stages complete, the orchestrator calls `WhyEngine.RecordCommand()` to create the top-level explanation node that links the command to all stage-level decisions and the resulting state deltas.

### 8.3 Explanation Graph Structure

```
CommandExplanation (root)
 ├── command metadata (who, when, why)
 ├── Stage 1 decisions
 │    ├── entity resolution
 │    └── template expansion
 ├── Stage 2 decisions
 │    └── interpretation choices
 ├── Stage 3 decisions
 │    ├── slot placement
 │    └── adjacency changes
 ├── ...stages 4-11...
 ├── state deltas (what changed)
 └── validation issues (what was flagged)
```

---

## 9. Error Handling Strategy

| Scenario | Behavior |
|---|---|
| **Pre-flight validation fails** | Return `CommandResult.Rejected()` immediately. No pipeline execution, no deltas, no undo entry |
| **Stage fails (Error/ManufactureBlocker)** | Halt pipeline. Discard deltas via `DeltaTracker.Finalize()`. Return `CommandResult.Failed()` with accumulated issues |
| **Stage produces warnings** | Accumulate on context. Continue pipeline. Warnings appear in final `CommandResult` |
| **Referenced entity not found (Stage 1)** | Stage 1 fails with contextual validation error. Pipeline halts |
| **Slot doesn't fit in run (Stage 3)** | Stage 3 fails with spatial validation error. Pipeline halts |
| **Constraint violation (Stage 5)** | Violations classified by severity. `Warning` = continue. `Error` = halt |
| **Manufacturing impossible (Stage 7)** | `ManufactureBlocker` severity. Pipeline halts. Issue includes remediation suggestion |
| **Recursion depth exceeded** | Immediate rejection before pipeline starts. Protects against circular system commands |
| **Unexpected exception** | Caught at orchestrator level. Delta tracker finalized (discarded). Returns `CommandResult.Failed()` with internal error issue. Exception logged |

---

## 10. Fast Path vs Deep Path

### 10.1 When to Use Each

| Scenario | Path | Stages |
|---|---|---|
| Drag in progress | Preview (fast) | 1–3 |
| Drop / commit | Full (deep) | 1–11 |
| Menu action confirmed | Full (deep) | 1–11 |
| Keyboard shortcut (e.g., resize) | Full (deep) | 1–11 |
| Undo / Redo | Delta application | N/A (no pipeline) |

### 10.2 Fast Path Guarantees

- **No state mutation**: Preview does not modify the authoritative state. It computes spatial layout on a scratch copy or lightweight graph
- **No undo entry**: Preview results are ephemeral
- **No Why Engine recording**: No explanation nodes for preview
- **No delta tracking**: No `DeltaTracker` for preview
- **Sub-frame latency target**: Stages 1-3 must complete within ~4ms for 60fps interaction

### 10.3 State Divergence

The system maintains two state representations:

| State | Updated By | Purpose |
|---|---|---|
| `LightweightLayoutGraph` | Preview (fast path) | Drag feedback, snap candidates, visual hints |
| `ResolvedProject` | Full resolution (deep path) | Authoritative state, undo history, manufacturing output |

After a commit (deep path), the lightweight graph is re-synced from the authoritative state.

---

## 11. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| **Pipeline stage ordering dependency** | Stages are explicitly numbered and sorted. The `IResolutionStage.StageNumber` property enforces ordering. Adding a new stage between existing ones requires careful numbering |
| **Stage results growing unbounded** | Each `ResolutionContext` is created per-command and garbage collected after the `CommandResult` is returned. No long-lived accumulation |
| **Preview/Full divergence** | The lightweight layout graph is always re-synced after full resolution. Preview never writes to authoritative state |
| **Slow pipeline blocking UI** | Single-threaded execution is by design for correctness. If stages 7-9 (manufacturing/costing) become slow, consider async execution with a loading indicator. Never compromise stage ordering |
| **Stage failure leaves partial state** | Delta tracker wraps the entire pipeline. On failure, `Finalize()` is called but deltas are discarded — no partial commit is possible |
| **System commands triggering re-entry** | Recursion depth limit (configurable, default 3) prevents infinite loops. System-generated commands (e.g., auto-filler from stage 4) re-enter `Execute()` with incremented depth |
| **Template expansion complexity** | Stage 1 expands templates before pipeline proceeds. Complex templates that generate multiple commands are decomposed into individual operations, not recursive command submissions |
| **Validation stage vs per-stage validation** | Stage 10 is cross-cutting (checks interactions between layers). Per-stage validation catches stage-specific issues. Both are needed — they serve different purposes |
| **Undo after non-trivial pipeline changes** | Undo applies reverse deltas, not re-running the pipeline backwards. This is correct because deltas capture the exact prior state. However, if domain logic has side effects not captured in deltas (should not happen by design), undo could diverge |
| **Cost calculation precision** | Costing uses `decimal` for monetary values. All intermediate calculations maintain full precision. Rounding happens only at display time |
