# P2 — Core Command System Design

Source: `cabinet_ai_implementation_playbook_v1_1.md` (Phase 2)
Context: `architecture_summary.md`, `geometry_system.md`

---

## 1. Goals

- Define the single mechanism through which all design state changes are expressed
- Capture **user intent** (not low-level mutations) as first-class, immutable command objects
- Provide the contract consumed by `ResolutionOrchestrator` — commands in, results out
- Seed the Why Engine with structured intent metadata on every state change
- Enable deterministic, auditable undo/redo without state drift
- Keep commands UI-independent, serializable, and fully testable
- Enforce geometry value objects (no primitives) across all command parameters

---

## 2. Design Decisions

| Decision | Rationale |
|---|---|
| Commands are immutable record types | Determinism, safe undo history, thread safety, serialization friendliness |
| Two command hierarchies: `IDesignCommand` / `IEditorCommand` | Design commands flow through the resolution pipeline; editor commands affect interaction state only (selection, zoom, mode) and never touch domain state. Within `IDesignCommand`, `CommandOrigin` classifies commands as `User`, `Editor`, `System`, `Template`, `Undo`, or `Redo`. |
| Commands carry structured metadata, not just parameters | Feeds the Why Engine — every state change must explain itself. `ParentCommandId` links child commands (Editor- and System-originated) to their parent user command; all share one `UndoEntry`. |
| Commands do NOT execute themselves | No `Execute()` method on commands. The orchestrator owns execution. Commands are data, not behavior |
| Delta-based undo with command journaling (hybrid) | Commands are journaled for audit; undo applies reverse deltas captured during execution. Avoids replaying entire history while maintaining full traceability |
| Command validation is a separate pre-flight step | Validation runs before orchestrator commits — commands can be rejected without side effects |
| Entity references use typed IDs, not object references | Commands reference entities by `CabinetId`, `RunId`, etc. — never by object reference. Enables serialization, journaling, and cross-session replay |

---

## 3. Command Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Editor / UI Layer                                      │
│  (drag, click, menu, keyboard)                          │
└──────────────┬──────────────────────────────────────────┘
               │ creates
               ▼
┌─────────────────────────────────────────────────────────┐
│  IDesignCommand (immutable intent description)          │
│  - metadata: who, when, why                             │
│  - parameters: geometry types, entity IDs               │
│  - intent description: human-readable                   │
└──────────────┬──────────────────────────────────────────┘
               │ submitted to
               ▼
┌─────────────────────────────────────────────────────────┐
│  ResolutionOrchestrator                                 │
│  1. Validate command                                    │
│  2. Execute resolution pipeline                         │
│  3. Capture state deltas                                │
│  4. Build explanation graph nodes                       │
│  5. Register undo entry                                 │
│  6. Return ResolutionResult                             │
└──────────────┬──────────────────────────────────────────┘
               │ returns
               ▼
┌─────────────────────────────────────────────────────────┐
│  CommandResult                                          │
│  - success / failure                                    │
│  - validation issues                                    │
│  - state delta references                               │
│  - explanation node IDs                                 │
│  - command metadata echo                                │
└─────────────────────────────────────────────────────────┘
```

---

## 4. Interfaces (C#)

### 4.1 Entity Identifier Types

Typed IDs prevent accidental cross-entity reference errors.

```csharp
namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct CommandId(Guid Value)
{
    public static CommandId New() => new(Guid.NewGuid());
    public override string ToString() => Value.ToString();
}

public readonly record struct CabinetId(Guid Value)
{
    public static CabinetId New() => new(Guid.NewGuid());
}

public readonly record struct RunId(Guid Value)
{
    public static RunId New() => new(Guid.NewGuid());
}

public readonly record struct ExplanationNodeId(Guid Value)
{
    public static ExplanationNodeId New() => new(Guid.NewGuid());
}
```

### 4.2 Command Origin

```csharp
namespace CabinetDesigner.Domain.Commands;

