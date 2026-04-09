# Cabinet Design Software — Code Phase Prompt Pack (v6, hybrid read-first)

This version is optimized for **minimal manual cutting/pasting** and **direct execution**.

Every executable prompt now starts with a mandatory `READ THIS FILE FIRST` directive that points to the shared global instructions file while still keeping the prompts largely self-contained.

It solves three problems from v4:
1. every prompt now explicitly declares the **model to use**
2. every prompt is still **self-contained enough** to be robust
3. every prompt now explicitly requires the model to read the shared global instructions file first

It also includes an optional shared file pattern if you want to keep one common context file in the repo.

---

# OPERATING PRINCIPLE

For reliability, each implementation prompt should be executable in one of two ways:

## Option A — Self-contained prompt (recommended default)
Use the prompt exactly as written.  
Each prompt already includes:
- core system context
- architecture guardrails
- execution rules
- required output format
- model designation

This minimizes setup mistakes.

## Option B — Shared context file + shorter task prompt
If you want cleaner, shorter prompts in daily use, create this file in the repo:

- `docs/ai/context/code_phase_global_instructions.md`

Then every shorter implementation prompt begins with:

Read this first before doing anything:
- `docs/ai/context/code_phase_global_instructions.md`

However, because AI models sometimes under-read or partially ignore shared files, the **self-contained prompts remain the safer default for business-critical implementation**.

---

# OPTIONAL SHARED FILE CONTENT

If you choose Option B, write the following into:

`docs/ai/context/code_phase_global_instructions.md`

---

## BEGIN SHARED FILE CONTENT

You are implementing a Windows desktop cabinet design system.

Stack:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith
- xUnit preferred unless the repo already standardizes on NUnit

Core product promise:
- fast to draw
- hard to mess up
- safe to build

Non-negotiable architectural guardrails:
- all design mutations must go through commands
- all command execution must flow through `ResolutionOrchestrator`
- no UI-driven domain mutation
- no primitive dimension math in domain logic where geometry value objects should exist
- domain layer must remain UI-independent
- persistence models must not leak into domain entities
- approved snapshots are immutable
- why/explanation data must remain traceable
- deterministic behavior only
- no hidden side effects
- no fake placeholders unless explicitly marked `NOT IMPLEMENTED YET`
- no architecture drift
- no speculative abstractions unless directly required by the current slice

Implementation quality bar:
- production-oriented code
- clear naming
- strong invariants
- defensive validation
- explicit failure paths
- focused unit tests for business logic
- integration tests where orchestration or persistence boundaries matter

Before writing code:
1. read only the minimum required files
2. infer existing repository conventions
3. reuse established namespaces and patterns where correct
4. do not rewrite unrelated files
5. do not rename files unless necessary
6. do not introduce new frameworks without explicit authorization
7. if architecture docs conflict with implementation, prefer architecture docs and refactor locally toward conformance

When writing code:
1. implement vertically coherent slices
2. keep constructors explicit
3. prefer immutable value objects where appropriate
4. prefer command/result types over booleans
5. avoid service locators
6. avoid dumping business logic into code-behind or view models
7. keep WPF concerns in presentation layer only
8. keep geometry math centralized
9. preserve undo/redo viability in all state-changing flows

When testing:
1. test invariants first
2. test failure cases, not just happy paths
3. test determinism
4. test command orchestration where business-critical
5. test snapshot immutability where relevant

Return output using these exact sections in this order:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

## END SHARED FILE CONTENT

---

# MODEL SELECTION RULES

Use these model choices unless a repo-specific constraint forces an exception.

## Claude Opus 4.6
Use for:
- repo conformance audits
- architecture drift correction
- orchestrator design/refactors
- domain model design/refactors
- validation engine
- why engine
- high-risk boundary cleanup
- broad refactor planning
- implementation reviews of code produced by faster models

Why:
- highest reasoning depth
- best for ambiguous architecture-heavy work
- safest for business-critical design decisions

## Claude Sonnet 4.6
Use for:
- most feature implementation slices
- WPF MVVM wiring
- rendering foundation
- persistence layer implementation
- editor interactions
- feature hardening
- medium-complexity refactors
- follow-on implementation after Opus defines file targets

