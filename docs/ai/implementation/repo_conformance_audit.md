# Repository Conformance Audit

Date: 2026-04-07
Mode: REFACTOR-CONFORMANCE
Auditor: Claude Opus 4.6

---

## 1. IMPLEMENTATION PLAN

### 1.1 Current State Summary

The repository contains **zero implementation code**. There is no `.sln` file, no `.csproj` files, no C# source files, and no test files. The entire repository consists of:

- Architecture documentation (`docs/ai/context/`, `docs/ai/outputs/`)
- Prompt pack / playbook markdown files (root-level)

### 1.2 Architecture Spec Coverage

The documentation is comprehensive and production-grade. The following specs are fully defined:

| Spec | File | Status |
|---|---|---|
| Global instructions | `docs/ai/context/code_phase_global_instructions.md` | Complete |
| Architecture summary | `docs/ai/context/architecture_summary.md` | Complete |
| Domain model | `docs/ai/outputs/domain_model.md` | Complete — entities, VOs, identifiers, invariants |
| Commands | `docs/ai/outputs/commands.md` | Complete |
| Orchestrator | `docs/ai/outputs/orchestrator.md` | Complete — 11-stage pipeline, interfaces, implementation |
| Application layer | `docs/ai/outputs/application_layer.md` | Complete — services, handlers, DTOs, events |
| Presentation | `docs/ai/outputs/presentation.md` | Complete — ViewModels, shell layout, event flow |
| Persistence | `docs/ai/outputs/persistence_strategy.md` | Complete — schema, repos, snapshots, migrations |
| Geometry system | `docs/ai/outputs/geometry_system.md` | Present (not fully audited — likely complete) |
| Validation engine | `docs/ai/outputs/validation_engine.md` | Present |
| Why engine | `docs/ai/outputs/why_engine.md` | Present |
| Editor engine | `docs/ai/outputs/editor_engine.md` | Present |
| Rendering | `docs/ai/outputs/rendering.md` | Present |
| Cross-cutting | `docs/ai/outputs/cross_cutting.md` | Present |
| Manufacturing | `docs/ai/outputs/manufacturing.md` | Present |
| Install planning | `docs/ai/outputs/install_planning.md` | Present |
| Architecture reconciliation | `docs/ai/outputs/architecture_reconciliation.md` | Present |

### 1.3 Architecture Drift

**No drift exists** — there is no code to drift. The risk is entirely forward-looking: the first implementation prompts must strictly follow the specs to avoid introducing drift from day one.

### 1.4 Missing Foundations (Everything)

Since no code exists, every layer is missing. The safest implementation sequence must respect dependency order (inner layers first) and MVP priorities from the architecture summary.

### 1.5 Proposed Implementation Sequence

Based on the dependency graph and MVP priorities from `architecture_summary.md`:

#### Phase 1: Solution Skeleton + Domain Foundation
1. **Solution and project structure** — Create `.sln` and all 11 `.csproj` files with correct dependency references
2. **Geometry value objects** — `Length`, `Angle`, `Point2D`, `Vector2D`, `Rect2D`, `LineSegment2D`, `Thickness`, `GeometryTolerance` in `CabinetDesigner.Domain.Geometry`
3. **Strongly typed identifiers** — All ID types in `CabinetDesigner.Domain.Identifiers`
4. **Cross-cutting domain abstractions** — `IClock`, `OverrideValue`

#### Phase 2: Core Domain Entities
5. **Project & Revision context** — `Project`, `Revision`, `ApprovalState`
6. **Spatial scene context** — `Room`, `Wall`, `WallOpening`, `Obstacle`
7. **Run engine context** — `CabinetRun`, `RunSlot`, `Filler`, `EndCondition`
8. **Cabinet & Assembly context** — `Cabinet`, `CabinetType`, `Opening`

#### Phase 3: Command System + Orchestrator
9. **Command interfaces and types** — `IDesignCommand`, `IEditorCommand`, `CommandMetadata`, concrete commands
10. **Validation primitives** — `ValidationIssue`, `ValidationSeverity`, `ValidationRule`
11. **Resolution pipeline** — `ResolutionContext`, `IResolutionStage`, `StageResult`, `IDeltaTracker`
12. **ResolutionOrchestrator** — Implementation with stage registration

#### Phase 4: Application Layer
13. **Application services** — `IProjectService`, `IRunService`, `IUndoRedoService`, etc.
14. **Handlers** — `IDesignCommandHandler`, `IPreviewCommandHandler`, `IEditorCommandHandler`
15. **DTOs** — All application DTOs
16. **Event bus** — `IApplicationEventBus`, event types