public enum CommandOrigin
{
    User,       // Direct user interaction (menu, keyboard, drag-drop)
    Editor,     // Editor layer interpretation (snap-to-fit, auto-align)
    System,     // System-generated during resolution (auto-filler, auto-end-condition)
    Template,   // Applied from a template or preset
    Undo,       // Generated by undo system
    Redo        // Generated by redo system
}
```

### 4.3 Command Metadata

```csharp
namespace CabinetDesigner.Domain.Commands;

public sealed record CommandMetadata
{
    public required CommandId CommandId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required CommandOrigin Origin { get; init; }
    public required string IntentDescription { get; init; }
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }

    /// <summary>
    /// Non-null for Editor- and System-originated commands.
    /// All commands sharing a ParentCommandId are grouped into one UndoEntry.
    /// </summary>
    public CommandId? ParentCommandId { get; init; }

    /// <summary>
    /// Factory method. Caller provides the timestamp (from IClock).
    /// The application layer owns clock access — never call DateTimeOffset.UtcNow in the domain.
    /// </summary>
    public static CommandMetadata Create(
        DateTimeOffset timestamp,
        CommandOrigin origin,
        string intentDescription,
        IReadOnlyList<string> affectedEntityIds,
        CommandId? parentCommandId = null) => new()
    {
        CommandId = CommandId.New(),
        Timestamp = timestamp,
        Origin = origin,
        IntentDescription = intentDescription,
        AffectedEntityIds = affectedEntityIds,
        ParentCommandId = parentCommandId
    };
}
```

### 4.4 IDesignCommand

The core interface for all commands that change design state. These flow through the `ResolutionOrchestrator`.

```csharp
namespace CabinetDesigner.Domain.Commands;

/// <summary>
/// Represents an intent-driven design change.
/// Commands are immutable descriptions of what the user wants to happen.
/// They do NOT execute themselves — the ResolutionOrchestrator owns execution.
/// </summary>
public interface IDesignCommand
{
    /// <summary>Structured metadata: identity, timestamp, origin, intent, affected entities.</summary>
    CommandMetadata Metadata { get; }

    /// <summary>Machine-readable command type discriminator.</summary>
    string CommandType { get; }

    /// <summary>
    /// Structural validation only. No state access — must be pure.
    /// Checks: required fields present, dimensions positive, enums valid.
    /// Safe to call during preview (drag operations).
    /// Contextual validation (does the run exist? does the slot fit?) happens inside pipeline stages.
    /// </summary>
    IReadOnlyList<ValidationIssue> ValidateStructure();
}
```

### 4.5 IEditorCommand

Editor commands change interaction state (selection, view, mode) but never design state. They do NOT flow through the resolution pipeline.

```csharp
namespace CabinetDesigner.Domain.Commands;

/// <summary>
/// Represents an editor interaction that does not change design state.
/// Selection, zoom, pan, mode switches, etc.
/// These bypass the ResolutionOrchestrator entirely.
/// </summary>
public interface IEditorCommand
{
    CommandMetadata Metadata { get; }
    string CommandType { get; }
}
```

### 4.6 Validation Issue

```csharp
namespace CabinetDesigner.Domain.Commands;

public enum ValidationSeverity
{
    Info,
    Warning,
    Error,
    ManufactureBlocker
}

public sealed record ValidationIssue(
    ValidationSeverity Severity,
    string Code,
    string Message,
    IReadOnlyList<string>? AffectedEntityIds = null);
```

---

## 5. Command Lifecycle

### 5.1 Stages

```
1. CREATION
   Editor interaction (drag-drop, menu, keyboard, numeric entry)
   → ViewModel or Editor creates an IDesignCommand instance
   → Command is fully populated, immutable from this point forward

