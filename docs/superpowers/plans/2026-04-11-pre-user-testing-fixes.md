# Pre-User-Testing Fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix all 42 issues (11 critical, 15 important, 8 test gaps, 5 UX gaps, 3 performance) identified in the pre-user-testing evaluation report to bring Carpenter Studio to a releasable state.

**Architecture:** All design mutations flow through `ResolutionOrchestrator`. Domain layer has no UI dependencies. Persistence uses repository pattern with `SqliteUnitOfWork`. Presentation uses MVVM with `ObservableObject`, `RelayCommand`, `AsyncRelayCommand`. Event bus (`IApplicationEventBus`) decouples layers.

**Tech Stack:** C# 12 / .NET 8 / WPF / SQLite / xUnit / `TreatWarningsAsErrors=true` / Nullable reference types enabled

---

## Phase Overview

| Phase | Tasks | Parallel Groups | Complexity |
|-------|-------|-----------------|------------|
| 1: Data Corruption & Deadlocks | C1, C2, C3 | All 3 parallel | High |
| 2: Thread Safety | C4, C5, I6, I2 | All 4 parallel | Medium |
| 3: Editor Interaction Stability | C6, C7, C9, I3, I4 | Group A: C6+I3 parallel; Group B: C7→C9→I4 sequential | High |
| 4: Domain & Pipeline Correctness | C8, C10, I7, I8, I10, I13, I15 | Group A: C10+I7+I8+I10+I15 parallel; Group B: C8→I13 sequential | Medium |
| 5: Persistence, Polish & Test Gaps | C11, I1, I5, I9, I11, I12, I14, TG1–TG8, UX1–UX5, P1–P3 | See phase section | Low–Medium |

## Dependency Graph

```
Phase 1 (data safety)
  └── Phase 2 (thread safety — tests unreliable until Phase 1 merged)
        └── Phase 3 (UX testing — editor unstable until thread-safe)
              └── Phase 4 (feature testing — pipeline correctness)
                    └── Phase 5 (beta hardening — persistence + polish)
```

**Smoke Test Protocol:** After each phase, run `dotnet test` from the repo root. All existing tests must pass. New regression tests introduced in the phase must also pass.

---

## Phase 1: Data Corruption & Deadlocks

All three tasks modify different files and have no shared dependencies. Run as parallel Codex agents.

| Task | File | Parallel? |
|------|------|-----------|
| C1 | `SnapshotService.cs` | ✅ Yes |
| C2 | `CurrentWorkingRevisionSource.cs` + `IDesignStateStore.cs` | ✅ Yes |
| C3 | `SpatialResolutionResult.cs` + `SpatialResolutionStage.cs` + `ValidationStage.cs` | ✅ Yes |

---

### Task C1: Fix WPF Deadlock in SnapshotService.GetRevisionHistory
**Evaluation Report ID:** C1  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`SnapshotService.GetRevisionHistory` (line 121) calls `_revisionRepository.ListAsync(...)`.`GetAwaiter().GetResult()` on the UI thread. On WPF's synchronization context, `.GetAwaiter().GetResult()` causes a classic deadlock: the async state machine posts its continuation to the UI thread, which is already blocked waiting for it. The application hangs permanently when the user views revision history. Universal rule: **Never call `.GetAwaiter().GetResult()` or `.Result`**.

#### Files to Read First
- `src/CabinetDesigner.Application/Services/SnapshotService.cs`
- `src/CabinetDesigner.Application/Services/ISnapshotService.cs` (if it exists — check for the interface)

#### Files to Modify
- `src/CabinetDesigner.Application/Services/SnapshotService.cs`
- The interface file for `ISnapshotService` (update the signature to `Task<IReadOnlyList<RevisionDto>>`)

#### What to Change
1. Change `GetRevisionHistory()` return type to `Task<IReadOnlyList<RevisionDto>>`.
2. Mark the method `async`.
3. Replace `.GetAwaiter().GetResult()` at line 121 with `await ... .ConfigureAwait(false)`.
4. Update the corresponding interface method signature.
5. Fix all callers of `GetRevisionHistory()` (grep for them) to `await` the result.

Exact before/after for the method body:
```csharp
// BEFORE (line 118-129):
public IReadOnlyList<RevisionDto> GetRevisionHistory()
{
    var state = _workingRevisionSource.CaptureCurrentState();
    var revisions = _revisionRepository.ListAsync(state.Project.Id).GetAwaiter().GetResult();
    return revisions.Select(...).ToArray();
}

// AFTER:
public async Task<IReadOnlyList<RevisionDto>> GetRevisionHistoryAsync(CancellationToken ct = default)
{
    var state = _workingRevisionSource.CaptureCurrentState();
    var revisions = await _revisionRepository.ListAsync(state.Project.Id, ct).ConfigureAwait(false);
    return revisions.Select(...).ToArray();
}
```

#### What NOT to Change
- Do not modify unrelated service methods.
- Do not change `_revisionRepository` or `_workingRevisionSource` internals.
- Do not refactor `RevisionDto` construction.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Application/Services/SnapshotServiceTests.cs`
- **Regression test TG8:** Verify `GetRevisionHistoryAsync` does NOT use `.GetAwaiter().GetResult()` (inspect via reflection or simply verify the method is `async` and compiles).
- **Behavior test:** Given a mock `IRevisionRepository` that returns 2 revisions, `GetRevisionHistoryAsync` returns 2 `RevisionDto` objects with correct state mappings.
- **Null guard test:** `GetRevisionHistoryAsync` throws `InvalidOperationException` when no project state is loaded (simulates calling before project opens).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] New regression test passes
- [ ] No `.GetAwaiter().GetResult()` or `.Result` remains in `SnapshotService.cs`

---

### Task C2: Fix Snapshot Data Corruption — Cabinet Type Hardcoded
**Evaluation Report ID:** C2  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`CurrentWorkingRevisionSource.BuildCabinets` (lines 54–65) always constructs `Cabinet` objects with `CabinetCategory.Base` and `ConstructionMethod.Frameless` regardless of the stored `CabinetTypeId`. Wall and Tall cabinets will be persisted to approved snapshots with wrong type data. Manufacturing outputs (cut heights, feature positions) will be based on the wrong category. The fix is to add `CabinetCategory` and `ConstructionMethod` to `CabinetStateRecord` so the correct values are captured at insertion time and are available during state rebuilds — without requiring a catalog lookup at snapshot time.

#### Files to Read First
- `src/CabinetDesigner.Application/State/IDesignStateStore.cs` (contains `CabinetStateRecord` definition, line 45)
- `src/CabinetDesigner.Application/Persistence/CurrentWorkingRevisionSource.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs` (creates `CabinetStateRecord` at line 92)
- `src/CabinetDesigner.Domain/CabinetContext/CabinetCategory.cs`
- `src/CabinetDesigner.Domain/CabinetContext/ConstructionMethod.cs`
- `src/CabinetDesigner.Domain/Commands/Layout/InsertCabinetIntoRunCommand.cs` (check if it carries category/construction)
- `src/CabinetDesigner.Application/Persistence/WorkingRevisionRepository.cs` (how cabinets are loaded from DB)

#### Files to Modify
- `src/CabinetDesigner.Application/State/IDesignStateStore.cs` — add `CabinetCategory` and `ConstructionMethod` to `CabinetStateRecord`
- `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs` — pass correct values when constructing `CabinetStateRecord`
- `src/CabinetDesigner.Application/Persistence/CurrentWorkingRevisionSource.cs` — use record fields instead of hardcoded constants
- `src/CabinetDesigner.Domain/Commands/Layout/AddCabinetToRunCommand.cs` — ensure it carries `CabinetCategory` and `ConstructionMethod`

#### What to Change

**Step 1** — Extend `CabinetStateRecord` in `IDesignStateStore.cs`:
```csharp
// BEFORE (line 45):
public sealed record CabinetStateRecord(
    CabinetId CabinetId,
    string CabinetTypeId,
    Length NominalWidth,
    Length NominalDepth,
    RunId RunId,
    RunSlotId SlotId) : IDomainEntity

// AFTER:
public sealed record CabinetStateRecord(
    CabinetId CabinetId,
    string CabinetTypeId,
    CabinetCategory Category,
    ConstructionMethod Construction,
    Length NominalWidth,
    Length NominalDepth,
    RunId RunId,
    RunSlotId SlotId) : IDomainEntity
```
Add the required `using` statements for `CabinetCategory` and `ConstructionMethod`.

**Step 2** — Read `AddCabinetToRunCommand`. If it already carries `CabinetCategory` / `ConstructionMethod`, use those. If not, add them to the command (check the command file first — do not add if already present).

**Step 3** — Update `InteractionInterpretationStage.ExecuteAddCabinet` at line 92 to pass the correct values from the command.

**Step 4** — Fix `BuildCabinets` in `CurrentWorkingRevisionSource.cs`:
```csharp
// BEFORE (lines 54-65):
private IReadOnlyList<Cabinet> BuildCabinets(RevisionId revisionId) =>
    _stateStore.GetAllCabinets()
        .Select(cabinet => new Cabinet(
            cabinet.CabinetId,
            revisionId,
            cabinet.CabinetTypeId,
            CabinetCategory.Base,        // ← BUG
            ConstructionMethod.Frameless, // ← BUG
            cabinet.NominalWidth,
            cabinet.NominalDepth,
            Length.FromInches(34.5m)))
        .ToArray();

// AFTER:
private IReadOnlyList<Cabinet> BuildCabinets(RevisionId revisionId) =>
    _stateStore.GetAllCabinets()
        .Select(cabinet => new Cabinet(
            cabinet.CabinetId,
            revisionId,
            cabinet.CabinetTypeId,
            cabinet.Category,
            cabinet.Construction,
            cabinet.NominalWidth,
            cabinet.NominalDepth,
            Length.FromInches(34.5m)))
        .ToArray();
```

**Step 5** — Fix the `WorkingRevisionRepository` constructor for `CabinetStateRecord` if it constructs them on load (check `LoadCabinetsAsync`). Ensure it reads `category` and `construction_method` columns from the DB row. If those columns don't exist in the schema yet, add a migration in Phase 5 (C11's task is already in the migration area — note this as a follow-up).

#### What NOT to Change
- Do not modify `Cabinet` domain entity constructor signature.
- Do not add business logic to `CabinetStateRecord`.
- Do not change how `CabinetId` or `NominalWidth` are stored.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Application/Services/CurrentWorkingRevisionSourceTests.cs`
- **Regression test:** After calling `BuildCabinets`, a Wall cabinet (`CabinetCategory.Wall`) retains `CabinetCategory.Wall` — not `CabinetCategory.Base`.
- **Regression test:** After calling `BuildCabinets`, a Tall cabinet retains `CabinetCategory.Tall`.
- **Smoke test:** Round-trip via `InteractionInterpretationStage` → `BuildCabinets` preserves category for all three cabinet categories.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] New regression tests pass
- [ ] `CabinetCategory.Base` and `ConstructionMethod.Frameless` no longer appear as hardcoded values in `BuildCabinets`

