# Final Architecture Conformance Review

Date: 2026-04-08
Mode: REVIEW
Auditor: Claude Opus 4.6
Scope: Full codebase review against architecture spec

## Closure Update

Status as of 2026-04-08 after remediation pass:

- `F-1` resolved: `SceneProjector` and `ISceneProjector` were moved out of `CabinetDesigner.Application` into `CabinetDesigner.Presentation`, removing the `Application -> Rendering` dependency inversion.
- `F-2` resolved: `Microsoft.AspNetCore.App` was removed from `CabinetDesigner.Application` and replaced with the standalone DI package.
- `F-3` resolved: `CabinetDesigner.Presentation` no longer references `CabinetDesigner.Domain`; `EditorCanvasSessionAdapter` now works with `Guid` and `EditorSession` owns `CabinetId` construction.
- `F-4` documented as intentional: selection changes remain direct editor-session updates because they are editor interaction state, not design-state mutation.
- `T-1` resolved: determinism coverage added.
- `T-2` resolved: failure-recovery / rollback coverage added.
- `T-3` resolved: working revision reconstruction coverage added.
- `T-4` resolved: snapshot blob corruption coverage added, and unreadable blobs now fail gracefully.
- `T-5` resolved: journal replay / idempotency coverage added.
- Full verification passed after remediation:
  `dotnet test tests/CabinetDesigner.Tests/CabinetDesigner.Tests.csproj`
  `dotnet test tests/CabinetDesigner.Persistence.Tests/CabinetDesigner.Persistence.Tests.csproj`

---

## 1. IMPLEMENTATION PLAN

### 1.1 Review Scope

This review examines the current implementation against the architecture defined in:
- `docs/ai/context/code_phase_global_instructions.md`
- `docs/ai/context/architecture_summary.md`
- All `docs/ai/outputs/*.md` subsystem specs

### 1.2 Current State Summary

The repository now contains **8 projects** with implementation code:

| Project | Status | File Count |
|---|---|---|
| `CabinetDesigner.Domain` | Implemented | ~96 files |
| `CabinetDesigner.Application` | Implemented | ~94 files |
| `CabinetDesigner.Persistence` | Implemented | Repositories, mappers, models, migrations |
| `CabinetDesigner.Presentation` | Implemented | ViewModels, WPF shell, commands |
| `CabinetDesigner.Editor` | Implemented | Session, interaction service, snap engine |
| `CabinetDesigner.Rendering` | Implemented | Canvas, layers, hit testing, DTOs |
| `CabinetDesigner.Tests` | Implemented | Domain, application, pipeline tests |
| `CabinetDesigner.Persistence.Tests` | Implemented | Integration, round-trip, constraint tests |

Missing from spec: `CabinetDesigner.App` (bootstrap), `CabinetDesigner.Infrastructure`, `CabinetDesigner.Integrations`, `CabinetDesigner.Exports`.

### 1.3 Conformance Summary

| Area | Verdict |
|---|---|
| Domain purity | PASS |
| Geometry value objects | PASS |
| Command architecture | PASS |
| Orchestrator single choke point | PASS |
| Persistence separation | PASS |
| Snapshot immutability | PASS |
| **Dependency direction** | **FAIL — 3 violations** |
| **MVVM boundary** | **FAIL — 1 violation** |
| **Missing regression tests** | **FAIL — 5 gaps** |

---

## 2. FILES TO CREATE

| File | Project | Purpose |
|---|---|---|
| `tests/CabinetDesigner.Tests/Pipeline/DeterminismTests.cs` | Tests | Verify same command input always produces same output |
| `tests/CabinetDesigner.Tests/Pipeline/FailureRecoveryTests.cs` | Tests | Verify rollback on partial persistence failure |
| `tests/CabinetDesigner.Persistence.Tests/WorkingRevisionReconstructionTests.cs` | Persistence.Tests | Verify `LoadAsync` correctly reconstructs run-cabinet slot assignments |
| `tests/CabinetDesigner.Persistence.Tests/BlobCorruptionTests.cs` | Persistence.Tests | Verify malformed snapshot blobs fail gracefully |