2. PRE-VALIDATION
   → Command.Validate() called before submission
   → Returns validation issues (may include warnings that don't block)
   → If any Error or ManufactureBlocker severity: command is rejected, UI shows feedback
   → No state has changed

3. SUBMISSION TO ORCHESTRATOR
   → Command passed to ResolutionOrchestrator.Execute(command)
   → Orchestrator takes ownership of the execution flow

4. RESOLUTION PIPELINE EXECUTION
   → Orchestrator runs the 11-stage resolution pipeline
   → Pipeline stages interpret command intent and apply to domain state
   → State deltas are captured (before/after references)

5. RESULT GENERATION
   → Orchestrator builds a CommandResult with:
     - success/failure status
     - any pipeline-generated validation issues
     - references to state changes (deltas)
     - explanation node IDs

6. EXPLANATION GRAPH LINKING
   → Orchestrator creates ExplanationNode(s) in the Why Engine
   → Links command metadata → resolution decisions → resulting state
   → Nodes reference the command ID for full traceability

7. UNDO REGISTRATION
   → If successful, orchestrator captures an UndoEntry:
     - the original command
     - the state deltas (for reversal)
     - explanation node references
   → UndoEntry pushed onto the undo stack
   → Redo stack is cleared (standard undo semantics)
```

### 5.2 Lifecycle Sequence (C#)

```csharp
// In Application layer — e.g., a use case or ViewModel

// 1. Creation
var command = new AddCabinetToRunCommand(
    runId: selectedRun.Id,
    cabinetTypeId: "base-36",
    position: PlacementPosition.EndOfRun,
    origin: CommandOrigin.User,
    intentDescription: "Add 36\" base cabinet to end of kitchen run"
);

// 2. Structural pre-validation (stateless, safe during preview)
var issues = command.ValidateStructure();
if (issues.Any(i => i.Severity >= ValidationSeverity.Error))
{
    // Show validation feedback to user, do not submit
    return CommandResult.Rejected(command.Metadata, issues);
}
// Note: contextual validation (does the run exist? does the slot fit?) runs inside the pipeline.

// 3–7. Submission (orchestrator handles the rest)
CommandResult result = _orchestrator.Execute(command);
```

---

## 6. Result Model (C#)

### 6.1 State Delta

Captures a reference to what changed — not the full state, just enough to identify and reverse.

```csharp
namespace CabinetDesigner.Domain.Commands;

public enum DeltaOperation
{
    Created,
    Modified,
    Removed
}

/// <summary>
/// Constrained delta value. Replaces raw object? to ensure all delta values
/// are serializable, type-safe, and auditable.
/// </summary>
public abstract record DeltaValue
{
    public sealed record OfLength(Length Value) : DeltaValue;
    public sealed record OfThickness(Thickness Value) : DeltaValue;
    public sealed record OfString(string Value) : DeltaValue;
    public sealed record OfBool(bool Value) : DeltaValue;
    public sealed record OfInt(int Value) : DeltaValue;
    public sealed record OfDecimal(decimal Value) : DeltaValue;
    public sealed record Null() : DeltaValue;
}

public sealed record StateDelta(
    string EntityId,
    string EntityType,
    DeltaOperation Operation,
    IReadOnlyDictionary<string, DeltaValue>? PreviousValues = null,
    IReadOnlyDictionary<string, DeltaValue>? NewValues = null);
```

### 6.2 CommandResult

```csharp
namespace CabinetDesigner.Domain.Commands;

public sealed record CommandResult
{
    public required CommandMetadata CommandMetadata { get; init; }
    public required bool Success { get; init; }
    public required IReadOnlyList<ValidationIssue> Issues { get; init; }
    public required IReadOnlyList<StateDelta> Deltas { get; init; }
    public required IReadOnlyList<ExplanationNodeId> ExplanationNodeIds { get; init; }

    public static CommandResult Succeeded(
        CommandMetadata metadata,
        IReadOnlyList<StateDelta> deltas,
        IReadOnlyList<ExplanationNodeId> explanationNodeIds,
        IReadOnlyList<ValidationIssue>? warnings = null) => new()
    {
        CommandMetadata = metadata,
        Success = true,
        Deltas = deltas,
        ExplanationNodeIds = explanationNodeIds,
        Issues = warnings ?? []
    };

    public static CommandResult Failed(
        CommandMetadata metadata,
        IReadOnlyList<ValidationIssue> issues) => new()
    {
        CommandMetadata = metadata,
        Success = false,
        Deltas = [],
        ExplanationNodeIds = [],
        Issues = issues
    };

    public static CommandResult Rejected(
        CommandMetadata metadata,
        IReadOnlyList<ValidationIssue> issues) => Failed(metadata, issues);
}
```

---

## 7. Undo / Redo Strategy

### 7.1 Approach: Hybrid (Command Journal + State Deltas)

| Aspect | Strategy |
|---|---|
| **Forward execution** | Command → orchestrator → pipeline → state changes captured as deltas |
| **Undo** | Apply reverse deltas (restore `PreviousValues`, un-create created entities, re-create removed entities) |
| **Redo** | Re-apply forward deltas (restore `NewValues`) |
| **Journal** | All commands are journaled in execution order for audit, explanation, and replay |

### 7.2 Why Not Pure Command Replay?

Replaying the entire command history from genesis to reconstruct state at position N is:
- Slow for long sessions
- Fragile if any command's resolution behavior changes between versions

Delta-based reversal is O(1) per undo step and version-immune.

### 7.3 Why Not Pure Snapshot?

Full state snapshots per command are expensive in memory. Deltas are compact.

### 7.4 Undo Stack Model

```csharp
namespace CabinetDesigner.Domain.Commands;

public sealed record UndoEntry(
    CommandMetadata CommandMetadata,
    IReadOnlyList<StateDelta> Deltas,
    IReadOnlyList<ExplanationNodeId> ExplanationNodeIds);

public interface IUndoStack
{
    bool CanUndo { get; }
    bool CanRedo { get; }

    /// <summary>Push a completed command onto the undo stack. Clears redo stack.</summary>
    void Push(UndoEntry entry);

    /// <summary>Pop the most recent entry for reversal.</summary>
    UndoEntry? Undo();

    /// <summary>Pop the most recent undone entry for re-application.</summary>
    UndoEntry? Redo();

    /// <summary>All entries in execution order (for journaling / audit).</summary>
    IReadOnlyList<UndoEntry> Journal { get; }

    /// <summary>Clear all history (e.g., on project close).</summary>
    void Clear();
}
```

### 7.5 Guarantees

- **Deterministic reversal**: deltas contain the exact previous values, not a re-computation
- **No state drift**: undo restores the literal prior state, not an approximation
- **Full auditability**: the journal preserves every command ever executed in the session, including those that were undone
- **Compound command support**: if a single user action produces multiple deltas, they are grouped in one `UndoEntry` and reversed atomically

---

## 8. Command Categories

### 8.1 Layout Commands

Affect spatial placement and position within the scene.

| Command | Intent |
|---|---|
| `AddCabinetToRunCommand` | Place a new cabinet at a specific position in a run |
| `MoveCabinetCommand` | Reposition a cabinet within or between runs |
| `InsertCabinetIntoRunCommand` | Insert a cabinet between two existing cabinets in a run |

### 8.2 Structural Commands

Affect the organizational structure of the design — runs, groupings, relationships.

| Command | Intent |
|---|---|
| `CreateRunCommand` | Create a new cabinet run along a wall segment |
| `DeleteRunCommand` | Remove a run (and optionally its contents) |
| `MergeRunsCommand` | Combine two adjacent runs into one |

### 8.3 Modification Commands

Change properties of existing entities.

| Command | Intent |
|---|---|
| `ResizeCabinetCommand` | Change the width of a cabinet |
| `ChangeCabinetTypeCommand` | Swap a cabinet's type (e.g., base → sink base) |
| `SetCabinetOverrideCommand` | Apply a parameter override to a specific cabinet |

### 8.4 System Commands

Bulk or automated operations.

| Command | Intent |
|---|---|
| `ApplyTemplateToRunCommand` | Apply a cabinet template/preset to an entire run |
| `AutoFillRunGapsCommand` | System-generated command to insert fillers into run gaps |
| `BulkResizeCabinetsCommand` | Resize multiple cabinets in one operation |

---

## 9. Concrete Command Examples (C#)

### 9.1 Abstract Base

```csharp
namespace CabinetDesigner.Domain.Commands;

public abstract record DesignCommandBase : IDesignCommand
{
    public CommandMetadata Metadata { get; }
    public abstract string CommandType { get; }

    protected DesignCommandBase(CommandMetadata metadata)
    {
        Metadata = metadata;
    }

    public abstract IReadOnlyList<ValidationIssue> ValidateStructure();
}
```

### 9.2 AddCabinetToRunCommand

```csharp
namespace CabinetDesigner.Domain.Commands.Layout;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum RunPlacement
{
    StartOfRun,
    EndOfRun,
    AtIndex
}

public sealed record AddCabinetToRunCommand : DesignCommandBase
{
    public override string CommandType => "layout.add_cabinet_to_run";

    public required RunId RunId { get; init; }
    public required string CabinetTypeId { get; init; }
    public required Length NominalWidth { get; init; }
    public required RunPlacement Placement { get; init; }
    public int? InsertAtIndex { get; init; }

    public AddCabinetToRunCommand(
        RunId runId,
        string cabinetTypeId,
        Length nominalWidth,
        RunPlacement placement,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp,
        int? insertAtIndex = null)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [runId.Value.ToString()]))
    {
        RunId = runId;
        CabinetTypeId = cabinetTypeId;
        NominalWidth = nominalWidth;
        Placement = placement;
        InsertAtIndex = insertAtIndex;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        var issues = new List<ValidationIssue>();

        if (NominalWidth <= Length.Zero)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_WIDTH",
                "Cabinet width must be greater than zero."));

        if (string.IsNullOrWhiteSpace(CabinetTypeId))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_TYPE",
                "Cabinet type ID is required."));

        if (Placement == RunPlacement.AtIndex && InsertAtIndex is null)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_INDEX",
                "InsertAtIndex is required when Placement is AtIndex."));

        return issues;
    }
}
```

### 9.3 MoveCabinetCommand

```csharp
namespace CabinetDesigner.Domain.Commands.Layout;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record MoveCabinetCommand : DesignCommandBase
{
    public override string CommandType => "layout.move_cabinet";