---

### Task C3: Fix Wrong Entity ID in ValidationStage Cabinet Position Snapshot
**Evaluation Report ID:** C3  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`ValidationStage.BuildCabinetPositions` (line 80) populates `CabinetPositionSnapshot.CabinetId` with `update.SlotId.ToString()` — the slot's GUID — instead of the cabinet's GUID. Per-entity validation lookups always miss because the stored ID never matches any `CabinetId`. The root cause is that `SlotPositionUpdate` does not carry a `CabinetId` field. The `SpatialResolutionStage` already has `cabinetId` in scope (line 69) — it just doesn't pass it to the record.

#### Files to Read First
- `src/CabinetDesigner.Application/Pipeline/StageResults/SpatialResolutionResult.cs` (lines 17–22: `SlotPositionUpdate` record)
- `src/CabinetDesigner.Application/Pipeline/Stages/SpatialResolutionStage.cs` (lines 52–69: where `SlotPositionUpdate` is constructed)
- `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs` (lines 77–86: `BuildCabinetPositions`)

#### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/StageResults/SpatialResolutionResult.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/SpatialResolutionStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs`

#### What to Change

**Step 1** — Add `CabinetId` to `SlotPositionUpdate` in `SpatialResolutionResult.cs`:
```csharp
// BEFORE (line 17):
public sealed record SlotPositionUpdate(
    RunSlotId SlotId,
    RunId RunId,
    int NewIndex,
    Point2D WorldPosition,
    Length OccupiedWidth);

// AFTER:
public sealed record SlotPositionUpdate(
    RunSlotId SlotId,
    CabinetId CabinetId,
    RunId RunId,
    int NewIndex,
    Point2D WorldPosition,
    Length OccupiedWidth);
```

**Step 2** — Update `SpatialResolutionStage.cs` (line 69) to pass `cabinetId` (which is already in scope from line 55):
```csharp
// BEFORE (line 69):
slotPositionUpdates.Add(new SlotPositionUpdate(slot.Id, run.Id, slot.SlotIndex, leftEdge, slot.OccupiedWidth));

// AFTER:
slotPositionUpdates.Add(new SlotPositionUpdate(slot.Id, cabinetId, run.Id, slot.SlotIndex, leftEdge, slot.OccupiedWidth));
```

**Step 3** — Update `ValidationStage.cs` (line 80) to use the correct ID:
```csharp
// BEFORE (line 79-80):
.Select(update => new CabinetPositionSnapshot(
    CabinetId: update.SlotId.ToString(),

// AFTER:
.Select(update => new CabinetPositionSnapshot(
    CabinetId: update.CabinetId.ToString(),
```

#### What NOT to Change
- Do not modify `CabinetPositionSnapshot` record structure.
- Do not change `RunSummary`, `AdjacencyChange`, or `RunPlacement` records.
- Do not add extra validation fields to `ValidationContext`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Pipeline/ValidationStageTests.cs` (or create it)
- **Regression test:** Build a `SpatialResolutionResult` containing a slot with a known `CabinetId`. Pass through `BuildCabinetPositions`. Assert `CabinetPositionSnapshot.CabinetId == cabinetId.ToString()` (not the slot ID).
- **Distinction test:** Assert that the `SlotId` and `CabinetId` in the resulting `CabinetPositionSnapshot` are different values (prevents regression where both are accidentally set to the same GUID).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] New regression test passes
- [ ] `update.SlotId.ToString()` no longer appears in `ValidationStage.BuildCabinetPositions`

---

### Phase 1 Smoke Tests
After all Phase 1 tasks are merged:
- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test` — all tests pass
- [ ] Manual: Open a project with Wall and Tall cabinets → Approve a revision → Verify the snapshot JSON shows correct `Category` values (not all `Base`)
- [ ] Manual: Open revision history panel → No deadlock → List loads within 2 seconds

---

## Phase 2: Thread Safety

All four tasks modify different files. Run as parallel Codex agents.

| Task | File | Parallel? |
|------|------|-----------|
| C4 | `WhyEngine.cs` | ✅ Yes |
| C5 | `ResolutionOrchestrator.cs` | ✅ Yes |
| I6 | `TextFileAppLogger.cs` | ✅ Yes |
| I2 | `ShellViewModel.cs` | ✅ Yes |

---

### Task C4: Fix Thread-Unsafe Singleton WhyEngine
**Evaluation Report ID:** C4  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`WhyEngine` (lines 20–29) maintains 9 mutable collections (`_nodes`, `_nodeLookup`, `_nodeOrder`, `_commandRoots`, `_commandIndex`, `_entityIndex`, `_stageIndex`, `_ruleIndex`, `_statusProjection`) and a `_nextSequence` counter with no synchronization. It is registered as a singleton. Event bus handlers and the resolution pipeline can call it concurrently, causing `List<T>` index-out-of-range crashes and dictionary corruption. Universal rule: **All singletons must be thread-safe**.

#### Files to Read First
- `src/CabinetDesigner.Application/Explanation/WhyEngine.cs`

#### Files to Modify
- `src/CabinetDesigner.Application/Explanation/WhyEngine.cs`

#### What to Change
Add a `private readonly object _lock = new();` field. Wrap every public method body — `RecordCommand`, `RecordDecision`, `RecordDecisionWithEdges`, `RecordUndo`, `RecordRedo`, `GetEntityHistory`, `GetCommandExplanation`, `GetCommandRoot`, `GetStageDecisions`, `GetDecisionsByRule`, `GetDecisionTrail`, `GetPropertyExplanation`, `GetNodesByStatus`, `GetAllNodes` — in `lock (_lock) { ... }`.

The private method `AppendNode` (which mutates all 9 collections) is called only from within already-locked methods, so it does NOT need its own lock. Do not double-lock.

Example pattern:
```csharp
private readonly object _lock = new();

public IReadOnlyList<ExplanationNodeId> RecordCommand(IDesignCommand command, IReadOnlyList<StateDelta> deltas)
{
    ArgumentNullException.ThrowIfNull(command);
    ArgumentNullException.ThrowIfNull(deltas);

    lock (_lock)
    {
        // ... existing body unchanged ...
    }
}
```

Apply the same pattern to ALL public methods. The `_nextSequence` field increments via `NextTimestamp()` — this is called inside locked methods, so no extra `Interlocked` needed.

#### What NOT to Change
- Do not change method signatures on `IWhyEngine`.
- Do not switch to `ConcurrentDictionary` — the lock approach is simpler and sufficient.
- Do not refactor `AppendNode` or index management methods.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Explanation/WhyEngineTests.cs` (create if missing)
- **Regression test TG7:** Run 4 tasks concurrently, each calling `RecordCommand` with unique commands. After all complete, assert `GetAllNodes().Count == 4 * expectedNodesPerCommand` (no lost writes, no exceptions).
- **Thread safety test:** 2 concurrent callers — one calling `RecordCommand`, one calling `GetAllNodes` in a tight loop — must not throw `InvalidOperationException` ("collection was modified").

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] Concurrent regression test passes without exceptions
- [ ] Every public method in `WhyEngine` is wrapped in `lock (_lock)`

---

### Task C5: Fix Unsound Recursion Guard in ResolutionOrchestrator
**Evaluation Report ID:** C5  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`ResolutionOrchestrator._currentRecursionDepth` (line 20) is a plain `int` incremented at line 71 and decremented in `finally` (line 104). On a singleton orchestrator accessed concurrently, two threads can read the same value, both pass the `>= MaxRecursionDepth` check, and both proceed to increment — bypassing the guard. Similarly, two concurrent decrements can push the value below zero. Universal rule: **All singletons must be thread-safe**.

#### Files to Read First
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs`

#### Files to Modify
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs`

#### What to Change
Replace the plain `int _currentRecursionDepth` with atomic operations:

```csharp
// BEFORE (line 20):
private int _currentRecursionDepth;

// AFTER:
private int _currentRecursionDepth; // accessed only via Interlocked
```

Replace the check-then-increment pattern:
```csharp
// BEFORE (lines 55-71):
if (_currentRecursionDepth >= MaxRecursionDepth)
{
    return CommandResult.Failed(...);
}
// ...
_currentRecursionDepth++;

// AFTER — atomic check-and-increment:
var depth = Interlocked.Increment(ref _currentRecursionDepth);
if (depth > MaxRecursionDepth)
{
    Interlocked.Decrement(ref _currentRecursionDepth);
    return CommandResult.Failed(command.Metadata, [CreateRecursionIssue()]);
}
```

Replace the `finally` decrement:
```csharp
// BEFORE (line 104):
_currentRecursionDepth--;

// AFTER:
Interlocked.Decrement(ref _currentRecursionDepth);
```

Apply the same pattern to the `Preview` method if it also guards `_currentRecursionDepth` (check the file — it does at line 113).

Add `using System.Threading;` if not already present.

#### What NOT to Change
- Do not change `MaxRecursionDepth` value.
- Do not modify `Execute` or `Preview` method signatures.
- Do not add locks around the entire `Execute` method — only the recursion guard needs to be atomic.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Pipeline/ResolutionOrchestratorTests.cs`
- **Regression test:** Two threads simultaneously calling `Execute` on the same orchestrator. Neither should bypass the recursion guard or produce a depth below zero.
- **Existing test:** `MaxRecursionDepth` is enforced — already tested; verify it still passes.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] No plain `_currentRecursionDepth++` or `_currentRecursionDepth--` remains (only `Interlocked.*`)
- [ ] `Preview` method also uses `Interlocked` if it guards depth

---

### Task I6: Fix Concurrent File Access in TextFileAppLogger
**Evaluation Report ID:** I6  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`TextFileAppLogger.Log` (lines 47–51) calls `File.AppendAllText` with no synchronization. The logger is registered as a singleton, and multiple threads (event bus handlers, pipeline stages, UI) log concurrently. `File.AppendAllText` is not thread-safe — concurrent calls produce `IOException: The process cannot access the file`. Universal rule: **All singletons must be thread-safe**.

#### Files to Read First
- `src/CabinetDesigner.Application/Diagnostics/TextFileAppLogger.cs`

#### Files to Modify
- `src/CabinetDesigner.Application/Diagnostics/TextFileAppLogger.cs`

#### What to Change
Add a per-instance lock object and guard the file write:

