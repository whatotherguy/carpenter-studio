# Fix Batch Plan — Reconciled (User Testing Readiness)

Date: 2026-04-11
Mode: PLANNING (reconciled)
Input: `fix_batch_plan.md`, `final_conformance_review.md`, direct source inspection
Goal: Corrected, priority-ordered implementation plan for user-testing readiness

---

## Reconciliation Summary

The original `fix_batch_plan.md` correctly captured several issues but was **too narrow** on three fronts:

| Gap | Finding | Action |
|---|---|---|
| `ShellViewModel async void` event handler | Verified in code — separate from AsyncRelayCommand | Merged into Batch 1 |
| Persistence test project DLL `HintPath` references | Verified in code — breaks clean-CI builds | New Batch 2 |
| Undo/redo integration for committed drag ops | Plausible but unverified — no test exists | New Batch 5 |
| `CommandPersistenceService` UoW disposal | Not reproduced — `CommitAsync`/`RollbackAsync` both call `DisposeSessionAsync()` internally | Downgraded to observation only |
| `WorkingRevisionRepository` interpolated SQL | Verified in code — table names are hardcoded constants; zero SQL-injection risk | Not an action item; document as non-issue |

---

## Items Already Fixed (Remove from Active Backlog)

| Finding | Status | Evidence |
|---|---|---|
| **F-1: Application → Rendering dependency** | ✅ ALREADY FIXED | `CabinetDesigner.Application.csproj` has no `CabinetDesigner.Rendering` reference; `SceneProjector` lives in `CabinetDesigner.Presentation/Projection/`. |
| **F-2: Microsoft.AspNetCore.App reference** | ✅ ALREADY FIXED | Only `Microsoft.Extensions.DependencyInjection` (8.0.1) standalone package used. |
| **F-3: Presentation → Domain reference** | ✅ ALREADY FIXED | `CabinetDesigner.Presentation.csproj` no longer references Domain directly; `EditorCanvasSessionAdapter` works with `Guid`. |
| **T-1: Determinism tests** | ✅ ALREADY FIXED | `DeterminismTests.cs` exists. |
| **T-2: Failure recovery tests** | ✅ ALREADY FIXED | `CommandPersistenceServiceTests.cs` has `CommitCommandAsync_WhenRepositoryThrows_RollsBackAllWrites`. |
| **T-3: Working revision reconstruction test** | ✅ ALREADY FIXED | `WorkingRevisionReconstructionTests.cs` exists. |
| **T-4: Snapshot blob corruption tests** | ✅ ALREADY FIXED | `BlobCorruptionTests.cs` exists. |
| **T-5: Journal replay / idempotency tests** | ✅ ALREADY FIXED | `JournalReplayTests.cs` exists. |
| **CommandPersistenceService UoW disposal** | ✅ NOT REPRODUCED | `SqliteUnitOfWork.CommitAsync` and `RollbackAsync` each call `DisposeSessionAsync()` before returning, nulling both `_transaction` and `_connection`. The `IAsyncDisposable.DisposeAsync()` is a belt-and-suspenders cleanup, not the primary disposal path. No actual resource leak identified. |
| **WorkingRevisionRepository interpolated SQL** | ✅ NOT REPRODUCED | Table names at `DeleteExistingRowsAsync` are hardcoded string constants (`"parts"`, `"cabinets"`, etc.), not derived from user input. No SQL injection vector exists. Pattern is a minor code-smell only. |

---

## Implementation Batches

### Batch 1 — Exception Surfacing (AsyncRelayCommand + ShellViewModel)

**Objective:** Prevent all silent exception paths in async UI code so that failures surface to error logging and, where possible, to the UX status bar.

**User Testing Impact:** ⛔ BLOCKER — Two distinct silent-failure paths exist:
1. `AsyncRelayCommand.Execute()` calls `ExecuteAsync()` with `ConfigureAwait(false)` but does not catch or route exceptions from the async delegate. Users performing save/open/create/close receive no feedback on failure.
2. `ShellViewModel.OnCatalogItemActivated` is declared `async void`. Exceptions escape to the WPF dispatcher unhandled-exception chain and can crash the application when the user drags a catalog item onto the canvas.

