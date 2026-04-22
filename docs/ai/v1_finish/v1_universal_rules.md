# Carpenter Studio — V1 Universal Rules

These rules are authoritative for every prompt in `v1_prompt_pack.md`. Every implementation
run must load and obey this file. Any conflict between a prompt and this file is resolved
in favor of this file.

---

## 1. V1 Product Definition

Carpenter Studio V1 is a **professional-grade cabinet design application** that lets a
real cabinet shop:

1. Create a project containing one or more **rooms**.
2. Define each room's walls, ceiling height, and obstacles.
3. **Visually** design runs of cabinets inside rooms via a graphical canvas editor
   with drag, drop, resize, and snap.
4. Add, edit, and remove any cabinet and all of its buildable components
   (sides, top, bottom, back, shelves, face-frame rails/stiles/mullions, openings for
   doors and drawers, material and thickness overrides).
5. Generate a **cut list export** (CSV, plain text, and printable HTML) from the
   resolved design.

Anything outside this scope is **V2** and must be tracked in `docs/V2_enhancements.md`.

## 2. In Scope for V1 (must be complete, not skeletons)

- Project + room + wall creation UI and persistence.
- Canvas-based cabinet placement, selection, resize, and deletion.
- Cabinet property inspector covering width, depth, height, category,
  construction method, openings (doors/drawers/false-fronts), material
  overrides per part type, thickness overrides, shelf count, and toe-kick.
- Pipeline Stages 1–6 and 10–11 (Input Capture → Part Generation, Manufacturing
  Planning, Packaging) producing deterministic output.
- Constraint propagation for materials and thickness using **shop defaults**
  (a locally seeded default material/thickness table — no pricing, no vendor data).
- Engineering resolution (assemblies, fillers, end conditions) and validation
  wiring tied to real workflow state.
- Cut list export in CSV + TXT + HTML.
- Deterministic, hash-addressed project snapshot that does **not** depend on
  pricing being present.
- Fail-closed safety net so skeleton stages cannot silently succeed in Full mode.

## 3. Out of Scope for V1 (explicitly deferred to V2)

- External parts/material/hardware catalog API integration (e.g. Richelieu,
  Blum, Hafele).
- Real pricing data, cost totals, markup, tax, or revision cost deltas.
- Bid/quote generation, customer proposals, contract export.
- Installation instructions, step-by-step install plans sent to the field.
- Per-vendor boring patterns, clearances, or hinge/slide vendor systems.
- Revision compare / diff UI and approval workflow polish beyond basic
  snapshot approve.
- Grain policy beyond the default `Lengthwise` / `None` assignment.
- Cloud sync, multi-user collaboration, plug-in system.

**Critical rule:** no V1 feature may be wired to a V2 feature in a way that
makes the V1 feature break when the V2 feature is not implemented. If the
existing codebase has such a coupling, the prompt that touches it must break
the coupling. Costing is the main example and is dealt with explicitly in the
pack (see `P02_decouple_costing.md`).

## 4. Engineering Rules (apply to every prompt)

1. **Read current source before changing it.** Ground every change in the
   files listed in the prompt; do not infer from file names.
2. **Extend the existing architecture.** Do not invent parallel
   abstractions. Domain → Application → Presentation → App is the layering.
   Do **not** add a reverse reference. `CabinetDesigner.Application` must
   not reference `CabinetDesigner.Rendering`. `CabinetDesigner.Presentation`
   must not import `CabinetDesigner.Domain` directly from its viewmodel
   adapters — use Editor/Application abstractions.
3. **No skeletons in V1.** If a prompt is producing a V1-scope feature, the
   output must be complete, tested, and deterministic. "I'll fill this in
   later" is a failure condition. If the feature cannot be completed in the
   prompt, stop and report why — do **not** commit a placeholder.
4. **Fail closed in Full mode.** Skeleton or unimplemented stages must
   return `StageResult.Failed` with a stable, uppercase error code in
   `ResolutionMode.Full`. Preview mode may still surface warnings.
5. **Determinism.** Identical inputs must produce byte-identical outputs.
   No `Guid.NewGuid()` in a stage, no `DateTime.Now` outside `IClock`,
   no unordered dictionary enumeration leaking into serialized output.
   Every stage you touch must continue to satisfy the existing
   `DeterminismTests`; add a new determinism assertion when you add a
   new stage output.
