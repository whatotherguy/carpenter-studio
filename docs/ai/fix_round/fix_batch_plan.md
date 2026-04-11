# Fix Batch Plan — User Testing Readiness

Date: 2026-04-11
Mode: PLANNING
Context: `final_conformance_review.md`, codebase inspection
Goal: Make the application materially closer to user-testing readiness

---

## Backlog Status Assessment

### Items Already Fixed (Verified in Code)

The following items from `final_conformance_review.md` are **already resolved** and should be removed from the active backlog:

| Finding | Status | Evidence |
|---|---|---|
| **F-1: Application → Rendering dependency** | ✅ FIXED | `CabinetDesigner.Application.csproj` has no `CabinetDesigner.Rendering` reference. `SceneProjector` now lives in `CabinetDesigner.Presentation/Projection/`. |
| **F-2: Microsoft.AspNetCore.App reference** | ✅ FIXED | `CabinetDesigner.Application.csproj` uses `Microsoft.Extensions.DependencyInjection` (8.0.1) standalone package only. |
| **F-3: Presentation → Domain reference** | ✅ PARTIALLY FIXED | `CabinetDesigner.Presentation.csproj` no longer references Domain directly. However, several files still import Domain types via transitive paths (see below). |
| **T-1: Determinism tests** | ✅ FIXED | `DeterminismTests.cs` exists with `SpatialResolutionStage_WithSameState_ProducesIdenticalPlacements`. |
| **T-2: Failure recovery tests** | ✅ FIXED | `CommandPersistenceServiceTests.cs` contains `CommitCommandAsync_WhenRepositoryThrows_RollsBackAllWrites`. |
| **T-3: Working revision reconstruction test** | ✅ FIXED | `WorkingRevisionReconstructionTests.cs` verifies run-slot assignments survive save/load. |
| **T-4: Snapshot blob corruption tests** | ✅ FIXED | `BlobCorruptionTests.cs` covers malformed JSON, missing revision_id, and type mismatches. |
| **T-5: Journal replay / idempotency tests** | ✅ FIXED | `JournalReplayTests.cs` verifies deterministic replay across repeated reads. |

### Items Requiring Attention

| Finding | Status | Notes |
|---|---|---|
| **Domain types in Presentation** | ⚠️ PLAUSIBLE | `EditorCanvasSessionAdapter`, `ApplicationEditorSceneGraph`, and other files import `CabinetDesigner.Domain.*`. While the csproj no longer has a direct reference, these imports work via `Editor → Domain` transitive path. This is technically valid but erodes the DTO-only boundary described in `presentation.md`. |
| **AsyncRelayCommand exception swallowing** | ⚠️ PLAUSIBLE | `AsyncRelayCommand.Execute()` calls `ExecuteAsync()` with `ConfigureAwait(false)` but exceptions from the async lambda are not surfaced to the caller or logged. Silent failures during async command execution. |
| **CabinetFaceSnapCandidateSource sourceIndex** | ⚠️ PLAUSIBLE | The `sourceIndex` counter increments even when a candidate is rejected (distance > snap radius), producing non-contiguous indices. This may cause hysteresis misbehavior if index is used as tie-breaker. |
| **RunService stub methods** | ✅ VERIFIED | `DeleteRunAsync`, `SetCabinetOverrideAsync`, `GetRunSummary` throw `NotImplementedException`. These are known gaps, not bugs. |
| **F-4: Selection bypass documented** | ⚠️ UNVERIFIED | The conformance review marked this as intentional but no explicit documentation exists in the codebase. |

---

## Implementation Batches

### Batch 1: AsyncRelayCommand Exception Handling

**Objective:** Prevent silent exception swallowing in async commands; ensure failures surface to error logging and UX notification infrastructure.

**User Testing Impact:** BLOCKER — Users performing async operations (save, open, create) receive no feedback when operations fail silently.