Why:
- strong balance of reasoning and output speed
- good for production implementation in bounded scope

## Codex GPT-5.4 / GPT-5.4-mini
Use for:
- focused file generation
- test generation after architecture is already clear
- narrow refactors with explicit file targets
- repetitive or mechanical implementation work
- polish passes on tightly scoped areas

Preferred split:
- **Codex GPT-5.4** for medium-risk coding tasks that still require good judgment
- **Codex GPT-5.4-mini** for narrow, repetitive, or clearly-specified codehorse tasks

Never send Codex first into an ambiguous architectural zone.
First define the slice with Opus or Sonnet, then use Codex for execution if helpful.

---

# EXECUTION MODES

Every prompt must explicitly declare one mode:

- `FOUNDATION` — create core types and contracts
- `VERTICAL SLICE` — implement one end-to-end feature path
- `HARDENING` — add validation, invariants, failure handling, and tests
- `REFACTOR-CONFORMANCE` — reshape code to match architecture without changing intended behavior

---

# MASTER IMPLEMENTATION ORDER

1. geometry foundation
2. command contracts + base abstractions
3. domain value objects and aggregates
4. `ResolutionOrchestrator` skeleton
5. validation engine skeleton
6. why engine skeleton
7. application handlers
8. persistence mapping + snapshot persistence
9. editor interaction engine
10. presentation binding layer
11. rendering pipeline
12. manufacturing projection
13. install planning projection
14. cross-cutting infrastructure hardening
15. end-to-end conformance pass

---

# MASTER PROMPT PACK

All prompts below are self-contained.  
That means you can paste one prompt directly into the target model with no extra wrapper.

---

## C0 — Repository Conformance Audit

**Recommended model:** Claude Opus 4.6  
**Execution mode:** REFACTOR-CONFORMANCE

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REFACTOR-CONFORMANCE

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith
- xUnit preferred unless repo already standardizes otherwise

CORE PRODUCT PROMISE:
- fast to draw
- hard to mess up
- safe to build

NON-NEGOTIABLE ARCHITECTURAL GUARDRAILS:
- all design mutations must go through commands
- all command execution must flow through ResolutionOrchestrator
- no UI-driven domain mutation
- no primitive dimension math in domain logic where geometry value objects should exist
- domain layer must remain UI-independent
- persistence models must not leak into domain entities
- approved snapshots are immutable
- why/explanation data must remain traceable
- deterministic behavior only
- no hidden side effects
- no fake placeholders unless explicitly marked NOT IMPLEMENTED YET
- no architecture drift
- no speculative abstractions unless directly required by the current slice

IMPLEMENTATION QUALITY BAR:
- production-oriented code
- clear naming
- small files where practical
- strong invariants
- defensive validation
- explicit failure paths
- unit tests for business logic
- integration tests where orchestration or persistence boundaries matter

READ THESE FILES:
- solution file
- project files
- current folder structure
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/persistence_strategy.md

PRIMARY SPEC:
- docs/ai/context/architecture_summary.md

SUPPORTING SPECS:
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/persistence_strategy.md

TASK:
Audit the current repository against the architecture docs and produce a concrete implementation map before writing major code.

IN SCOPE:
- identify current modules/projects
- map current code to intended layers
- identify architecture drift
- identify missing foundations
- propose exact file/folder targets for upcoming prompts
- identify the safest implementation sequence based on current repo state

OUT OF SCOPE:
- large-scale rewrites
- speculative refactors
- UI polish
- feature additions not tied to architecture conformance

REQUIRED CONSTRAINTS:
- do not modify more than minimal scaffolding files unless strictly necessary
- prefer analysis and tiny setup changes only
- if a layer is missing, propose the smallest viable structure to add it

