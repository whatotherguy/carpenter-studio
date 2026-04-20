# Carpenter Studio Finish Prompt Pack

Paste-and-run prompts for finishing Carpenter Studio into a genuinely usable cabinet design application rather than a partially completed architecture shell.

Every prompt in this pack is self-contained. Each one loads `finish_round_global_instructions.md` so you never have to paste global rules again — just paste the prompt and run it.

---

## How to use this pack

1. Run the prompts in the order listed below. Each one tells you what to run next.
2. Do not skip the "Read first" list — those files are how the agent grounds itself.
3. Do not skip the skill invocations. Every code prompt names specific skills that must run.
4. Model split:
   - **Opus / strong reasoning model** for P0, P1, optional P2, and P15 audit.
   - **Codex / Sonnet mid-tier** is fine for P3–P14 because each prompt is already scoped.

---

## Execution order at a glance

| Order | Prompt | Batch | Purpose |
|---|---|---|---|
| done | P0 | — | Execution plan (already written) |
| done | P1 | — | Work queue (already written) |
| opt | P2 | — | Implementation blueprint (optional extra planning) |
| 1 | P3 | B0 | Fail-closed safety net — remove false-success pipeline behavior |
| 2 | P11 | B1 | Mount all panels in the shell so every later batch is visible during testing |
| 3 | P6 | B2 | Part generation — produce real cut-list-worthy parts |
| 4 | P5 | B3 | Constraint propagation — real material / thickness / hardware assignment |
| 5 | P4 | B4 | Engineering resolution + workflow-state wiring |
| 6 | P7 | — | Manufacturing planning hardening |
| 7 | P8 | — | Install planning hardening |
| 8 | P9 | B5 | Costing — real cost or hard block |
| 9 | P10 | B6 | Packaging / snapshot integrity with deterministic hash |
| 10 | P12 | B7 | ProjectService: OpenProjectAsync + CreateProjectAsync hardening |
| 11 | P13 | — | Persistence + mapping round-trip hardening for newly populated domain state |
| 12 | P14 | — | Editor / command polish |
| 13 | P15 | — | Final production-readiness audit |

Batches (B0–B7) are the units defined in `finish_work_queue.md`. The prompt number (P*) is preserved for traceability against the original pack.

---

## Always-loaded reference documents

Every code prompt in this pack reads these three files before doing anything else:

- `docs/ai/finish_round/finish_round_global_instructions.md` — mission, engineering rules, output rules, skill usage.
- `docs/ai/finish_round/finish_execution_plan.md` — batch rationale and go/no-go lines.
- `docs/ai/finish_round/finish_work_queue.md` — authoritative per-batch spec.

If any prompt's instructions appear to conflict with these documents, those documents win — specifically the work queue.

---

## Prompt 0 — Reality check and execution plan

Already executed. Output lives at `docs/ai/finish_round/finish_execution_plan.md`. Re-run only if the plan becomes stale.

---

## Prompt 1 — Build the detailed work queue

Already executed. Output lives at `docs/ai/finish_round/finish_work_queue.md`. Re-run only if the work queue becomes stale.

---

## Prompt 2 — Optional implementation blueprint

Skip unless you want an additional planning pass before code generation.

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md

Task:
Produce a coding blueprint that resolves ambiguity before implementation.
Focus only on the highest-risk unfinished systems:
- engineering resolution
- constraint propagation
- part generation
- fail-closed manufacturing readiness
- costing
- packaging/snapshot integrity
- shell/UI completion for actual testing

For each system, define:
- target behavior
- important data inputs
- important outputs
- invariants
- failure/blocker behavior
- unit/integration test strategy

Do not re-plan anything already resolved in the execution plan or work queue.
This blueprint refines, it does not replace.

Write the result to:
- docs/ai/finish_round/finish_blueprint.md