**Status of sub-items:**
- `AsyncRelayCommand` exception swallowing: **verified in code** (`ExecuteAsync` has no exception routing in the `finally` / catch path)
- `OnCatalogItemActivated async void`: **verified in code** (line 126, `ShellViewModel.cs`)

**Files to Change:**
- `src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs`

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Presentation/Commands/AsyncRelayCommandTests.cs`
  - `ExecuteAsync_WhenDelegateThrows_SurfacesExceptionToHandler`
  - `Execute_WhenDelegateThrows_DoesNotCrashSilently`
- `tests/CabinetDesigner.Tests/Presentation/ViewModels/ShellViewModelTests.cs`
  - `OnCatalogItemActivated_WhenAddCabinetThrows_RoutesExceptionAndDoesNotCrash`

**Architectural Constraints:**
- Do not introduce WPF dependencies into `AsyncRelayCommand`.
- Use existing `IApplicationEventBus` or `IAppLogger` for error routing — do not add a new interface.
- `OnCatalogItemActivated` should wrap the await in try-catch and route to the status bar or logger, not re-throw. `async void` is acceptable for event handlers if exceptions are caught inside.
- Preserve `INotifyPropertyChanged` contract for `IsExecuting`.

**Regression Risks:**
- Making previously-silent failures visible may expose latent bugs in callsites — review all `AsyncRelayCommand` usages after this change.
- Wrapping `OnCatalogItemActivated` with try-catch changes exception visibility — verify the catalog panel still recovers correctly when an add-cabinet command fails (e.g., no active project).

**Out of Scope:**
- Global WPF dispatcher unhandled-exception handler.
- User-facing error dialog boxes (follow-up work).
- Retry logic.

---

### Batch 2 — Persistence Test Project Build References

**Objective:** Convert `CabinetDesigner.Persistence.Tests.csproj` from DLL `HintPath` references to `ProjectReference` elements so the test project builds correctly in clean CI environments.

**User Testing Impact:** ⛔ BLOCKER for CI/CD — The current `.csproj` uses:
```xml
<Reference Include="CabinetDesigner.Application">
  <HintPath>..\..\src\CabinetDesigner.Application\bin\Debug\net8.0\CabinetDesigner.Application.dll</HintPath>
</Reference>
```
This requires pre-built assemblies to be on disk. A fresh `dotnet test` in CI without a prior build will fail with a missing-assembly error. All five persistence test files (`WorkingRevisionReconstructionTests`, `BlobCorruptionTests`, `JournalReplayTests`, `CommandPersistenceServiceTests`, integration tests) are gated behind this.

**Status:** **verified in code** — All three references (`CabinetDesigner.Application`, `CabinetDesigner.Domain`, `CabinetDesigner.Persistence`) use `HintPath` to pre-built DLLs.

**Files to Change:**
- `tests/CabinetDesigner.Persistence.Tests/CabinetDesigner.Persistence.Tests.csproj`

**Tests to Add/Update:**
- No new tests. Existing tests should all continue to pass after the reference change.
- Verify: `dotnet test tests/CabinetDesigner.Persistence.Tests/` builds from clean without a prior `dotnet build`.

**Architectural Constraints:**
- `ProjectReference` is the correct mechanism for within-solution dependencies.
- Do not add `<TargetFramework>` or other project properties as part of this change unless required.

**Regression Risks:**
- Minimal. Converting `HintPath` to `ProjectReference` is a mechanical change. The only risk is if the test project picks up source-level types that were previously hidden behind the compiled DLL (unlikely).

**Out of Scope:**
- Changes to `CabinetDesigner.Tests.csproj` (separate test project, already uses `ProjectReference`).

---

### Batch 3 — Snap Engine Index Consistency

**Objective:** Fix `CabinetFaceSnapCandidateSource` so that candidate `sourceIndex` values are contiguous and assigned only to candidates that are actually emitted — not to candidates that are rejected due to distance.

**User Testing Impact:** HIGH — Snap jitter during cabinet placement and move is directly visible and frustrating to users. When a candidate is skipped (distance > snap radius), `sourceIndex` currently still increments, so emitted candidates receive non-contiguous indices. If the hysteresis algorithm uses `sourceIndex` as a tie-breaker for stability, non-contiguous indices produce unstable snap behavior.

**Status:** **verified in code** — `CabinetFaceSnapCandidateSource.cs` lines 36-38: `sourceIndex++` is called inside the `if (distance > request.Settings.SnapRadius)` guard, incrementing the counter for rejected candidates.

**Files to Change:**
- `src/CabinetDesigner.Editor/Snap/CabinetFaceSnapCandidateSource.cs`

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Editor/Snap/CabinetFaceSnapCandidateSourceTests.cs`
  - `GetCandidates_WithMixedDistances_AssignsContiguousIndices` — verify returned candidates have indices 0, 1, 2, … without gaps
  - `GetCandidates_RejectedCandidates_DoNotConsumeIndex` — verify a candidate beyond snap radius does not advance the index counter for subsequent candidates
  - `GetCandidates_CandidateIndicesAreStableAcrossUpdates` — same scene state produces same indices