```csharp
// Add field:
private readonly object _fileLock = new();

// In Log() method, wrap the AppendAllText call:
public void Log(LogEntry entry)
{
    ArgumentNullException.ThrowIfNull(entry);

    try
    {
        Directory.CreateDirectory(_logDirectory);
        var filePath = Path.Combine(_logDirectory, $"app-{entry.Timestamp.UtcDateTime:yyyyMMdd}.log");
        var line = Format(entry);
        lock (_fileLock)
        {
            File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
        }
        if (_mirrorToDebug)
        {
            Debug.WriteLine(line);
        }
    }
    catch
    {
        // Logging must never alter control flow.
    }
}
```

#### What NOT to Change
- Do not change `Format()` or `Sanitize()`.
- Do not change log retention/pruning behavior.
- Do not switch to `StreamWriter` — the lock approach is minimal and sufficient.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Application/Diagnostics/TextFileAppLoggerTests.cs`
- **Regression test:** 10 concurrent threads each log 100 entries. Assert total line count in the log file equals 1000 (no entries lost, no exceptions).
- Use `internal TextFileAppLogger(string logDirectory, bool mirrorToDebug)` constructor to inject a temp directory.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] Concurrent log test passes (1000 entries, no exceptions)
- [ ] `File.AppendAllText` is inside `lock (_fileLock)`

---

### Task I2: Fix NotifyCanExecuteChanged Called Off UI Thread
**Evaluation Report ID:** I2  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`ShellViewModel.RefreshCommandStates` (lines 345–351) calls `NotifyCanExecuteChanged` on four commands. This method is triggered by `OnUndoRedoApplied` (line 314), which is an event bus handler that fires on any thread. WPF binding infrastructure throws `InvalidOperationException` when `CanExecuteChanged` is raised off the UI thread. Universal rule: **UI property changes must happen on the dispatcher thread; event bus handlers may fire on any thread**.

#### Files to Read First
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` (lines 314, 345–351)
- Check existing `Dispatcher.InvokeAsync` patterns in the same file or other ViewModels for the local convention.

#### Files to Modify
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`

#### What to Change
Wrap `RefreshCommandStates()` calls that originate from event bus handlers in `Dispatcher.InvokeAsync`:

```csharp
// BEFORE (line 314):
private void OnUndoRedoApplied<TEvent>(TEvent _) where TEvent : IApplicationEvent => RefreshCommandStates();

// AFTER:
private void OnUndoRedoApplied<TEvent>(TEvent _) where TEvent : IApplicationEvent =>
    System.Windows.Application.Current.Dispatcher.InvokeAsync(RefreshCommandStates);
```

Check whether there are other event bus subscribers in `ShellViewModel` that call `RefreshCommandStates` or `OnPropertyChanged` — apply the same `Dispatcher.InvokeAsync` wrapper to those as well.

Add `using System.Windows.Threading;` if not already present (or check if `System.Windows.Application` is already in scope).

#### What NOT to Change
- Do not change `RefreshCommandStates` itself.
- Do not add marshalling to calls that already originate on the UI thread (e.g., button-click handlers).
- Do not change command implementations.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs`
- **Regression test:** Publish an `UndoAppliedEvent` from a background thread via the event bus. Assert no `InvalidOperationException` is thrown. Assert `UndoCommand.NotifyCanExecuteChanged` was eventually called (use a test dispatcher or count invocations with a mock command).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `OnUndoRedoApplied` wraps `RefreshCommandStates` in `Dispatcher.InvokeAsync`
- [ ] No `NotifyCanExecuteChanged` call path is reachable from an event bus handler without dispatching to UI thread

---

### Phase 2 Smoke Tests
After all Phase 2 tasks are merged:
- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test` — all tests pass including new concurrent tests
- [ ] Manual: Trigger undo/redo rapidly from a background — no cross-thread exception in Output window

---

## Phase 3: Editor Interaction Stability

Two parallel groups. Run C6 and I3 simultaneously. Then run C7, C9, I4 sequentially (all modify `EditorCanvasViewModel.cs`).

| Task | File | Group |
|------|------|-------|
| C6 | `WpfEditorCanvasHost.cs` | Group A (parallel) |
| I3 | `EditorInteractionService.cs` | Group A (parallel) |
| C7 | `EditorCanvasViewModel.cs` | Group B (sequential, first) |
| C9 | `EditorCanvasViewModel.cs` | Group B (sequential, second) |
| I4 | `EditorCanvasViewModel.cs` | Group B (sequential, third) |

---

### Task C6: Fix Mouse Capture Corruption (Left + Middle Button)
**Evaluation Report ID:** C6  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`WpfEditorCanvasHost.OnCanvasMouseDown` (lines 88–108) allows a middle-button press to call `_canvas.CaptureMouse()` even when left-button drag is already in progress. WPF mouse capture is exclusive — the second `CaptureMouse()` call silently supersedes the first. When the left button releases, `OnCanvasMouseUp` skips `ReleaseMouseCapture()` because `_middleDragOrigin.HasValue` (line 134). The capture is never released. `EditorSession` is stuck in a non-Idle mode and the canvas becomes unresponsive. Universal rule: **WPF mouse capture is shared state — guard against concurrent left+middle button interactions**.

#### Files to Read First
- `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs`

#### Files to Modify
- `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs`

#### What to Change

Add an `_isLeftDragActive` flag and guard middle-button capture:

```csharp
// Add field:
private bool _isLeftDragActive;

// In OnCanvasMouseDown:
private void OnCanvasMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (_disposed) return;
    
    if (e.ChangedButton == MouseButton.Left)
    {
        var pos = e.GetPosition(_canvas);
        _canvas.Focus();
        _canvas.CaptureMouse();
        _isLeftDragActive = true;
        _mouseDownHandler?.Invoke(pos.X, pos.Y);
    }
    else if (e.ChangedButton == MouseButton.Middle && !_isLeftDragActive)  // ← guard added
    {
        _middleDragOrigin = e.GetPosition(_canvas);
        _canvas.CaptureMouse();
        var pos = _middleDragOrigin.Value;
        _panStartHandler?.Invoke(pos.X, pos.Y);
    }
}

// In OnCanvasMouseUp, clear the flag:
private void OnCanvasMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
{
    if (_disposed) return;
    
    if (e.ChangedButton == MouseButton.Left)
    {
        _isLeftDragActive = false;
        var pos = e.GetPosition(_canvas);
        if (!_middleDragOrigin.HasValue)
        {
            _canvas.ReleaseMouseCapture();
        }
        _mouseUpHandler?.Invoke(pos.X, pos.Y);
    }
    else if (e.ChangedButton == MouseButton.Middle && _middleDragOrigin.HasValue)
    {
        _middleDragOrigin = null;
        _canvas.ReleaseMouseCapture();
        _panEndHandler?.Invoke();
    }
}
```

Also clear `_isLeftDragActive` in `Dispose()`:
```csharp
public void Dispose()
{
    if (_disposed) return;
    _disposed = true;
    _isLeftDragActive = false;
    _canvas.MouseDown -= OnCanvasMouseDown;
    // ... rest unchanged
}
```

#### What NOT to Change
- Do not change keyboard handling or wheel events.
- Do not change handler registration methods (`SetMouseDownHandler`, etc.).
- Do not modify `EditorCanvasViewModel`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Presentation/WpfEditorCanvasHostTests.cs`
- **Regression test:** Simulate left MouseDown, then middle MouseDown (while left is still held). Assert `_panStartHandler` was NOT invoked (middle button ignored during left drag).
- **Normal test:** Simulate middle MouseDown with no left drag active. Assert `_panStartHandler` was invoked.
- **Release test:** After left drag + ignored middle, simulate left MouseUp. Assert `ReleaseMouseCapture` is called and session can restart.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] Middle-button during left drag is rejected
- [ ] `_isLeftDragActive` is set/cleared correctly around left button events

---

### Task I3: Fix Zero-Width Cabinet Resize — No Minimum Width
**Evaluation Report ID:** I3  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`EditorInteractionService` (line 248) computes `newWidth = Length.FromInches(Math.Max(0m, distanceAlongAxis))`. This allows zero-width cabinets when the user drags the resize handle past the left edge. A zero-width cabinet is invisible, cannot be selected, and can never be deleted through normal interaction. Universal rule: **Clamp user-controlled values to valid ranges — never allow zero-width cabinets**.

#### Files to Read First
- `src/CabinetDesigner.Editor/EditorInteractionService.cs` (around line 248)

#### Files to Modify
- `src/CabinetDesigner.Editor/EditorInteractionService.cs`

#### What to Change
Replace the minimum of `0m` with a minimum meaningful cabinet width. Use a named constant for clarity:

```csharp
// Add constant at class level:
private static readonly Length MinimumCabinetWidth = Length.FromInches(1m);

// BEFORE (line 248):
var newWidth = Length.FromInches(Math.Max(0m, distanceAlongAxis));

// AFTER:
var rawWidth = Length.FromInches(Math.Max(0m, distanceAlongAxis));
var newWidth = rawWidth < MinimumCabinetWidth ? MinimumCabinetWidth : rawWidth;
```

#### What NOT to Change
- Do not change snap point resolution or candidate ranking.
- Do not change `BeginResizeCabinet` or `BeginMoveCabinet`.
- Do not add visual feedback (that is UX2, handled in Phase 5).

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Editor/EditorInteractionServiceTests.cs`
- **Regression test:** Call the resize computation path with a `distanceAlongAxis` of `0m`. Assert returned `ResizeCabinetCommand.NewNominalWidth >= MinimumCabinetWidth`.
- **Regression test:** Call with `distanceAlongAxis = -5m` (drag past left edge). Assert the command width is still `>= MinimumCabinetWidth`, not zero or negative.
- **Pass-through test:** Call with `distanceAlongAxis = 36m`. Assert returned width equals `Length.FromInches(36m)` (valid values unchanged).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] Zero and negative `distanceAlongAxis` produce `>= MinimumCabinetWidth`
- [ ] `Math.Max(0m, ...)` as the final output is gone

---

### Task C7: Fix Unobserved Task Exceptions in CommitDragAsync
**Evaluation Report ID:** C7  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`EditorCanvasViewModel.OnMouseUp` (line 276) uses `_ = CommitDragAsync()` to fire-and-forget. If `CommitDragAsync` throws after its first `await`, the exception is captured in the returned `Task` but never observed — it gets swallowed silently (or raises `UnobservedTaskException` much later). `IsBusy` and `BeginBusy/EndBusy` can become permanently unbalanced. Universal rule: **Never discard Tasks with `_ =`** — use `async void` wrapper with error handling, or `.ContinueWith` with explicit error handling.

#### Files to Read First
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` (lines 265–278 for `OnMouseUp`, lines 437–477 for `CommitDragAsync`)

