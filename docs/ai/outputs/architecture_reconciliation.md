# Architecture Reconciliation Pass

Source: All P1–P5 outputs + `architecture_summary.md`
Model: Claude Opus 4.6

---

## 1. Executive Summary

The architecture is strong. The fundamental decisions — command-driven state changes, a single orchestrator choke point, geometry value objects, six-reality separation, append-only explanation tracing, and delta-based undo — are sound and well-documented. The system is safe to continue from.

However, six cross-cutting inconsistencies need resolution before implementation proceeds. The most critical are: domain entities calling `DateTimeOffset.UtcNow` directly (domain impurity), `CabinetRun` mixing ordered-slot logic with world-space computation (boundary leak), and pervasive `object` typing in overrides, deltas, and stage parameters (type safety hole). These are design-time fixes, not rewrites — the bones are good.

---

## 2. What Is Already Strong

These design decisions should be preserved without modification:

1. **Commands as immutable data, not behavior.** Commands carry intent; the orchestrator owns execution. This is the right call and must not drift.

2. **Typed entity IDs** (`CabinetId`, `RunId`, etc.) preventing cross-entity reference errors. Well-implemented, consistent across all documents.

3. **Geometry value objects** (`Length`, `Offset`, `Thickness`, `Point2D`). The `Length - Length = Offset` decision is excellent. The `decimal` canonical storage is correct for shop-grade precision.

4. **Fast path / deep path separation.** Preview runs stages 1–3 without deltas, undo, or Why Engine recording. Deep path runs all 11. Clean boundary.

5. **Delta-based undo with command journaling.** Hybrid approach avoids full-history replay (fragile, slow) and full snapshots (expensive). Correct trade-off.

6. **Why Engine as append-only graph with typed edges.** The decision to record per-stage rather than post-hoc is critical for accuracy. The edge types (`CausedBy`, `ConstrainedBy`, `Produced`, etc.) enable precise causal queries.

7. **Aggregate boundaries** (`Project`, `Room`, `CabinetRun`, `Cabinet`). Cross-aggregate coordination via orchestrator only. Clean DDD.

8. **Validation severity levels** (`Info`, `Warning`, `Error`, `ManufactureBlocker`). The four-tier model maps directly to shop workflow.

9. **Recursion depth guard** on system-generated commands. Prevents circular auto-filler chains.

10. **Six realities** as a conceptual frame. Keeps interaction, intent, engineering, manufacturing, install, and commercial concerns from collapsing into each other.

---

## 3. Inconsistencies and Risks

### 3.1 Domain impurity: `DateTimeOffset.UtcNow` in entities

**Location:** `Project` constructor and `Touch()`, `Revision` constructor and `TransitionTo()`, `CommandMetadata.Create()`.

**Problem:** Domain entities directly call `DateTimeOffset.UtcNow`. This couples the domain to the system clock, makes entities non-deterministic, and makes testing require time manipulation. The architecture guardrails explicitly require "deterministic outputs only."

**Severity:** High. This is a design-time fix that becomes increasingly expensive to retrofit.

### 3.2 Run aggregate boundary leak

**Location:** `CabinetRun.SlotPosition()` computes world-space coordinates. `CabinetRun` stores `StartPoint` and `EndPoint`.

**Problem:** `CabinetRun` mixes two responsibilities: (1) ordered-slot/run logic (slot management, contiguous width invariants, end conditions) and (2) world-space placement (start/end points, slot-to-world-space mapping). These are separate realities (design intent vs spatial scene).

**Severity:** Medium-high. This will cause confusion when spatial resolution logic needs to change independently of run logic.

### 3.3 Preview runs full pre-flight validation

**Location:** `ResolutionOrchestrator.Preview()` calls `command.Validate()` with the same logic as `Execute()`.

**Problem:** The `IDesignCommand.Validate()` method has no concept of preview-safe vs full validation. During drag, some validations (e.g., "does this run exist?") are necessary but others (e.g., "is this a standard width?") should be deferred. Currently there is no contract distinction.

**Severity:** Medium. Will cause UI friction during drag operations as non-blocking warnings accumulate.

### 3.4 `IWhyEngine` interface inconsistency between documents

**Location:** `orchestrator.md` section 8.1 defines a simplified `IWhyEngine` with 5 methods. `why_engine.md` section 5 defines the full interface with 12+ methods.

**Problem:** Two different contracts for the same interface. The orchestrator document omits `RecordDecisionWithEdges`, all query methods, and the `ExplanationRuleRecord` parameter.

**Severity:** Medium. The `why_engine.md` version is authoritative, but the orchestrator document should reference it rather than define a subset.

