# Cabinet Design Software — Code Phase Prompt Pack (v7, plan/build/review)

This version is optimized for your actual working model split:

- **Claude Opus 4.6** = architecture-critical review, boundary decisions, conformance
- **Claude Sonnet 4.6** = implementation planning
- **Codex GPT-5.4 / GPT-5.4-mini** = majority of actual coding

This pack replaces the earlier “one prompt = one model writes everything” approach with a **3-stage execution workflow**:

1. **PLAN** — Sonnet defines the slice, files, contracts, risks, and tests
2. **BUILD** — Codex writes the actual code within the approved boundaries
3. **REVIEW** — Opus reviews the output only where architecture risk justifies it

This is the recommended workflow for keeping usage practical while still protecting architecture quality.

---

# REQUIRED GLOBAL FILE

Every prompt in this pack assumes this file exists and must be read first:

`docs/ai/context/code_phase_global_instructions.md`

Every prompt in this pack begins with a mandatory read-first directive.

---

# EXECUTION STRATEGY

## Default workflow for most subsystems

### Stage 1 — PLAN
Run a **Sonnet 4.6** planning prompt.

Goal:
- define exact file targets
- define contracts/interfaces
- define invariants
- define edge cases
- define required tests
- identify architecture risks
- avoid writing full production code unless explicitly asked

### Stage 2 — BUILD
Run a **Codex GPT-5.4** build prompt.

Goal:
- implement the approved plan only
- stay inside listed files
- do not redesign architecture
- add tests
- keep changes narrow and deterministic

### Stage 3 — REVIEW
Run an **Opus 4.6** review prompt only when the subsystem is architecture-sensitive or the generated code looks suspicious.

Goal:
- verify architecture conformance
- detect boundary violations
- detect domain leakage
- detect orchestrator bypasses
- detect geometry primitive leakage
- identify required corrective patches

---

# WHEN TO USE EACH MODEL

## Claude Opus 4.6
Use only for:
- repo conformance audit
- domain model architecture review
- ResolutionOrchestrator review
- architecture drift correction
- final conformance pass
- review of risky persistence/editor/rendering boundaries when needed

## Claude Sonnet 4.6
Use for:
- implementation planning
- defining file targets
- defining handler flows
- outlining persistence mapping strategy
- outlining vertical slice execution details
- planning validation/why engine structures

## Codex GPT-5.4
Use for:
- most production code writing
- implementing geometry types
- command contracts
- application handlers
- persistence plumbing
- MVVM wiring
- rendering transforms
- projections
- editor implementation
- test writing
- constrained refactors

## Codex GPT-5.4-mini
Use for:
- adding tests
- mechanical refactors
- small bug fixes
- mapping code
- DI registration cleanup
- repetitive code generation
- follow-up patches from review output

---

# MANDATORY OUTPUT FORMAT

Every PLAN, BUILD, and REVIEW prompt must return output using these exact sections:

1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

For PLAN and REVIEW prompts:
- the `CODE` section may be partial or intentionally minimal
- but the section must still exist

---

# MASTER IMPLEMENTATION ORDER

1. geometry foundation
2. command contracts + base abstractions
3. domain model foundation
4. ResolutionOrchestrator skeleton
5. validation engine skeleton
6. why engine skeleton
7. application handlers
8. persistence + snapshot foundation
9. first editor vertical slice
10. snapping hardening
11. presentation MVVM wiring
12. rendering foundation
13. manufacturing projection
14. install planning projection
15. cross-cutting hardening
16. final architecture conformance pass

---

# HOW TO USE THIS PACK

For each subsystem:
- run the **PLAN** prompt first
- then run the matching **BUILD** prompt
- then optionally run the **REVIEW** prompt if the area is risky

For low-risk or highly mechanical work, you can skip PLAN and go straight to BUILD if:
- the spec doc is already clear
- the target files are already obvious
- the work is constrained

Do **not** skip PLAN for:
- domain model
- orchestrator
- persistence if schema/mapping is unclear
- first editor slice
- any area where command flow or architecture boundaries are easy to violate

---

# PROMPT SET

## R0 — Repository Conformance Audit (review only)

**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REVIEW

TASK TYPE:
Repository conformance audit