---

## 3. FILES TO MODIFY

| File | Change | Severity |
|---|---|---|
| `src/CabinetDesigner.Application/CabinetDesigner.Application.csproj` | Remove `ProjectReference` to `CabinetDesigner.Rendering` | HIGH |
| `src/CabinetDesigner.Application/CabinetDesigner.Application.csproj` | Remove `FrameworkReference` to `Microsoft.AspNetCore.App` (wrong framework for desktop app) | MEDIUM |
| `src/CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj` | Remove `ProjectReference` to `CabinetDesigner.Domain` | HIGH |
| `src/CabinetDesigner.Presentation/ViewModels/EditorCanvasSessionAdapter.cs` | Remove `using CabinetDesigner.Domain.Identifiers`; work with `Guid` only | MEDIUM |
| `src/CabinetDesigner.Application/Projection/SceneProjector.cs` | Move to `CabinetDesigner.Presentation` or a new `CabinetDesigner.Application.Abstractions` project so rendering DTOs flow correctly | HIGH |
| `src/CabinetDesigner.Application/Projection/ISceneProjector.cs` | Move with `SceneProjector.cs` (same dependency issue) | HIGH |

---

## 4. CODE

### 4.1 Conformance Findings by Layer

#### DOMAIN — PASS (Exemplary)

The domain layer has **zero conformance violations**:

- **No primitive dimension leaks**: All spatial properties use `Length`, `Offset`, `Angle`, `Point2D`, `Vector2D`, `Thickness`, or `Rect2D`. The only raw `decimal` usage is inside geometry value object internals (appropriate). `double` appears only as a documented escape hatch for transcendental math (`sqrt`, `sin`, `cos`), always re-wrapped into domain types.
- **No public setters on aggregates**: All mutable properties use `private set` with explicit mutation methods (`Cabinet.Resize()`, `CabinetRun.AppendCabinet()`, `Revision.TransitionTo()`, etc.).
- **No framework dependencies**: Zero references to WPF, ASP.NET, EF, or persistence. The `.csproj` has no `ProjectReference` elements.
- **Strongly typed identifiers**: 15 `readonly record struct` ID types prevent cross-entity reference errors.
- **Immutable commands**: All commands are sealed records implementing `IDesignCommand`. Commands carry metadata but no `Execute()` method — the orchestrator owns execution.
- **Collections encapsulated**: Private `List<T>` with public `IReadOnlyList<T>` throughout.

#### APPLICATION — PASS with 2 dependency violations

**Orchestrator (PASS):**
- `ResolutionOrchestrator` is the single choke point for all design state changes.
- 11-stage pipeline executes in order with mode-aware stage execution (full vs. preview).
- Recursion protection (`MaxRecursionDepth = 3`).
- Delta tracking via `IDeltaTracker` — all mutations captured for undo/redo.
- Failed stages halt pipeline, discard deltas, return failure result. No partial commits.
- Undo/redo applies reverse/forward deltas with explanation graph recording.

**Command Handlers (PASS):**
- `DesignCommandHandler`: validates structure → delegates to orchestrator → persists on success → publishes event. Correct flow.
- `PreviewCommandHandler`: read-only fast-path via `ResolutionOrchestrator.Preview()`. No mutations.
- `EditorCommandHandler`: isolated editor-state-only path via `IEditorStateManager`. Does not touch design state.

**Services (PASS):**
- All services (`RunService`, `ProjectService`, `UndoRedoService`, `SnapshotService`) construct commands and delegate to handlers. No domain bypass.

**VIOLATION F-1: Application → Rendering dependency**

```
src/CabinetDesigner.Application/CabinetDesigner.Application.csproj:
  <ProjectReference Include="..\CabinetDesigner.Rendering\CabinetDesigner.Rendering.csproj" />
```

Per architecture spec: `Application → Domain` (only). The `SceneProjector` and `ISceneProjector` in `Application/Projection/` import `CabinetDesigner.Rendering.DTOs`, creating an inverted dependency. Rendering should depend on Application, not the reverse.

