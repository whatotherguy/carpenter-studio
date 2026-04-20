# Final Finish-Round Audit

Date: 2026-04-20
Scope: Independent second-pass audit of finish-round batches B0-B7 against `finish_execution_plan.md` and `finish_work_queue.md`, followed by remediation of all identified blockers and highs.
Method: Direct source review + independent `feature-dev:code-reviewer` subagent (cold context) + `dotnet build -warnaserror` + `dotnet test`.

---

## Verdicts

- **User testing: GO.** B0-B4 are complete with their tests green; the user-testing prerequisites in `finish_execution_plan.md` section 6 are all met.
- **Professional cabinet-shop use: GO.** B7 is now complete: `ManufacturingReadinessRule` and `InstallReadinessRule` exist and are registered in `ValidationStage.CreateDefaultEngine`; `ValidationContext` carries `ManufacturingBlockers` / `InstallBlockers` snapshot lists populated from `context.ManufacturingResult.Plan.Readiness.Blockers` / `context.InstallResult.Plan.Readiness.Blockers`; both rules emit `Error`-severity issues that reach the IssuePanel through the validation surface. `CostingStage.RevisionDelta` is now populated from the prior approved snapshot via `IPreviousApprovedCostLookup`. `EndToEndPipelineTests` covers the B2-B6 stages running together.

---

## Build And Test Status

- `dotnet build -warnaserror`: **clean** (exit 0).
- `dotnet test`: **921 tests pass** (841 in `CabinetDesigner.Tests`, 80 in `CabinetDesigner.Persistence.Tests`). 0 failures, 0 skipped.
  - Note: a pre-existing testhost flake causes intermittent hangs when the whole `CabinetDesigner.Tests` assembly runs in one invocation (hang dump points at WPF-dispatcher-heavy `RunSummaryPanelViewModel` / `StatusBarViewModel` tests; the test that "appears to be running" varies run-to-run). Running the suite split by `--filter "FullyQualifiedName~Presentation"` and its complement gives a clean 128 + 713 pass. Not a regression from this round; tracked separately as a test-infrastructure issue.

---

## Per-Batch Status

### B0 — Safety Net — COMPLETE
[ResolutionOrchestrator.cs](../../../src/CabinetDesigner.Application/ResolutionOrchestrator.cs) emits an `Error`-severity `STAGE_NOT_IMPLEMENTED` issue and returns false in Full mode while keeping Preview as a Warning. [ManufacturingProjector.cs](../../../src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs) emits `ManufacturingBlockerCode.NoPartsProduced` when `Parts.Count == 0`. `CostingStage` and `PackagingStage` both `StageResult.Failed(...)` with explicit codes. The flipped orchestrator tests in `ResolutionOrchestratorTests.cs` are present.

### B1 — UI Shell — COMPLETE
[MainWindow.xaml](../../../src/CabinetDesigner.App/MainWindow.xaml) mounts `ShellToolbarView`, `CatalogPanelView`, the canvas `ContentControl`, a `TabControl` with `PropertyInspectorView`/`RunSummaryPanelView`/`IssuePanelView`, and `StatusBarView`. `MainWindowSmokeTests.cs` is present and asserts each named region is bound.

### B2 — Part Generation — COMPLETE
`PartGenerationStage` consumes `IDesignStateStore` via the constructor (wired in `ResolutionOrchestrator`), uses pure-math [PartGeometry.cs](../../../src/CabinetDesigner.Application/Pipeline/Parts/PartGeometry.cs), emits stable `PartId = "part:{cabinetId:D}:{PartType}:{ordinal}"`, sorts deterministically, and fails with `PART_GEN_EMPTY` when no cabinets resolve in Full mode. `PartGenerationStageTests.cs` covers the spec'd cases.

### B3 — Constraint Propagation — COMPLETE
`ConstraintPropagationStage` resolves materials via override chain → `ICatalogService.ResolvePartMaterial`, emits `MATERIAL_UNRESOLVED` errors and `NO_HARDWARE_CATALOG` warnings. `MaterialAssignmentRule` and `HardwareAssignmentRule` exist and are wired in [ValidationStage.cs](../../../src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs). Tests present.

### B4 — Engineering Resolution + Workflow State — COMPLETE
`EngineeringResolutionStage` produces `AssemblyResolution` per cabinet, `FillerRequirement` when gap > 0.125", and `EndConditionUpdate` from wall attachment. [ValidationStage.cs](../../../src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs) constructs `WorkflowStateSnapshot` from real `_projectState` (Q-4 fixed). `EngineeringResolutionStageTests.cs` and `ValidationStageTests.cs` are present.

### B5 — Costing — COMPLETE
[CostingStage.cs](../../../src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs) computes Material/Hardware/Labor/Install with deterministic ordering and `MidpointRounding.ToEven`; [ICostingPolicy.cs](../../../src/CabinetDesigner.Application/Costing/ICostingPolicy.cs) and `DefaultCostingPolicy` exist. `RevisionDelta` is now populated via [IPreviousApprovedCostLookup.cs](../../../src/CabinetDesigner.Application/Costing/IPreviousApprovedCostLookup.cs); in production it's wired to [SnapshotApprovedCostLookup.cs](../../../src/CabinetDesigner.Application/Costing/SnapshotApprovedCostLookup.cs), which reads the most-recent approved snapshot's `EstimateBlob` and parses `costing.total` safely. `CostingStageTests.cs` covers determinism, price-missing failure, and both `Execute_RevisionDelta_*` cases.