READ:
- solution file
- project files
- current folder structure
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/persistence_strategy.md

TASK:
Audit the current repository against the architecture docs and produce a concrete implementation map before major coding begins.

IN SCOPE:
- identify current modules/projects
- map current code to intended layers
- identify architecture drift
- identify missing foundations
- propose exact file/folder targets for upcoming work
- identify the safest implementation sequence based on current repo state

OUT OF SCOPE:
- large rewrites
- speculative redesign
- UI polish
- broad feature additions

CONSTRAINTS:
- prefer analysis over changes
- only add minimal scaffolding if strictly necessary

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- docs/ai/implementation/repo_conformance_audit.md
```

---

## G1 — Geometry Foundation

### G1-PLAN
**Model:** Claude Sonnet 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
Geometry foundation planning

READ:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/geometry_system.md
- existing domain/common utility files
- existing test project conventions

TASK:
Do not write the full implementation yet.
Produce an implementation plan for the geometry foundation used across the design system.

IN SCOPE:
- Length
- Offset
- Point2D
- Vector2D
- Rect2D
- tolerance and equality rules
- invariant boundaries
- operator support only where justified
- exact tests to add

REQUIRED OUTPUT:
- exact file list
- exact type list
- exact invariants per type
- tolerance policy
- public API shape
- risks Codex must avoid
- test plan

OUT OF SCOPE:
- rendering
- WPF converters
- persistence mapping
- cabinet-specific rules

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G1-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Geometry foundation implementation

READ:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/geometry_system.md
- approved G1-PLAN output
- existing domain/common utility files
- existing test project conventions

TASK:
Implement the geometry foundation exactly according to the architecture doc and approved plan.
Do not redesign the slice.
Stay within the listed files unless a narrowly justified extra file is required.

IN SCOPE:
- Length
- Offset
- Point2D
- Vector2D
- Rect2D
- deterministic equality/tolerance
- invariant enforcement
- tests

OUT OF SCOPE:
- rendering
- persistence
- editor interactions
- cabinet-specific rules

REQUIRED CONSTRAINTS:
- no primitive leakage in public APIs where a geometry type belongs
- no mutable structs
- deterministic behavior only
- test all invariants and failure paths

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G1-REVIEW
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REVIEW

TASK TYPE:
Geometry conformance review

READ:
- docs/ai/outputs/geometry_system.md
- implemented geometry files
- geometry tests

TASK:
Review the geometry implementation for architecture conformance and deterministic correctness.

FOCUS:
- primitive leakage
- tolerance correctness
- invariant enforcement
- public API quality
- determinism
- missing tests

Do not rewrite the subsystem unless necessary.
Prefer targeted corrective recommendations.

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G2 — Command Contracts + Base Infrastructure

### G2-PLAN
**Model:** Claude Sonnet 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
Command system planning

READ:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/geometry_system.md
- current application/domain abstractions
- existing undo/redo infrastructure if any

TASK:
Do not write full production code yet.
Produce the implementation plan for core command abstractions and metadata contracts.

IN SCOPE:
- IDesignCommand
- command metadata
- command result type
- base command abstractions
- undo/redo contract surface
- validation hook contracts
- deterministic identifiers if required
- exact test list

REQUIRED OUTPUT:
- exact file list
- exact interfaces/types to create
- serialization/replay expectations
- command/result contract shape
- risks Codex must avoid
- test plan

OUT OF SCOPE:
- full orchestrator execution
- persistence
- UI wiring

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G2-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Command system implementation

READ:
- docs/ai/context/architecture_summary.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/geometry_system.md
- approved G2-PLAN output
- current application/domain abstractions
- existing undo/redo infrastructure if any

TASK:
Implement the command contracts and base infrastructure exactly according to the approved plan.
Do not redesign architecture.

IN SCOPE:
- IDesignCommand
- command metadata
- command result type
- base command abstractions
- undo/redo contract surface
- command validation hook contracts
- tests

OUT OF SCOPE:
- full orchestrator implementation
- persistence
- UI code

REQUIRED CONSTRAINTS:
- commands represent intent only
- no direct state mutation
- no repository mutation from commands
- deterministic behavior only

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G3 — Domain Model Foundation

### G3-PLAN
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
Domain model foundation planning

READ:
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/geometry_system.md
- docs/ai/outputs/commands.md
- current domain entities/value objects

TASK:
Do not write full implementation yet.
Produce the implementation plan for the foundational domain entities, aggregates, and value objects.

REQUIRED OUTPUT:
- exact bounded context structure
- exact aggregate roots
- exact value objects
- invariant boundaries
- relationships
- update patterns consistent with command-driven mutation
- exact file list
- explicit Codex implementation notes
- test plan

OUT OF SCOPE:
- editor gestures
- persistence DTOs
- WPF view models
- manufacturing/install projections unless foundationally required

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G3-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Domain model foundation implementation

READ:
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/geometry_system.md
- docs/ai/outputs/commands.md
- approved G3-PLAN output
- current domain entities/value objects

TASK:
Implement the domain foundation exactly according to the architecture doc and approved plan.
Do not redesign the domain model.

REQUIRED CONSTRAINTS:
- keep domain UI-independent
- protect invariants in constructors/factories
- no public setters on aggregate state without strong justification
- prefer explicit composition
- no persistence leakage into domain

REQUIRED TESTS:
- aggregate invariant tests
- creation/update rule tests
- invalid relationship tests
- read-only/snapshot-related behavior tests where applicable

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G3-REVIEW
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REVIEW

TASK TYPE:
Domain model conformance review

READ:
- docs/ai/outputs/domain_model.md
- implemented domain files
- domain tests

TASK:
Review the domain model implementation for architecture conformance, invariants, dependency direction, and aggregate quality.

FOCUS:
- invariant protection
- aggregate boundaries
- UI leakage
- persistence leakage
- improper mutation surfaces
- missing tests

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G4 — ResolutionOrchestrator Skeleton

### G4-PLAN
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
ResolutionOrchestrator planning

READ:
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md
- current application pipeline code if any

TASK:
Do not write full implementation yet.
Produce the implementation plan for the ResolutionOrchestrator skeleton and staged execution pipeline.

REQUIRED OUTPUT:
- exact orchestrator contract
- stage list and execution ordering
- stage result propagation shape
- failure/short-circuit behavior
- extension point design
- exact file list
- Codex implementation notes
- exact test plan

OUT OF SCOPE:
- full business logic for every stage
- manufacturing/install outputs
- broad UI integration

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G4-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
ResolutionOrchestrator implementation

READ:
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md
- approved G4-PLAN output
- current application pipeline code if any

TASK:
Implement the ResolutionOrchestrator skeleton exactly according to the approved plan.
Do not redesign the pipeline.

REQUIRED CONSTRAINTS:
- all mutations must pass through ResolutionOrchestrator
- stages must be explicit and traceable
- failure must short-circuit deterministically
- no hidden control flow
- no business-rule dumping into glue code

REQUIRED TESTS:
- pipeline traversal tests
- stage failure tests
- deterministic ordering tests
- no-mutation-on-failure tests
- extension point invocation tests

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G4-REVIEW
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REVIEW

TASK TYPE:
ResolutionOrchestrator conformance review

READ:
- docs/ai/outputs/orchestrator.md
- implemented orchestrator files
- orchestrator tests

TASK:
Review the orchestrator implementation for architecture conformance, deterministic stage behavior, extension point quality, and hidden-control-flow risks.

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G5 — Validation Engine Skeleton

### G5-PLAN
**Model:** Claude Sonnet 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
Validation engine planning

READ:
- docs/ai/outputs/validation_engine.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md

TASK:
Do not write the full implementation yet.
Produce an implementation plan for the validation engine interfaces and initial rule wiring.

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G5-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Validation engine implementation

READ:
- docs/ai/outputs/validation_engine.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md
- approved G5-PLAN output

TASK:
Implement the validation engine according to the approved plan.

REQUIRED CONSTRAINTS:
- validation must not mutate state
- rules must be individually testable
- output must be deterministic and machine-readable

REQUIRED TESTS:
- rule execution
- severity aggregation
- multi-rule aggregation
- orchestrator integration seam tests

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G6 — Why Engine Skeleton

### G6-PLAN
**Model:** Claude Sonnet 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
Why engine planning

READ:
- docs/ai/outputs/why_engine.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/commands.md

TASK:
Do not write the full implementation yet.
Produce an implementation plan for the structured explanation and trace model.

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G6-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Why engine implementation

READ:
- docs/ai/outputs/why_engine.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/commands.md
- approved G6-PLAN output

TASK:
Implement the why engine according to the approved plan.

REQUIRED CONSTRAINTS:
- explanations must be structured
- links to commands/stages/rules must be preserved
- ordering must be deterministic and auditable

REQUIRED TESTS:
- object creation
- linkage
- aggregation
- deterministic ordering

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G7 — Application Command Handlers

### G7-PLAN
**Model:** Claude Sonnet 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
Application handler planning

READ:
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/domain_model.md
- current DI/application setup

TASK:
Do not write the full implementation yet.
Produce the implementation plan for application-layer command handlers and dispatch flow.

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G7-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Application handler implementation

READ:
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/domain_model.md
- approved G7-PLAN output
- current DI/application setup

TASK:
Implement the application-layer handler pattern and dispatch path exactly according to the approved plan.

REQUIRED CONSTRAINTS:
- handlers coordinate only
- no deep business logic in handlers
- no bypass around orchestrator
- explicit result mapping

REQUIRED TESTS:
- dispatch tests
- failure propagation tests
- dependency interaction tests
- no-bypass tests

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G8 — Persistence + Snapshot Foundation

### G8-PLAN
**Model:** Claude Sonnet 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
Persistence planning

READ:
- docs/ai/outputs/persistence_strategy.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md
- existing SQLite/repository code if any

TASK:
Do not write the full implementation yet.
Produce an implementation plan for the persistence layer and immutable snapshot boundary.

REQUIRED OUTPUT:
- exact repository contracts
- mapping strategy
- schema/migration targets
- transaction boundaries
- snapshot immutability enforcement
- integration test plan
- risks Codex must avoid

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G8-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Persistence implementation

READ:
- docs/ai/outputs/persistence_strategy.md
- docs/ai/outputs/domain_model.md
- docs/ai/outputs/orchestrator.md
- approved G8-PLAN output
- existing SQLite/repository code if any

TASK:
Implement the persistence layer and snapshot boundary according to the approved plan.
Do not redesign the persistence strategy.

REQUIRED CONSTRAINTS:
- persistence models remain separate from domain models
- approved snapshots are immutable once approved
- round-trip behavior must be deterministic
- transaction integrity must protect command commits

REQUIRED TESTS:
- SQLite integration tests
- mapping tests
- snapshot immutability tests
- round-trip tests
- transaction failure tests

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G8-REVIEW
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REVIEW

TASK TYPE:
Persistence conformance review

READ:
- docs/ai/outputs/persistence_strategy.md
- implemented persistence files
- persistence tests

TASK:
Review the persistence implementation for model separation, deterministic round trips, transaction boundaries, and snapshot immutability enforcement.

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G9 — First Editor Vertical Slice: Create + Move Cabinet

### G9-PLAN
**Model:** Claude Sonnet 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Sonnet 4.6

EXECUTION MODE:
PLAN

TASK TYPE:
First editor slice planning

READ:
- docs/ai/outputs/editor_engine.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/rendering.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md
- existing editor/viewmodel/render files

TASK:
Do not write full implementation yet.
Produce the implementation plan for the first end-to-end editor slice: create a cabinet and move it in 2D through the correct command/orchestrator/application flow.

REQUIRED OUTPUT:
- exact file list
- input flow from UI intent to application handler to command to orchestrator
- rendering refresh strategy
- selection state plan if needed
- exact tests
- architecture risks Codex must avoid

OUT OF SCOPE:
- advanced snapping
- resize gestures
- door/drawer editing
- manufacturing calculations

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G9-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
First editor slice implementation

READ:
- docs/ai/outputs/editor_engine.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/rendering.md
- docs/ai/outputs/application_layer.md
- docs/ai/outputs/commands.md
- docs/ai/outputs/domain_model.md
- approved G9-PLAN output
- existing editor/viewmodel/render files

TASK:
Implement the first editor slice exactly according to the approved plan.

REQUIRED CONSTRAINTS:
- UI gestures must produce application intent, not direct domain mutation
- movement must use geometry types
- state changes must remain undo/redo compatible
- keep the slice truly end-to-end but minimal

REQUIRED TESTS:
- create path tests
- move path tests
- invalid move tests
- orchestrator invocation tests
- minimal presentation/viewmodel contract tests if applicable

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

### G9-REVIEW
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REVIEW

TASK TYPE:
First editor slice conformance review

READ:
- docs/ai/outputs/editor_engine.md
- docs/ai/outputs/presentation.md
- docs/ai/outputs/rendering.md
- implemented editor/application/presentation files
- slice tests

TASK:
Review the create+move cabinet vertical slice for architecture conformance, command flow integrity, MVVM boundary correctness, and geometry safety.

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G10 — Snapping + Drag Hardening

### G10-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Snapping and drag hardening

READ:
- docs/ai/outputs/editor_engine.md
- docs/ai/outputs/geometry_system.md
- current editor implementation
- relevant tests

TASK:
Implement snapping services and drag hardening in the existing editor code.

REQUIRED CONSTRAINTS:
- snapping must be deterministic
- preview state must remain separate from committed state
- do not scatter snapping logic across UI and domain layers

REQUIRED TESTS:
- snap candidate selection
- tolerance thresholds
- ambiguous snap resolution
- preview/commit separation

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G11 — Presentation MVVM Wiring

### G11-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Presentation MVVM wiring

READ:
- docs/ai/outputs/presentation.md
- docs/ai/outputs/application_layer.md
- current WPF project structure
- current view/viewmodel files

TASK:
Implement or refactor presentation wiring so WPF views communicate only through MVVM and application-layer abstractions.

REQUIRED CONSTRAINTS:
- keep code-behind thin
- no domain mutation from view models
- no direct domain references in XAML/code-behind
- preserve deterministic state flow

REQUIRED TESTS:
- viewmodel behavior tests
- property change tests
- command invocation tests where practical

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G12 — 2D Rendering Foundation

### G12-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
2D rendering foundation

READ:
- docs/ai/outputs/rendering.md
- docs/ai/outputs/geometry_system.md
- current render/editor presentation files

TASK:
Implement the 2D rendering foundation needed to display cabinet geometry cleanly and deterministically.

REQUIRED CONSTRAINTS:
- keep rendering separate from domain logic
- geometry transforms must be testable
- avoid UI-thread-only math if it can be isolated

REQUIRED TESTS:
- transform tests
- render model generation tests
- bounds tests
- selection overlay tests if implemented

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G13 — Manufacturing Projection Foundation

### G13-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Manufacturing projection implementation

READ:
- docs/ai/outputs/manufacturing.md
- docs/ai/outputs/domain_model.md
- current projection/export files if any

TASK:
Implement the manufacturing projection layer that converts approved cabinet/domain state into parts and machining-ready structures.

REQUIRED CONSTRAINTS:
- projections must not mutate design state
- output must be reproducible
- dimensional accuracy must use geometry value objects and explicit conversions

REQUIRED TESTS:
- part projection tests
- dimension accuracy tests
- deterministic ordering tests
- invalid state protection tests

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G14 — Install Planning Projection Foundation

### G14-BUILD
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

EXECUTION MODE:
BUILD

TASK TYPE:
Install planning implementation

READ:
- docs/ai/outputs/install_planning.md
- docs/ai/outputs/domain_model.md
- current planning/export files if any

TASK:
Implement the install planning projection layer that derives install sequence and install steps from approved design state.

REQUIRED CONSTRAINTS:
- output must be derived, not manually mutated primary state
- sequence generation must be deterministic
- rationale should stay compatible with the why engine if possible

REQUIRED TESTS:
- sequence generation tests
- dependency ordering tests
- deterministic output tests

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G15 — Cross-Cutting Hardening

### G15-BUILD
**Model:** Codex GPT-5.4-mini

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4-mini

EXECUTION MODE:
BUILD

TASK TYPE:
Cross-cutting hardening

READ:
- docs/ai/outputs/cross_cutting.md
- current DI/logging/autosave/event infrastructure
- docs/ai/outputs/orchestrator.md
- docs/ai/outputs/persistence_strategy.md

TASK:
Implement cross-cutting infrastructure improvements without violating architecture boundaries.

REQUIRED CONSTRAINTS:
- cross-cutting concerns must not become hidden control flow
- autosave must respect approved snapshot strategy
- logs must support diagnosis without polluting domain types

REQUIRED TESTS:
- DI composition tests where feasible
- autosave trigger tests
- event emission tests if implemented

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

## G16 — Final Architecture Conformance Pass

### G16-REVIEW
**Model:** Claude Opus 4.6

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Claude Opus 4.6

EXECUTION MODE:
REVIEW

TASK TYPE:
Final architecture conformance review

READ:
- docs/ai/context/architecture_summary.md
- all relevant docs/ai/outputs/*.md for implemented subsystems
- current implementation files for completed slices
- current tests

TASK:
Review the current implementation against the architecture and identify remaining conformance gaps without proposing speculative redesign.

IN SCOPE:
- dependency direction problems
- orchestrator bypasses
- domain leakage
- persistence leakage
- geometry primitive leaks
- MVVM boundary violations
- missing invariant protection
- missing regression tests

OUT OF SCOPE:
- speculative optimization
- broad redesign unless required to fix conformance failure

RETURN OUTPUT USING THESE SECTIONS:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

WRITE TARGET:
- docs/ai/implementation/final_conformance_review.md
```

