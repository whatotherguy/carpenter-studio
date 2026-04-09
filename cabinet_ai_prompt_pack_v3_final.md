# Cabinet Design Software — AI Prompt Pack (v3 FINAL FORM)

This is the **final, production-grade, deterministic AI prompt system**.

Every prompt:
- Fully re-anchors context
- Enforces architecture
- Eliminates ambiguity
- Minimizes drift
- Produces consistent outputs across long-running workflows

---

# GLOBAL SYSTEM CONTEXT (INCLUDE IN EVERY PROMPT)

You are building a Windows desktop cabinet design system.

Stack:
- C#
- .NET 8+
- WPF (MVVM)
- SQLite
- Modular monolith

Core promise:
Fast to draw. Hard to mess up. Safe to build.

---

# GLOBAL ARCHITECTURE GUARDRAILS (MANDATORY)

- All design changes go through ResolutionOrchestrator
- Commands are the ONLY way to change state
- No primitive dimensions — geometry value objects only
- Separate six realities strictly
- Deterministic outputs only
- Immutable approved snapshots
- Full explanation traceability (Why Engine)
- Domain must be UI-independent
- Runs are first-class

---

# CONTEXT LOADING RULE

- ONLY load required files
- NEVER load entire history
- Use architecture_summary.md for compression

---

# PROMPT STRUCTURE (ENFORCED)

Each prompt must include:
1. System Context
2. Read Files
3. Guardrails
4. Responsibilities
5. Non-Responsibilities
6. Required Structures
7. Output Format
8. Write Target

---

# 🚀 PROMPTS

---

## P0 — ARCHITECTURE SUMMARY (Opus)

READ:
docs/ai/context/cabinet_architecture_playbook_windows_dotnet_v2.md

GOAL:
Produce a compressed architecture summary.

OUTPUT FORMAT:
- mission
- guardrails
- pipeline
- key systems

WRITE:
docs/ai/context/architecture_summary.md

---

## P1 — GEOMETRY SYSTEM (Opus)

READ:
docs/ai/context/architecture_summary.md

RESPONSIBILITIES:
Define geometry primitives.

NON-RESPONSIBILITIES:
- UI
- persistence

REQUIRED:
- Length, Offset, Point2D, Vector2D, Rect2D
- tolerance system
- equality rules

OUTPUT FORMAT:
- goals
- design
- C# types
- invariants

WRITE:
docs/ai/outputs/geometry_system.md

---

## P2 — COMMAND SYSTEM (Opus)

READ:
docs/ai/context/architecture_summary.md
docs/ai/outputs/geometry_system.md

RESPONSIBILITIES:
Define ALL state changes.

REQUIRED:
- IDesignCommand
- lifecycle
- undo/redo
- metadata
- examples

WRITE:
docs/ai/outputs/commands.md

---

## P3 — DOMAIN MODEL (Opus)

READ:
docs/ai/context/architecture_summary.md
docs/ai/outputs/geometry_system.md
docs/ai/outputs/commands.md

RESPONSIBILITIES:
Define domain entities.

REQUIRED:
- bounded contexts
- invariants
- relationships

WRITE:
docs/ai/outputs/domain_model.md

---

## P4 — ORCHESTRATOR (Opus)

READ:
docs/ai/outputs/commands.md
docs/ai/outputs/domain_model.md

RESPONSIBILITIES:
Define pipeline execution.

REQUIRED:
- 11 stages
- command execution
- explanation integration

WRITE:
docs/ai/outputs/orchestrator.md

---

## P5 — WHY ENGINE (Opus)

READ:
docs/ai/outputs/commands.md
docs/ai/outputs/orchestrator.md

RESPONSIBILITIES:
Trace all decisions.

WRITE:
docs/ai/outputs/why_engine.md

---

## P6 — APPLICATION LAYER (Sonnet)

READ:
docs/ai/outputs/orchestrator.md
docs/ai/outputs/domain_model.md

RESPONSIBILITIES:
Define services + handlers.

WRITE:
docs/ai/outputs/application_layer.md

---

## P7 — EDITOR ENGINE (Sonnet)

READ:
docs/ai/outputs/commands.md
docs/ai/outputs/geometry_system.md
docs/ai/outputs/application_layer.md

RESPONSIBILITIES:
Drag/drop + snapping.

WRITE:
docs/ai/outputs/editor_engine.md

---

## P8 — VALIDATION ENGINE (Opus)

READ:
docs/ai/outputs/domain_model.md
docs/ai/outputs/orchestrator.md

RESPONSIBILITIES:
Validation rules.

WRITE:
docs/ai/outputs/validation_engine.md

---

## P9 — PERSISTENCE (Sonnet)

READ:
docs/ai/outputs/domain_model.md

RESPONSIBILITIES:
SQLite + snapshots.

WRITE:
docs/ai/outputs/persistence_strategy.md

---

## P10 — MANUFACTURING (Opus)

READ:
docs/ai/outputs/domain_model.md

RESPONSIBILITIES:
Parts + machining.

WRITE:
docs/ai/outputs/manufacturing.md

---

## P11 — INSTALL PLANNING (Opus)

READ:
docs/ai/outputs/domain_model.md

RESPONSIBILITIES:
Install sequencing.

WRITE:
docs/ai/outputs/install_planning.md

---

## P12 — CROSS-CUTTING (Sonnet)

RESPONSIBILITIES:
- logging
- autosave
- DI
- events

WRITE:
docs/ai/outputs/cross_cutting.md

---

## P13 — PRESENTATION (Sonnet)

RESPONSIBILITIES:
WPF MVVM.

WRITE:
docs/ai/outputs/presentation.md

---

## P14 — RENDERING (Sonnet)

RESPONSIBILITIES:
2D rendering.

WRITE:
docs/ai/outputs/rendering.md

---

# 🐎 CODE PHASE (Codex)

READ:
specific output file

GOAL:
Implement EXACTLY.

REQUIRE:
- strict adherence
- unit tests
- no architecture drift

---

# 🔁 REVIEW LOOP

After each implementation:
Use Sonnet to validate + refine.

---

# FINAL NOTE

This is a deterministic AI execution system.
Follow it exactly for best results.