**Architectural Constraints:**
- Snap candidate sources must remain stateless per request (no mutable instance fields).
- Index assignment must be deterministic given the same scene state.

**Regression Risks:**
- Changing index semantics may affect existing hysteresis behavior — run the full snap test suite after this change.
- If any caller serializes `sourceIndex` values (e.g., for undo/redo label matching), the new contiguous values will differ from previously-stored values. Audit callers.

**Out of Scope:**
- Other snap candidate sources (run endpoints, wall intersections).
- Changes to the hysteresis algorithm itself.

---

### Batch 4 — WpfEditorCanvasHost Robustness

**Objective:** Add defensive null checks and event handler cleanup to `WpfEditorCanvasHost` to prevent crashes during canvas lifecycle transitions (e.g., canvas replaced at runtime, `Dispose` called before interaction completes).

**User Testing Impact:** HIGH — A crash during canvas interaction is an immediate and complete user-testing blocker.

**Status:** **plausible but unverified** — `WpfEditorCanvasHost` subscribes to four WPF routed events in the constructor but has no `Dispose` / `IDisposable` implementation and no event-unsubscription path. If the canvas is torn down before the host, event handlers will fire against a disposed canvas. The existing `Set*Handler` methods accept `null` implicitly but the private fields are not checked before invocation in all paths.