---

# CODEX PATCH PROMPTS

Use these after BUILD or REVIEW when the change is narrow and well-defined.

## Patch A — Fix Architecture Drift
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

TASK:
Refactor [exact file path] so it conforms to [exact architecture doc and/or review findings].

REQUIREMENTS:
- preserve intended behavior
- minimize surface-area changes
- add or update regression tests
- do not redesign adjacent systems

RETURN:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

## Patch B — Add Missing Tests
**Model:** Codex GPT-5.4-mini

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4-mini

TASK:
Add missing tests for invariants, failure paths, determinism, and regression risks in [exact feature area].

REQUIREMENTS:
- no production code changes unless required for testability
- keep tests focused and readable

RETURN:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

## Patch C — Convert Direct Mutation to Command Flow
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

TASK:
Find direct state mutation in [feature area] and convert it to command-driven orchestration flow.

REQUIREMENTS:
- preserve UX behavior
- route through application layer and ResolutionOrchestrator
- add regression tests

RETURN:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

## Patch D — Replace Primitive Dimensions with Geometry Types
**Model:** Codex GPT-5.4

```text
READ THIS FILE FIRST (REQUIRED):
- docs/ai/context/code_phase_global_instructions.md

MODEL TO USE:
Codex GPT-5.4

TASK:
Refactor [feature area] to replace primitive numeric dimension usage with approved geometry value objects.

REQUIREMENTS:
- identify public boundary leaks
- migrate incrementally
- preserve behavior
- add tests for unit correctness and tolerance behavior

RETURN:
1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES
```