**Root cause:** `SceneProjector` produces `RenderSceneDto` (defined in Rendering). The projector should either:
(a) Live in Presentation (which already references both Application and Rendering), or
(b) The render DTOs should be defined in Application (as output DTOs), with Rendering consuming them.

**VIOLATION F-2: Microsoft.AspNetCore.App framework reference**

```
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

This is a desktop WPF application. ASP.NET Core framework reference is incorrect — likely added for DI extensions that are available via standalone NuGet packages (`Microsoft.Extensions.DependencyInjection`).

#### PERSISTENCE — PASS with 1 concern

**Separation (PASS):**
- Persistence models (`ProjectRow`, `RevisionRow`, `CabinetRow`, etc.) are completely separate from domain entities.
- Bidirectional mappers (`ProjectMapper.ToRow()` / `ProjectMapper.ToRecord()`, etc.) handle all translation.
- Domain entities never escape the persistence layer boundary. Repositories return application-layer records (`ProjectRecord`, `RevisionRecord`, `WorkingRevision`).

**Snapshot Immutability (PASS):**
- `ApprovedSnapshot` is a sealed record.
- Database enforces immutability via trigger/constraint (tested: UPDATE and DELETE on `approved_snapshots` fail).
- `SnapshotRepository.WriteAsync()` checks existence before insert, throws `InvalidOperationException` on duplicate.
- No UPDATE path for snapshots exists (confirmed by codebase scan in tests).

**Transaction Safety (PASS):**
- `CommandPersistenceService.CommitCommandAsync()` wraps all saves in `IUnitOfWork.BeginAsync()` / `CommitAsync()` with rollback on exception.
- Saves atomically: project, revision, working revision, command journal, explanation nodes, validation issues, autosave checkpoint.

**CONCERN P-1: Working revision reconstruction mutates domain outside orchestrator**

`WorkingRevisionRepository.LoadAsync()` (line 46) calls `run.AppendCabinet(cabinet.Id, cabinet.NominalWidth)` to rebuild run-slot assignments after loading. This is a domain mutation outside the command/orchestrator pipeline.

This is *acceptable* for state reconstitution (the same pattern as event-sourcing rehydration), but it is **untested**. There is no test verifying that loaded runs have correct slot assignments after reconstitution.

#### PRESENTATION — FAIL (2 violations)

**VIOLATION F-3: Direct Domain reference from Presentation**

```
src/CabinetDesigner.Presentation/CabinetDesigner.Presentation.csproj:
  <ProjectReference Include="..\CabinetDesigner.Domain\CabinetDesigner.Domain.csproj" />