### 3.5 Append-only claim violated by undo/redo

**Location:** `WhyEngine.RecordUndo()` mutates existing nodes in-place via list index replacement: `_nodes[index] = _nodes[index] with { Status = ... }`.

**Problem:** The design claims "append-only" semantics but then mutates node status. While the `with` expression creates a new record instance, the list slot is overwritten. This is a semantic violation of append-only, even if the implementation is technically safe for single-threaded use.

**Severity:** Low-medium. The behavior is correct, but the contract should be honest about what "append-only" means here.

### 3.6 Pervasive `object` typing

**Locations:**
- `Cabinet._overrides`: `Dictionary<string, object>`
- `ShopStandard.Parameters`: `IReadOnlyDictionary<string, object>`
- `CabinetTemplate.DefaultOverrides`: `IReadOnlyDictionary<string, object>`
- `StateDelta.PreviousValues/NewValues`: `IReadOnlyDictionary<string, object?>`
- `DomainOperation.Parameters`: `IReadOnlyDictionary<string, object>`
- `InputCaptureResult.ResolvedEntities/NormalizedParameters`: `IReadOnlyDictionary<string, object>`
- `MachiningOperation.Parameters`: `IReadOnlyDictionary<string, object>`

**Problem:** `object` values cannot be validated at compile time, cannot be safely serialized without type discriminators, and will eventually produce `InvalidCastException` at runtime. The domain model document acknowledges this risk but defers the fix.

**Severity:** High. This is the single largest type-safety gap in the system and should be closed before implementation begins.

---

## 4. Reconciled Decisions

### 4.1 Domain Purity

**Decision:** Extract all `DateTimeOffset.UtcNow` calls from domain entities. Introduce an `IClock` abstraction in the domain layer. Inject timestamps from the application layer.

**Rationale:** The guardrails require deterministic outputs. A domain entity that calls `UtcNow` is non-deterministic. Testing requires time manipulation. The application layer (orchestrator, command handlers) is the correct owner of "when did this happen?"

**Exact ownership boundary:**
- **Domain:** Entities accept `DateTimeOffset` as constructor/method parameters. Never call `UtcNow`.
- **Application:** Orchestrator and command handlers inject timestamps via `IClock` at execution time.
- **Infrastructure:** `IClock` implementation wraps `DateTimeOffset.UtcNow`. Test doubles provide fixed timestamps.

**Impact on existing documents:**
- `domain_model.md`: `Project`, `Revision` constructors must accept `DateTimeOffset` parameters instead of calling `UtcNow`.
- `commands.md`: `CommandMetadata.Create()` must accept a `DateTimeOffset` parameter.
- `why_engine.md`: `WhyEngine` must accept `IClock` via constructor injection.

### 4.2 Run Ownership vs Spatial Ownership

**Decision:** `CabinetRun` owns ordered-slot logic only. World-space placement is computed by the spatial resolution stage, not the run aggregate.

**Rationale:** A run is a design-intent concept: "these cabinets belong together in this order along this wall." The world-space position of each cabinet is a spatial-resolution concern that depends on wall geometry, offsets, and run placement strategy. Mixing them in the aggregate makes the run responsible for two realities.

**Exact ownership boundary:**
- **`CabinetRun` (domain) owns:**
  - Ordered slot list (`_slots`)
  - Slot insertion/removal/reindexing
  - Total width, occupied width, remaining width (all in run-local `Length`)
  - End conditions (left/right)
  - Wall reference (`WallId`)
  - Contiguous-width invariant enforcement
- **Spatial Resolution Stage (application) owns:**
  - `StartPoint`, `EndPoint` (stored on a separate `RunPlacement` or on the spatial resolution result)
  - Slot-to-world-space position mapping
  - Direction vector computation
  - Run-to-wall alignment

**What moves out of `CabinetRun`:**
- `StartPoint`, `EndPoint` properties → move to a `RunPlacement` value object managed by the spatial layer
- `SlotPosition(int)` method → move to a spatial service or the `SpatialResolutionResult`
- `SlotOffset(int)` can stay — it's a run-local calculation (cumulative width from slot 0)
- `TotalLength` → redefine as the sum of slot capacities or derive from the spatial placement. The run should not own a length derived from world-space endpoints.

**Impact on existing documents:**
- `domain_model.md`: Remove `StartPoint`, `EndPoint`, `SlotPosition()` from `CabinetRun`. Add `RunPlacement` value object or move endpoints to a spatial context.
- `orchestrator.md`: Stage 3 (`SpatialResolution`) becomes the owner of run-to-world mapping.
- `commands.md`: `CreateRunCommand` still carries `StartPoint`/`EndPoint`, but these flow to the spatial layer, not the run aggregate.