### B6 — Packaging + Snapshot Integrity — COMPLETE
[PackagingStage.cs](../../../src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs) serializes five artifact blobs through [DeterministicJson.cs](../../../src/CabinetDesigner.Application/Packaging/DeterministicJson.cs), hashes the concatenation with SHA-256 in fixed order, fails with `PACKAGING_INVALID_DESIGN` when validation is invalid, and persists through `IPackagingResultStore`. `ApprovedSnapshot` carries `ContentHash` and a V3 migration adds the column. `PackagingStageTests.cs` and `SnapshotApprovalContentTests.cs` are present.

### B7 — Validation Wiring, Service Completion, UI Cleanup — COMPLETE
[ManufacturingReadinessRule.cs](../../../src/CabinetDesigner.Domain/Validation/Rules/ManufacturingReadinessRule.cs) and [InstallReadinessRule.cs](../../../src/CabinetDesigner.Domain/Validation/Rules/InstallReadinessRule.cs) emit `Error`-severity issues from the new `ManufacturingBlockers` / `InstallBlockers` snapshots on `ValidationContext`. `ValidationStage.CreateDefaultEngine` registers both rules. `ValidationStage.BuildContext` populates the snapshots from the manufacturing / install plan readiness. [ManufacturingReadinessRuleTests.cs](../../../tests/CabinetDesigner.Tests/Domain/Validation/Rules/ManufacturingReadinessRuleTests.cs), [InstallReadinessRuleTests.cs](../../../tests/CabinetDesigner.Tests/Domain/Validation/Rules/InstallReadinessRuleTests.cs), and [EndToEndPipelineTests.cs](../../../tests/CabinetDesigner.Tests/Pipeline/EndToEndPipelineTests.cs) all pass. `RunService.DeleteRunAsync` / `SetCabinetOverrideAsync` route through real commands; `ProjectService.OpenProjectAsync` fails loudly on ambiguous state; `CreateProjectAsync` persists the empty `WorkingRevision`; no `NotImplementedException` throws or catches remain anywhere in `src/`.

---

## Remaining Non-Blocking Items

These are Medium/Low items noted during the audit. None gate professional-use GO; left as follow-ups.

1. **Q-2 limitation persists by design.** [ProjectService.OpenProjectAsync](../../../src/CabinetDesigner.Application/Services/ProjectService.cs) throws on ambiguous projects rather than resolving by `filePath`. This matches the work-queue policy ("fail loudly when ambiguous"), but remains a limitation if the product ever needs to host more than one project in a single store.
2. **`ValidationRuleCategory` enum uses `Manufacturing` and `Installation`**; the work queue spec says `Install`. Pick one and reconcile.
3. **Logging timestamps inside deterministic stages bypass `IClock`.** [PartGenerationStage.cs](../../../src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs), [ConstraintPropagationStage.cs](../../../src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs), and [CostingStage.cs](../../../src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs) call `DateTimeOffset.UtcNow` directly in log entries. Stage outputs are unaffected (logs are not part of `ContentHash` input), but it breaks the project-wide `IClock` discipline that `PackagingStage` already follows.
4. **`SnapshotApprovalContentTests` asserts on JSON key strings.** Prefer round-tripping the blob and asserting on the deserialized structure.
5. **Test doubles still throw `NotImplementedException`.** `MainWindowSmokeTests.cs` `RecordingRunService` and `RecordingProjectService.SaveRevisionAsync` keep `throw new NotImplementedException()`. Production code is clean; these stubs only surface if a smoke-test path hits an unstubbed method.
6. **Presentation-layer test-suite flake.** Running the whole `CabinetDesigner.Tests` assembly in one `dotnet test` invocation occasionally hangs in a WPF-dispatcher-heavy Presentation test. Split runs pass clean. Candidates to investigate: `RunSummaryPanelViewModelTests`, `StatusBarViewModelTests`.

---

## Cross-Cutting Quality Check

- **Determinism.** Stages sort their outputs deterministically; `DeterministicJson` normalizes property order and culture; `PackagingStage` builds `ContentHash` from a fixed concatenation. `EndToEndPipelineTests.FullPipeline_IsDeterministic` locks this in across Costing→Validation→Packaging.
- **Fail-closed.** Skeleton paths are gone; `STAGE_NOT_IMPLEMENTED` blocks Full mode; `COSTING_NO_PARTS`, `PACKAGING_INVALID_DESIGN`, `PACKAGING_REQUIRED_STATE_MISSING`, `PART_GEN_EMPTY`, `MATERIAL_UNRESOLVED` are all explicit. `ManufacturingReadinessRule` and `InstallReadinessRule` now fail-closed as well: when readiness blockers are present, validation surfaces `Error`-severity issues that block approval.
- **Error-code stability.** All issue codes are bare string literals at the call site; no helpers reformat them.
- **Silent catches.** No `catch (Exception)` in stages without either `_logger` routing or rethrow. `PackagingStage` narrows by exception type. `SnapshotApprovedCostLookup` narrows to `JsonException` / `InvalidOperationException` and returns `null` safely. `ResolutionOrchestrator` routes through `IAppLogger`.
- **Layering.** `Application` does not reference `Rendering`. New `Costing` and `Packaging` namespaces stay inside `Application`. `IPreviousApprovedCostLookup` lives in `Application/Costing` with its production impl; the interface is testable without a repository.
- **`NotImplementedException` removal.** None remain anywhere under `src/`. Test scaffolding still has a few — see Remaining Non-Blocking #5.
