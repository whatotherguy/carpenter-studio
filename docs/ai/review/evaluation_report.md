# Carpenter Studio — Pre-User-Testing Evaluation Report

**Date:** 2026-04-11
**Reviewer:** Senior SWE / UX audit (AI-assisted)
**Scope:** Full codebase review across all 7 source projects and 2 test projects

---

## Executive Summary

Carpenter Studio is a well-architected cabinet design system with strong domain modeling, a principled 11-stage resolution pipeline, and extensive test coverage (500+ test files). The architecture is sound and the domain layer is mature.

**However, the application is NOT ready for user testing.** There are 11 critical bugs, 15 important bugs, and several UX/accessibility gaps that must be addressed first. The most dangerous issues are: data corruption in approved snapshots (wrong cabinet types), a WPF deadlock in snapshot service, thread-safety violations in singletons, and mouse capture state corruption in the editor.

### Readiness Verdict: **BLOCK — Fix critical issues before user testing**

---

## Critical Issues (Must Fix Before User Testing)

### C1. `SnapshotService.GetRevisionHistory` — WPF Deadlock
- **File:** `src/CabinetDesigner.Application/Services/SnapshotService.cs:121`
- **Bug:** `.GetAwaiter().GetResult()` on async call blocks UI thread, causing classic WPF synchronization context deadlock
- **Impact:** Application hangs permanently when user views revision history
- **Fix:** Make `GetRevisionHistory` async, or use `ConfigureAwait(false)` as interim

### C2. `CurrentWorkingRevisionSource.BuildCabinets` — Snapshot Data Corruption
- **File:** `src/CabinetDesigner.Application/Persistence/CurrentWorkingRevisionSource.cs:54-65`
- **Bug:** All cabinets hardcoded as `CabinetCategory.Base` / `ConstructionMethod.Frameless` regardless of actual type
- **Impact:** Approved snapshots for Wall and Tall cabinets contain wrong type data; manufacturing outputs will be wrong
- **Fix:** Parse `CabinetTypeId` to derive correct category, or add `CabinetCategory` to `CabinetStateRecord`

### C3. `ValidationStage` — Wrong Entity ID in Cabinet Position Snapshot
- **File:** `src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs:80`
- **Bug:** `CabinetPositionSnapshot.CabinetId` populated with `SlotId` instead of `CabinetId`
- **Impact:** Per-entity validation lookups always miss; validation issues never map to the correct cabinet
- **Fix:** Add `CabinetId` to `SlotPositionUpdate` and use it

### C4. `WhyEngine` — Thread-Unsafe Singleton
- **File:** `src/CabinetDesigner.Application/Explanation/WhyEngine.cs:20-29`
- **Bug:** 9 mutable collections with no synchronization; registered as singleton
- **Impact:** Data corruption under concurrent access from event bus handlers and pipeline
- **Fix:** Add `lock` or `ReaderWriterLockSlim` on all read/write operations

### C5. `ResolutionOrchestrator` — Unsound Recursion Guard
- **File:** `src/CabinetDesigner.Application/ResolutionOrchestrator.cs:20,71`
- **Bug:** `_currentRecursionDepth` (plain `int`) mutated without atomicity on a singleton
- **Impact:** Concurrent commands can bypass recursion depth limit or decrement below zero
- **Fix:** Use `Interlocked.Increment`/`Decrement` or a `lock` block

### C6. Concurrent Mouse Capture Corruption
- **File:** `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs:125-147`
- **Bug:** Left+middle click interaction leads to unmatched mouse capture; `EditorSession` gets stuck in non-Idle mode
- **Impact:** Canvas becomes unresponsive — user must restart the application
- **Fix:** Guard `OnCanvasMouseDown` to reject middle-button if left-drag is in progress; maintain capture-ownership flags

### C7. `CommitDragAsync` — Unobserved Task Exceptions
- **File:** `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs:277`
- **Bug:** `_ = CommitDragAsync()` discards task; exceptions after first `await` are swallowed
- **Impact:** Silent failures during drag commit; `IsBusy` can get permanently stuck
- **Fix:** Change to `async void OnMouseUp` with `await`, or add `.ContinueWith` error handler

### C8. `InsertCabinetIntoRunCommand` — Silent No-Op
- **File:** `src/CabinetDesigner.Application/Services/RunService.cs:63-79`
- **Bug:** `InsertCabinetIntoRunCommand` not handled in `InputCaptureStage` or `InteractionInterpretationStage`
- **Impact:** `InsertCabinetAsync` accepts the call but produces no mutation — cabinet is never inserted
- **Fix:** Add pipeline stage handling or remove the service method until wired

