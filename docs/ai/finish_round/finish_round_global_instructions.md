# Finish Round — Global Instructions

This file is referenced by every prompt in `carpenter_studio_finish_prompt_pack.md`. Every implementation run must treat it as authoritative.

---

## Mission

Finish the unfinished systems and raise all critical workflows to professional cabinet-design-software quality.

- Produce working code, not architecture-only output.
- Do not preserve skeleton implementations if they block real functionality.
- Do not add fake "success" paths that allow placeholder outputs to look production-ready.

## Core product standard

- This is cabinet design software for real carpenters and cabinet shops.
- Measurement accuracy, deterministic behavior, and trustworthy manufacturing outputs matter more than cleverness.
- Fail closed, not open. If manufacturing / cut-list / costing data is incomplete, surface blockers and prevent false readiness.
- The app must be safe for user testing and progressing toward production, not just passing superficial tests.

## Required engineering rules

- Read the actual current code before changing anything. Grounding in source beats assumption.
- Prefer extending the existing architecture over inventing a parallel one.
- Remove skeletons / placeholders when implementing real behavior (delete the stubs; do not leave dead branches).
- Add or update automated tests for every meaningful behavior change.
- Respect project references: Domain → Application → Presentation → App. Do not introduce reverse references. `CabinetDesigner.Application` must not reference `CabinetDesigner.Rendering`.
- Keep UI-thread-affine WPF behavior correct — continue to use the existing `DispatchIfNeeded` pattern when reacting to event-bus messages in viewmodels.
- Do not silently swallow important failures. If a `catch` block is unavoidable, route to `IAppLogger` or surface via `IApplicationEventBus` / `StatusBarViewModel`.
- Any incomplete professional workflow must surface a validation blocker or explicit warning in the UI / service layer.
- Error codes are stable string literals — do not rename or reformat once published.
- Every stage must be deterministic: identical inputs produce identical outputs.
- `dotnet build -warnaserror` and `dotnet test` must be clean before closing any batch.

## Output rules

At the end of every implementation run, report these four sections verbatim:

1. **Files changed** — absolute paths (`src/…` / `tests/…`).
2. **What was implemented** — specific per-file behavior, not hand-waving.
3. **Tests added/updated** — test class + method names alongside their files.
4. **Remaining risks / blockers** — explicit. If none, say "none" with confidence.

No trailing narrative beyond these four sections.

## Skill usage

Invoke skills by name at the start of the run. Do not mention them without invoking.

### Implementation prompts (P3–P14)

- `superpowers:executing-plans` — these prompts execute a written plan with review checkpoints. This is the primary skill for every code prompt.
- `superpowers:test-driven-development` — every batch has a "Test Coverage Required" section. Write the tests first where feasible.
- `superpowers:verification-before-completion` — invoke before claiming a batch's Definition of Done is met. This catches "tests pass locally but the claim is false" failures.

### Audit prompt (P15)

- Use the `Agent` tool with `subagent_type: "feature-dev:code-reviewer"` for an independent second pass. The subagent does not share your working context and therefore cannot rubber-stamp your own work.

### Cross-cutting

- `superpowers:systematic-debugging` — invoke when a test cascade fails across batches, before proposing fixes.
- `superpowers:subagent-driven-development` — when a batch has independent sub-tasks (e.g., P7 + P8 + P12 all partially implement B7 and can be parallelized).

## Reference documents (always loaded)

- `docs/ai/finish_round/finish_round_global_instructions.md` — this file.
- `docs/ai/finish_round/finish_execution_plan.md` — why each batch exists and the go/no-go lines.
- `docs/ai/finish_round/finish_work_queue.md` — authoritative per-batch spec (objective, files, behaviors, validation rules, tests, DoD).

If any prompt's instructions appear to conflict with the work queue, the work queue wins.