### 4.3 Preview Validation vs Full Validation

**Decision:** Split validation into two tiers. Preview uses structural validation only (is the command well-formed?). Full resolution adds contextual validation (is the operation valid against current state?).

**Rationale:** During drag, the user needs fast feedback on whether the drop target is valid. They do not need "this width is non-standard" warnings cluttering the preview. Full validation belongs at commit time.

**Contracts:**

```csharp
public interface IDesignCommand
{
    CommandMetadata Metadata { get; }
    string CommandType { get; }

    /// <summary>
    /// Structural validation: is this command well-formed?
    /// No state access. Safe to call during preview.
    /// Checks: required fields, positive dimensions, valid enum values.
    /// </summary>
    IReadOnlyList<ValidationIssue> ValidateStructure();
}
```

- **Preview path:** Orchestrator calls `ValidateStructure()`. Stages 1–3 perform lightweight spatial checks (does the slot fit?). No full engineering/constraint/manufacturing validation.
- **Full path:** Orchestrator calls `ValidateStructure()` first. Stages 1–11 run full validation. Stage 10 performs cross-cutting validation.

**Where each happens:**
| Validation tier | When | Who | What it checks |
|---|---|---|---|
| Structural | Preview + Full | `command.ValidateStructure()` | Required fields, positive dimensions, enum validity |
| Spatial feasibility | Preview + Full | Stage 3 | Slot fits in run, no collision with preview state |
| Engineering validity | Full only | Stage 4 | Opening resolution, filler requirements |
| Constraint satisfaction | Full only | Stage 5 | Material/hardware compatibility |
| Manufacturability | Full only | Stage 7 | CNC capacity, grain feasibility |
| Cross-cutting | Full only | Stage 10 | Collision, completeness, installability |

**Impact on existing documents:**
- `commands.md`: Rename `Validate()` → `ValidateStructure()` on `IDesignCommand`. Remove state-dependent checks from command-level validation (these belong in pipeline stages).
- `orchestrator.md`: `Preview()` calls `ValidateStructure()` only. `Execute()` calls `ValidateStructure()` + pipeline validation.

### 4.4 Command Hierarchy and Causal Chain

**Decision:** Formalize three command tiers via `CommandOrigin` and metadata. No new interfaces needed — classification is a metadata concern.

**Rationale:** The current `CommandOrigin` enum (`User`, `System`, `Template`, `Undo`, `Redo`) is close but does not distinguish user-authored from editor-originated commands. The `ParentCommandId` field is sufficient for causal chains but its semantics are undocumented.

**Command classification:**

| Tier | Origin | Created By | Example | Undo behavior |
|---|---|---|---|---|
| **User-authored** | `User` | User interaction via UI/keyboard | `AddCabinetToRunCommand` from drag-drop | Undoable as a single unit |
| **Editor-originated** | `Editor` (new) | Editor layer interprets interaction | `InsertFillerCommand` from snap-to-fit | Undoable as part of parent command |
| **System-generated** | `System` | Pipeline stage during resolution | `AutoFillRunGapsCommand` from stage 4 | Undoable as part of parent command |

**Causal chain rules:**
1. `ParentCommandId` is non-null for editor-originated and system-generated commands.
2. All commands sharing a `ParentCommandId` are grouped into a single `UndoEntry`.
3. System-generated commands increment recursion depth. User and editor commands do not.
4. The orchestrator logs the full causal chain: user command → editor commands → system commands.

**Revised `CommandOrigin`:**