**Files to Change:**
- `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs`

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Presentation/WpfEditorCanvasHostTests.cs`
  - `OnCanvasMouseDown_WithNoHandler_DoesNotThrow`
  - `OnCanvasMouseUp_WithNoHandler_DoesNotThrow`
  - `Dispose_UnsubscribesAllCanvasEvents`
  - `Dispose_CalledTwice_DoesNotThrow`

**Architectural Constraints:**
- Must not break existing handler callback contracts.
- Implement `IDisposable` if not already present; unsubscribe all four routed event handlers in `Dispose`.
- WPF routed events require UI-thread access — ensure `Dispose` is called on the UI thread or guard appropriately.

**Regression Risks:**
- Premature event unsubscription could cause missed drag events if `Dispose` is called mid-drag — ensure drag state is terminated before disposal.

**Out of Scope:**
- Multi-touch support.
- Additional gesture recognition.

---

### Batch 5 — Undo/Redo Integration Test for Committed Drag Operations

**Objective:** Verify — with an executable test — that committed editor drag operations (place cabinet, move cabinet, resize cabinet) produce undo entries and that `UndoAsync()` correctly reverses them.

**User Testing Impact:** HIGH — Undo is a core UX guarantee. If drag-committed commands silently bypass the undo stack, users will encounter a confusing partial-undo state during testing.

**Status:** **plausible but unverified** — The architecture routes `EditorInteractionService.OnDragCommittedAsync()` → `ICommitCommandExecutor.ExecuteAsync()` → `DesignCommandHandler` → `ResolutionOrchestrator` (which tracks deltas via `IDeltaTracker`). This chain should produce undo entries, but no test exercises this end-to-end path. A regression here would not be caught by existing tests.

**Files to Change (tests only):**
- `tests/CabinetDesigner.Tests/Editor/EditorInteractionServiceUndoIntegrationTests.cs` (new file)

**Tests to Add:**
- `DragCommit_PlaceCabinet_ProducesUndoEntry`
- `DragCommit_MoveCabinet_ProducesUndoEntry`
- `DragCommit_ResizeCabinet_ProducesUndoEntry`
- `DragCommit_PlaceCabinet_ThenUndo_RevertsCabinetAddition`

**Architectural Constraints:**
- Tests should use real `EditorInteractionService`, real `EditorSession`, and a stub/mock `ICommitCommandExecutor` that records commands — or a lightweight in-process orchestrator where feasible.
- Do not introduce WPF dependencies into test assembly.
- If the test reveals that drag commits do NOT produce undo entries, fix the production code and re-run. Document the fix in this batch.

**Regression Risks:**
- If the test reveals the undo path is already broken, the fix may require changes to `EditorInteractionService`, `ICommitCommandExecutor`, or the orchestrator. Those changes carry medium risk to the command pipeline.

**Out of Scope:**
- Redo-after-undo (separate concern, already covered by `UndoRedoServiceTests`).

---

### Batch 6 — RunService Stub Progress (GetRunSummary)

**Objective:** Implement `GetRunSummary` to unblock the run summary panel. Document `DeleteRunAsync` and `SetCabinetOverrideAsync` as explicitly known-deferred stubs.

**User Testing Impact:** MEDIUM — The run summary panel is part of core UX and will display an error or crash if `GetRunSummary` throws `NotImplementedException` during a test session.

**Status:** **verified in code** — `RunService.GetRunSummary` throws `NotImplementedException`. `DeleteRunAsync` and `SetCabinetOverrideAsync` also throw `NotImplementedException` but are not called by the current UI flow.

**Files to Change:**
- `src/CabinetDesigner.Application/Services/RunService.cs`
- `src/CabinetDesigner.Application/State/IDesignStateStore.cs` (if a query interface is needed)

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Application/Services/RunServiceTests.cs`
  - `GetRunSummary_ReturnsCorrectRunId`
  - `GetRunSummary_ReturnsCorrectSlotWidths`
  - `GetRunSummary_ForUnknownRunId_ReturnsNull` (or throws — define the contract)

**Architectural Constraints:**
- `GetRunSummary` must query application state store, not domain entities directly.
- Return a DTO (`RunSummaryDto`), not domain aggregates.
- Do not introduce a new interface or abstraction just for this method unless the existing `IDesignStateStore` does not provide the required data.

**Regression Risks:**
- Minimal — new functionality only. `DeleteRunAsync` and `SetCabinetOverrideAsync` remain as `NotImplementedException` stubs; they must retain their existing exception type and message so that any caller can distinguish between "not yet implemented" and a real error.

**Out of Scope:**
- `DeleteRunAsync` — requires `DeleteRunCommand` domain implementation.
- `SetCabinetOverrideAsync` — requires `SetCabinetOverrideCommand` domain implementation.

---

### Batch 7 — Pipeline Skeleton Stage Logging

**Objective:** Ensure skeleton pipeline stages (`CostingStage`, `ConstraintPropagationStage`, `PackagingStage`, `PartGenerationStage`, `EngineeringResolutionStage`) return appropriate success results and emit a debug-level log entry so that silent "passes" are visible in diagnostics output.

**User Testing Impact:** LOW — These stages are not exercised in the current user-testing workflow. However, stages that silently succeed without logging can mask future bugs.

**Status:** **plausible but unverified** — Skeleton stages are expected to exist but their current behavior (silent success vs. silent return) has not been verified against the latest codebase.