#### Files to Modify
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`

#### What to Change
Change `OnMouseUp` to an `async void` method with a top-level `try/catch`:

```csharp
// BEFORE (lines 265-278):
public void OnMouseUp(double screenX, double screenY)
{
    _pendingDragCabinetId = null;
    var wasDragActive = _isDragActive;
    _isDragActive = false;
    if (wasDragActive)
    {
        _ = CommitDragAsync();
    }
}

// AFTER:
public async void OnMouseUp(double screenX, double screenY)
{
    _pendingDragCabinetId = null;
    var wasDragActive = _isDragActive;
    _isDragActive = false;
    if (wasDragActive)
    {
        try
        {
            await CommitDragAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Error,
                Category = "EditorCanvasViewModel",
                Message = "Unhandled exception in CommitDragAsync.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = exception
            });
            _interactionService.OnDragAborted();
            StatusMessage = "Drag failed unexpectedly.";
            EndBusy();
        }
    }
}
```

Note: `CommitDragAsync` already calls `BeginBusy()` / `EndBusy()` in its own `try/finally`. The outer catch here handles the case where `CommitDragAsync` itself throws before or after those calls — only call `EndBusy()` in the outer catch if the task was in flight (i.e., `BeginBusy` was already called inside `CommitDragAsync`). Check whether `CommitDragAsync` calls `BeginBusy` at the very start (it does, before the `try`) — so if it throws there, `EndBusy` in the outer catch is needed. If it throws inside its own `try/finally`, `EndBusy` is already called. To be safe, track a flag.

Simpler: Given `CommitDragAsync` calls `BeginBusy()` at line 439 and always calls `EndBusy()` in the `finally` block, the outer `catch` should NOT call `EndBusy()` again. Just log, abort, and update status.

```csharp
// FINAL AFTER:
public async void OnMouseUp(double screenX, double screenY)
{
    _pendingDragCabinetId = null;
    var wasDragActive = _isDragActive;
    _isDragActive = false;
    if (wasDragActive)
    {
        try
        {
            await CommitDragAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Error,
                Category = "EditorCanvasViewModel",
                Message = "Unhandled exception escaping CommitDragAsync.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = exception
            });
            StatusMessage = "Drag failed unexpectedly.";
            RefreshScene();
        }
    }
}
```

#### What NOT to Change
- Do not change `CommitDragAsync` internals.
- Do not change `OnMouseDown` or `OnMouseMove`.
- Do not change `BeginBusy`/`EndBusy`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs`
- **Regression test:** Mock `IEditorInteractionService.OnDragCommittedAsync` to throw an exception. Call `OnMouseUp` (with an active drag). Assert no `UnobservedTaskException` is raised. Assert `StatusMessage` reflects failure state. Assert `IsBusy` returns `false` after completion.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `_ = CommitDragAsync()` no longer exists
- [ ] `OnMouseUp` is `async void` with top-level try/catch

---

### Task C9: Fix Permanent Mode Lock from Second Click During In-Flight Drag Commit
**Evaluation Report ID:** C9  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
When a user rapidly clicks a second cabinet before `CommitDragAsync` completes, `OnMouseDown` (line 165) runs while the async commit is still in flight. The `EditorSession` is still in `MovingCabinet` mode. The subsequent `CommitDragAsync` continuation calls `EditorSession.AssertMode(Idle)`, which throws, leaving the session permanently stuck. The fix is to detect the in-flight state at the start of `OnMouseDown` and abort the prior drag before proceeding. This task depends on C7 being merged first (since C7 changed `OnMouseUp`).

#### Files to Read First
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` (full file — understand all drag-related fields)
- `src/CabinetDesigner.Editor/IEditorInteractionService.cs` (check `OnDragAborted` signature)

#### Files to Modify
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`

#### What to Change

Add a `_isCommitInFlight` flag:
```csharp
// Add field at class level:
private bool _isCommitInFlight;
```

Set it when commit starts, clear it when commit finishes. In `CommitDragAsync`:
```csharp
private async Task CommitDragAsync()
{
    _isCommitInFlight = true;  // ← add at top
    BeginBusy();
    try
    {
        // ... existing body ...
    }
    // ... existing catches ...
    finally
    {
        _isCommitInFlight = false;  // ← add in finally
        EndBusy();
        RefreshScene();
    }
}
```

At the start of `OnMouseDown`, abort any in-flight commit:
```csharp
public void OnMouseDown(double screenX, double screenY)
{
    // Guard: if a drag commit is still in flight, abort the session before accepting new input.
    if (_isCommitInFlight || _isDragActive)
    {
        _isDragActive = false;
        _isCommitInFlight = false;
        _pendingDragCabinetId = null;
        _interactionService.OnDragAborted();
    }

    if (Scene is null)
    {
        return;
    }
    // ... existing body unchanged ...
}
```

#### What NOT to Change
- Do not change `CommitDragAsync` return type or its `BeginBusy`/`EndBusy` calls.
- Do not change `OnMouseMove`.
- Do not change `EditorSession` directly.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs`
- **Regression test:** Begin a drag. Simulate mouse-up (triggering `CommitDragAsync`). Before `CommitDragAsync` completes (use a `TaskCompletionSource` to pause it), call `OnMouseDown`. Assert `OnDragAborted` was called on the interaction service. Assert `_isDragActive == false`. Assert the session does not throw when `CommitDragAsync` eventually resumes.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `_isCommitInFlight` flag is set before `BeginBusy()` in `CommitDragAsync`
- [ ] `OnMouseDown` guards against in-flight commit

---

### Task I4: Fix WpfEditorCanvasHost Memory Leak (Missing Dispose Call)
**Evaluation Report ID:** I4  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`EditorCanvasViewModel.Dispose` (lines 325–331) unsubscribes from event bus events but never calls `_canvasHost.Dispose()`. `WpfEditorCanvasHost.Dispose()` exists and correctly unhooks the four WPF mouse event handlers. Without it, the canvas retains the entire `EditorCanvasViewModel` object graph via the routed event handler closures, leaking memory as long as the WPF visual tree lives. Universal rule: **IDisposable chains must be complete — if a VM owns a disposable resource, it must dispose it**.

#### Files to Read First
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` (lines 325–331)
- `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs` (the `Dispose` method, lines 75–86)

#### Files to Modify
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`

#### What to Change
In the `Dispose` method:
```csharp
// BEFORE (lines 325-331):
public void Dispose()
{
    _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
    _eventBus.Unsubscribe<UndoAppliedEvent>(OnUndoApplied);
    _eventBus.Unsubscribe<RedoAppliedEvent>(OnRedoApplied);
    _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
}

// AFTER:
public void Dispose()
{
    _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
    _eventBus.Unsubscribe<UndoAppliedEvent>(OnUndoApplied);
    _eventBus.Unsubscribe<RedoAppliedEvent>(OnRedoApplied);
    _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
    if (_canvasHost is IDisposable disposableHost)
    {
        disposableHost.Dispose();
    }
}
```

Check if `IEditorCanvasHost` extends `IDisposable`. If it does, use `_canvasHost.Dispose()` directly. If not, cast to `IDisposable` as shown above (safer against future implementations that may not be disposable).

#### What NOT to Change
- Do not change event bus subscriptions.
- Do not change `WpfEditorCanvasHost.Dispose`.
- Do not add `IDisposable` to `IEditorCanvasHost` unless it already extends it.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Presentation/EditorCanvasViewModelTests.cs`
- **Regression test:** Create an `EditorCanvasViewModel` with a mock `IEditorCanvasHost` that also implements `IDisposable`. Call `Dispose()` on the VM. Assert `disposableHost.Dispose()` was called exactly once.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `EditorCanvasViewModel.Dispose()` calls `_canvasHost.Dispose()` (directly or via cast)

---

### Phase 3 Smoke Tests
After all Phase 3 tasks are merged:
- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test` — all tests pass
- [ ] Manual: Hold left button + click middle button — canvas remains responsive
- [ ] Manual: Start a cabinet drag, rapidly click again — no permanent lock
- [ ] Manual: Resize a cabinet to minimum — cabinet visible and selectable

---

## Phase 4: Domain & Pipeline Correctness

Two parallel groups. Run Group A simultaneously, then run Group B sequentially.

| Task | File | Group |
|------|------|-------|
| C10 | `DesignCommandHandler.cs` | Group A (parallel) |
| I7 | `CabinetRun.cs` | Group A (parallel) |
| I8 | `ValidationEngine.cs` + `FullValidationResult.cs` | Group A (parallel) |
| I10 | `Wall.cs` | Group A (parallel) |
| I15 | `EndCondition.cs` | Group A (parallel) |
| C8 | `InteractionInterpretationStage.cs` | Group B (sequential, first) |
| I13 | `InteractionInterpretationStage.cs` | Group B (sequential, second) |

---

### Task C10: Remove Redundant Pre-Check in DesignCommandHandler
**Evaluation Report ID:** C10  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`DesignCommandHandler.ExecuteAsync` (lines 30–34) calls `command.ValidateStructure()` and returns early if errors exist. The `ResolutionOrchestrator.Execute` method (which is called at line 36) performs the exact same validation (line 60–63 in `ResolutionOrchestrator.cs`). If the redundant pre-check throws a synchronous exception — which `ValidateStructure` may do in edge cases — that exception bypasses the orchestrator's fault handling and error logging path. The orchestrator already handles this correctly. Universal rule: **Log before re-throwing; orchestrator is the single choke point for all design changes**.

#### Files to Read First
- `src/CabinetDesigner.Application/Handlers/DesignCommandHandler.cs` (lines 26–36)
- `src/CabinetDesigner.Application/ResolutionOrchestrator.cs` (lines 54–65, showing it already validates)

#### Files to Modify
- `src/CabinetDesigner.Application/Handlers/DesignCommandHandler.cs`

#### What to Change
Remove the redundant pre-validation block:
```csharp
// BEFORE (lines 30-34):
var structureIssues = command.ValidateStructure();
if (structureIssues.Any(issue => issue.Severity >= ValidationSeverity.Error))
{
    return CommandResultDto.Rejected(command.Metadata, command.CommandType, structureIssues);
}

// AFTER: Delete those 5 lines entirely.
```

The method body should now proceed directly from `ArgumentNullException.ThrowIfNull(command)` to `var result = _orchestrator.Execute(command)`.

#### What NOT to Change
- Do not change the orchestrator.
- Do not change the persistence commit path below.
- Do not remove `ArgumentNullException.ThrowIfNull(command)`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Application/Handlers/DesignCommandHandlerTests.cs`
- **Regression test:** Create a command whose `ValidateStructure()` returns an error-severity issue. Call `ExecuteAsync`. Assert the returned `CommandResultDto` reflects the rejected/failed status (the orchestrator returns `CommandResult.Rejected`, which `CommandResultDto.From` maps correctly).
- **Regression test:** The test above must not depend on the pre-check being present — it should pass because the orchestrator handles it.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] Lines 30–34 (redundant `ValidateStructure` call) are deleted