```csharp
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

**Impact on existing documents:**
- `commands.md`: Add `Editor` to `CommandOrigin`. Document `ParentCommandId` semantics. Document compound undo grouping.
- `orchestrator.md`: Document that editor-originated and system-generated commands group under the parent's `UndoEntry`.

### 4.5 Why Engine Consistency

**Decision:** Redefine "append-only" to mean "nodes are never deleted and content is never changed, but status markers are mutable." Introduce explicit `StatusChange` events rather than in-place mutation.

**Rationale:** The current implementation claims append-only but mutates node status in-place. This is technically safe in single-threaded use but semantically dishonest. The fix is either: (a) make undo/redo status changes append new marker nodes that override the original's effective status, or (b) honestly document that status is a mutable projection.

**Decision: Option (b) — honest documentation with a clean separation.**

Split the Why Engine's data into two layers:

1. **Immutable history layer** (truly append-only):
   - `ExplanationNode` records with all decision data
   - Undo/redo marker nodes (`UndoMarker`, `RedoMarker`)
   - Never mutated after creation

2. **Mutable projection layer** (queryable current state):
   - `NodeEffectiveStatus` index: `ExplanationNodeId → ExplanationNodeStatus`
   - Derived from the history layer (scan marker nodes to compute effective status)
   - Rebuilt on query or maintained incrementally
   - This is what the query methods consult

**Impact on existing documents:**
- `why_engine.md`: Remove in-place `Status` mutation from `RecordUndo`/`RecordRedo`. Status lives in the projection layer. `ExplanationNode.Status` is removed or made init-only (set at creation). The `GetNodesByStatus` and `GetPropertyExplanation` methods query the projection.
- `orchestrator.md`: No change — the orchestrator's `IWhyEngine` contract is unaffected.

### 4.6 ResolutionContext / Stage Contract Clarity

**Decision:** Keep the single mutable `ResolutionContext` but tighten the contract with non-nullable stage result wrappers and a phase accessor pattern.

**Rationale:** Replacing the shared context with strict per-stage input/output types would require a complex builder pattern and make the pipeline harder to extend. The shared context is pragmatic for a desktop app with single-threaded execution. The real risk is not the mutable context — it's the nullable result properties that let stages silently read null data from prior stages.

**Revised approach:**

```csharp
public sealed class ResolutionContext
{
    // Input (immutable after construction)
    public required IDesignCommand Command { get; init; }
    public required ResolutionMode Mode { get; init; }

    // Stage results — set by each stage, read by subsequent stages
    // Non-nullable accessors throw if a prior stage didn't run
    private InputCaptureResult? _inputCapture;
    private InteractionInterpretationResult? _interpretation;
    private SpatialResolutionResult? _spatialResult;
    // ... etc for stages 4-11

    public InputCaptureResult InputCapture
    {
        get => _inputCapture ?? throw new InvalidOperationException(
            "InputCapture not set. Stage 1 must execute before accessing this.");
        set => _inputCapture = value;
    }

    // ... same pattern for other stage results

    // Cross-cutting (mutable, accumulated)
    public List<ValidationIssue> AccumulatedIssues { get; } = [];
    public List<ExplanationNodeId> ExplanationNodeIds { get; } = [];

    public bool HasBlockingIssues =>
        AccumulatedIssues.Any(i => i.Severity >= ValidationSeverity.Error);
}
```

**Stage result typing improvements:**
- `DomainOperation.Parameters`: Replace `IReadOnlyDictionary<string, object>` with a discriminated union or sealed hierarchy of operation-specific parameter types (see section 4.7).
- `InputCaptureResult.ResolvedEntities`: Replace `IReadOnlyDictionary<string, object>` with `IReadOnlyDictionary<string, IDomainEntity>` or a typed entity bag.

**Impact on existing documents:**
- `orchestrator.md`: Update `ResolutionContext` to use the accessor pattern. Document stage ordering guarantees.
- Stage result types: Replace nullable properties with throwing accessors.

### 4.7 Override Typing

**Decision:** Replace `object` override values with a constrained `OverrideValue` discriminated union now, before implementation begins.

**Rationale:** `object` is the most dangerous type in a system that promises determinism and serialization safety. Every `object` usage is a serialization landmine and a runtime cast waiting to fail. The set of valid override types is finite and known.

**Revised type:**

```csharp
namespace CabinetDesigner.Domain;

/// <summary>
/// A constrained override value. All possible override types are enumerated.
/// No raw object values cross domain boundaries.
/// </summary>
public abstract record OverrideValue
{
    public sealed record LengthValue(Length Value) : OverrideValue;
    public sealed record ThicknessValue(Thickness Value) : OverrideValue;
    public sealed record AngleValue(Angle Value) : OverrideValue;
    public sealed record StringValue(string Value) : OverrideValue;
    public sealed record BoolValue(bool Value) : OverrideValue;
    public sealed record IntValue(int Value) : OverrideValue;
    public sealed record DecimalValue(decimal Value) : OverrideValue;
    public sealed record MaterialIdValue(MaterialId Value) : OverrideValue;
    public sealed record HardwareItemIdValue(HardwareItemId Value) : OverrideValue;