**Files to Change:**
- `src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs`

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Pipeline/SkeletonStageTests.cs`
  - `SkeletonStage_ReturnsSuccess_WithEmptyResult` (one test per skeleton stage)

**Architectural Constraints:**
- Stages must not throw exceptions.
- Use `IAppLogger` if available to log "not yet implemented" at `Debug` level.
- Do not remove the skeleton stages; they are placeholders for future work.

**Regression Risks:**
- Minimal — behavior change is adding a log line only. No state changes.

**Out of Scope:**
- Actual implementation of any of these stages.

---

### Batch 8 — Documentation and Architecture Decision Records

**Objective:** Document intentional architectural decisions (selection state bypass, editor-only state pattern, interpolated SQL non-issue) in code comments and/or architecture docs.

**User Testing Impact:** LOW — No effect on functionality.

**Status:** **plausible but unverified** — No explicit documentation of the selection-bypass decision exists in the codebase.

**Files to Change:**
- `src/CabinetDesigner.Editor/EditorSession.cs` — Add XML doc explaining why selection changes bypass the orchestrator (editor interaction state, not design state).
- `docs/ai/outputs/editor_engine.md` — Add "Editor State vs Design State" boundary section.
- `src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs` — Add comment to `DeleteExistingRowsAsync` explaining that table names are compile-time constants and string interpolation is safe here.

**Tests to Add/Update:**
- None (documentation only).

**Architectural Constraints:**
- Comments should explain "why" not just "what".
- Architecture doc additions must not contradict existing spec sections.

**Regression Risks:**
- None.

**Out of Scope:**
- Architecture refactoring to route selection through the orchestrator.

---

## Summary Table

| Batch | Priority | Impact | Risk | Blocker? |
|---|---|---|---|---|
| 1. Exception Surfacing (AsyncRelayCommand + ShellViewModel) | P0 | BLOCKER | Medium | ✅ Yes |
| 2. Persistence Test Project References | P0 | BLOCKER (CI) | Low | ✅ Yes |
| 3. Snap Engine Index Consistency | P1 | HIGH | Low-Medium | No |
| 4. WpfEditorCanvasHost Robustness | P1 | HIGH | Low | No |
| 5. Undo/Redo Drag Integration Test | P1 | HIGH | Medium (if broken) | No |
| 6. RunService GetRunSummary | P2 | MEDIUM | Low | No |
| 7. Pipeline Skeleton Logging | P3 | LOW | Minimal | No |
| 8. Documentation | P3 | LOW | None | No |

**Recommended Merge Order:** 2 → 1 → 4 → 3 → 5 → 6 → 7 → 8

Rationale: Fix CI first (Batch 2) so all subsequent batches are verifiable. Then fix the user-visible crash risk (Batch 1). Then the interaction-layer stability fixes (3, 4). Then the undo verification (5, which may reveal a code fix needed). Then MEDIUM and LOW items.

---

## Risks and Follow-Ups

### Risks

1. **Batch 1** may expose previously-silent failures in async callsites — review all `AsyncRelayCommand` usages and all `async void` event handlers after landing.
2. **Batch 5** may reveal that drag-committed commands do NOT produce undo entries (the architecture suggests they should, but it is unverified). If that is the case, Batch 5 becomes a higher-risk code fix, not just a test addition.
3. **Batch 3** index renumbering may break stable snap in edge cases — run the full snap test suite before merging.

### Observations (No Action Required)

- **CommandPersistenceService UoW disposal**: `SqliteUnitOfWork.CommitAsync` and `RollbackAsync` both call `DisposeSessionAsync()` internally, so resources are cleaned up on every code path. `CommandPersistenceService` not calling `await using _unitOfWork` is a code-smell, not a leak. No action needed unless the implementation of `SqliteUnitOfWork` changes.
- **WorkingRevisionRepository interpolated SQL**: `DeleteExistingRowsAsync` uses `$"DELETE FROM {table}"` where `table` is drawn from a hardcoded `string[]` constant. There is no SQL injection vector. This is intentionally safe and does not require a parameterized alternative.

### Follow-Up Work (Not in This Round)

- Global WPF unhandled-exception handler with user-facing error dialog.
- `DeleteRunCommand` and `SetCabinetOverrideCommand` domain implementation.
- Multi-document workflow support.
- Full pipeline stage implementations (Costing, Packaging, Engineering Resolution, etc.).
- Editor Presentation layer domain-import hygiene (Batch 3 of original plan) — transitive `Domain.*` imports in `ApplicationEditorSceneGraph`, `EditorCanvasViewModel`, `SceneProjector` are technically valid via the `Editor → Domain` reference but erode the DTO-only Presentation boundary over time.