---

### Task I7: Fix CabinetRun.RemainingLength Hides Over-Capacity
**Evaluation Report ID:** I7  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`CabinetRun.RemainingLength` (lines 21–29) returns `Length.Zero` when the run is over capacity. Error messages read "remaining capacity (0)" which is misleading — the run is over capacity, not at zero remaining. Downstream validation messages and the status bar show `0` instead of the actual overage. Universal rule: **Domain invariants must be surfaced clearly, not silently clamped**.

#### Files to Read First
- `src/CabinetDesigner.Domain/RunContext/CabinetRun.cs` (lines 15–31)

#### Files to Modify
- `src/CabinetDesigner.Domain/RunContext/CabinetRun.cs`

#### What to Change
Add an `IsOverCapacity` property and make `RemainingLength` return the signed value:

```csharp
// BEFORE (lines 21-29):
public Length RemainingLength
{
    get
    {
        return OccupiedLength <= Capacity
            ? (Capacity - OccupiedLength).Abs()
            : Length.Zero;
    }
}

// AFTER:
public bool IsOverCapacity => OccupiedLength > Capacity;

public Length RemainingLength => IsOverCapacity
    ? Length.Zero
    : Capacity - OccupiedLength;

public Length OverCapacityAmount => IsOverCapacity
    ? OccupiedLength - Capacity
    : Length.Zero;
```

Then fix UX5 (Phase 5) to use `OverCapacityAmount` in status messages — but that is a separate task. This task only adds the properties.

Also check `RunSummary` in `SpatialResolutionResult.cs` — it contains a `RemainingLength` field populated from `run.RemainingLength`. No change needed there; it will now correctly show zero when over-capacity. The new `IsOverCapacity` property is on the domain entity for use by validation rules.

#### What NOT to Change
- Do not change `OccupiedLength` computation.
- Do not change `Capacity` or `Length` value object.
- Do not modify validation rules (those belong in Phase 5 Polish).

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/RunContext/CabinetRunTests.cs`
- **Regression test:** Add cabinets totaling more than the run capacity. Assert `IsOverCapacity == true`, `RemainingLength == Length.Zero`, `OverCapacityAmount > Length.Zero`.
- **Normal test:** Add cabinets totaling less than capacity. Assert `IsOverCapacity == false`, `RemainingLength == (Capacity - OccupiedLength)`, `OverCapacityAmount == Length.Zero`.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `IsOverCapacity` and `OverCapacityAmount` properties exist
- [ ] `RemainingLength` no longer uses `.Abs()` or returns misleading values

---

### Task I8: Fix ValidationEngine ContextualIssues Always Empty
**Evaluation Report ID:** I8  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`ValidationEngine.Validate` (lines 35–40) hardcodes `ContextualIssues = []`. The `ValidationStage` works around this by overwriting the field with `context.AccumulatedIssues`, but `FullValidationResult.IsValid` uses `AllBaseIssues`, which DOES include `ContextualIssues`. So if the stage wires it up correctly, `IsValid` DOES check contextual issues. The actual problem is that `ValidationEngine` itself pretends it has no contextual evaluation — making the dead field a source of confusion and future misuse. Remove the field from the engine's output and let `ValidationStage` be the sole authority for merging contextual issues. Universal rule: **`ContextualIssues` must be populated or removed — don't ship dead data structures**.

#### Files to Read First
- `src/CabinetDesigner.Domain/Validation/ValidationEngine.cs` (lines 35–40)
- `src/CabinetDesigner.Domain/Validation/FullValidationResult.cs` (full file)
- `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs` (lines 33–52)

#### Files to Modify
- `src/CabinetDesigner.Domain/Validation/FullValidationResult.cs`
- `src/CabinetDesigner.Domain/Validation/ValidationEngine.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs`

#### What to Change

**Option chosen:** Remove `ContextualIssues` from `FullValidationResult` (the engine owns this type) and have `ValidationStage` merge accumulated issues into `CrossCuttingIssues` directly, or add a new `MergedResult` type at the stage level. 

**Simpler approach (least change):** Keep `ContextualIssues` on `FullValidationResult` but change `ValidationEngine.Validate` to accept contextual issues as a parameter, and remove the hardcoded `[]`. `ValidationStage` already has `context.AccumulatedIssues` — pass it.

```csharp
// In ValidationEngine.cs, change Validate signature:
public FullValidationResult Validate(ValidationContext context, IReadOnlyList<ValidationIssue>? contextualIssues = null) =>
    new()
    {
        CrossCuttingIssues = EvaluateRules(_rules, context),
        ContextualIssues = contextualIssues ?? []
    };

// In ValidationStage.cs, pass accumulated issues:
var engineResult = _engine.Validate(validationContext, context.AccumulatedIssues.ToArray());
// Remove the "result = engineResult with { ContextualIssues = ... }" line — it's now redundant.
var result = engineResult;
```

Also update `IValidationEngine` interface accordingly if the signature is defined there.

#### What NOT to Change
- Do not change validation rules.
- Do not change `FullValidationResult.IsValid` or `AllBaseIssues`.
- Do not change `ValidationStrictness` or `ValidationMode`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Validation/ValidationEngineTests.cs`
- **Regression test:** Call `Validate` with 2 contextual issues passed explicitly. Assert `result.ContextualIssues.Count == 2`.
- **Regression test:** Call `Validate` with a blocking contextual issue. Assert `result.IsValid == false`.
- **Default test:** Call `Validate` without contextual issues. Assert `result.ContextualIssues` is empty.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `ContextualIssues = []` hardcode is gone from `ValidationEngine`
- [ ] `FullValidationResult.IsValid` considers contextual issues (existing behavior preserved)

---

### Task I10: Fix Wall.AddOpening — Validation After Construction
**Evaluation Report ID:** I10  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`Wall.AddOpening` (lines 51–68) constructs a new `WallOpening` (which allocates a `WallOpeningId` GUID) before validating that the opening fits within the wall's bounds and doesn't overlap with existing openings. If validation throws, the GUID was allocated unnecessarily and any partially-constructed state is inconsistent. Universal rule: **Guard clauses before object construction — validate inputs before allocating objects or GUIDs**.

#### Files to Read First
- `src/CabinetDesigner.Domain/SpatialContext/Wall.cs` (lines 51–68)

#### Files to Modify
- `src/CabinetDesigner.Domain/SpatialContext/Wall.cs`

#### What to Change
Move both guard clauses before `new WallOpening(...)`:

```csharp
// BEFORE (lines 51-68):
public WallOpening AddOpening(
    WallOpeningType type,
    Length offsetFromStart,
    Length width,
    Length height,
    Length sillHeight)
{
    var opening = new WallOpening(WallOpeningId.New(), Id, type, offsetFromStart, width, height, sillHeight);

    if ((offsetFromStart + width) > Length)
        throw new InvalidOperationException("Opening extends beyond wall length.");

    if (HasOverlappingOpening(opening))
        throw new InvalidOperationException("Opening overlaps with an existing opening.");

    _openings.Add(opening);
    return opening;
}

// AFTER:
public WallOpening AddOpening(
    WallOpeningType type,
    Length offsetFromStart,
    Length width,
    Length height,
    Length sillHeight)
{
    if ((offsetFromStart + width) > Length)
        throw new InvalidOperationException("Opening extends beyond wall length.");

    // Build a temporary span to check overlap without allocating a WallOpeningId.
    if (HasOverlappingOpeningAt(offsetFromStart, width))
        throw new InvalidOperationException("Opening overlaps with an existing opening.");

    var opening = new WallOpening(WallOpeningId.New(), Id, type, offsetFromStart, width, height, sillHeight);
    _openings.Add(opening);
    return opening;
}
```

You must refactor `HasOverlappingOpening(WallOpening candidate)` to `HasOverlappingOpeningAt(Length offsetFromStart, Length width)` to avoid constructing the object for the overlap check:

```csharp
private bool HasOverlappingOpeningAt(Length offsetFromStart, Length width)
{
    var candidateEnd = offsetFromStart + width;
    foreach (var existing in _openings)
    {
        var existingEnd = existing.OffsetFromWallStart + existing.Width;
        if (offsetFromStart < existingEnd && candidateEnd > existing.OffsetFromWallStart)
        {
            return true;
        }
    }
    return false;
}
```

Remove the old `HasOverlappingOpening(WallOpening candidate)` overload if it is no longer used.

#### What NOT to Change
- Do not change `WallOpening` constructor or `WallOpeningId`.
- Do not change `Wall` constructor.
- Do not add extra parameters to `AddOpening`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/SpatialContext/WallTests.cs`
- **Regression test:** Call `AddOpening` with an offset that places the opening exactly beyond the wall length. Assert `InvalidOperationException` is thrown. Assert no `WallOpeningId` was added to `_openings`.
- **Regression test:** Add two openings that overlap. Assert `InvalidOperationException` on the second call. Assert only one opening exists.
- **Pass-through test:** Add a valid opening. Assert it is returned and `_openings.Count == 1`.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] Guard clauses appear before `new WallOpening(...)` in `AddOpening`
- [ ] `HasOverlappingOpeningAt` takes `Length` parameters, not a `WallOpening` instance

---

### Task I15: Fix EndCondition Allows Zero-Width Filler
**Evaluation Report ID:** I15  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`EndCondition` constructor (lines 11–17) validates that `Filler` and `Scribe` types require a non-null `fillerWidth`, but does not validate that `fillerWidth > Length.Zero`. A `FillerWidth` of `Length.Zero` creates an invisible zero-width filler that passes all construction checks and propagates into manufacturing output. Universal rule: **Guard clauses before object construction**.

#### Files to Read First
- `src/CabinetDesigner.Domain/RunContext/EndCondition.cs`

#### Files to Modify
- `src/CabinetDesigner.Domain/RunContext/EndCondition.cs`

#### What to Change
```csharp
// BEFORE (lines 11-17):
public EndCondition(EndConditionType type, Length? fillerWidth = null)
{
    if (type is EndConditionType.Filler or EndConditionType.Scribe && fillerWidth is null)
        throw new ArgumentException($"{type} end condition requires a width.", nameof(fillerWidth));

    Type = type;
    FillerWidth = fillerWidth;
}

