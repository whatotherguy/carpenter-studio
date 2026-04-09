# Architecture Summary — Cabinet Design Software

Condensed from: `cabinet_architecture_playbook_windows_dotnet_v2.md`

---

## Mission

**Fast to draw. Hard to mess up. Safe to build.**

A Windows desktop cabinet design and production system combining assisted layout authoring with shop-grade precision, so carpenters can design faster, make fewer mistakes, and move confidently from concept to cut list to install.

---

## Stack

- **C# / .NET 8+** — core language and runtime
- **WPF** — native Windows desktop UI (MVVM)
- **SQLite** — local project storage and snapshots
- **Modular monolith** — no microservices, no web-first assumptions

---

## Architecture Guardrails (Non-Negotiable)

1. All design changes go through `ResolutionOrchestrator`
2. No primitive dimensions — use geometry value objects (`Length`, `Angle`, `Point2D`, `Vector2D`, `Rect2D`, `LineSegment2D`, `Thickness`)
3. Separate the six realities: interaction / intent / engineering / manufacturing / install / commercial
4. Deterministic outputs only
5. Immutable approved snapshots
6. Validation must include severity (`info` / `warning` / `error` / `manufacture_blocker`)
7. Full explanation traceability required (Why Engine)
8. Domain must be UI-independent and testable without WPF
9. Runs are first-class — continuity, reveals, fillers, end conditions solved at run level
10. Capture authoring as intent-driven commands, not raw geometry mutation
11. Approved historical revisions must remain durable even as working schema evolves
12. All dimensional logic must flow through the Geometry system (Length, Offset, etc.) and never use primitives.
---

## Six Realities

| Reality | Owns | Examples |
|---|---|---|
| **Interaction** | What the user is doing now | Drag cabinet, append to run, resize segment |
| **Design Intent** | What the user means to create | 36" base, shaker style, center sink on window |
| **Engineering** | How design resolves into constructible logic | Opening breakdown, filler math, corner strategy |
| **Manufacturing** | How design becomes shop output | Parts, cut lists, boring patterns, nesting |
| **Install** | How product is physically installed | Install order, fastening zones, stud checks, shim allowances |
| **Commercial** | How job is costed, versioned, approved | Estimates, taxes, approval states, release to shop |

---

## Solution Structure

| Project | Role |
|---|---|
| `CabinetDesigner.App` | WPF startup, bootstrap, DI wiring |
| `CabinetDesigner.Presentation` | MVVM ViewModels, WPF views, UI commands |
| `CabinetDesigner.Application` | Use cases, `ResolutionOrchestrator`, command handlers, DTOs |
| `CabinetDesigner.Domain` | Entities, value objects, rules, invariants, geometry foundation |
| `CabinetDesigner.Infrastructure` | File system, logging, settings, caching |
| `CabinetDesigner.Persistence` | SQLite, repositories, snapshots, migrations |
| `CabinetDesigner.Editor` | Interaction engine, snap engine, placement candidates, drag/drop |
| `CabinetDesigner.Rendering` | Canvas rendering, hit testing, guides, adorners |
| `CabinetDesigner.Integrations` | Hardware catalog import, vendor adapters |
| `CabinetDesigner.Exports` | Cut lists, shop drawings, machine exports, proposals |
| `CabinetDesigner.Tests` | Unit, pipeline, snapshot, validation, property-based tests |

---

## Resolution Pipeline (11 Stages)

1. **Input capture** — drag/drop, numeric input, templates, overrides → interaction + intent state
2. **Interaction interpretation** — translate actions into intent commands (e.g., `AppendCabinetToRun`)
3. **Spatial resolution** — scene placement, run membership, adjacency → layout graph
4. **Engineering resolution** — assemblies, openings, fillers, frame logic → constructible assembly graph
5. **Constraint propagation** — material thickness, hardware rules, grain, clearances → constrained graph
6. **Part generation** — parts, labels, dimensions, material, edge treatment → part graph
7. **Manufacturing planning** — cut lists, machining, kerf, nesting → manufacturing plan
8. **Install planning** — install order, dependencies, fastening, access checks → install plan
9. **Costing** — materials, labor, taxes, markup, revision delta → estimate snapshot
10. **Validation** — checks across all layers → issue set with severity + fix suggestions
11. **Packaging / snapshot** — freeze immutable snapshot, bind to revision ID