Next: run Prompt 3.
```

---

## Prompt 3 — B0 fail-closed safety net

Start here. This installs the guardrails every later batch depends on.

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B0)
- src/CabinetDesigner.Application/ResolutionOrchestrator.cs
- src/CabinetDesigner.Application/ApplicationServiceRegistration.cs
- src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ManufacturingPlanningStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/InstallPlanningStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs
- src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs
- tests/CabinetDesigner.Tests/Pipeline/ResolutionOrchestratorTests.cs

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task (per work queue B0):
Eliminate dangerous false-success behavior in the full pipeline.

Required outcomes:
1. In full (non-preview) runs, no critical production stage may return Succeeded when its result is a skeleton/placeholder. Emit a blocker-severity issue with a stable error code and mark the stage failed.
2. ManufacturingProjector.IsReady must not be true when Parts is empty or when any upstream stage is a skeleton. Add ManufacturingBlockerCode.NoPartsProduced (or equivalent per work queue) and surface it.
3. Preview mode may remain lighter but must still tag any skeleton stage output as preview-only, never manufacturing-ready.
4. Orchestrator severity for NotImplementedYet outcomes in full runs must be Error, not Warning — update CreateNotImplementedIssue accordingly.
5. Tests must prove incomplete engineering / constraints / parts / costing / packaging cannot silently produce a manufacturing-ready result.

Preserve architecture:
- Do not move code across layer boundaries.
- Keep error code strings stable (they are part of the public contract).
- Do not break editor preview flows that are already known to work.

Definition of done is the B0 DoD block in finish_work_queue.md.

Report per Output rules in finish_round_global_instructions.md.

Next: run Prompt 11.
```

---

## Prompt 11 — B1 shell mount