    public required CabinetId CabinetId { get; init; }
    public required RunId SourceRunId { get; init; }
    public required RunId TargetRunId { get; init; }
    public required RunPlacement TargetPlacement { get; init; }
    public int? TargetIndex { get; init; }

    public MoveCabinetCommand(
        CabinetId cabinetId,
        RunId sourceRunId,
        RunId targetRunId,
        RunPlacement targetPlacement,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp,
        int? targetIndex = null)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [cabinetId.Value.ToString(), sourceRunId.Value.ToString(), targetRunId.Value.ToString()]))
    {
        CabinetId = cabinetId;
        SourceRunId = sourceRunId;
        TargetRunId = targetRunId;
        TargetPlacement = targetPlacement;
        TargetIndex = targetIndex;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        var issues = new List<ValidationIssue>();

        if (TargetPlacement == RunPlacement.AtIndex && TargetIndex is null)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_INDEX",
                "TargetIndex is required when TargetPlacement is AtIndex."));

        return issues;
    }
}
```

### 9.4 ResizeCabinetCommand

```csharp
namespace CabinetDesigner.Domain.Commands.Modification;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record ResizeCabinetCommand : DesignCommandBase
{
    public override string CommandType => "modification.resize_cabinet";

    public required CabinetId CabinetId { get; init; }
    public required Length NewNominalWidth { get; init; }
    public required Length PreviousNominalWidth { get; init; }