### C9. Second Click During In-Flight Drag Commit — Permanent Mode Lock
- **File:** `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs:165`
- **Bug:** Rapid second click before `CommitDragAsync` completes causes `EditorSession.AssertMode(Idle)` to throw; session stuck in `MovingCabinet` mode
- **Impact:** Editor becomes permanently unresponsive to drag operations
- **Fix:** Set `_isDragActive = false` and call `OnDragAborted()` at start of `OnMouseDown` if a drag is pending

### C10. `DesignCommandHandler` — Unlogged Exception Path
- **File:** `src/CabinetDesigner.Application/Handlers/DesignCommandHandler.cs:30-34`
- **Bug:** Redundant `ValidateStructure()` pre-check; if it throws, exception bypasses orchestrator fault handling and is unlogged
- **Impact:** User sees a crash instead of a validation message
- **Fix:** Remove redundant pre-check (orchestrator already validates), or wrap in try/catch

### C11. `V2_RepairSchemaDrift.EnsureColumn` — SQL Injection Vector
- **File:** `src/CabinetDesigner.Persistence/Migrations/V2_RepairSchemaDrift.cs:33-48`
- **Bug:** String interpolation of unvalidated table/column names in SQL (PRAGMA + ALTER TABLE)
- **Impact:** Currently safe (hardcoded callers), but any future call with external input is injectable
- **Fix:** Add compile-time allowlist validation before interpolation

---

## Important Issues (Should Fix Before User Testing)

### I1. `SnapshotRepository.WriteAsync` — TOCTOU Race
- **File:** `src/CabinetDesigner.Persistence/Repositories/SnapshotRepository.cs:17-23`
- **Bug:** SELECT COUNT then INSERT without enclosing transaction; concurrent approval flows can create duplicate snapshots
- **Fix:** Use `INSERT OR IGNORE` + check `changes()`

### I2. `NotifyCanExecuteChanged` Called Off UI Thread
- **File:** `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs:314,345-351`
- **Bug:** Event bus delivers on any thread; `RefreshCommandStates()` calls `NotifyCanExecuteChanged` directly
- **Impact:** Cross-thread `InvalidOperationException` on WPF binding
- **Fix:** Wrap in `Dispatcher.InvokeAsync`

### I3. Zero-Width Resize — No Minimum Width
- **File:** `src/CabinetDesigner.Editor/EditorInteractionService.cs:248`
- **Bug:** `Math.Max(0m, distanceAlongAxis)` allows zero-width cabinets
- **Impact:** 0-pixel invisible cabinets that can't be selected
- **Fix:** Clamp to minimum meaningful width (e.g., 1 inch)

### I4. `WpfEditorCanvasHost` — Never Disposed (Memory Leak)
- **File:** `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs:75-86`
- **Bug:** `EditorCanvasViewModel.Dispose` doesn't call `_canvasHost.Dispose()`; WPF event handlers leak entire object graph
- **Fix:** Call `_canvasHost.Dispose()` from VM's `Dispose()`, null out handler delegates

### I5. `ProjectService.SaveRevisionAsync` — Inverted `HasUnsavedChanges`
- **File:** `src/CabinetDesigner.Application/Services/ProjectService.cs:198-201`
- **Bug:** Sets `HasUnsavedChanges = true` immediately after persisting
- **Impact:** User sees "unsaved changes" warning right after saving
- **Fix:** Set to `false` or call `MarkCleanAsync`

### I6. `TextFileAppLogger.Log` — Concurrent File Access
- **File:** `src/CabinetDesigner.Application/Diagnostics/TextFileAppLogger.cs:47-51`
- **Bug:** `File.AppendAllText` per entry with no locking; logger is singleton
- **Fix:** Use a `lock`-protected `StreamWriter` or `BlockingCollection`

### I7. `CabinetRun.RemainingLength` — Hides Over-Capacity
- **File:** `src/CabinetDesigner.Domain/RunContext/CabinetRun.cs:21-29`
- **Bug:** Returns `Length.Zero` when over capacity; error messages say "remaining capacity (0)"
- **Fix:** Surface actual value or add separate `IsOverCapacity` property

### I8. `ValidationEngine.Validate` — `ContextualIssues` Always Empty
- **File:** `src/CabinetDesigner.Domain/Validation/ValidationEngine.cs:35-40`
- **Bug:** `ContextualIssues` hardcoded to `[]`; `FullValidationResult.IsValid` ignores contextual issues
- **Fix:** Wire up contextual evaluation or remove the field