    /// <summary>Display-friendly string representation.</summary>
    public abstract override string ToString();
}
```

**Where this applies:**

| Current `object` usage | Replacement |
|---|---|
| `Cabinet._overrides` | `Dictionary<string, OverrideValue>` |
| `ShopStandard.Parameters` | `IReadOnlyDictionary<string, OverrideValue>` |
| `CabinetTemplate.DefaultOverrides` | `IReadOnlyDictionary<string, OverrideValue>` |
| `StateDelta.PreviousValues/NewValues` | `IReadOnlyDictionary<string, DeltaValue>` (similar union) |
| `DomainOperation.Parameters` | Typed operation-specific parameter records (see below) |
| `MachiningOperation.Parameters` | `IReadOnlyDictionary<string, OverrideValue>` or typed record |

**For `DomainOperation`:** Replace the generic `DomainOperation` with a sealed hierarchy:

```csharp
public abstract record DomainOperation
{
    public required string TargetEntityId { get; init; }
    public required string TargetEntityType { get; init; }

    public sealed record InsertSlot(
        string TargetEntityId, string TargetEntityType,
        CabinetId CabinetId, Length Width, int SlotIndex)
        : DomainOperation;

    public sealed record RemoveSlot(
        string TargetEntityId, string TargetEntityType,
        RunSlotId SlotId)
        : DomainOperation;

    public sealed record ResizeCabinet(
        string TargetEntityId, string TargetEntityType,
        CabinetId CabinetId, Length NewWidth)
        : DomainOperation;