```

Per architecture spec and `presentation.md` (§2.3): "Presentation never references `CabinetDesigner.Domain` directly — all domain data arrives pre-shaped as DTOs."

The violation originates in `EditorCanvasSessionAdapter.cs`:
```csharp
using CabinetDesigner.Domain.Identifiers;  // ❌
_session.SetSelection(cabinetIds.Select(id => new CabinetId(id)).ToArray());  // ❌ Creates domain identifiers
```

**Fix:** The adapter should pass raw `Guid` values to the Editor layer. The Editor's `EditorSession.SetSelection()` should accept `Guid[]` and construct `CabinetId` internally (Editor already references Domain).

**VIOLATION F-4: ViewModel bypasses Application layer for selection state**

`EditorCanvasViewModel.OnMouseDown()` (line 104) calls `_editorSession.SetSelectedCabinetIds()` directly, mutating editor state without going through an Application service or `IEditorCommand`.

Design commands (add cabinet, move cabinet) correctly flow through `IRunService` → `DesignCommandHandler` → `ResolutionOrchestrator`. But selection changes go directly to the Editor session, bypassing the Application layer.

Per `code_phase_global_instructions.md`: "No UI-driven domain mutation." Selection is editor state (not domain state), so this is not a domain mutation violation. However, it creates an inconsistent pattern where some state changes are orchestrated and some are not.

**Severity:** MEDIUM. Selection is legitimately editor-only state. The architecture doc (`editor_engine.md`) supports direct editor state manipulation for interaction concerns. But the inconsistency should be documented, and the Presentation → Domain dependency it introduces must be removed.

#### EDITOR — PASS

- `EditorSession`: Pure state management (selection, mode, viewport). No domain mutations.
- `EditorInteractionService`: Correctly builds `IDesignCommand` instances without executing them. Commands are returned to the caller for submission to the Application layer.
- Clean dependency: Editor → Domain only (correct per spec).

#### RENDERING — PASS with minor observations

- `RenderSceneComposer.ApplyInteractionState()`: Applies selection/hover visual state to render DTOs. This is display-only logic (not domain mutation), acceptable in rendering layer.
- `DefaultHitTester`: Hit testing resides in Rendering. Architecturally it's a rendering-adjacent concern. Acceptable.
- No business logic detected in render layers (`BackgroundLayer`, `CabinetLayer`, `RunLayer`, `WallLayer`, `SelectionOverlayLayer`).
- Correct dependency: Rendering → Domain, Editor (per spec).

---

## 5. TESTS

### 5.1 Current Coverage Assessment

| Category | Status | Notes |
|---|---|---|
| Geometry value objects | PASS | Construction, equality, arithmetic, tolerance |
| Domain invariants | PASS | Cabinet constructor guards, Revision state machine |
| Command structural validation | PASS | AddCabinetToRunCommand, ResizeCabinetCommand |
| Orchestrator pipeline | PASS | Validation before execution, failed stages halt pipeline |
| Undo/redo | PASS | UndoRedoServiceTests |
| Persistence round-trips | PASS | All mappers tested (Project, Revision, Room, Wall, Run, Part, ExplanationNode, ValidationIssue) |
| Snapshot immutability | PASS | Write-once enforcement, DB constraints, no UPDATE path |
| Foreign key enforcement | PASS | Integration test |
| Transaction rollback | PASS | Integration test |
| Command journal monotonicity | PASS | Integration test |

### 5.2 Missing Regression Tests

**GAP T-1: Determinism tests (MISSING)**

Architecture guardrail: "All behavior must be deterministic." No test verifies that executing the same command twice produces identical state deltas. Required test: execute a command N times from the same initial state, assert delta equality.

**GAP T-2: Failure recovery tests (MISSING)**

`CommandPersistenceService.CommitCommandAsync()` has try-catch-rollback, but no test verifies the rollback actually undoes partial writes. Required tests:
- Mock a repository that throws mid-transaction
- Verify state is unchanged after rollback

**GAP T-3: Working revision reconstruction test (MISSING)**

`WorkingRevisionRepository.LoadAsync()` calls `run.AppendCabinet()` to rebuild slot assignments. No test verifies this produces correct run-cabinet relationships. Required test: save a working revision with cabinets in runs → load → verify `run.Slots` matches expected assignments.

**GAP T-4: Snapshot blob corruption tests (MISSING)**

`V1SnapshotDeserializer` validates `schema_version` and `revision_id` presence but does not test:
- Malformed JSON payload
- Missing required fields in payload
- Type mismatches in deserialization

**GAP T-5: Idempotency tests (MISSING)**

No test verifies that replaying a command journal produces consistent state. This is important for crash recovery scenarios where the journal may be replayed.

---

## 6. RATIONALE

### 6.1 Why the Dependency Violations Matter

The architecture mandates: `Presentation → Application, Editor, Rendering` and `Application → Domain` (only). The current violations create:

1. **Application → Rendering:** Inverts the intended dependency. If Rendering types change, Application must recompile. This couples business orchestration to visual concerns. The `SceneProjector` is a projection (display-preparation) concern — it belongs in or near the Presentation layer, not in Application.

2. **Presentation → Domain:** Breaks the DTO boundary. The spec explicitly states Presentation should never import domain types. Today it's just `CabinetId` in one adapter, but this crack allows future developers to import domain entities directly, eroding the boundary incrementally.

3. **Microsoft.AspNetCore.App:** Pulls in ASP.NET Core runtime dependencies that are unnecessary for a desktop WPF application. The DI container is available via the standalone `Microsoft.Extensions.DependencyInjection` package.

### 6.2 Why the Test Gaps Matter

The architecture guardrails require deterministic behavior, explainable outputs, and trustworthy results ("incorrect measurements can cost real money"). The missing test categories — determinism, failure recovery, reconstitution correctness, and blob corruption — are exactly the categories where silent failures produce incorrect state without visible errors.

### 6.3 Why Selection Bypass is Acceptable

The architecture distinguishes between **design state** (flows through `ResolutionOrchestrator`) and **editor/interaction state** (selection, hover, mode, viewport). `EditorCanvasViewModel.SetSelectedCabinetIds()` modifies editor state only — it never touches domain entities or the resolution pipeline. The `editor_engine.md` spec explicitly supports this pattern. The violation is limited to the unnecessary Domain import it requires, not the interaction pattern itself.

---

## 7. FOLLOW-UP NOTES

### 7.1 Priority Fixes (Ordered)

| Priority | Issue | Effort | Risk if Deferred |
|---|---|---|---|
| **P1** | F-1: Remove Application → Rendering reference; relocate `SceneProjector` + `ISceneProjector` | Medium | Dependency rot — every new projector deepens the inversion |
| **P1** | F-3: Remove Presentation → Domain reference; refactor `EditorCanvasSessionAdapter` to use `Guid` | Small | Boundary erosion — easy for future code to import domain types |
| **P2** | T-3: Add working revision reconstruction test | Small | Silent data corruption on load |
| **P2** | T-1: Add determinism tests | Small | Violates core guardrail without detection |
| **P2** | T-2: Add failure recovery tests | Medium | Untested rollback path in production |
| **P3** | F-2: Replace `Microsoft.AspNetCore.App` with standalone DI package | Small | Unnecessary dependency bloat |
| **P3** | T-4: Add snapshot blob corruption tests | Small | Edge case, but important for long-lived projects |
| **P3** | T-5: Add idempotency/journal replay tests | Medium | Crash recovery correctness |
| **P3** | F-4: Document selection state bypass as intentional architecture decision | Small | Future confusion about inconsistent patterns |

### 7.2 Recommended Fix for Application → Rendering Inversion

The cleanest approach is to move the render DTO types (`RenderSceneDto`, `CabinetRenderDto`, `WallRenderDto`, `RunRenderDto`, etc.) into `CabinetDesigner.Application/DTOs/` as output DTOs. Then:

1. `SceneProjector` stays in Application (producing Application DTOs).
2. Rendering consumes Application DTOs (Rendering → Application is not currently a reference, but Rendering → Domain is, and Application DTOs would live alongside domain-derived DTOs).
3. Remove the `CabinetDesigner.Rendering` ProjectReference from Application.

Alternatively, move `SceneProjector` and `ISceneProjector` to `CabinetDesigner.Presentation` (which already references both Application and Rendering). This is simpler but places projection logic in the Presentation layer, which may not be the best long-term home.

### 7.3 Recommended Fix for Presentation → Domain

In `EditorCanvasSessionAdapter.cs`, replace:
```csharp
using CabinetDesigner.Domain.Identifiers;
// ...
_session.SetSelection(cabinetIds.Select(id => new CabinetId(id)).ToArray());
```

With an Editor-layer method that accepts `Guid[]`:
```csharp
_session.SetSelection(cabinetIds);  // EditorSession handles CabinetId construction internally
```

Then remove the Domain ProjectReference from `CabinetDesigner.Presentation.csproj`.

### 7.4 Items NOT Requiring Action

These were reviewed and found conformant — no changes needed:

- Domain purity, geometry system, strongly-typed identifiers
- Command immutability and IDesignCommand/IEditorCommand separation
- ResolutionOrchestrator pipeline execution, delta tracking, undo/redo
- Persistence model separation and mapper pattern
- Snapshot immutability enforcement (DB + code)
- Transaction safety in CommandPersistenceService
- Editor command building pattern (creates commands, does not execute)
- Rendering layer purity (no domain mutation, display-only logic)
- Validation engine architecture (stateless structural + stateful contextual + cross-cutting)
- Why Engine recording pattern (append-only, per-stage)