    public ResizeCabinetCommand(
        CabinetId cabinetId,
        Length previousNominalWidth,
        Length newNominalWidth,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [cabinetId.Value.ToString()]))
    {
        CabinetId = cabinetId;
        NewNominalWidth = newNominalWidth;
        PreviousNominalWidth = previousNominalWidth;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        var issues = new List<ValidationIssue>();

        if (NewNominalWidth <= Length.Zero)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_WIDTH",
                "New width must be greater than zero."));

        if (NewNominalWidth == PreviousNominalWidth)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Warning,
                "NO_CHANGE",
                "New width is the same as the current width."));

        return issues;
    }
}
```

### 9.5 CreateRunCommand

```csharp
namespace CabinetDesigner.Domain.Commands.Structural;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record CreateRunCommand : DesignCommandBase
{
    public override string CommandType => "structural.create_run";

    public required Point2D StartPoint { get; init; }
    public required Point2D EndPoint { get; init; }
    public required string WallId { get; init; }

    public CreateRunCommand(
        Point2D startPoint,
        Point2D endPoint,
        string wallId,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [wallId]))
    {
        StartPoint = startPoint;
        EndPoint = endPoint;
        WallId = wallId;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        var issues = new List<ValidationIssue>();

        var runLength = StartPoint.DistanceTo(EndPoint);
        if (runLength <= Length.Zero)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "ZERO_LENGTH_RUN",
                "Run start and end points must be different."));

        if (string.IsNullOrWhiteSpace(WallId))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_WALL",
                "A run must be associated with a wall."));

        return issues;
    }
}
```

### 9.6 InsertCabinetIntoRunCommand

```csharp
namespace CabinetDesigner.Domain.Commands.Layout;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record InsertCabinetIntoRunCommand : DesignCommandBase
{
    public override string CommandType => "layout.insert_cabinet_into_run";