    // ... other concrete operations
}
```

**For `StateDelta`:** Introduce a parallel `DeltaValue` union:

```csharp
public abstract record DeltaValue
{
    public sealed record LengthDelta(Length Value) : DeltaValue;
    public sealed record StringDelta(string Value) : DeltaValue;
    public sealed record BoolDelta(bool Value) : DeltaValue;
    public sealed record IntDelta(int Value) : DeltaValue;
    public sealed record DecimalDelta(decimal Value) : DeltaValue;
    public sealed record NullDelta() : DeltaValue;
    // ... mirror OverrideValue variants as needed
}
```

**Impact on existing documents:**
- `domain_model.md`: `Cabinet._overrides`, `ShopStandard.Parameters`, `CabinetTemplate.DefaultOverrides` all change to `OverrideValue`.
- `commands.md`: `StateDelta.PreviousValues/NewValues` change to `DeltaValue`.
- `orchestrator.md`: `DomainOperation` becomes a sealed hierarchy. `InputCaptureResult` dictionaries get typed.

---

## 5. Required Revisions by File

### 5.1 `docs/ai/outputs/commands.md`

**What should change:**
1. Rename `IDesignCommand.Validate()` → `ValidateStructure()`. Update all concrete command implementations.
2. Add `Editor` to `CommandOrigin` enum.
3. Document `ParentCommandId` semantics: non-null for editor-originated and system-generated commands; all commands sharing a parent are grouped in one `UndoEntry`.
4. `CommandMetadata.Create()` must accept a `DateTimeOffset` parameter instead of calling `UtcNow`.
5. `StateDelta.PreviousValues/NewValues` → `IReadOnlyDictionary<string, DeltaValue>`.
6. Document two-tier validation explicitly: structural (command-level) vs contextual (pipeline-level).
7. `AffectedEntityIds` on `CommandMetadata` should be `IReadOnlyList<string>` consistently (it already is, but the factory method constructs from typed ID `.ToString()` — consider typed ID list).

**What should stay:**
- Command-as-data pattern
- Typed IDs
- Immutable record types
- Delta-based undo with command journaling
- `IEditorCommand` separation
- `UndoEntry` grouping model
- Recursion depth guard (document it here, not just in orchestrator)

**Why:** The command system is the entry point for all state changes. Tightening the validation contract and override typing here prevents downstream ambiguity.

### 5.2 `docs/ai/outputs/domain_model.md`

**What should change:**
1. Remove `DateTimeOffset.UtcNow` from all entity constructors and methods. Accept timestamps as parameters.
2. Add `IClock` interface to the domain layer (simple: `DateTimeOffset Now { get; }`).
3. `CabinetRun`: Remove `StartPoint`, `EndPoint`, `SlotPosition()`. Keep `SlotOffset()`. Introduce `RunCapacity` (a `Length`) that represents the available length for this run, set by the spatial layer when the run is associated with a wall segment.
4. `Cabinet._overrides` → `Dictionary<string, OverrideValue>`.
5. `Cabinet.SetOverride(string, object)` → `Cabinet.SetOverride(string, OverrideValue)`.
6. `ShopStandard.Parameters` → `IReadOnlyDictionary<string, OverrideValue>`.
7. `CabinetTemplate.DefaultOverrides` → `IReadOnlyDictionary<string, OverrideValue>`.
8. `CabinetRun` should accept `RunCapacity` (a `Length`) rather than computing `TotalLength` from world-space endpoints.

**What should stay:**
- Entity structures (`Project`, `Revision`, `Room`, `Wall`, `Cabinet`, `CabinetRun`, etc.)
- Aggregate boundaries
- Invariant enforcement patterns
- Typed ID references
- `ApprovalState` transition graph
- `EndCondition` value object
- `RunSlot` record type
- Material and hardware catalog entities

**Why:** Domain purity is a guardrail. The domain must be deterministic, UI-independent, and testable. Clock coupling and world-space computation in the run aggregate violate this.

### 5.3 `docs/ai/outputs/orchestrator.md`

**What should change:**
1. `ResolutionContext`: Replace nullable stage result properties with the throwing-accessor pattern (section 4.6).
2. `Preview()` path: Call `ValidateStructure()` (not `Validate()`). Document that preview validation is structural only.
3. `IWhyEngine` reference in section 8: Replace the inline subset definition with a reference to `why_engine.md`. The authoritative interface lives there.
4. `DomainOperation` in stage 2: Replace `IReadOnlyDictionary<string, object>` parameters with a sealed operation hierarchy.
5. `InputCaptureResult.ResolvedEntities`: Replace `IReadOnlyDictionary<string, object>` with typed entity references.
6. Document that `SlotPositionUpdate.WorldPosition` in stage 3 is the canonical source of world-space position for run slots (since this is moving out of `CabinetRun`).
7. Document compound undo grouping for editor-originated and system-generated commands.

**What should stay:**
- 11-stage pipeline with clear stage numbering
- Fast path (stages 1-3) / deep path (all 11) separation
- Stage interface (`IResolutionStage`)
- `StageResult` type
- `IDeltaTracker` interface
- `PreviewResult` type
- Error handling strategy (halt on blocking issues, accumulate warnings)
- Single-threaded execution guarantee
- Recursion depth guard

**Why:** The orchestrator is the system's spine. Tightening context contracts and validation semantics here prevents every downstream implementation from inheriting ambiguity.

### 5.4 `docs/ai/outputs/why_engine.md`

**What should change:**
1. Remove in-place `Status` mutation from `RecordUndo()`/`RecordRedo()`. Status is a projection derived from marker nodes.
2. Remove `Status` property from `ExplanationNode` (or make it init-only, set at creation to `Active`).
3. Add a `NodeEffectiveStatus` projection concept: a separate index mapping `ExplanationNodeId → ExplanationNodeStatus`, rebuilt from marker nodes.
4. `GetNodesByStatus()` and `GetPropertyExplanation()` consult the projection, not the node directly.
5. Replace `DateTimeOffset.UtcNow` calls with `IClock` injection.
6. `RecordCommand()` root node: Fix the `BuildCommandRootEdges` placeholder `SourceNodeId` pattern — edges should be created with the root node's actual ID, not a placeholder.
7. Document the honest invariant: "node content is immutable after creation; effective status is a derived projection."

**What should stay:**
- Graph structure (nodes, typed edges)
- Per-stage recording pattern
- `ExplanationRuleRecord` for rule traceability
- All query methods
- `RecordDecisionWithEdges` for inter-stage causal linking
- Index structures (entity, command, stage, rule)
- Decision type catalog (section 7.1)
- Query patterns (section 8)
- Performance analysis

**Why:** The Why Engine is architecturally sound. The changes are about contract honesty and implementation correctness, not redesign.

---

## 6. Revised Contracts

### 6.1 Command Classification

```csharp
public enum CommandOrigin
{
    User,       // Direct user interaction
    Editor,     // Editor layer interpretation (snap, auto-align)
    System,     // System-generated during resolution (auto-filler)
    Template,   // Applied from a template or preset
    Undo,       // Generated by undo system
    Redo        // Generated by redo system
}

public sealed record CommandMetadata
{
    public required CommandId CommandId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required CommandOrigin Origin { get; init; }
    public required string IntentDescription { get; init; }
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }
    public CommandId? ParentCommandId { get; init; }

    /// <summary>
    /// Factory method. Caller provides the timestamp (from IClock).
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

### 6.2 Preview vs Full Validation Split