#### Phase 5: Persistence
17. **Repository contracts** — Interfaces in `CabinetDesigner.Application.Persistence`
18. **Persistence models** — Internal flat row types
19. **Mappers** — Domain-to-row and row-to-domain
20. **SQLite schema + migrations** — Initial schema, migration runner
21. **Repository implementations** — SQLite-backed repos
22. **Unit of work** — Transaction wrapper

#### Phase 6: Infrastructure + Editor + Rendering
23. **Infrastructure** — `SystemClock`, logging, settings
24. **Editor engine** — Interaction state, snap engine, placement candidates
25. **Rendering** — Canvas rendering, hit testing

#### Phase 7: Presentation
26. **Shell and ViewModels** — `ShellViewModel`, panel VMs, WPF views
27. **App bootstrap** — DI wiring, startup

#### Phase 8: Exports + Projections
28. **Cut list generation**, shop drawings, machine exports (post-MVP)

---

## 2. FILES TO CREATE

### 2.1 Solution Structure (Minimal Scaffolding)

```
CabinetDesigner.sln

src/
  CabinetDesigner.App/
    CabinetDesigner.App.csproj
  CabinetDesigner.Presentation/
    CabinetDesigner.Presentation.csproj
  CabinetDesigner.Application/
    CabinetDesigner.Application.csproj
  CabinetDesigner.Domain/
    CabinetDesigner.Domain.csproj
  CabinetDesigner.Infrastructure/
    CabinetDesigner.Infrastructure.csproj
  CabinetDesigner.Persistence/
    CabinetDesigner.Persistence.csproj
  CabinetDesigner.Editor/
    CabinetDesigner.Editor.csproj
  CabinetDesigner.Rendering/
    CabinetDesigner.Rendering.csproj
  CabinetDesigner.Integrations/
    CabinetDesigner.Integrations.csproj
  CabinetDesigner.Exports/
    CabinetDesigner.Exports.csproj

tests/
  CabinetDesigner.Tests/
    CabinetDesigner.Tests.csproj
```

### 2.2 Project Dependency Map

```
App → Presentation, Application, Domain, Infrastructure, Persistence, Editor, Rendering, Integrations, Exports
Presentation → Application, Editor, Rendering
Application → Domain
Editor → Domain
Rendering → Domain
Persistence → Application (for repository interfaces), Domain (for mapping)
Infrastructure → Domain (for IClock)
Integrations → Domain
Exports → Domain, Application
Tests → All projects
```

### 2.3 File Targets for Phase 1 (First Implementation Prompt)

| File | Project | Purpose |
|---|---|---|
| `src/CabinetDesigner.Domain/Geometry/Length.cs` | Domain | Immutable length value object |
| `src/CabinetDesigner.Domain/Geometry/Angle.cs` | Domain | Immutable angle value object |
| `src/CabinetDesigner.Domain/Geometry/Point2D.cs` | Domain | Immutable 2D point |
| `src/CabinetDesigner.Domain/Geometry/Vector2D.cs` | Domain | Immutable 2D vector |
| `src/CabinetDesigner.Domain/Geometry/Rect2D.cs` | Domain | Immutable rectangle |
| `src/CabinetDesigner.Domain/Geometry/LineSegment2D.cs` | Domain | Immutable line segment |
| `src/CabinetDesigner.Domain/Geometry/Thickness.cs` | Domain | Nominal + actual thickness |
| `src/CabinetDesigner.Domain/Geometry/GeometryTolerance.cs` | Domain | Tolerance constants and comparison |
| `src/CabinetDesigner.Domain/Identifiers/*.cs` | Domain | All strongly typed IDs |
| `src/CabinetDesigner.Domain/IClock.cs` | Domain | Clock abstraction |
| `src/CabinetDesigner.Domain/OverrideValue.cs` | Domain | Constrained override union |
| `tests/CabinetDesigner.Tests/Geometry/*.cs` | Tests | Geometry VO tests |
| `tests/CabinetDesigner.Tests/Identifiers/*.cs` | Tests | ID type tests |

---

## 3. FILES TO MODIFY

None. There are no existing code files to modify.

---

## 4. CODE

No code is written in this audit. This is analysis-only per the REFACTOR-CONFORMANCE execution mode and the task scope (audit + implementation map, not code generation).

The only scaffolding file that could be introduced is the solution file and empty project files, but this is deferred to the first FOUNDATION prompt to avoid creating empty shells that may mislead subsequent prompts about what exists.

---

## 5. TESTS

No tests are written in this audit. Tests will be introduced alongside the first domain code in Phase 1.

Required test categories for Phase 1:
- **Geometry value object tests**: construction, equality, arithmetic, tolerance, serialization round-trip, edge cases (zero, negative, overflow)
- **Identifier tests**: uniqueness, equality, value semantics
- **OverrideValue tests**: all union cases, pattern matching exhaustiveness