### I9. `Angle.Full` — Self-Defeating Constant
- **File:** `src/CabinetDesigner.Domain/Geometry/Angle.cs:26`
- **Bug:** `Angle.Full` normalizes 360° to 0°; `angle == Angle.Full` is never true
- **Fix:** Remove or replace with non-`Angle` constant

### I10. `Wall.AddOpening` — Validation After Construction
- **File:** `src/CabinetDesigner.Domain/SpatialContext/Wall.cs:51-68`
- **Bug:** GUID allocated and object constructed before bounds/overlap validation
- **Fix:** Move guard clauses before `new WallOpening()`

### I11. `SqliteUnitOfWork.DisposeAsync` — No Explicit Rollback
- **File:** `src/CabinetDesigner.Persistence/UnitOfWork/SqliteUnitOfWork.cs:53-56`
- **Bug:** Relies on implicit rollback-on-dispose; inconsistent with codebase patterns
- **Fix:** Explicitly call `RollbackAsync` before disposal when uncommitted

### I12. `WorkingRevisionRepository.LoadAsync` — Double Query
- **File:** `src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs:27-33`
- **Bug:** Cabinet rows queried twice per load (identical SELECT)
- **Fix:** Cache first result and pass to both consumers

### I13. `InteractionInterpretationStage.ResolveTargetIndex` — Dead Parameters / Off-by-One
- **File:** `src/CabinetDesigner.Application/Pipeline/Stages/InteractionInterpretationStage.cs:200-210`
- **Bug:** `isSameRunMove` and `sourceIndex` accepted but never used; same-run end-of-run move has off-by-one
- **Fix:** Implement the adjustment or remove parameters

### I14. `async void OnCatalogItemActivated` — Unhandled Exceptions
- **File:** `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs:126-149`
- **Bug:** Synchronous throw before first `await` crashes the dispatcher
- **Fix:** Wrap entire body in try/catch starting before `ResolveTargetRunId()`

### I15. `EndCondition` — Allows Zero-Width Filler
- **File:** `src/CabinetDesigner.Domain/RunContext/EndCondition.cs:11-17`
- **Bug:** `FillerWidth = Length.Zero` passes construction for Filler/Scribe types
- **Fix:** Add `fillerWidth > Length.Zero` guard

---

## Test Coverage Gaps

| Gap | Description |
|-----|-------------|
| **TG1** | No test for `SnapshotRepository.WriteAsync` inside a rolled-back `UnitOfWork` — leaked snapshot rows would be permanent |
| **TG2** | No integration test for `CommandPersistenceService` with real SQLite under partial failure |
| **TG3** | No test for `AutosaveCheckpointRepository.MarkCleanAsync` when no checkpoint exists |
| **TG4** | `SqliteTestFixture.DisposeAsync` uses `Task.Delay(25)` — flaky on slow CI (Windows) |
| **TG5** | `ExplanationRepository` ORDER BY lacks tiebreaker — non-deterministic for same-tick nodes |
| **TG6** | No test for `InsertCabinetIntoRunCommand` pipeline handling (because it's not implemented) |
| **TG7** | No test for concurrent `WhyEngine` access |
| **TG8** | No test for `SnapshotService.GetRevisionHistory` synchronization context deadlock |

---

## UX / Accessibility Gaps

| Gap | Description |
|-----|-------------|
| **UX1** | No `AutomationProperties.Name` on canvas host, toolbar buttons, or issue "Select" button |
| **UX2** | No drag-time visual feedback when resize reaches invalid state (zero-width) |
| **UX3** | Stale scene snapshot can transiently revert optimistic width display after edit |
| **UX4** | Status bar shows stale unsaved-changes state when `ProjectSummaryDto` is reference-equal |
| **UX5** | Over-capacity runs show "remaining: 0" instead of the actual overage amount |

---

## Performance Concerns

| Issue | Description |
|-------|-------------|
| **P1** | `Project.CurrentRevision` uses `OrderByDescending().First()` on every access — allocates `IOrderedEnumerable` each time; use `MaxBy` |
| **P2** | `WorkingRevisionRepository.LoadAsync` queries cabinet rows twice |
| **P3** | `ValidationIssueMapper.ToRecord` deserializes `AffectedEntityIds` JSON twice |

---

## Summary by Severity

| Severity | Count |
|----------|-------|
| Critical | 11 |
| Important | 15 |
| Test Gap | 8 |
| UX/A11y | 5 |
| Performance | 3 |
| **Total** | **42** |