// AFTER:
public EndCondition(EndConditionType type, Length? fillerWidth = null)
{
    if (type is EndConditionType.Filler or EndConditionType.Scribe)
    {
        if (fillerWidth is null)
            throw new ArgumentException($"{type} end condition requires a width.", nameof(fillerWidth));
        if (fillerWidth <= Length.Zero)
            throw new ArgumentException($"{type} end condition width must be greater than zero.", nameof(fillerWidth));
    }

    Type = type;
    FillerWidth = fillerWidth;
}
```

#### What NOT to Change
- Do not change `WithFiller` or `WithScribe` factory methods.
- Do not change `Open()` or `AgainstWall()`.
- Do not add width checks to non-filler types.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/RunContext/EndConditionTests.cs`
- **Regression test:** `new EndCondition(EndConditionType.Filler, Length.Zero)` throws `ArgumentException`.
- **Regression test:** `EndCondition.WithFiller(Length.Zero)` throws `ArgumentException`.
- **Pass-through test:** `EndCondition.WithFiller(Length.FromInches(1m))` constructs successfully.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `fillerWidth <= Length.Zero` guard is present in the constructor

---

### Task C8: Wire InsertCabinetIntoRunCommand Through Pipeline
**Evaluation Report ID:** C8  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`RunService.InsertCabinetAsync` (lines 63–79) creates an `InsertCabinetIntoRunCommand` and routes it through `DesignCommandHandler`, which passes it to `ResolutionOrchestrator`. But `InteractionInterpretationStage.Execute` (lines 41–48) only handles `CreateRunCommand`, `AddCabinetToRunCommand`, `MoveCabinetCommand`, and `ResizeCabinetCommand` — the `InsertCabinetIntoRunCommand` case falls through to `DomainOperation.None()`. The service method silently succeeds but produces no state change. Universal rule: **All design mutations flow through `ResolutionOrchestrator`; no silent no-ops**.

#### Files to Read First
- `src/CabinetDesigner.Domain/Commands/Layout/InsertCabinetIntoRunCommand.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs` (full file — study `ExecuteAddCabinet` as the pattern to follow)
- `src/CabinetDesigner.Application/State/IDesignStateStore.cs` (understand `CabinetStateRecord`)
- `src/CabinetDesigner.Domain/RunContext/CabinetRun.cs` (understand `InsertCabinetAt` method)

#### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs`

#### What to Change
Add `InsertCabinetIntoRunCommand` to the switch in `Execute`:

```csharp
// BEFORE (lines 41-48):
var operations = context.Command switch
{
    CreateRunCommand createRun => ExecuteCreateRun(createRun, context),
    AddCabinetToRunCommand addCabinet => ExecuteAddCabinet(addCabinet, context),
    MoveCabinetCommand moveCabinet => ExecuteMoveCabinet(moveCabinet, context),
    ResizeCabinetCommand resizeCabinet => ExecuteResizeCabinet(resizeCabinet, context),
    _ => [new DomainOperation.None()]
};

// AFTER:
var operations = context.Command switch
{
    CreateRunCommand createRun => ExecuteCreateRun(createRun, context),
    AddCabinetToRunCommand addCabinet => ExecuteAddCabinet(addCabinet, context),
    InsertCabinetIntoRunCommand insertCabinet => ExecuteInsertCabinet(insertCabinet, context),
    MoveCabinetCommand moveCabinet => ExecuteMoveCabinet(moveCabinet, context),
    ResizeCabinetCommand resizeCabinet => ExecuteResizeCabinet(resizeCabinet, context),
    _ => [new DomainOperation.None()]
};
```

Add the handler method. Model it on `ExecuteAddCabinet` but use `InsertCabinetAt` with the command's index:

```csharp
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
        command.Category,          // from InsertCabinetIntoRunCommand — verify field exists
        command.Construction,      // from InsertCabinetIntoRunCommand — verify field exists
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
```

**Important:** Before writing this method, read `InsertCabinetIntoRunCommand.cs` to confirm what fields it carries. If `Category` and `Construction` are not on the command (because C2 may not have added them yet), add them. This task depends on C2 being merged.

#### What NOT to Change
- Do not modify `AddCabinetToRunCommand` or its handler.
- Do not change the `InputCaptureStage` or how entities are resolved — note that this method gets `run` directly from `_stateStore`, not from `context.InputCapture.ResolvedEntities`. This is acceptable for insert operations where the run ID is explicit.
- Do not change `CabinetRun.InsertCabinetAt` behavior.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Pipeline/InteractionInterpretationStageTests.cs`
- **Regression test TG6:** Create a run with 2 cabinets. Execute `InsertCabinetIntoRunCommand` at index 1. Assert: state store contains 3 cabinets, the inserted cabinet is at slot index 1, the original cabinet that was at index 1 is now at index 2.
- **Error test:** Execute `InsertCabinetIntoRunCommand` with a non-existent `RunId`. Assert the stage returns a `Failed` result (not a silent no-op).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `InsertCabinetIntoRunCommand` is handled in the switch statement
- [ ] `ExecuteInsertCabinet` produces correct delta records

---

### Task I13: Fix ResolveTargetIndex Dead Parameters and Off-by-One
**Evaluation Report ID:** I13  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`InteractionInterpretationStage.ResolveTargetIndex` (lines 200–211) accepts `isSameRunMove` and `sourceIndex` parameters but never uses them. For a same-run move to `EndOfRun`, the correct index should be `targetRun.Slots.Count - 1` (because removing the source slot first shifts the count by one), but the code returns the unmodified `targetRun.Slots.Count`. This causes an off-by-one insertion error for end-of-run same-run moves. This task must be applied AFTER C8 is merged (both touch `InteractionInterpretationStage.cs`).

#### Files to Read First
- `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs` (full file — read `ExecuteMoveCabinet` to understand how `ResolveTargetIndex` is called and what `isSameRunMove`/`sourceIndex` represent)

#### Files to Modify
- `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs`

#### What to Change
Implement the same-run end-of-run adjustment, then remove the now-used parameters (or keep them used):

```csharp
// BEFORE (lines 200-211):
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

// AFTER:
private static int ResolveTargetIndex(MoveCabinetCommand command, CabinetRun targetRun, bool isSameRunMove, int sourceIndex)
{
    var index = command.TargetPlacement switch
    {
        DomainRunPlacement.StartOfRun => 0,
        DomainRunPlacement.EndOfRun => targetRun.Slots.Count,
        DomainRunPlacement.AtIndex when command.TargetIndex is int targetIndex => targetIndex,
        _ => throw new InvalidOperationException("Move command is missing a target index.")
    };

    // When moving within the same run, the source slot is removed before insertion,
    // shifting all subsequent indices by -1. Adjust for end-of-run to avoid an off-by-one.
    if (isSameRunMove && command.TargetPlacement == DomainRunPlacement.EndOfRun)
    {
        index -= 1;
    }

    return index;
}
```

Read `ExecuteMoveCabinet` to verify how `isSameRunMove` and `sourceIndex` are computed — ensure the call site passes correct values before this fix is meaningful.

#### What NOT to Change
- Do not change `ExecuteMoveCabinet` calling conventions unless `isSameRunMove` / `sourceIndex` are not being passed correctly.
- Do not modify `MoveCabinetCommand` or `CabinetRun.MoveSlot`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Pipeline/InteractionInterpretationStageTests.cs`
- **Regression test:** Create a run with 3 cabinets [A, B, C]. Move cabinet A to `EndOfRun` (same run). Assert result is [B, C, A] (A is now last, not appended past the end causing an index error).
- **Cross-run test:** Move cabinet A from run 1 to `EndOfRun` of run 2 (not a same-run move). Assert run 2 gets A appended correctly (no off-by-one).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `isSameRunMove` is used in the adjustment logic (no longer dead)
- [ ] Same-run end-of-run move produces correct index

---

### Phase 4 Smoke Tests
After all Phase 4 tasks are merged:
- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test` — all tests pass
- [ ] Manual: Insert a cabinet at a specific position in a run — cabinet appears at the correct index
- [ ] Manual: Move a cabinet to end-of-run within the same run — correct final position
- [ ] Manual: Resize a run past capacity — status shows actual overage, not "0"

---

## Phase 5: Persistence, Polish & Test Gaps

Multiple parallel groups. See table.

| Task | File(s) | Group |
|------|---------|-------|
| C11 | `V2_RepairSchemaDrift.cs` | Group A (parallel) |
| I1 | `SnapshotRepository.cs` | Group A (parallel) |
| I11 | `SqliteUnitOfWork.cs` | Group A (parallel) |
| I12 | `WorkingRevisionRepository.cs` | Group A (parallel) |
| I5 | `ProjectService.cs` | Group B (parallel) |
| I9 | `Angle.cs` | Group B (parallel) |
| I14 | `ShellViewModel.cs` | Group B (parallel) |
| P1 | `Project.cs` | Group B (parallel) |
| P2 | `WorkingRevisionRepository.cs` | Sequential after I12 |
| P3 | `ValidationIssueMapper.cs` | Group B (parallel) |
| TG1–TG5, TG7, TG8 | Various test files | Group C (parallel) |
| UX1–UX5 | Various XAML/ViewModel files | Group D |

---

### Task C11: Fix SQL Injection Vector in V2_RepairSchemaDrift
**Evaluation Report ID:** C11  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`V2_RepairSchemaDrift.EnsureColumn` (lines 33–48) interpolates `tableName` and `columnName` directly into SQL strings for `PRAGMA table_info(...)` and `ALTER TABLE ... ADD COLUMN ...`. While current callers are hardcoded constants, any future call with external input would be injectable. Universal rule: **No string interpolation for SQL identifiers — validate against a compile-time allowlist**.

#### Files to Read First
- `src/CabinetDesigner.Persistence/Migrations/V2_RepairSchemaDrift.cs` (full file — see all callers of `EnsureColumn`)

#### Files to Modify
- `src/CabinetDesigner.Persistence/Migrations/V2_RepairSchemaDrift.cs`

#### What to Change
Add a compile-time allowlist and validate inputs:

```csharp
private static readonly IReadOnlySet<string> AllowedTableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "working_revisions",
    "cabinet_slots",
    "cabinets",
    // Add all table names that EnsureColumn is currently called with — grep the file.
};

private static readonly IReadOnlySet<string> AllowedColumnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "slot_index",
    "nominal_depth",
    "construction_method",
    // Add all column names that EnsureColumn is currently called with — grep the file.
};