---

## 6. RATIONALE

### 6.1 Why No Code in This Audit

The task explicitly scopes this to "analysis and tiny setup changes only." Since the repo has zero code, the audit's value is in mapping the gap and defining the implementation sequence — not in creating empty project shells.

### 6.2 Why Domain-First Sequence

The architecture is layered with strict dependency rules:
- Domain has zero dependencies — it can be built and tested in isolation
- Application depends on Domain — cannot start until domain primitives exist
- Persistence depends on Application interfaces and Domain entities — cannot start until both exist
- Presentation depends on Application DTOs/events — must come after Application
- App (bootstrap) wires everything — must come last

Building bottom-up from Domain ensures each layer can be fully tested at the point it's built, and no layer is created with missing dependencies that would require placeholder hacks.

### 6.3 Why Geometry First Within Domain

The architecture summary lists geometry value objects as the #1 MVP priority and declares "no primitive dimension math" as a non-negotiable guardrail. Every subsequent entity (`Wall`, `Room`, `Cabinet`, `Run`) uses `Length`, `Point2D`, `Thickness`, etc. Building geometry first means all entity constructors can use the real types from day one — no `double`/`decimal` placeholders that would need refactoring.

### 6.4 Why Single Test Project

The architecture summary specifies a single `CabinetDesigner.Tests` project. This is sufficient for a modular monolith and avoids test project proliferation. Test classes are organized by namespace mirroring the source project structure.

---

## 7. FOLLOW-UP NOTES

### 7.1 Recommended First Prompt

**Execution mode:** FOUNDATION

**Task:** Create the solution skeleton (`CabinetDesigner.sln`, all 11 `.csproj` files with correct dependency references, test project) and implement the complete geometry value object system (`Length`, `Angle`, `Point2D`, `Vector2D`, `Rect2D`, `LineSegment2D`, `Thickness`, `GeometryTolerance`) with full unit tests.

This is the highest-value first step because:
- It establishes the project structure that all subsequent prompts will build on
- Geometry VOs are the most foundational domain type — everything depends on them
- They are self-contained and fully testable without any other layer
- They enforce the "no primitive dimensions" guardrail from the start

### 7.2 Risks to Watch

| Risk | Mitigation |
|---|---|
| **Geometry internal storage**: spec says "decimal-oriented, not binary float" | Use `decimal` internally in `Length`, not `double`. Test round-trip precision. |
| **Project reference cycles** | Follow the dependency map strictly. `Application` must not reference `Persistence` implementations — only interfaces. |
| **Namespace drift** | All namespaces must match the spec exactly (e.g., `CabinetDesigner.Domain.Geometry`, not `CabinetDesigner.Geometry`). |
| **WPF project type for App/Presentation** | Use `<OutputType>WinExe</OutputType>` and `<UseWPF>true</UseWPF>` only on `App` and `Presentation`. Domain, Application, and other layers must be plain class libraries with no WPF dependency. |
| **Test framework** | Use xUnit per global instructions. Do not introduce NUnit or MSTest. |
| **.NET version** | Target `net8.0` minimum per stack spec. |

### 7.3 Prompt Sequence After Phase 1

| Order | Mode | Scope |
|---|---|---|
| 2 | FOUNDATION | Strongly typed identifiers + `IClock` + `OverrideValue` |
| 3 | FOUNDATION | Project & Revision entities with invariant tests |
| 4 | FOUNDATION | Spatial scene entities (Room, Wall, WallOpening, Obstacle) |
| 5 | FOUNDATION | Command interfaces and concrete command types |
| 6 | FOUNDATION | Validation primitives (`ValidationIssue`, `ValidationSeverity`) |
| 7 | FOUNDATION | Resolution pipeline interfaces + `ResolutionOrchestrator` skeleton |
| 8 | VERTICAL SLICE | First end-to-end: Create project + add room + add wall (domain → orchestrator → persistence) |
| 9 | FOUNDATION | Application services + handlers + DTOs |
| 10 | FOUNDATION | Persistence layer (schema, repos, mappers, unit of work) |
| 11 | FOUNDATION | Why Engine foundation |
| 12 | FOUNDATION | Editor engine skeleton |
| 13 | FOUNDATION | WPF shell + bootstrap |
| 14 | VERTICAL SLICE | First visual: render a room with walls on canvas |
| 15 | VERTICAL SLICE | Add cabinet run to wall, see it rendered |

### 7.4 Files That Should NOT Be Created Yet

- No `CLAUDE.md` or project-level AI configuration files (the docs directory already serves this purpose)
- No CI/CD configuration (premature without code)
- No `.editorconfig` or `Directory.Build.props` (introduce with the solution file in Phase 1)
- No NuGet package management beyond what the `.csproj` files require