    public required RunId RunId { get; init; }
    public required string CabinetTypeId { get; init; }
    public required Length NominalWidth { get; init; }
    public required int InsertAtIndex { get; init; }
    public required CabinetId LeftNeighborId { get; init; }
    public required CabinetId RightNeighborId { get; init; }

    public InsertCabinetIntoRunCommand(
        RunId runId,
        string cabinetTypeId,
        Length nominalWidth,
        int insertAtIndex,
        CabinetId leftNeighborId,
        CabinetId rightNeighborId,
        CommandOrigin origin,
        string intentDescription,
        DateTimeOffset timestamp)
        : base(CommandMetadata.Create(
            timestamp,
            origin,
            intentDescription,
            [runId.Value.ToString(), leftNeighborId.Value.ToString(), rightNeighborId.Value.ToString()]))
    {
        RunId = runId;
        CabinetTypeId = cabinetTypeId;
        NominalWidth = nominalWidth;
        InsertAtIndex = insertAtIndex;
        LeftNeighborId = leftNeighborId;
        RightNeighborId = rightNeighborId;
    }

    public override IReadOnlyList<ValidationIssue> ValidateStructure()
    {
        var issues = new List<ValidationIssue>();

        if (NominalWidth <= Length.Zero)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_WIDTH",
                "Cabinet width must be greater than zero."));

        if (string.IsNullOrWhiteSpace(CabinetTypeId))
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "MISSING_TYPE",
                "Cabinet type ID is required."));

        if (InsertAtIndex < 0)
            issues.Add(new ValidationIssue(
                ValidationSeverity.Error,
                "INVALID_INDEX",
                "Insert index cannot be negative."));

        return issues;
    }
}
```

---

## 10. Orchestrator Integration Contract

### 10.1 Core Contract

```csharp
namespace CabinetDesigner.Application;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// The single choke point for all design state changes.
/// Every IDesignCommand flows through here — no exceptions.
/// </summary>
public interface IResolutionOrchestrator
{
    /// <summary>
    /// Execute a design command through the full resolution pipeline.
    /// </summary>
    /// <param name="command">An immutable, pre-validated design command.</param>
    /// <returns>A result describing success/failure, deltas, and explanation references.</returns>
    CommandResult Execute(IDesignCommand command);