All design-changing commands must flow through this pipeline via `ResolutionOrchestrator`.

---

## Key Bounded Contexts

- **Project & Revision** — metadata, history, approvals, immutable snapshots
- **Spatial Scene** — room, walls, openings, obstacles, placement coordinates
- **Authoring / Interaction** — drag/drop, selection, placement intent, suggested actions
- **Snap & Placement** — snap anchors, ranked candidates, alignment guides, drop validity
- **Run Engine** — grouped cabinet runs, reveal continuity, shared stiles, fillers, end conditions
- **Cabinet / Assembly Resolver** — cabinet types, openings, doors/drawers, construction method
- **Material Catalog + Grain** — nominal vs actual thickness, grain rules, veneer matching
- **Hardware Catalog + Constraints** — manufacturer SKUs, boring patterns, clearances, compatibility
- **Part Generation** — parts, geometry, labels, material assignment, edge treatment
- **Manufacturing Planning** — cut lists, CNC/saw workflows, kerf, nesting
- **Install Planning** — dependency graph, fastening zones, stud/blocking, tolerances
- **Costing / Estimating** — material/labor/install cost, taxes, discounts, revision deltas
- **Validation / Error Detection** — collision, compatibility, manufacturability, installability checks
- **Why Engine / Explanation Graph** — lineage, rule records, source-to-output traceability
- **Template / Library** — cabinet templates, style presets, shop standards

---

## Command Architecture

- `IDesignCommand` — intent-driven design commands
- `IEditorCommand` — editor interaction commands
- `ResolutionOrchestrator` — single choke point for all design changes
- `ResolutionContext` / `ResolutionResult` — pipeline context and output

Commands flow: UI → Editor → Application (Orchestrator) → Domain → back up with results.

---

## Editor: Fast Path vs Deep Path

- **During drag (fast path):** lightweight snap evaluation, quick candidate ranking, preview — no full recompute
- **After commit (deep path):** full deterministic resolution, validation, explanation records, cost/manufacturing refresh

Maintain distinct `LightweightLayoutGraph` (preview) and `ResolvedProject` (authoritative state).

---

## Geometry Foundation

Immutable value objects in `CabinetDesigner.Domain.Geometry`:

`Length`, `Angle`, `Point2D`, `Vector2D`, `Rect2D`, `LineSegment2D`, `Thickness`

- Internal storage: decimal-oriented (not binary float) for dimensional truth
- Support fractional inches, decimal inches, imperial/metric display
- Actual vs nominal thickness on all materials

---

## Parameter Hierarchy

Resolution order (most general → most specific):
1. Global shop standards → 2. Project defaults → 3. Room defaults → 4. Run-level rules → 5. Cabinet-level → 6. Opening/interior → 7. Local overrides

---

## Versioning & Workflow

**States:** draft → under_review → approved → locked_for_manufacture → released_to_shop → ready_for_install → installed → superseded

**Snapshots on approval:** design, part, manufacturing, install, estimate — all frozen and immutable, stored in versioned serialized form insulated from working-schema drift.

---

## MVP Priorities

- Geometry value objects and dimensional engine
- Room scene + drag/drop + snap foundation
- Run-based cabinet modeling
- Engineering resolution + actual vs nominal thickness
- Part generation + cut list generation
- `ResolutionOrchestrator`
- Validation framework + Why Engine foundation
- Revision + estimate snapshots
- WPF desktop shell with core editor workflow
- Autosave and crash recovery baseline