```csharp
public interface IDesignCommand
{
    CommandMetadata Metadata { get; }
    string CommandType { get; }

    /// <summary>
    /// Structural validation only. No state access. Safe during preview.
    /// Checks: required fields present, dimensions positive, enums valid.
    /// </summary>
    IReadOnlyList<ValidationIssue> ValidateStructure();
}

// In the orchestrator:
public PreviewResult Preview(IDesignCommand command)
{
    var issues = command.ValidateStructure();
    if (issues.Any(i => i.Severity >= ValidationSeverity.Error))
        return PreviewResult.Failed(issues);

    // Stages 1-3 only. No delta tracking. No Why Engine.
    // Stage 3 performs lightweight spatial feasibility checks.
    // ...
}

public CommandResult Execute(IDesignCommand command)
{
    var issues = command.ValidateStructure();
    if (issues.Any(i => i.Severity >= ValidationSeverity.Error))
        return CommandResult.Rejected(command.Metadata, issues);

    // Full pipeline. Stage 1 does contextual validation (entity exists?).
    // Stage 10 does cross-cutting validation.
    // ...
}
```

### 6.3 Run vs Spatial Ownership Boundary

```csharp
// Domain layer — CabinetRun owns ordered-slot logic only
namespace CabinetDesigner.Domain.RunContext;

public sealed class CabinetRun
{
    public RunId Id { get; }
    public WallId WallId { get; }

    /// <summary>
    /// The available length for this run. Set by the spatial layer
    /// when the run is placed along a wall segment.
    /// </summary>
    public Length Capacity { get; private set; }

    private readonly List<RunSlot> _slots = [];
    public IReadOnlyList<RunSlot> Slots => _slots;

    public EndCondition LeftEndCondition { get; private set; }
    public EndCondition RightEndCondition { get; private set; }

    public CabinetRun(RunId id, WallId wallId, Length capacity)
    {
        Id = id;
        WallId = wallId;
        Capacity = capacity;
        LeftEndCondition = EndCondition.Open();
        RightEndCondition = EndCondition.Open();
    }

    public Length OccupiedLength =>
        _slots.Aggregate(Length.Zero, (sum, s) => sum + s.OccupiedWidth);

    public Length RemainingLength
    {
        get
        {
            var remaining = Capacity - OccupiedLength;
            return Length.Max(remaining.Abs(), Length.Zero);
        }
    }

    /// <summary>
    /// Run-local offset of a slot's left edge from slot 0.
    /// This is a run-internal calculation, not world-space.
    /// </summary>
    public Length SlotOffset(int slotIndex) =>
        _slots.Take(slotIndex)
              .Aggregate(Length.Zero, (sum, s) => sum + s.OccupiedWidth);

    public void UpdateCapacity(Length newCapacity)
    {
        Capacity = newCapacity;
    }

    // ... slot management methods unchanged ...
}

// Application layer — Spatial resolution owns world-space mapping
namespace CabinetDesigner.Application.Pipeline.Stages;

/// <summary>
/// World-space placement of a run, owned by the spatial layer.
/// </summary>
public sealed record RunPlacement(
    RunId RunId,
    Point2D StartPoint,
    Point2D EndPoint)
{
    public Length Length => StartPoint.DistanceTo(EndPoint);
    public Vector2D Direction => (EndPoint - StartPoint).Normalized();

    /// <summary>
    /// Compute world-space position from run-local offset.
    /// </summary>
    public Point2D PositionAtOffset(Length offset) =>
        StartPoint + Direction * offset.Inches;
}
```

### 6.4 Stage Context / Stage Result Contracts

```csharp
public sealed class ResolutionContext
{
    // Immutable input
    public required IDesignCommand Command { get; init; }
    public required ResolutionMode Mode { get; init; }

    // Stage results with non-nullable accessors
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

    // ... same pattern for stages 4-11 ...

    // Cross-cutting
    public List<ValidationIssue> AccumulatedIssues { get; } = [];
    public List<ExplanationNodeId> ExplanationNodeIds { get; } = [];
    public bool HasBlockingIssues =>
        AccumulatedIssues.Any(i => i.Severity >= ValidationSeverity.Error);
}
```

### 6.5 Override Value Typing

```csharp
namespace CabinetDesigner.Domain;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Constrained override value. Closed set of types that can appear
/// as parameter overrides, shop standard values, or template defaults.
/// </summary>
public abstract record OverrideValue
{
    public sealed record OfLength(Length Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record OfThickness(Thickness Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record OfAngle(Angle Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record OfString(string Value) : OverrideValue
    {
        public override string ToString() => Value;
    }

    public sealed record OfBool(bool Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record OfInt(int Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record OfDecimal(decimal Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record OfMaterialId(MaterialId Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }

    public sealed record OfHardwareItemId(HardwareItemId Value) : OverrideValue
    {
        public override string ToString() => Value.ToString();
    }
}
```

### 6.6 Why Engine History / Projection Split

