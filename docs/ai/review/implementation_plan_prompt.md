# Implementation Plan Prompt — Pre-User-Testing Bug Fixes

**To be given to: Sonnet (planning) -> Codex (implementation)**

---

## Context for Sonnet

You are planning the fix implementation for Carpenter Studio, a Windows desktop cabinet design application (C#/.NET 8/WPF/SQLite). A comprehensive code review found 42 issues across 5 layers. Your job is to produce a phased implementation plan that Codex agents can execute independently.

### Required Reading (before planning)

Read these files in order:
1. `docs/ai/review/evaluation_report.md` — Full bug list with file paths and line numbers
2. `docs/ai/review/universal_implementation_rules.md` — Rules that apply to ALL implementation prompts
3. `docs/ai/context/architecture_summary.md` — Architecture guardrails
4. `docs/ai/context/code_phase_global_instructions.md` — Coding standards and output format

### Architecture You Must Preserve

- All design mutations go through `ResolutionOrchestrator` (11-stage pipeline)
- Domain layer has zero UI dependencies
- Commands represent intent; orchestrator executes
- Persistence uses repository pattern with `SqliteUnitOfWork`
- Presentation uses MVVM with `ObservableObject`, `RelayCommand`, `AsyncRelayCommand`
- Event bus (`IApplicationEventBus`) decouples layers

---

## Planning Instructions

### Phase Structure

Organize fixes into 5 phases. Each phase should be independently mergeable and testable. Within each phase, identify tasks that can run as parallel Codex agents (no shared file edits).

**Phase 1: Data Corruption & Deadlocks** (blocks all testing)
- C1: SnapshotService deadlock (`.GetAwaiter().GetResult()`)
- C2: CurrentWorkingRevisionSource cabinet type corruption
- C3: ValidationStage wrong entity ID (SlotId vs CabinetId)

**Phase 2: Thread Safety** (blocks reliable testing)
- C4: WhyEngine thread-unsafe singleton
- C5: ResolutionOrchestrator recursion guard atomicity
- C6: TextFileAppLogger concurrent file access (I6)
- I2: NotifyCanExecuteChanged off UI thread

**Phase 3: Editor Interaction Stability** (blocks UX testing)
- C6 (from eval): Mouse capture corruption (left+middle concurrent)
- C7: CommitDragAsync unobserved task
- C9: Second click during in-flight drag commit
- I3: Zero-width resize clamping
- I4: WpfEditorCanvasHost memory leak / disposal chain

**Phase 4: Domain & Pipeline Correctness** (blocks feature testing)
- C8: InsertCabinetIntoRunCommand silent no-op
- C10: DesignCommandHandler redundant pre-check
- I7: CabinetRun.RemainingLength over-capacity hiding
- I8: ValidationEngine ContextualIssues always empty
- I10: Wall.AddOpening validation ordering
- I13: ResolveTargetIndex dead parameters / off-by-one
- I15: EndCondition zero-width filler

**Phase 5: Persistence, Polish & Test Gaps** (hardens for beta)
- C11: V2_RepairSchemaDrift SQL injection vector
- I1: SnapshotRepository TOCTOU race
- I5: ProjectService.SaveRevisionAsync inverted HasUnsavedChanges
- I9: Angle.Full self-defeating constant
- I11: SqliteUnitOfWork explicit rollback
- I12: WorkingRevisionRepository double query
- All test gaps (TG1-TG8)
- All UX gaps (UX1-UX5)
- All performance issues (P1-P3)

### For Each Task, Produce a Codex Prompt

Each Codex prompt must follow this template:

```
## Task: [Short title]
**Evaluation Report ID:** [C1, I3, etc.]
**Execution Mode:** HARDENING

### Universal Rules
Read and follow: `docs/ai/review/universal_implementation_rules.md`

### Context
[2-3 sentences explaining the bug, why it matters, and which architectural constraint it violates]

### Files to Read First
- [exact file path — the buggy file]
- [related interface or caller, if relevant]

### Files to Modify
- [exact file path]

### What to Change
[Precise description of the fix — not just "fix the bug" but the specific code change]

### What NOT to Change
- Do not modify unrelated files
- Do not refactor surrounding code
- Do not add features beyond the fix

### Tests Required
- [Specific test scenario 1]
- [Specific test scenario 2]
- [Where to put the test file]

### Verification
- [ ] Solution builds with zero warnings
- [ ] All existing tests pass
- [ ] New regression test passes
- [ ] Fix addresses the root cause, not a symptom
```

### Parallelization Rules

Within each phase, mark tasks that can be done by separate Codex agents in parallel. Two tasks can be parallel if and only if:
1. They modify different files
2. Neither depends on the other's output
3. They don't modify the same interface

If two tasks touch the same file, they must be sequential within the phase.

### Output Format

Your plan must be a single markdown file with:
1. Phase overview table (phase, tasks, parallel groups, estimated complexity)
2. Dependency graph (which phases block which)
3. Full Codex prompts for every task (using the template above)
4. A "smoke test" checklist per phase (what to verify after all tasks in the phase are merged)

---

## Critical Notes for Sonnet

1. **Do NOT combine unrelated fixes into one prompt** — each Codex prompt should be atomic and independently verifiable
2. **Include exact line numbers and file paths** from the evaluation report — Codex needs precise targets
3. **Reference the universal rules file in every prompt** — Codex doesn't carry context between tasks
4. **Test file locations follow existing conventions:**
   - Domain/Application/Editor/Rendering/Presentation tests: `tests/CabinetDesigner.Tests/`
   - Persistence tests: `tests/CabinetDesigner.Persistence.Tests/`
5. **The codebase uses `TreatWarningsAsErrors`** — any new warning is a build failure
6. **Nullable reference types are enabled** — new code must handle nullability correctly
7. **Some "important" issues may be reclassified as critical during planning** if you determine they block basic user workflows — use your judgment
8. **Phase 1 is the highest priority** — if the user can only do one phase before testing, it must be Phase 1