private static void EnsureColumn(IDbConnection connection, IDbTransaction transaction, string tableName, string columnName, string columnDefinition)
{
    if (!AllowedTableNames.Contains(tableName))
        throw new ArgumentException($"Table name '{tableName}' is not on the schema migration allowlist.", nameof(tableName));

    if (!AllowedColumnNames.Contains(columnName))
        throw new ArgumentException($"Column name '{columnName}' is not on the schema migration allowlist.", nameof(columnName));

    // ... existing implementation unchanged ...
}
```

Read the file to populate the allowlists with the actual values used in the migration.

#### What NOT to Change
- Do not change the migration's logic or existing column additions.
- Do not change other migration files.

#### Tests Required
- Test file: `tests/CabinetDesigner.Persistence.Tests/Migrations/V2RepairSchemaDriftTests.cs`
- **Security regression test:** Call `EnsureColumn` with a table name not in the allowlist (e.g., `"users; DROP TABLE cabinets;--"`). Assert `ArgumentException` is thrown before any SQL is executed.
- **Pass-through test:** Run the full migration against an in-memory SQLite database. Assert it completes without error (existing behavior preserved).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] Allowlist validation occurs before any string interpolation
- [ ] All actual callers' table/column names are in the allowlists

---

### Task I1: Fix SnapshotRepository TOCTOU Race
**Evaluation Report ID:** I1  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`SnapshotRepository.WriteAsync` (lines 17–23) performs `SELECT COUNT(*)` then `INSERT` without an enclosing transaction. Concurrent approval flows can both pass the count check and insert duplicate snapshots for the same revision. Universal rule: **Wrap check-then-act patterns in transactions**.

#### Files to Read First
- `src/CabinetDesigner.Persistence/Repositories/SnapshotRepository.cs` (lines 14–45)

#### Files to Modify
- `src/CabinetDesigner.Persistence/Repositories/SnapshotRepository.cs`

#### What to Change
Replace the SELECT COUNT + INSERT pattern with `INSERT OR IGNORE` plus a rowcount check:

```csharp
public Task WriteAsync(ApprovedSnapshot snapshot, CancellationToken ct = default) =>
    WithConnectionAsync(async (connection, transaction) =>
    {
        var row = SnapshotMapper.ToRow(snapshot);
        using var command = CreateCommand(connection, transaction, """
            INSERT OR IGNORE INTO approved_snapshots(
                revision_id, snapshot_schema_ver, approved_at, approved_by,
                design_blob, parts_blob, manufacturing_blob, install_blob,
                estimate_blob, validation_blob, explanation_blob)
            VALUES(
                @revisionId, @snapshotSchemaVer, @approvedAt, @approvedBy,
                @designBlob, @partsBlob, @manufacturingBlob, @installBlob,
                @estimateBlob, @validationBlob, @explanationBlob);
            """);
        // ... bind all parameters (same as existing) ...
        var rowsAffected = await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"Revision {snapshot.RevisionId} already has an approved snapshot.");
        }
    }, ct);
```

Remove the old `SELECT COUNT(*)` block entirely.

#### What NOT to Change
- Do not change `SnapshotMapper.ToRow`.
- Do not change how the snapshot blobs are parameterized.
- Do not change the `approved_snapshots` immutability trigger in migrations.

#### Tests Required
- Test file: `tests/CabinetDesigner.Persistence.Tests/Repositories/SnapshotRepositoryTests.cs`
- **Regression test TG1:** Two concurrent `WriteAsync` calls for the same `RevisionId`. Assert only one succeeds and the other throws `InvalidOperationException`. Assert the database has exactly one row for that revision ID.
- **Rollback test:** Write a snapshot inside a `SqliteUnitOfWork`. Roll back the unit of work. Assert the snapshot row is NOT present in the database.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `SELECT COUNT(*)` check is removed
- [ ] `INSERT OR IGNORE` + rowcount check is used
- [ ] Concurrent insert regression test passes

---

### Task I5: Fix Inverted HasUnsavedChanges in ProjectService.SaveRevisionAsync
**Evaluation Report ID:** I5  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`ProjectService.SaveRevisionAsync` (line 202) sets `HasUnsavedChanges = true` immediately after successfully persisting the revision. This causes the user to see "unsaved changes" warning immediately after they save — the opposite of the expected behavior. Universal rule: **Domain invariants must be surfaced clearly**.

#### Files to Read First
- `src/CabinetDesigner.Application/Services/ProjectService.cs` (lines 182–215)

#### Files to Modify
- `src/CabinetDesigner.Application/Services/ProjectService.cs`

#### What to Change
```csharp
// BEFORE (line 202):
HasUnsavedChanges = true

// AFTER:
HasUnsavedChanges = false
```

#### What NOT to Change
- Do not change the revision state machine (`ApprovalState.UnderReview`).
- Do not change logging.
- Do not change `MarkCleanAsync` behavior.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Application/Services/ProjectServiceTests.cs`
- **Regression test:** Call `SaveRevisionAsync`. Assert `CurrentProject.HasUnsavedChanges == false` after completion.
- **Pre-condition test:** Set `HasUnsavedChanges = true` before calling `SaveRevisionAsync`. Assert it is `false` after.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `HasUnsavedChanges = false` after save

---

### Task I9: Fix Self-Defeating Angle.Full Constant
**Evaluation Report ID:** I9  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`Angle.Full = new(360m)` (line 26) normalizes 360° to 0° via the `Normalize` method. `angle == Angle.Full` is therefore always equivalent to `angle == Angle.Zero` — the constant is misleading and will cause silent bugs if used in "is this a full rotation?" checks. Currently unused in the codebase. The simplest fix is to remove it and document why.

#### Files to Read First
- `src/CabinetDesigner.Domain/Geometry/Angle.cs`
- Verify `Angle.Full` has no usages: `grep -r "Angle.Full" src/`

#### Files to Modify
- `src/CabinetDesigner.Domain/Geometry/Angle.cs`

#### What to Change
Remove the `Full` constant:
```csharp
// REMOVE this line (line 26):
public static readonly Angle Full = new(360m);  // normalizes to 0
```

If there are any usages of `Angle.Full` that were introduced since the last grep, replace them with the appropriate decimal `360m` comparison or an `IsFullRotation(decimal degrees)` helper that checks the original degree value before normalization.

#### What NOT to Change
- Do not change `Normalize`, `Zero`, `Right`, or `Straight`.
- Do not change `FromDegrees` or `FromRadians`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Geometry/AngleTests.cs`
- **Regression test:** Confirm `Angle.FromDegrees(360m) == Angle.Zero` (documents the normalization behavior explicitly).
- **Documentation test:** Confirm `Angle.FromDegrees(360m).Degrees == 0m` (so any future developer understands why `Full` was removed).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `Angle.Full` no longer exists in the codebase
- [ ] `grep -r "Angle.Full" src/` returns no results

---

### Task I14: Fix Unhandled Exception Before First Await in OnCatalogItemActivated
**Evaluation Report ID:** I14  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`ShellViewModel.OnCatalogItemActivated` (line 126) is an `async void` event handler. Lines 128–138 (`if (!HasActiveProject)` and `ResolveTargetRunId()`) execute synchronously before the first `await` at line 142. If `ResolveTargetRunId()` throws, the exception is raised directly on the WPF dispatcher thread, crashing the application. Universal rule: **Synchronous throws in async void methods must be caught** — the top-level try/catch must wrap the entire method body, starting before `ResolveTargetRunId()`.

#### Files to Read First
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` (lines 126–149)

#### Files to Modify
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`

#### What to Change
```csharp
// BEFORE (lines 126-149):
private async void OnCatalogItemActivated(object? sender, CatalogItemViewModel item)
{
    if (!HasActiveProject)
    {
        return;
    }

    var runId = ResolveTargetRunId();
    if (runId is null)
    {
        Canvas.SetStatusMessage("No runs available to add to.");
        return;
    }

    try
    {
        await Canvas.AddCabinetToRunAsync(runId.Value, item.TypeId, item.DefaultNominalWidthInches)
            .ConfigureAwait(true);
    }
    catch (Exception ex)
    {
        StatusBar.SetStatusMessage($"Failed to add cabinet: {ex.Message}");
    }
}

// AFTER:
private async void OnCatalogItemActivated(object? sender, CatalogItemViewModel item)
{
    try
    {
        if (!HasActiveProject)
        {
            return;
        }

        var runId = ResolveTargetRunId();
        if (runId is null)
        {
            Canvas.SetStatusMessage("No runs available to add to.");
            return;
        }

        await Canvas.AddCabinetToRunAsync(runId.Value, item.TypeId, item.DefaultNominalWidthInches)
            .ConfigureAwait(true);
    }
    catch (Exception ex)
    {
        StatusBar.SetStatusMessage($"Failed to add cabinet: {ex.Message}");
    }
}
```

#### What NOT to Change
- Do not change `ResolveTargetRunId()` logic.
- Do not change `Canvas.AddCabinetToRunAsync` behavior.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/Presentation/ShellViewModelTests.cs`
- **Regression test:** Configure mock such that `ResolveTargetRunId()` throws. Simulate a catalog item activation. Assert no unhandled exception propagates. Assert status bar shows an error message.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] The entire `OnCatalogItemActivated` body is inside the try/catch

---

### Task I11: Fix SqliteUnitOfWork Missing Explicit Rollback
**Evaluation Report ID:** I11  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`SqliteUnitOfWork.DisposeAsync` (lines 53–56) disposes the transaction without checking if it was committed. ADO.NET implicitly rolls back on dispose, but this is inconsistent with the codebase's explicit rollback pattern and can behave unexpectedly with certain SQLite drivers. Universal rule: **Explicit rollback before dispose**.

#### Files to Read First
- `src/CabinetDesigner.Persistence/UnitOfWork/SqliteUnitOfWork.cs` (full file — understand `_committed` flag if it exists, or determine how to track it)

#### Files to Modify
- `src/CabinetDesigner.Persistence/UnitOfWork/SqliteUnitOfWork.cs`

#### What to Change
Track whether the transaction was committed and roll back explicitly on dispose if not:

```csharp
// Add field:
private bool _committed;

// In CommitAsync (wherever it exists):
public async Task CommitAsync(CancellationToken ct = default)
{
    await _transaction.CommitAsync(ct).ConfigureAwait(false);
    _committed = true;
}

// In DisposeAsync:
public async ValueTask DisposeAsync()
{
    if (!_committed && _transaction is not null)
    {
        try
        {
            await _transaction.RollbackAsync().ConfigureAwait(false);
        }
        catch
        {
            // Rollback failure during dispose must not throw.
        }
    }
    await DisposeSessionAsync().ConfigureAwait(false);
}
```

Read the file first — there may already be a `RollbackAsync` method and partial tracking. Adapt to the existing structure.

#### What NOT to Change
- Do not change `CommitAsync` behavior other than setting the flag.
- Do not change `WithConnectionAsync` helpers in repositories.

#### Tests Required
- Test file: `tests/CabinetDesigner.Persistence.Tests/UnitOfWork/SqliteUnitOfWorkTests.cs`
- **Regression test:** Begin a unit of work, write a row, dispose without committing. Assert the row is not present in the database (explicit rollback occurred).
- **Commit test:** Begin, write, commit, dispose. Assert the row is present.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `DisposeAsync` rolls back explicitly if `_committed == false`

---

