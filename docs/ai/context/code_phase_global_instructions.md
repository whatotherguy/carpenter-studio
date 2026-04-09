# Code Phase — Global Instructions

You are implementing a Windows desktop cabinet design system.

---

# STACK

- C#
- .NET 8+
- WPF
- MVVM
- SQLite
- Modular monolith architecture
- xUnit preferred unless the repository already standardizes on NUnit

---

# CORE PRODUCT PROMISE

- Fast to draw
- Hard to mess up
- Safe to build

---

# NON-NEGOTIABLE ARCHITECTURAL GUARDRAILS

These rules override all other considerations:

- All design mutations must go through commands
- All command execution must flow through `ResolutionOrchestrator`
- No UI-driven domain mutation
- No primitive dimension math in domain logic where geometry value objects should exist
- Domain layer must remain UI-independent
- Persistence models must not leak into domain entities
- Approved snapshots are immutable
- All behavior must be deterministic
- No hidden side effects
- No architecture drift
- No speculative abstractions unless directly required
- No fake placeholders unless explicitly marked `NOT IMPLEMENTED YET`
- All state-changing flows must remain undo/redo compatible

---

# SYSTEM ARCHITECTURE SHAPE (REFERENCE MODEL)

The system is organized into these layers:

## Domain
- Aggregates
- Value objects
- Invariants
- Business rules
- No framework dependencies

## Application
- Command handlers
- Orchestration entry points
- Coordinates domain + orchestrator
- No UI logic

## Orchestration
- `ResolutionOrchestrator`
- Deterministic execution pipeline
- Validation + rule enforcement + state transitions

## Validation Engine
- Rule-based validation
- Deterministic outputs
- No mutation of state

## Why Engine
- Structured explanation system
- Traceable decisions
- No ad hoc string-only reasoning

## Persistence
- SQLite-backed
- Separate persistence models
- Immutable approved snapshots
- Deterministic load/save behavior

## Presentation (WPF)
- MVVM only
- ViewModels talk to Application layer only
- No domain mutation from UI

## Rendering
- Pure projection of domain state
- No business logic

## Projections
- Manufacturing output
- Install planning output
- Derived only (no mutation)

---

# IMPLEMENTATION RULES

## Before Writing Code

1. Read only the minimum required files
2. Identify existing repository patterns and conventions
3. Reuse existing namespaces and structures when correct
4. Do not rewrite unrelated files
5. Do not rename files unless necessary
6. Do not introduce new frameworks without explicit instruction
7. If architecture docs conflict with implementation, prefer architecture and refactor locally

---

## While Writing Code

1. Implement vertically coherent slices
2. Keep constructors explicit
3. Prefer immutable value objects
4. Prefer explicit result types over booleans
5. Avoid service locators
6. Avoid “manager” classes without clear boundaries
7. Do not place business logic in:
   - code-behind
   - view models
8. Keep WPF concerns in presentation layer only
9. Keep geometry math centralized
10. Ensure all state changes are undo/redo compatible
11. Ensure all behavior is deterministic

---

## Domain Rules

- No public setters on aggregates unless strictly justified
- Protect invariants at construction boundaries
- Prefer composition over inheritance
- No persistence leakage into domain
- No UI references

---

## Command Rules

- Commands represent intent, not execution
- Commands must be explicit and structured
- Commands must not mutate state directly
- Commands must be replay-safe if required
- No “god command” abstractions

---

## Orchestrator Rules

- All mutations flow through `ResolutionOrchestrator`
- Execution must be stage-based and traceable
- Failures must short-circuit safely
- No hidden control flow
- Must support deterministic execution

---

## Validation Rules

- Validation must not mutate state
- Rules must be individually testable
- Output must be structured and machine-readable
- Severity must be explicit

---

## Why Engine Rules

- Explanations must be structured, not just strings
- Must link to:
  - commands
  - rules
  - stages
- Must be deterministic and auditable

---

## Persistence Rules

- Domain and persistence models must be separate
- Approved snapshots are immutable
- All persistence operations must be deterministic
- Transactions must protect command commits

---

## Rendering Rules

- Rendering must not contain domain logic
- Geometry transformations must be testable
- Keep render pipeline isolated

---

## Projection Rules

- Manufacturing/install outputs are derived only
- Must not mutate design state
- Must be deterministic and reproducible

---

# TESTING RULES

## Always Test:

- Invariants
- Failure cases
- Determinism
- Command orchestration paths
- Snapshot immutability (where applicable)

## Prefer:

- Focused tests over broad tests
- Explicit assertions over implicit expectations

---

# OUTPUT FORMAT (MANDATORY)

Every response must follow this exact structure:

1. IMPLEMENTATION PLAN
2. FILES TO CREATE
3. FILES TO MODIFY
4. CODE
5. TESTS
6. RATIONALE
7. FOLLOW-UP NOTES

Do not omit sections.

---

# EXECUTION MODES

Each task will declare one of:

- FOUNDATION — build core systems
- VERTICAL SLICE — implement one feature end-to-end
- HARDENING — add validation, invariants, tests
- REFACTOR-CONFORMANCE — align code to architecture without changing behavior

---

# FINAL RULE

You are not writing demo code.

You are building a production-grade system where:

- incorrect measurements can cost real money
- behavior must be explainable
- outputs must be trustworthy

When in doubt:
- choose correctness over speed
- choose clarity over cleverness
- choose explicit over implicit