```csharp
namespace CabinetDesigner.Application.Explanation;

/// <summary>
/// Explanation node. Immutable after creation.
/// Status is NOT stored on the node — it's a projection.
/// </summary>
public sealed record ExplanationNode
{
    public required ExplanationNodeId Id { get; init; }
    public required ExplanationNodeType NodeType { get; init; }
    public required CommandId CommandId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public int? StageNumber { get; init; }
    public required string DecisionType { get; init; }
    public required string Description { get; init; }
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }
    public required IReadOnlyList<ExplanationEdge> Edges { get; init; }
    public IReadOnlyDictionary<string, string>? Context { get; init; }
    // No Status property — status is derived from marker nodes
}

/// <summary>
/// Projection layer: maps node IDs to their effective status.
/// Rebuilt from undo/redo marker nodes.
/// </summary>
public sealed class ExplanationStatusProjection
{
    private readonly Dictionary<ExplanationNodeId, ExplanationNodeStatus> _statuses = [];

    public ExplanationNodeStatus GetStatus(ExplanationNodeId nodeId) =>
        _statuses.GetValueOrDefault(nodeId, ExplanationNodeStatus.Active);

    public void MarkUndone(IEnumerable<ExplanationNodeId> nodeIds)
    {
        foreach (var id in nodeIds)
            _statuses[id] = ExplanationNodeStatus.Undone;
    }

    public void MarkRedone(IEnumerable<ExplanationNodeId> nodeIds)
    {
        foreach (var id in nodeIds)
        {
            if (_statuses.GetValueOrDefault(id) == ExplanationNodeStatus.Undone)
                _statuses[id] = ExplanationNodeStatus.Redone;
        }
    }
}
```

---

## 7. Implementation Guidance

### 7.1 Revise First (Before Continuing Implementation)

These revisions should be applied to the design documents before any further implementation prompts are issued:

1. **Override typing** (`OverrideValue` union). This touches `domain_model.md`, `commands.md`, and `orchestrator.md`. It is the highest-risk type-safety issue and affects every layer.

2. **`IClock` injection / remove `DateTimeOffset.UtcNow` from domain**. Touches `domain_model.md`, `commands.md`, `why_engine.md`. Simple mechanical change but foundational for determinism.

3. **Rename `Validate()` → `ValidateStructure()`**. Touches `commands.md` and all concrete command implementations. Small change, large contract clarity improvement.

### 7.2 Revise Soon (Before Spatial/Run Implementation)

4. **Extract world-space placement from `CabinetRun`**. Touches `domain_model.md` and `orchestrator.md`. Must be done before the run engine and spatial resolution stages are implemented.

5. **Tighten `ResolutionContext` accessors**. Touches `orchestrator.md`. Should be done before stage implementations begin.

### 7.3 Can Wait (Revise Alongside Implementation)

6. **Why Engine projection split**. Touches `why_engine.md`. The current implementation works correctly for single-threaded use. Can be refined when the Why Engine is implemented.

7. **`DomainOperation` sealed hierarchy**. Touches `orchestrator.md`. Can be refined when stage 2 (Interaction Interpretation) is implemented. Start with 3–4 concrete operations and extend as needed.

8. **`IWhyEngine` interface deduplication** between `orchestrator.md` and `why_engine.md`. Low urgency — just add a cross-reference note.

### 7.4 What Downstream Prompts Should Assume After Reconciliation

- **All domain entities receive timestamps as parameters, never call `UtcNow`.** Application layer owns clock.
- **`CabinetRun` does not store or compute world-space positions.** It owns ordered slots, capacity, and end conditions. Spatial resolution maps runs to world space.
- **Commands expose `ValidateStructure()` (stateless, structural only).** Contextual validation happens in pipeline stages.
- **Override values are `OverrideValue`, not `object`.** Delta values are `DeltaValue`, not `object?`.
- **The Why Engine's node content is immutable.** Effective status is a projection.
- **`CommandOrigin` includes `Editor` for editor-originated commands.** `ParentCommandId` links child commands to their initiating user command.

---

## 8. Final Recommendation

**Revise the current files first.** Specifically, apply revisions 1–3 (override typing, `IClock`, validation rename) to the design documents before issuing the next implementation prompt. These changes are mechanical and affect contracts that every downstream implementation will depend on. Carrying them forward as "known issues" will compound the cost of fixing them later.

Revisions 4–5 (run boundary, context accessors) should be applied before the run engine or orchestrator stages are implemented.

Revisions 6–8 can be carried forward and applied when their respective subsystems are implemented.

The architecture is strong. These are tightening passes, not course corrections. Proceed with confidence after applying the priority revisions.