    /// <summary>
    /// Undo the most recent command. Returns the result of the reversal.
    /// </summary>
    CommandResult? Undo();

    /// <summary>
    /// Redo the most recently undone command. Returns the result of re-application.
    /// </summary>
    CommandResult? Redo();
}
```

### 10.2 Execution Flow (Inside Orchestrator)

```csharp
// Pseudocode — actual implementation is a later phase

public CommandResult Execute(IDesignCommand command)
{
    // 1. Pre-validation
    var issues = command.Validate();
    if (issues.Any(i => i.Severity >= ValidationSeverity.Error))
        return CommandResult.Failed(command.Metadata, issues);

    // 2. Begin delta tracking
    var deltaTracker = new DeltaTracker(_currentState);

    // 3. Run resolution pipeline (11 stages)
    //    Pipeline reads the command, interprets intent, mutates state through tracked operations
    var pipelineResult = _pipeline.Run(command, deltaTracker);

    if (!pipelineResult.Success)
        return CommandResult.Failed(command.Metadata, pipelineResult.Issues);

    // 4. Capture deltas
    var deltas = deltaTracker.GetDeltas();

    // 5. Create explanation nodes
    var explanationNodes = _whyEngine.RecordCommand(command, deltas);

    // 6. Register undo entry
    var undoEntry = new UndoEntry(command.Metadata, deltas, explanationNodes);
    _undoStack.Push(undoEntry);

    // 7. Return result
    return CommandResult.Succeeded(
        command.Metadata,
        deltas,
        explanationNodes,
        pipelineResult.Warnings);
}
```

### 10.3 Contract Expectations

| Aspect | Expectation |
|---|---|
| **Input** | A fully constructed, immutable `IDesignCommand` |
| **Pre-condition** | Caller may optionally pre-validate; orchestrator always validates |
| **Execution** | Synchronous, single-threaded — no concurrent commands |
| **State mutation** | Only the orchestrator (via pipeline) may mutate design state |
| **Output** | `CommandResult` — always returned, never throws for business logic failures |
| **Side effects** | Undo stack updated, explanation graph updated, state committed |
| **Failure** | On validation/pipeline failure: no state changes, no undo entry, result indicates failure |

---

## 11. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| **Delta tracking complexity** | Start with property-level deltas on entities. Evolve to more granular tracking only if needed. Keep delta model simple — `PreviousValues` / `NewValues` dictionaries cover most cases |
| **Compound commands (user action triggers multiple design changes)** | Group all deltas from a single user action into one `UndoEntry`. The `ParentCommandId` on metadata supports tracing sub-commands back to the initiating action |
| **Undo after schema migration** | Deltas reference property names as strings — if schema changes, old deltas may reference stale properties. Mitigation: clear undo history on project schema migration. Long-term: version-tag delta schemas |
| **Large delta payloads** | For bulk operations (e.g., resize 50 cabinets), deltas can be numerous. Mitigation: lazy serialization of delta details; keep in-memory representation compact |
| **Command serialization for journaling** | Commands are `record` types — System.Text.Json handles them. But polymorphic deserialization requires a type discriminator. Use `CommandType` string as discriminator with a registry |
| **Validation that requires state (e.g., "does this run exist?")** | `Command.Validate()` is stateless (pure). State-dependent validation happens inside the orchestrator pipeline. Two validation layers: command-level (structural) and pipeline-level (contextual) |
| **Editor commands leaking into design pipeline** | `IEditorCommand` is a separate interface with no `Execute` path on the orchestrator. Type system enforces the boundary |
| **Circular command chains (command A triggers system command B which triggers C...)** | Limit system-generated command depth. The orchestrator tracks recursion depth and rejects commands beyond a configurable limit (e.g., 3 levels) |
| **Concurrent undo from different contexts** | Single-threaded execution guarantee. UI dispatches commands on the main thread. No concurrent undo/redo possible |
| **StateDelta values** | Constrained via `DeltaValue` discriminated union. Non-serializable types are a compile-time error, not a runtime surprise. |