**Files to Change:**
- `src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs`
- `src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs` (if error routing needed)

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Presentation/Commands/AsyncRelayCommandTests.cs` — Add `ExecuteAsync_WhenDelegateThrows_SurfacesException` and `Execute_WhenDelegateThrows_RoutesToErrorHandler`.

**Architectural Constraints:**
- Do not introduce WPF dependencies into the command class.
- Use existing `IApplicationEventBus` or `IAppLogger` for error routing.
- Preserve `INotifyPropertyChanged` contract for `IsExecuting`.

**Regression Risks:**
- Changing exception handling may cause previously-silent failures to become visible. This is desirable but may expose latent bugs in callers.

**Out of Scope:**
- Global unhandled exception handler for WPF dispatcher.
- Retry logic or user-facing error dialogs (follow-up work).

---

### Batch 2: Snap Engine Consistency

**Objective:** Fix CabinetFaceSnapCandidateSource index assignment so that candidate indices are contiguous and stable for hysteresis tie-breaking.

**User Testing Impact:** HIGH — Snap jitter during cabinet placement/move is confusing and frustrating for users.

**Files to Change:**
- `src/CabinetDesigner.Editor/Snap/CabinetFaceSnapCandidateSource.cs`

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Editor/Snap/CabinetFaceSnapCandidateSourceTests.cs` — Add `GetCandidates_WithMixedDistances_AssignsContiguousIndices` and `GetCandidates_CandidateIndicesAreStableAcrossUpdates`.

**Architectural Constraints:**
- Snap candidate sources must remain stateless per request.
- Index assignment must be deterministic given the same scene state.

**Regression Risks:**
- Changing index semantics may affect existing hysteresis behavior. Run full snap test suite.

**Out of Scope:**
- Other snap candidate sources (run endpoints, wall intersections).
- Hysteresis algorithm changes.

---

### Batch 3: Presentation Layer Domain Import Cleanup

**Objective:** Reduce Domain type imports in Presentation layer to align with DTO-only boundary. Where Editor/Application interfaces accept domain types, evaluate whether adapters should convert at the boundary.

**User Testing Impact:** IMPORTANT (not blocker) — Improves architecture hygiene and prevents accidental domain coupling.

**Files to Inspect/Change:**
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasSessionAdapter.cs` — Already uses `Guid` overloads; verify no direct `CabinetId` construction.
- `src/CabinetDesigner.Presentation/ViewModels/ApplicationEditorSceneGraph.cs` — Imports `CabinetDesigner.Domain.Geometry` and `CabinetDesigner.Domain.Identifiers`. Evaluate if these can use DTOs.
- `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs` — Imports `CabinetDesigner.Domain.Identifiers`.
- `src/CabinetDesigner.Presentation/Projection/SceneProjector.cs` — Uses `CabinetDesigner.Domain.Geometry` for projection math.

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Presentation/EditorCanvasSessionAdapterTests.cs` — Verify adapter works with `Guid` types only.

**Architectural Constraints:**
- Presentation must not directly instantiate domain aggregates.
- Geometry value objects (`Point2D`, `Length`, etc.) may be acceptable for projection math if Editor layer already exposes them.

**Regression Risks:**
- Refactoring type boundaries may break existing functionality. Run full presentation test suite.

**Out of Scope:**
- Editor layer refactoring to accept pure DTOs.
- Removing all transitive domain access (would require deeper architecture change).

---

### Batch 4: WpfEditorCanvasHost Robustness

**Objective:** Add defensive null checks and ensure proper event handler cleanup in `WpfEditorCanvasHost` to prevent crashes during canvas lifecycle.

**User Testing Impact:** HIGH — Crashes during canvas interaction are immediate test blockers.