REQUIRED TESTS:
- only add tests if tiny scaffolding is introduced that requires them

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- docs/ai/implementation/repo_conformance_audit.md
- minimal scaffolding files only if strictly required
```

---

## C1 — Geometry Foundation Implementation

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith
- xUnit preferred unless repo already standardizes otherwise

ARCHITECTURAL GUARDRAILS:
- all design mutations must go through commands
- all command execution must flow through ResolutionOrchestrator
- no UI-driven domain mutation
- no primitive dimension math in domain logic where geometry value objects should exist
- domain layer must remain UI-independent
- persistence models must not leak into domain entities
- deterministic behavior only
- no architecture drift

READ THESE FILES:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/geometry_system.md
- existing domain/common utility files
- existing test project conventions

PRIMARY SPEC:
- docs/ai/outputs/geometry_system.md

SUPPORTING SPECS:
- docs/ai/context/architecture_summary.md

TASK:
Implement the geometry value object foundation used across the design system.

IN SCOPE:
- Length
- Offset
- Point2D
- Vector2D
- Rect2D
- tolerance/equality rules
- parsing/formatting only if spec requires it
- invariant enforcement
- operator support only where clearly justified
- unit tests

OUT OF SCOPE:
- rendering
- WPF converters
- persistence mappings unless trivial and required
- editor interactions
- cabinet-specific domain rules

REQUIRED CONSTRAINTS:
- no primitive leakage in public APIs where a geometry type belongs
- no mutable structs
- avoid floating-point traps where possible
- explicitly define units and tolerance semantics
- equality must be deterministic and test-backed

REQUIRED TESTS:
- invariant tests
- equality/tolerance tests
- arithmetic tests
- invalid input tests
- serialization tests only if serialization support is added

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- domain/common/geometry/*
- tests for geometry foundation
```

---

## C2 — Command Contracts + Base Infrastructure

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith
- xUnit preferred unless repo already standardizes otherwise

ARCHITECTURAL GUARDRAILS:
- all design mutations must go through commands
- all command execution must flow through ResolutionOrchestrator
- no direct repository mutation from commands
- domain layer must remain UI-independent
- deterministic behavior only
- no architecture drift

READ THESE FILES:
- docs/ai/outputs/commands.md
- docs/ai/outputs/geometry_system.md
- docs/ai/context/architecture_summary.md
- current application/domain abstractions
- existing undo/redo infrastructure if any

PRIMARY SPEC:
- docs/ai/outputs/commands.md

SUPPORTING SPECS:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/geometry_system.md

TASK:
Implement the core command abstractions and metadata contracts that all state changes must use.

IN SCOPE:
- IDesignCommand
- command metadata
- command result type
- base command abstractions
- undo/redo contract surface
- command validation hook contracts
- deterministic identifiers where required by spec
- tests for command contracts

OUT OF SCOPE:
- concrete cabinet commands beyond a tiny example if useful
- full orchestrator execution
- persistence
- UI wiring

REQUIRED CONSTRAINTS:
- commands must be explicit and serializable if architecture requires replay
- no generic god-command abstraction
- commands are intent carriers, not mini-service containers

REQUIRED TESTS:
- metadata presence/validation
- command immutability expectations
- undo/redo contract semantics
- result/failure path tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- domain/application command contracts
- tests for command infrastructure
```

---

## C3 — Domain Model Foundation

**Recommended model:** Claude Opus 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith
- xUnit preferred unless repo already standardizes otherwise

ARCHITECTURAL GUARDRAILS:
- domain layer must remain UI-independent
- all state-changing intent originates as commands
- no public setters on aggregate state without strong justification
- persistence models must not leak into domain
- deterministic behavior only
- no architecture drift

READ THESE FILES:
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/geometry_system.md
- docs/ai/outputs/commands.md
- current domain entities/value objects

PRIMARY SPEC:
- docs/ai/outputs/domain_model.md

SUPPORTING SPECS:
- docs/ai/outputs/geometry_system.md
- docs/ai/outputs/commands.md

TASK:
Implement the foundational domain entities, aggregates, and value objects needed for cabinet design state.

IN SCOPE:
- bounded context root types
- aggregate roots
- essential value objects
- invariants
- relationships
- construction/update patterns consistent with command-driven mutation
- unit tests

OUT OF SCOPE:
- editor gestures
- persistence DTOs
- WPF view models
- manufacturing/install projections unless foundationally required

REQUIRED CONSTRAINTS:
- keep domain UI-independent
- protect invariants in constructors/factories
- no premature inheritance trees
- prefer explicit composition

REQUIRED TESTS:
- aggregate invariant tests
- creation/update rule tests
- invalid relationship tests
- snapshot/read-only behavior tests if applicable

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- domain model files
- domain tests
```