### Task I12: Fix WorkingRevisionRepository Double Query
**Evaluation Report ID:** I12 / P2  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`WorkingRevisionRepository.LoadAsync` queries cabinet rows twice. The `cabinets` list (line 28) and `cabinetRows` list (line 33) are loaded with separate database queries. The second query (`LoadCabinetRowsAsync`) likely loads the same cabinet rows that `LoadCabinetsAsync` already loaded. Universal rule: **Don't query the same data twice — cache intermediate results in locals**.

#### Files to Read First
- `src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs` (full `LoadAsync` implementation — lines 15–52)
- Understand what `LoadCabinetsAsync` and `LoadCabinetRowsAsync` each return — are they actually the same query or different ones?

#### Files to Modify
- `src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs`

#### What to Change
Read both methods (`LoadCabinetsAsync` and `LoadCabinetRowsAsync`) to understand what data they return. If they are truly duplicate queries:

```csharp
// BEFORE (lines 28-33):
var cabinets = await LoadCabinetsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);
var parts = await LoadPartsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);

var runsById = runs.ToDictionary(run => run.Id);
var cabinetsById = cabinets.ToDictionary(cabinet => cabinet.Id);
var cabinetRows = await LoadCabinetRowsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);

// AFTER — if cabinetRows is redundant:
var cabinets = await LoadCabinetsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);
var parts = await LoadPartsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);

var runsById = runs.ToDictionary(run => run.Id);
var cabinetsById = cabinets.ToDictionary(cabinet => cabinet.Id);
// Use cabinets directly (already loaded above) — if their data provides slot index info.
```

If `LoadCabinetRowsAsync` loads DIFFERENT data than `LoadCabinetsAsync` (e.g., the join table rows for slot assignments), then consolidate them into a single query. The specific fix depends on the actual SQL — read both methods before implementing.

This task also addresses P2 (performance) — note this in the commit message.

#### What NOT to Change
- Do not change `LoadRunsAsync`, `LoadRoomsAsync`, or `LoadWallsAsync`.
- Do not change `SaveAsync`.

#### Tests Required
- Test file: `tests/CabinetDesigner.Persistence.Tests/Repositories/WorkingRevisionRepositoryTests.cs`
- **Regression test:** Create a project with 3 cabinets, save, reload. Assert 3 cabinets are returned with correct slot assignments.
- **Performance assertion:** The number of SQL queries issued during `LoadAsync` must be less than it was before (verify via query count tracking if available, or by reading the implementation).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] No duplicate cabinet data query in `LoadAsync`

---

### Task P1: Fix Project.CurrentRevision Performance — Use MaxBy
**Evaluation Report ID:** P1  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`Project.CurrentRevision` uses `OrderByDescending().First()` which allocates an `IOrderedEnumerable` on every property access. `MaxBy` (available in .NET 6+) returns the maximum element directly without sorting. This is accessed frequently during pipeline execution.

#### Files to Read First
- `src/CabinetDesigner.Domain/ProjectContext/Project.cs`

#### Files to Modify
- `src/CabinetDesigner.Domain/ProjectContext/Project.cs`

#### What to Change
Find the `CurrentRevision` property and replace:
```csharp
// BEFORE:
public Revision? CurrentRevision => _revisions.OrderByDescending(r => r.RevisionNumber).FirstOrDefault();

// AFTER:
public Revision? CurrentRevision => _revisions.MaxBy(r => r.RevisionNumber);
```

#### What NOT to Change
- Do not change `Revision` or `RevisionNumber`.
- Do not change other properties.

#### Tests Required
- Test file: `tests/CabinetDesigner.Tests/ProjectContext/ProjectTests.cs`
- **Regression test:** Add 3 revisions with different revision numbers. Assert `CurrentRevision` returns the one with the highest revision number.

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `OrderByDescending().First()` is replaced with `MaxBy()`

---

### Task P3: Fix ValidationIssueMapper Double Deserialization
**Evaluation Report ID:** P3  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

#### Context
`ValidationIssueMapper.ToRecord` deserializes `AffectedEntityIds` JSON twice. Cache the intermediate result.

#### Files to Read First
- Locate `ValidationIssueMapper.cs`: `grep -r "ValidationIssueMapper" src/` to find the file path.

#### Files to Modify
- The `ValidationIssueMapper.cs` file found above.

#### What to Change
Read the `ToRecord` method. If `AffectedEntityIds` JSON is deserialized twice (e.g., once to check count, once to use), assign the result to a local variable and reuse it:
```csharp
// Example pattern:
var affectedIds = JsonSerializer.Deserialize<IReadOnlyList<string>>(row.AffectedEntityIds) ?? [];
// Use affectedIds in both places rather than deserializing twice.
```

#### What NOT to Change
- Do not change the JSON format.
- Do not change `ValidationIssue` construction.

#### Tests Required
- Verify existing tests pass (no new tests required for a pure performance fix unless behavior changes).

#### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] `AffectedEntityIds` JSON is deserialized at most once per `ToRecord` call

---

### Task TG1–TG5, TG7, TG8: Address Remaining Test Coverage Gaps
**Evaluation Report IDs:** TG1, TG2, TG3, TG4, TG5, TG7, TG8  
**Execution Mode:** HARDENING

Each gap is a separate test file addition. These can be run as parallel Codex agents.

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

---

**TG1** — `SnapshotRepository` inside rolled-back `UnitOfWork`  
Test: `tests/CabinetDesigner.Persistence.Tests/Repositories/SnapshotRepositoryTests.cs`  
Scenario: Write a snapshot inside a `UnitOfWork`. Roll back. Assert no row exists.

**TG2** — `CommandPersistenceService` integration with partial failure  
Test: `tests/CabinetDesigner.Persistence.Tests/Services/CommandPersistenceServiceTests.cs`  
Scenario: Persist a command where the second write operation fails. Assert both writes are rolled back (no partial data).

**TG3** — `AutosaveCheckpointRepository.MarkCleanAsync` with no checkpoint  
Test: `tests/CabinetDesigner.Persistence.Tests/Repositories/AutosaveCheckpointRepositoryTests.cs`  
Scenario: Call `MarkCleanAsync` when no checkpoint row exists for the project. Assert no exception, no row created (or behavior matches intended design — read the implementation first).

**TG4** — Replace `Task.Delay(25)` in `SqliteTestFixture.DisposeAsync`  
Test: `tests/CabinetDesigner.Persistence.Tests/` (find `SqliteTestFixture`)  
Fix: Replace `Task.Delay(25)` with deterministic synchronization — retry the file deletion up to N times with a `File.Exists` check, not a time-based wait.

**TG5** — `ExplanationRepository` ORDER BY tiebreaker  
Fix: Add a secondary sort on a unique column (e.g., `id` or `sequence`) to the ORDER BY clause in the repository's list query.  
Test: Assert deterministic ordering when multiple nodes share the same timestamp.

**TG7** — Concurrent `WhyEngine` access (covered in C4 task, but add the test here if not already added)  
Test: `tests/CabinetDesigner.Tests/Explanation/WhyEngineTests.cs`

**TG8** — `SnapshotService.GetRevisionHistoryAsync` synchronization deadlock test  
This is covered by the C1 task. If the test was not added there, add it here:  
Test: Verify `GetRevisionHistoryAsync` completes without deadlock when called from a `SynchronizationContext`-capturing context.

---

### Task UX1–UX5: UX and Accessibility Gaps
**Evaluation Report IDs:** UX1–UX5  
**Execution Mode:** HARDENING

#### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

---

**UX1** — Add `AutomationProperties.Name` to canvas host, toolbar buttons, issue "Select" button  
Files: XAML files in `src/CabinetDesigner.Presentation/Views/`  
Fix: Add `AutomationProperties.Name="Canvas"` to the canvas host element, `AutomationProperties.Name="Undo"` to toolbar buttons, `AutomationProperties.Name="Select Cabinet"` to the issue panel's Select button.

**UX2** — Visual feedback when resize reaches zero/minimum width  
Files: `src/CabinetDesigner.Rendering/` (wherever resize handle rendering occurs)  
Fix: Apply a visual indicator (e.g., handle color change) when the resize would produce a cabinet at `MinimumCabinetWidth`. Coordinate with the `MinimumCabinetWidth` constant introduced in I3.

**UX3** — Stale scene snapshot after optimistic width edit  
Files: `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs`  
Fix: Invalidate the scene snapshot immediately on drag commit so the optimistic width display is replaced by authoritative state on the first re-render.

**UX4** — Status bar stale when `ProjectSummaryDto` is reference-equal  
Files: `src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs`  
Fix: Force a `PropertyChanged` notification even when the reference is equal but content may have changed (use value comparison or always force the notification).

**UX5** — Over-capacity runs show "remaining: 0" instead of actual overage  
Files: Status bar or run summary panel view  
Fix: Use `CabinetRun.OverCapacityAmount` (introduced in I7) to display "Over capacity by X\"" when `IsOverCapacity`.

---

### Phase 5 Smoke Tests
After all Phase 5 tasks are merged:
- [ ] `dotnet build` — zero warnings, zero errors
- [ ] `dotnet test` — all tests pass (including all new TG tests)
- [ ] Manual: Save a project → no "unsaved changes" indicator appears after save
- [ ] Manual: Over-capacity run → status shows actual overage amount
- [ ] Manual: Run `ASAN` / memory profiler → no canvas host leak after close/reopen
- [ ] Manual: Screen reader (Narrator) — canvas and toolbar buttons have accessible names

---

## Full Verification Checklist (All Phases Complete)

```
[ ] dotnet build — zero warnings, zero errors
[ ] dotnet test -- no test failures (500+ existing + all new regressions)
[ ] No .GetAwaiter().GetResult() or .Result in application code
[ ] No _ = taskName() discards
[ ] No async void without top-level try/catch
[ ] No hardcoded CabinetCategory.Base or ConstructionMethod.Frameless in BuildCabinets
[ ] SlotPositionUpdate carries CabinetId
[ ] WhyEngine public methods are locked
[ ] ResolutionOrchestrator uses Interlocked for recursion depth
[ ] TextFileAppLogger.Log is lock-protected
[ ] RefreshCommandStates() dispatches to UI thread from event bus handlers
[ ] InsertCabinetIntoRunCommand is handled in InteractionInterpretationStage
[ ] EndCondition rejects zero-width FillerWidth
[ ] Wall.AddOpening validates before constructing WallOpening
[ ] Angle.Full is removed from Angle.cs
[ ] SnapshotRepository uses INSERT OR IGNORE
[ ] SqliteUnitOfWork rolls back explicitly on dispose
[ ] ProjectService.SaveRevisionAsync sets HasUnsavedChanges = false
[ ] SQL injection allowlist is in V2_RepairSchemaDrift.EnsureColumn
[ ] AutomationProperties.Name on all interactive controls (UX1)
```