Done second so every subsequent batch's behavior is visible during testing.

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B1)
- src/CabinetDesigner.App/MainWindow.xaml
- src/CabinetDesigner.App/MainWindow.xaml.cs
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/CatalogPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/PropertyInspectorViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/RunSummaryPanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/IssuePanelViewModel.cs
- src/CabinetDesigner.Presentation/ViewModels/StatusBarViewModel.cs
- src/CabinetDesigner.Presentation/Views/*.xaml

Invoke:
- superpowers:executing-plans
- superpowers:verification-before-completion

Task (per work queue B1):
Mount every already-wired panel in MainWindow so testing reflects the real system.
Required regions: catalog, property inspector, run summary, issue panel, shell toolbar, status bar, plus the existing canvas.

Requirements:
- Respect MVVM: no business logic in code-behind.
- Preserve DI composition. ShellViewModel already wires the panel viewmodels; do not duplicate that.
- Preserve DispatchIfNeeded pattern in viewmodels reacting to IApplicationEventBus.
- Layout must be usable for cabinet-design testing — not a demo grid.
- Commands and status bar must light up as real pipeline work happens.

Tests:
- Smoke test that each region has a non-null DataContext at startup.
- Any binding regression risk should be covered by a simple viewmodel-level test.

Definition of done is the B1 DoD block in finish_work_queue.md.

Report per Output rules.

Next: run Prompt 6.
```

---

## Prompt 6 — B2 part generation

Parts must exist before constraints can be assigned to them.

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B2)
- src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs
- src/CabinetDesigner.Application/Pipeline/StageResults/PartResult.cs
- src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs
- src/CabinetDesigner.Domain/ManufacturingContext/*.cs
- src/CabinetDesigner.Domain/CabinetContext/*.cs
- tests/CabinetDesigner.Tests/Pipeline/PartGenerationStageTests.cs  (create if missing)

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task (per work queue B2):
Replace the PartGenerationStage skeleton with real part generation for currently supported cabinet types.

For each supported cabinet, emit the parts defined in the work queue: side panels, top/bottom, back, shelves where applicable, toe-kick or structural equivalents. Door/drawer-front hooks only if the domain supports them cleanly today.

Requirements:
- Dimensions derived deterministically from cabinet geometry — no placeholders, no randomness.
- Part IDs and labels stable and testable. Ordering deterministic.
- Edge treatment metadata populated where current rules support it.
- Unsupported cabinet cases must produce a blocker issue with a stable error code and fail the stage in full runs — never emit an empty Parts list as success.

Tests:
- Representative base cabinet part generation (dimension correctness + count).
- Representative wall cabinet part generation.
- Deterministic labels and ordering across repeated runs.
- Unsupported / incomplete data produces a blocker.

Definition of done is the B2 DoD block in finish_work_queue.md.

Report per Output rules.

Next: run Prompt 5.
```

---

## Prompt 5 — B3 constraint propagation

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B3)
- src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs
- src/CabinetDesigner.Application/Pipeline/StageResults/ConstraintResult.cs
- src/CabinetDesigner.Application/Services/CatalogService.cs
- src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs
- src/CabinetDesigner.Domain/MaterialsContext/*.cs
- src/CabinetDesigner.Domain/Validation/IValidationRule.cs
- src/CabinetDesigner.Domain/Validation/ValidationEngineBuilder.cs

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task (per work queue B3):
Replace the ConstraintPropagationStage skeleton with real material, thickness, grain-direction, and hardware propagation, building on the real Parts produced by B2.

Implement:
- Extend ICatalogService with the methods named in the work queue (e.g., ResolvePartMaterial, GetMaterialPricePerSquareFoot) so constraint propagation and costing share a single catalog surface.
- Deterministic material assignment per part based on cabinet body vs back vs shelf etc.
- Resolved thickness assigned from material.
- Grain direction assigned where relevant.
- Hardware assignments where supported by current domain data.
- New validation rules: MaterialAssignmentRule, HardwareAssignmentRule (codes per work queue). Register in ValidationEngineBuilder.
- Blocker when a required assignment is impossible; no silent fallback to empty/default.

Tests:
- Material propagation to common cabinet parts.
- Thickness propagation.
- Missing material → MaterialAssignmentRule blocks with the documented error code.
- Deterministic assignment ordering across repeated runs.

Definition of done is the B3 DoD block in finish_work_queue.md.

Report per Output rules.

Next: run Prompt 4.
```

---

## Prompt 4 — B4 engineering resolution + workflow-state wiring

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B4)
- src/CabinetDesigner.Application/Pipeline/Stages/EngineeringResolutionStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ValidationStage.cs
- src/CabinetDesigner.Application/Pipeline/StageResults/EngineeringResult.cs
- src/CabinetDesigner.Domain/CabinetContext/*.cs
- src/CabinetDesigner.Domain/RoomContext/*.cs
- src/CabinetDesigner.Domain/Validation/*.cs
- tests/CabinetDesigner.Tests/Pipeline/EngineeringResolutionStageTests.cs  (create if missing)

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task (per work queue B4):
Replace the EngineeringResolutionStage skeleton with a real first-pass implementation, and fix the hardcoded WorkflowStateSnapshot in ValidationStage (see work queue Q-4).

Implement:
- Cabinet assembly derivation from current spatial/layout state (deterministic ordering).
- End-condition / filler requirement derivation where applicable.
- ValidationStage must receive a real WorkflowStateSnapshot derived from the current pipeline context, not the hardcoded one at lines ~67–71.
- Additional validation rules per work queue (e.g., ManufacturingReadinessRule, InstallReadinessRule if scoped here) registered in ValidationEngineBuilder.
- Blocker messages for missing inputs or unsupported cabinet conditions.

Tests:
- Normal base cabinet assembly generation.
- Wall cabinet assembly generation.
- Filler / end-condition handling where supported.
- Unsupported / insufficient-input case produces a blocker.
- ValidationStage consumes the real workflow state (regression test for Q-4).

Definition of done is the B4 DoD block in finish_work_queue.md.

Report per Output rules.

Next: run Prompt 7.
```

---

## Prompt 7 — Manufacturing planning hardening

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B7 manufacturing hardening rows)
- src/CabinetDesigner.Application/Projection/ManufacturingProjector.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ManufacturingPlanningStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/PartGenerationStage.cs
- src/CabinetDesigner.Application/Pipeline/Stages/ConstraintPropagationStage.cs
- tests/CabinetDesigner.Tests/Application/Projection/ManufacturingProjectorTests.cs
- tests/CabinetDesigner.Tests/Pipeline/ManufacturingPlanningStageTests.cs

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task:
Raise manufacturing planning to professional quality now that B2/B3/B4 produce real upstream data.

Implement or improve:
- Fail-closed readiness when part lists are empty or incomplete (reinforce B0 behavior end-to-end).
- Blocker behavior when required material/hardware/thickness data is missing.
- Trustworthy cut-list generation invariants.
- Deterministic grouping and ordering.
- Stronger validation for impossible dimensions and malformed parts.

Invariant: a successful manufacturing plan must mean something trustworthy. Empty cut lists or incomplete upstream data never masquerade as production readiness.

Tests:
- Complete valid manufacturing result.
- Empty part list blocked (end-to-end from B2).
- Missing material blocked (end-to-end from B3).
- Invalid dimensions blocked.
- Deterministic cut-list order across repeated runs.

Report per Output rules.

Next: run Prompt 8.
```

---

## Prompt 8 — Install planning hardening

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (install rows)
- src/CabinetDesigner.Application/Pipeline/Stages/InstallPlanningStage.cs
- src/CabinetDesigner.Application/Projection/*.cs  (install projector if present)
- tests/CabinetDesigner.Tests/Pipeline/InstallPlanningStageTests.cs  (create if missing)

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task:
Raise install planning beyond a thin pass-through.

Implement or improve:
- Install-readiness blockers derived from engineering/manufacturing state.
- Basic install sequencing logic where current domain supports it.
- Explicit blocker messages for unsupported or unsafe conditions.
- Deterministic output order.

Focus on correctness and meaningful blockers, not flashy features.

Tests:
- Install-ready scenario (upstream fully valid).
- Blocked scenario due to upstream missing engineering/manufacturing data.
- Deterministic output.

Report per Output rules.

Next: run Prompt 9.
```

---

## Prompt 9 — B5 costing

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B5)
- src/CabinetDesigner.Application/Pipeline/Stages/CostingStage.cs
- src/CabinetDesigner.Application/Pipeline/StageResults/CostingResult.cs
- src/CabinetDesigner.Application/Services/CatalogService.cs
- src/CabinetDesigner.Domain/MaterialsContext/*.cs
- src/CabinetDesigner.Domain/HardwareContext/*.cs  (if present)
- tests/CabinetDesigner.Tests/Pipeline/CostingStageTests.cs  (create if missing)

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task (per work queue B5):
Replace the CostingStage skeleton with a real first-pass costing system driven by the catalog surface extended in B3.

Implement:
- Material cost aggregation from manufacturing parts using ICatalogService.GetMaterialPricePerSquareFoot (or the equivalent contract named in the work queue).
- Hardware cost aggregation where data exists.
- Deterministic cabinet-level breakdowns.
- Explicit "cannot calculate cost" blocker when required pricing inputs are missing — do not invent shop pricing, and never return zeros as if valid.

Tests:
- Valid priced result.
- Missing price data → documented blocker error code.
- Deterministic breakdown totals across repeated runs.
- Fail-closed: empty parts → blocker (reinforces B0).

Definition of done is the B5 DoD block in finish_work_queue.md.

Report per Output rules.

Next: run Prompt 10.
```

---

## Prompt 10 — B6 packaging / snapshot integrity

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B6)
- src/CabinetDesigner.Application/Pipeline/Stages/PackagingStage.cs
- src/CabinetDesigner.Application/Pipeline/StageResults/PackagingResult.cs
- src/CabinetDesigner.Application/Services/SnapshotService.cs
- src/CabinetDesigner.Application/Services/RunService.cs
- src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs
- tests/CabinetDesigner.Tests/Pipeline/PackagingStageTests.cs  (create if missing)

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task (per work queue B6):
Replace the PackagingStage skeleton with real packaging behavior and fix the SnapshotService shortcomings called out as Q-8 in the execution plan.

Implement:
- Real snapshot metadata — no UnixEpoch, no empty IDs.
- Deterministic content hash over the manufacturing-relevant state (parts, constraints, costing). Same input → same hash, byte-identical.
- Revision / snapshot summary that actually reflects content.
- Blocker when required state for packaging is missing.

Tests:
- Successful package creation.
- Stable content hash across repeated runs on identical state.
- Hash changes when a meaningful part field changes.
- Blocker on missing required state.

Definition of done is the B6 DoD block in finish_work_queue.md.

Report per Output rules.

Next: run Prompt 12.
```

---

## Prompt 12 — B7 ProjectService hardening

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (section: Batch B7 project rows)
- src/CabinetDesigner.Application/Services/ProjectService.cs
- src/CabinetDesigner.Application/Services/RunService.cs
- src/CabinetDesigner.Persistence/Repositories/*.cs
- src/CabinetDesigner.Presentation/ViewModels/ShellViewModel.cs
- src/CabinetDesigner.Presentation/Services/IDialogService.cs  (if present)
- tests/CabinetDesigner.Tests/Application/Services/ProjectServiceTests.cs  (create if missing)

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task (per work queue B7 project rows — Q-2, Q-3, and RunService NotImplementedException removals):
Raise project open/save/close/revision behavior to professional quality.

Implement:
- OpenProjectAsync must respect the selected file path (current Q-2 behavior ignores it). Define single-project-per-file semantics explicitly if that is the design, and enforce it.
- CreateProjectAsync must persist the WorkingRevision it creates (Q-3).
- Remove the RunService NotImplementedException sites (S-6, S-7) — implement the behavior per the work queue, or replace with an explicit, UI-surfaced blocker until a later batch.
- Update IssuePanelViewModel and StatusBarViewModel catch-blocks that currently swallow NotImplementedException so they no longer need to.
- Save/dirty/close behavior coherent and testable.

Tests:
- Open selected project/file semantics with real file path.
- CreateProjectAsync persists WorkingRevision and can be re-read.
- Save/dirty behavior (isDirty transitions).
- Close-with-unsaved-changes behavior.
- RunService no longer throws NotImplementedException in the wired code paths.

Definition of done is the B7 DoD block in finish_work_queue.md.

Report per Output rules.

Next: run Prompt 13.
```

---

## Prompt 13 — Persistence and mapping hardening

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (persistence rows)
- src/CabinetDesigner.Persistence/Mapping/RunMapper.cs
- src/CabinetDesigner.Persistence/Mapping/CabinetMapper.cs
- src/CabinetDesigner.Persistence/Mapping/PartMapper.cs
- src/CabinetDesigner.Persistence/Mapping/RoomMapper.cs
- src/CabinetDesigner.Persistence/Mapping/WallMapper.cs
- src/CabinetDesigner.Persistence/Repositories/WorkingRevisionRepository.cs
- tests/CabinetDesigner.Persistence.Tests/Mapping/*.cs
- tests/CabinetDesigner.Persistence.Tests/Integration/*.cs

Invoke:
- superpowers:executing-plans
- superpowers:test-driven-development
- superpowers:verification-before-completion

Task:
Persistence hardening pass for the domain state populated by B2–B6.

Focus on:
- Round-trip fidelity for engineering / constraint / part / manufacturing state that was empty before this finish round.
- Deterministic mapping behavior (no dictionary-order leaks).
- No TODO-driven data loss — if a mapper drops a field, either map it or mark it as explicitly out-of-scope in the work queue.
- Working-revision persistence integrity end-to-end with B7's ProjectService changes.

Tests:
- Mapper-level round-trip tests for any field added during B2–B6.
- Integration test: create → save → reload → equivalent domain state.
- Deterministic serialization (same state → byte-identical persisted blob).

Report per Output rules.

Next: run Prompt 14.
```

---

## Prompt 14 — Editor and command polish

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md  (editor rows)
- src/CabinetDesigner.Editor/EditorInteractionService.cs
- src/CabinetDesigner.Presentation/Commands/AsyncRelayCommand.cs
- src/CabinetDesigner.Presentation/ViewModels/WpfEditorCanvasHost.cs
- src/CabinetDesigner.Presentation/ViewModels/EditorCanvasViewModel.cs
- tests/CabinetDesigner.Tests/Editor/EditorInteractionServiceTests.cs
- tests/CabinetDesigner.Tests/Presentation/Commands/AsyncRelayCommandTests.cs

Invoke:
- superpowers:executing-plans
- superpowers:verification-before-completion

Task:
Raise the editor and presentation interaction path from "working" to "professional quality."

Focus on:
- Surfaced errors instead of silent behavior (route through IAppLogger / IApplicationEventBus / StatusBarViewModel, never swallow).
- Command robustness under rapid input and cancellation.
- UI-thread correctness preserved (DispatchIfNeeded pattern).
- Rough edges that would confuse user testing eliminated.

Do not destabilize the editor path. Prefer small hardening changes over broad rewrites.

Tests:
- Any corrected edge case gets a regression test.
- AsyncRelayCommand behavior under concurrent invocations if applicable.

Report per Output rules.

Next: run Prompt 15.
```

---

## Prompt 15 — Final production-readiness audit

Run after P3 through P14 are complete. Use your strongest reasoning model for this.

```text
Read first:
- docs/ai/finish_round/finish_round_global_instructions.md
- docs/ai/finish_round/finish_execution_plan.md
- docs/ai/finish_round/finish_work_queue.md
- every code file touched during the finish round (use git log/diff against the start of the round)
- every test file touched or added during the finish round

Invoke:
- Use the Agent tool with subagent_type: "feature-dev:code-reviewer" for an independent second-pass review. The subagent must not share the implementation context — this is what prevents rubber-stamping.
- superpowers:systematic-debugging if any batch's tests are flaky or cross-batch failures appear.

Task:
Audit the finish round against two separate standards, matching the go/no-go lines in finish_execution_plan.md:
1. Readiness for user testing.
2. Readiness for professional cabinet design use.

For each batch B0–B7, confirm the Definition of Done in finish_work_queue.md is met. Call out:
- What is truly finished.
- What is still incomplete.
- Any remaining false-success paths.
- Any remaining professional-quality gaps (determinism, fail-closed, error-code stability, silent catches).
- dotnet build -warnaserror status.
- dotnet test status.

Deliver a punch list sorted by severity: blocker / high / medium / low.

Write the result to:
- docs/ai/finish_round/final_finish_audit.md

Report per Output rules.

If the audit surfaces blockers, loop back to the relevant prompt (e.g., a manufacturing-readiness regression → rerun P7; a costing issue → rerun P9).
```

---

## Why this pack is structured the way it is

- **Global instructions live in a file**, not in every prompt. One source of truth, no drift, no copy-paste fatigue.
- **Prompts are in execution order**, not original numbering order. Each one ends with the next step so running the pack is mechanical.
- **Each prompt references the work queue's batch**. That way DoD and validation rules never get re-invented per run.
- **Skills are invoked, not just mentioned**. `superpowers:executing-plans`, `test-driven-development`, and `verification-before-completion` run on every code prompt. `feature-dev:code-reviewer` handles the independent audit.
- **P11 is second, not thirteenth**. Mounting the panels early means every later batch's behavior is visible the moment it lands.
- **P6 precedes P5**. Parts must exist before constraint propagation can assign materials to them.
- **B0 is first, always**. Without the fail-closed safety net, every later batch could silently regress the pipeline into false-success behavior.