---

# RECOMMENDED RUN ORDER FOR YOUR USAGE BUDGET

Minimum recommended sequence:

1. `R0` — Opus repo conformance audit
2. `G1-PLAN` — Sonnet
3. `G1-BUILD` — Codex
4. `G2-PLAN` — Sonnet
5. `G2-BUILD` — Codex
6. `G3-PLAN` — Opus
7. `G3-BUILD` — Codex
8. `G4-PLAN` — Opus
9. `G4-BUILD` — Codex
10. `G5-PLAN` — Sonnet
11. `G5-BUILD` — Codex
12. `G6-PLAN` — Sonnet
13. `G6-BUILD` — Codex
14. `G7-PLAN` — Sonnet
15. `G7-BUILD` — Codex
16. `G8-PLAN` — Sonnet
17. `G8-BUILD` — Codex
18. `G9-PLAN` — Sonnet
19. `G9-BUILD` — Codex
20. `G10-BUILD` onward mostly Codex
21. `G16-REVIEW` — Opus at the end

If usage is especially tight:
- skip REVIEW on low-risk subsystems
- use Opus only for `R0`, `G3-PLAN`, `G4-PLAN`, and `G16-REVIEW`
- use Sonnet only for planning
- use Codex for almost all actual code writing

---

# FINAL RULE

Do not let Codex redesign architecture.

Do not let Sonnet spend tokens writing full code when planning is enough.

Do not use Opus for routine implementation when a reviewed plan plus Codex can do the job safely.