**Files to Change:**
- `src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs`

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Presentation/WpfEditorCanvasHostTests.cs` — Add `OnCanvasMouseDown_WithNoHandler_DoesNotThrow`, `Dispose_UnsubscribesAllEvents`.

**Architectural Constraints:**
- Must not break existing handler callback contracts.
- Implement `IDisposable` pattern if not already present.

**Regression Risks:**
- Event unsubscription changes may cause missed events if done prematurely.

**Out of Scope:**
- Multi-touch support.
- Additional gesture recognition.

---

### Batch 5: RunService Stub Implementation Progress

**Objective:** Implement `GetRunSummary` to unblock run summary panel functionality. Document remaining stubs as known limitations for user testing.

**User Testing Impact:** MEDIUM — Run summary panel is part of core UX but can be deferred for initial testing.

**Files to Change:**
- `src/CabinetDesigner.Application/Services/RunService.cs`
- `src/CabinetDesigner.Application/State/IDesignStateStore.cs` (if query interface needed)

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Application/Services/RunServiceTests.cs` — Add `GetRunSummary_ReturnsCorrectSlotWidths`.

**Architectural Constraints:**
- Service must query application state store, not domain entities directly.
- Return DTO, not domain aggregates.

**Regression Risks:**
- Minimal — new functionality only.

**Out of Scope:**
- `DeleteRunAsync` — requires domain command implementation.
- `SetCabinetOverrideAsync` — requires domain command implementation.

---

### Batch 6: Documentation and Selection Bypass Rationale

**Objective:** Document intentional architectural decisions (selection state bypass, editor-only state patterns) in code comments and/or architecture docs.

**User Testing Impact:** LOW (follow-up work) — Does not affect functionality but prevents future confusion.

**Files to Change:**
- `src/CabinetDesigner.Editor/EditorSession.cs` — Add XML doc explaining why selection bypasses orchestrator.
- `docs/ai/outputs/editor_engine.md` — Add section on "Editor State vs Design State" boundary.

**Tests to Add/Update:**
- None (documentation only).

**Architectural Constraints:**
- Comments should explain "why" not just "what".

**Regression Risks:**
- None.

**Out of Scope:**
- Architecture refactoring to route selection through orchestrator.

---

### Batch 7: Remaining Pipeline Stage Skeletons

**Objective:** Ensure skeleton pipeline stages (`CostingStage`, `ConstraintPropagationStage`, `PackagingStage`, `PartGenerationStage`, `EngineeringResolutionStage`) return appropriate result types and log that they are unimplemented.

**User Testing Impact:** LOW — Stages are bypassed in current workflow but should not silently succeed.

**Files to Change:**
- `src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs`
- `src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs`

**Tests to Add/Update:**
- `tests/CabinetDesigner.Tests/Pipeline/SkeletonStageTests.cs` — Verify each skeleton returns success with empty result.

**Architectural Constraints:**
- Stages must not throw exceptions.
- Should use `IAppLogger` if available to log "not implemented" at debug level.

**Regression Risks:**
- Minimal — behavior is already "pass through".

**Out of Scope:**
- Actual implementation of these stages (future work).

---

## Summary

| Batch | Priority | Impact | Risk |
|---|---|---|---|
| 1. AsyncRelayCommand Exception Handling | P0 | BLOCKER | Medium |
| 2. Snap Engine Consistency | P1 | HIGH | Low |
| 3. Presentation Domain Import Cleanup | P2 | IMPORTANT | Medium |
| 4. WpfEditorCanvasHost Robustness | P1 | HIGH | Low |
| 5. RunService Stub Progress | P2 | MEDIUM | Low |
| 6. Documentation | P3 | LOW | None |
| 7. Pipeline Skeleton Logging | P3 | LOW | Minimal |

**Recommended Merge Order:** 1 → 4 → 2 → 3 → 5 → 6 → 7

---

## Risks and Follow-Ups

### Risks
1. **Batch 1** may expose latent exceptions that were previously swallowed — verify all async command callsites handle failures appropriately.
2. **Batch 3** domain import cleanup may be incomplete due to transitive dependencies — full removal would require Editor layer changes.

### Follow-Up Work (Not in This Round)
- Global WPF exception handler with user-facing error dialog.
- `DeleteRunCommand` and `SetCabinetOverrideCommand` domain implementation.
- Multi-document workflow support.
- Full pipeline stage implementations (Costing, Packaging, etc.).