6. **Decouple costing and other V2 features.** V1 pipeline runs must
   succeed even when costing is unavailable. Packaging, export, validation,
   and snapshot must not hard-depend on a non-null, non-zero
   `CostingResult`. When costing is skipped, record the reason explicitly.
7. **Tests are mandatory, not optional.** Every behavior change needs an
   xUnit test. Follow the file locations already used in the repo
   (`tests/CabinetDesigner.Tests/...`). Each prompt lists required test
   classes and method names; write those exact names.
8. **No silent exception swallowing.** `catch (Exception)` is only allowed
   when the caught exception is either logged via `IAppLogger` or surfaced
   via `IApplicationEventBus` / `StatusBarViewModel`. `catch
   (NotImplementedException)` is forbidden in production code under
   `src/`.
9. **Stable error codes.** Once an error code string is published, do not
   rename it. Codes are uppercase, underscore-separated ASCII.
10. **Build and test must be clean.** Run `dotnet build -warnaserror` and
    `dotnet test` before claiming done. A prompt that leaves either
    broken is **not** complete.
11. **UI-thread safety.** Viewmodels reacting to event-bus messages use
    the existing `DispatchIfNeeded` pattern. Do not `.Wait()` or
    `.Result` on async commands from the UI thread.
12. **Markdown file references use repo-relative paths.** When writing
    docs or comments, use `src/...` and `tests/...` rather than absolute
    paths.

## 5. V2 Deferral Discipline

Whenever a V1 prompt encounters a V2 feature (pricing, vendor catalog, bid,
install plan authoring, etc.), the implementing agent must:

1. Ensure the V1 pipeline / feature functions correctly without the V2
   feature being present.
2. Add a `// V2:` code comment at the point where the V2 extension point
   would hook in, naming the V2 feature in one short line.
3. If `docs/V2_enhancements.md` does not already list the feature, append
   a one-line bullet under the appropriate section.

Do **not** silently remove V2 code that already exists. Instead, guard it
behind an explicit "configured / not configured" check so it only activates
when V2 data is actually present.

## 6. Output Rules (end of every prompt)

Every code prompt must report these four sections **verbatim** at the end
of the run:

1. **Files changed** — absolute repo-relative paths (`src/...`,
   `tests/...`, `docs/...`).
2. **What was implemented** — per-file behavior, specific and concrete.
3. **Tests added/updated** — test class + method names next to their
   file paths.
4. **Remaining risks / blockers** — explicit. If none, say "none" with
   confidence. If a V2 deferral was recorded, list it here.

No trailing narrative beyond these four sections.

## 7. Skill Usage

Invoke skills by name at the start of the prompt. Do not mention a skill
without invoking it.

- `superpowers:executing-plans` — primary skill for every code prompt.
- `superpowers:test-driven-development` — each prompt has a test list;
  write failing tests first where feasible.
- `superpowers:verification-before-completion` — invoke before claiming
  a Definition of Done is met.
- `superpowers:systematic-debugging` — invoke when a test cascade fails.
- `feature-dev:code-reviewer` (via the `Agent` tool) — only for the final
  V1 acceptance audit prompt.

## 8. Reference Documents (always loaded)

- `docs/ai/v1_finish/v1_universal_rules.md` — this file.
- `docs/ai/v1_finish/v1_prompt_pack.md` — prompt pack with the V1 prompts.
- `docs/V2_enhancements.md` — authoritative V2 deferral list.
- `docs/ai/finish_round/finish_work_queue.md` — legacy per-batch spec.
  Still useful for historical context but V1 rules here override any
  V2-flavored expectations in that file (e.g. costing reconciliation).

## 9. Definition of "V1 complete"

V1 is complete when **all** of these are true:

- [ ] Full-mode pipeline succeeds end-to-end on a sample room with at
      least one base and one wall cabinet, and produces a non-empty
      deterministic cut list.
- [ ] Cut list export produces CSV + TXT + HTML files with byte-identical
      output across repeated runs against the same input.
- [ ] User can launch the app, create a project, add a room, draw walls,
      place cabinets from the catalog onto the canvas, edit their
      properties in the inspector, delete them, and export a cut list —
      without touching anything outside the shell.
- [ ] Invalid designs are rejected by packaging; valid designs are hash
      addressed deterministically.
- [ ] No `throw new NotImplementedException` or `catch
      (NotImplementedException)` remains in production code under `src/`.
- [ ] `dotnet build -warnaserror` and `dotnet test` are clean.
- [ ] All V2 extension points carry a `// V2:` comment and are listed in
      `docs/V2_enhancements.md`.