---

## C4 — ResolutionOrchestrator Skeleton

**Recommended model:** Claude Opus 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith
- xUnit preferred unless repo already standardizes otherwise

ARCHITECTURAL GUARDRAILS:
- all design mutations must pass through ResolutionOrchestrator
- stages must be explicit and traceable
- do not hide business rules inside pipeline glue
- deterministic behavior only
- no architecture drift

READ THESE FILES:
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md
- current application pipeline code if any

PRIMARY SPEC:
- docs/ai/outputs/orchestrator.md

SUPPORTING SPECS:
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md

TASK:
Implement the ResolutionOrchestrator skeleton and its staged execution pipeline.

IN SCOPE:
- orchestrator contract
- 11-stage pipeline skeleton
- command intake
- stage result propagation
- failure handling
- extension points for validation/why engine/persistence hooks
- orchestrator tests

OUT OF SCOPE:
- full concrete logic for every stage
- manufacturing/install output generation
- WPF integration
- long-running optimization logic unless required by spec

REQUIRED TESTS:
- command enters orchestrator and traverses expected stages
- stage failure short-circuits correctly
- deterministic execution ordering
- no mutation occurs on failed execution
- extension point invocation tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- application/orchestration/*
- orchestration tests
```

---

## C5 — Validation Engine Skeleton

**Recommended model:** Claude Opus 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/validation_engine.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md

PRIMARY SPEC:
- docs/ai/outputs/validation_engine.md

SUPPORTING SPECS:
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md

TASK:
Implement the validation engine interfaces and first rule set wiring.

IN SCOPE:
- validation rule contracts
- validation result model
- severity model
- orchestrator integration seam
- initial rule registration
- tests

OUT OF SCOPE:
- full rule library
- UI presentation of validation messages
- persistence details

REQUIRED CONSTRAINTS:
- validation output must be deterministic and machine-consumable
- rules must be individually testable
- validation must not mutate state

REQUIRED TESTS:
- rule execution tests
- severity aggregation tests
- multi-rule result tests
- orchestrator integration seam tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- validation engine files
- validation tests
```

---

## C6 — Why Engine Skeleton

**Recommended model:** Claude Opus 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/why_engine.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/commands.md

PRIMARY SPEC:
- docs/ai/outputs/why_engine.md

SUPPORTING SPECS:
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/commands.md

TASK:
Implement the explanation/trace model used to explain decisions and pipeline outcomes.

IN SCOPE:
- explanation record types
- trace/session identifiers
- stage explanation entries
- command-linked explanation objects
- collection/aggregation contracts
- tests

OUT OF SCOPE:
- final UI explanation rendering
- natural language generation
- persistence unless trivial and required now

REQUIRED CONSTRAINTS:
- explanation data must be structured, not ad hoc strings only
- links to command/stage/rule must be preserved
- traces must be deterministic and auditable

REQUIRED TESTS:
- explanation object creation tests
- stage linkage tests
- aggregation tests
- deterministic ordering tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- why engine files
- why engine tests
```

---

## C7 — Application Command Handlers

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** VERTICAL SLICE

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
VERTICAL SLICE

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/domain_model.md
- current DI/application setup

PRIMARY SPEC:
- docs/ai/outputs/application_layer.md

SUPPORTING SPECS:
- docs/ai/outputs/commands.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/domain_model.md

TASK:
Implement the application-layer command handler pattern that receives UI intent, constructs commands, and routes them through the orchestrator.

IN SCOPE:
- application service interfaces
- handler contracts
- command dispatch path
- result mapping
- error propagation
- tests

OUT OF SCOPE:
- direct UI code
- persistence-heavy concerns except handler dependencies
- rendering

REQUIRED CONSTRAINTS:
- handlers coordinate; they do not own deep business rules
- no bypass around orchestrator
- result types must stay explicit

REQUIRED TESTS:
- handler dispatch tests
- failure propagation tests
- dependency interaction tests
- no-bypass orchestrator tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- application layer files
- application layer tests
```

---

## C8 — Persistence + Snapshot Foundation

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/persistence_strategy.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md
- existing SQLite/repository code if any

PRIMARY SPEC:
- docs/ai/outputs/persistence_strategy.md

SUPPORTING SPECS:
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md

TASK:
Implement the persistence layer and immutable snapshot persistence boundary.

IN SCOPE:
- repository contracts
- SQLite mappings
- snapshot record structure
- save/load boundaries
- domain-to-persistence mapping
- integration tests

OUT OF SCOPE:
- schema for every future feature unless required by current slice
- manufacturing exports
- UI-level file dialogs

REQUIRED CONSTRAINTS:
- persistence models remain separate from domain models
- snapshot records are immutable once approved
- load/save behavior must be deterministic
- transactional integrity where command commits occur

REQUIRED TESTS:
- SQLite integration tests
- mapping tests
- snapshot immutability tests
- round-trip persistence tests
- transaction/failure tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- infrastructure/persistence/*
- migrations/schema files
- integration tests
```

---

## C9 — First Editor Vertical Slice: Create + Move Cabinet

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** VERTICAL SLICE

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
VERTICAL SLICE

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/editor_engine.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/rendering.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md
- existing editor/viewmodel/render files

PRIMARY SPEC:
- docs/ai/outputs/editor_engine.md

SUPPORTING SPECS:
- docs/ai/outputs/presentation.md
- docs/ai/outputs/rendering.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md

TASK:
Implement the first end-to-end editor slice: create a cabinet and move it in 2D through the proper command/orchestrator/application flow.

IN SCOPE:
- create cabinet command path
- move cabinet command path
- view model interaction boundary
- basic selection state if required
- rendering refresh trigger
- tests at domain/application level and minimal presentation tests if practical

OUT OF SCOPE:
- advanced snapping
- resize gestures unless required by current slice
- door/drawer editing
- manufacturing calculations

REQUIRED CONSTRAINTS:
- UI gestures produce application intents, not direct domain mutation
- movement must use geometry types
- state changes must remain undo/redo-compatible
- keep the slice small but truly end-to-end

REQUIRED TESTS:
- create path tests
- move path tests
- invalid move tests
- orchestrator invocation tests
- basic view model contract tests if added

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- editor engine files
- application files
- minimal presentation files
- tests for the slice
```

---

## C10 — Snapping + Drag Interaction Hardening

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** HARDENING

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
HARDENING

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/editor_engine.md
- current editor implementation
- geometry foundation
- relevant tests

PRIMARY SPEC:
- docs/ai/outputs/editor_engine.md

SUPPORTING SPECS:
- docs/ai/outputs/geometry_system.md
- current editor slice files

TASK:
Harden drag and snapping behavior after the first editor slice exists.

IN SCOPE:
- snapping services
- snap candidates
- tolerance handling
- drag preview vs committed move distinction
- tests

OUT OF SCOPE:
- full rendering overhaul
- cabinet style logic
- persistence schema changes unless unavoidable

REQUIRED CONSTRAINTS:
- snapping rules must be deterministic
- preview state must not become committed state accidentally
- do not scatter snapping logic across UI and domain layers

REQUIRED TESTS:
- snap candidate selection tests
- tolerance threshold tests
- ambiguous snap resolution tests
- preview/commit separation tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- editor interaction/snapping files
- tests
```

---

## C11 — Presentation Layer MVVM Wiring

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** VERTICAL SLICE

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
VERTICAL SLICE

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/presentation.md
- docs/ai/outputs/application_layer.md
- current WPF project structure
- current view/viewmodel files

PRIMARY SPEC:
- docs/ai/outputs/presentation.md

SUPPORTING SPECS:
- docs/ai/outputs/application_layer.md

TASK:
Implement or refactor presentation wiring so WPF views communicate through MVVM and application-layer abstractions without domain leakage.

IN SCOPE:
- view model contracts
- command bindings
- observable state shape
- application service injection
- tests where feasible

OUT OF SCOPE:
- visual redesign
- new product features
- direct domain references in XAML/code-behind

REQUIRED CONSTRAINTS:
- keep code-behind thin
- no domain mutation from view models
- avoid framework-heavy patterns unless already in repo

REQUIRED TESTS:
- view model behavior tests
- property change tests
- binding command invocation tests where practical

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- WPF presentation files
- presentation tests
```

---

## C12 — 2D Rendering Foundation

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/rendering.md
- docs/ai/outputs/geometry_system.md
- current render/editor presentation files

PRIMARY SPEC:
- docs/ai/outputs/rendering.md

SUPPORTING SPECS:
- docs/ai/outputs/geometry_system.md

TASK:
Implement the 2D rendering foundation needed to display cabinet geometry cleanly and deterministically.

IN SCOPE:
- render model/view model boundary
- world-to-screen mapping
- basic cabinet shape rendering
- selection overlay hooks if spec requires
- tests for geometry transforms

OUT OF SCOPE:
- photorealism
- advanced zoom UX unless spec directly requires
- manufacturing overlays

REQUIRED CONSTRAINTS:
- keep rendering pipeline separate from domain logic
- geometry transforms must be testable
- avoid UI-thread-only math logic if it can be isolated

REQUIRED TESTS:
- transform tests
- render model generation tests
- bounds tests
- selection overlay tests if implemented

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- rendering files
- rendering tests
```

---

## C13 — Manufacturing Projection Foundation

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/manufacturing.md
- docs/ai/outputs/domain_model.md
- current projection/export files if any

PRIMARY SPEC:
- docs/ai/outputs/manufacturing.md

SUPPORTING SPECS:
- docs/ai/outputs/domain_model.md

TASK:
Implement the manufacturing projection layer that converts approved cabinet/domain state into parts and machining-ready structures.

IN SCOPE:
- projection models
- part extraction contracts
- machining placeholder seams if not fully spec’d
- tests

OUT OF SCOPE:
- final report UI
- external file export formats unless directly required
- install sequencing

REQUIRED CONSTRAINTS:
- projections must not mutate design state
- manufacturing output must be reproducible
- dimensional accuracy must use geometry value objects and explicit conversions

REQUIRED TESTS:
- part projection tests
- dimension accuracy tests
- deterministic ordering tests
- invalid state protection tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- manufacturing projection files
- manufacturing tests
```

---

## C14 — Install Planning Projection Foundation

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** FOUNDATION

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
FOUNDATION

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/install_planning.md
- docs/ai/outputs/domain_model.md
- current planning/export files if any

PRIMARY SPEC:
- docs/ai/outputs/install_planning.md

SUPPORTING SPECS:
- docs/ai/outputs/domain_model.md

TASK:
Implement the install planning projection layer that derives install sequence/materialized install steps from approved design state.

IN SCOPE:
- install plan models
- sequencing contracts
- dependency relationship handling
- tests

OUT OF SCOPE:
- UI workflow screens
- manufacturing machining logic
- advanced scheduling integrations

REQUIRED CONSTRAINTS:
- plan generation must be deterministic
- install output must be derived, not manually mutated primary state
- sequence rationale should be compatible with why engine if possible

REQUIRED TESTS:
- sequence generation tests
- dependency ordering tests
- deterministic output tests

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- install planning files
- install planning tests
```

---

## C15 — Cross-Cutting Hardening

**Recommended model:** Claude Sonnet 4.6  
**Execution mode:** HARDENING

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
HARDENING

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- docs/ai/outputs/cross_cutting.md
- current DI/logging/autosave/event infrastructure
- orchestrator/persistence integration

PRIMARY SPEC:
- docs/ai/outputs/cross_cutting.md

SUPPORTING SPECS:
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/persistence_strategy.md

TASK:
Implement cross-cutting infrastructure that supports reliability without violating architecture boundaries.

IN SCOPE:
- DI registration cleanup
- logging boundaries
- autosave wiring
- event publication boundaries if architecture requires
- tests where practical

OUT OF SCOPE:
- telemetry platform integration unless already in repo
- random framework additions
- business rule changes

REQUIRED CONSTRAINTS:
- cross-cutting concerns must not become hidden control flow
- autosave must respect approved snapshot strategy
- logs must support diagnosis without polluting domain types

REQUIRED TESTS:
- DI composition tests if feasible
- autosave trigger tests
- event emission tests if implemented

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- infrastructure/cross-cutting files
- hardening tests
```

---

## C16 — Architecture Conformance Refactor Pass

**Recommended model:** Claude Opus 4.6  
**Execution mode:** REFACTOR-CONFORMANCE

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

You are implementing a Windows desktop cabinet design system.

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REFACTOR-CONFORMANCE

STACK:
- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith

READ THESE FILES:
- all architecture output docs
- current implementation files for completed slices
- current tests

PRIMARY SPEC:
- docs/ai/context/architecture_summary.md

SUPPORTING SPECS:
- all docs/ai/outputs/*.md relevant to implemented areas

TASK:
Refactor the existing implementation to better conform to the architecture without changing intended behavior.

IN SCOPE:
- boundary cleanup
- namespace cleanup
- dependency direction cleanup
- extraction of misplaced logic
- test stabilization
- dead code removal only when clearly safe

OUT OF SCOPE:
- major new features
- speculative performance optimization
- UI redesign

REQUIRED CONSTRAINTS:
- behavior-preserving refactors preferred
- if behavior must change to satisfy architecture, isolate and explain it clearly
- no wide destructive rewrites

REQUIRED TESTS:
- update/add regression tests for any moved logic
- verify all impacted tests still pass
- add tests where conformance gaps were previously unprotected

RETURN OUTPUT USING THESE EXACT SECTIONS IN THIS ORDER:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- affected implementation files
- affected tests
- docs/ai/implementation/conformance_refactor_notes.md
```

---

# CODEX PATCH PROMPTS

Use these only after the slice and file targets are already clear.

## Patch A — Fix Architecture Drift in a File
**Recommended model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

MODEL TO USE:
Codex GPT-5.4

Read the target file and the named architecture doc.

Task:
Refactor [exact file path] so it conforms to [exact architecture doc].

Requirements:
- identify specific architecture drift
- preserve intended behavior
- minimize surface-area changes
- add or update tests for the corrected boundary

Return:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

## Patch B — Add Test Coverage
**Recommended model:** Codex GPT-5.4-mini

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

MODEL TO USE:
Codex GPT-5.4-mini

Read the target feature files and current tests.

Task:
Add missing tests for invariants, failure paths, determinism, and regression risks.

Requirements:
- no production code changes unless required for testability
- keep tests focused and readable

Return:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

## Patch C — Convert Direct Mutation to Command Flow
**Recommended model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

MODEL TO USE:
Codex GPT-5.4

Task:
Find any direct state mutation in [feature area] and convert it to command-driven orchestration flow.

Requirements:
- preserve UX behavior
- route through application layer and ResolutionOrchestrator
- add regression tests

Return:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

## Patch D — Replace Primitive Dimensions with Geometry Types
**Recommended model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- `docs/ai/context/code_phase_global_instructions.md`

This file defines mandatory architecture rules and implementation constraints.
You must follow it strictly before doing anything else.

MODEL TO USE:
Codex GPT-5.4

Task:
Refactor [feature area] to replace primitive numeric dimension usage with approved geometry value objects.

Requirements:
- identify all public boundary leaks
- migrate incrementally
- preserve behavior
- add tests for unit correctness and tolerance behavior

Return:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

# RECOMMENDED DAILY WORKFLOW

For each implementation step:

1. Run `C0` once near the start of the repo effort with **Claude Opus 4.6**
2. Use one of `C1–C16` with the designated model
3. If the task is broad or risky:
   - Opus defines the exact slice
   - Sonnet implements it
   - Codex handles targeted follow-up patches/tests
4. Save implementation notes under:
   - `docs/ai/implementation/`
5. Keep every state-changing feature aligned to:
   - commands
   - orchestrator
   - deterministic tests

---

# FINAL RECOMMENDATION

Use the **self-contained prompts** for:
- architecture-critical areas
- domain work
- orchestrator work
- persistence
- first-pass implementation of a slice

Use the **shared file approach** only after the repo already has good discipline and the models are reliably reading the common context file.

This gives you the least brittle workflow while still reducing repeated manual prep.
