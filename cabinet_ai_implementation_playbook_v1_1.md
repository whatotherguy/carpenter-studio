# Cabinet Design Software — AI Implementation Playbook (v1.1 REVISED)

This is the **corrected and production-ready version** of the implementation playbook.

It incorporates:
- Path correctness
- Context control
- Missing architectural components
- Stronger guardrails
- Better prompt sequencing
- Safer code generation workflow

---

# 🧠 MODEL STRATEGY

- **Opus 4.6** → deep architecture + system reasoning
- **Sonnet 4.6** → structured systems + integration
- **GPT-5.4-mini (Codex)** → code generation ONLY

---

# 📁 FILE PATH STANDARD (CRITICAL)

Always use FULL paths:

- docs/ai/context/cabinet_architecture_playbook_windows_dotnet_v2.md
- docs/ai/outputs/<file>.md

Never use shorthand file names.

---

# ⚠️ ARCHITECTURE GUARDRAILS (INCLUDE IN EVERY PROMPT)

You MUST follow:

- All design changes go through ResolutionOrchestrator
- No primitive dimensions (use geometry value objects)
- Separate the six realities:
  interaction / intent / engineering / manufacturing / install / commercial
- Deterministic outputs only
- Immutable approved snapshots
- Validation must include severity
- Full explanation traceability required
- Domain must be UI-independent

---

# ⚠️ CONTEXT RULE (CRITICAL)

DO NOT load all files.

Instead:
- Load only relevant files
- Include a short summary of prior work

---

# 🚀 PROMPT ORDER (FINAL)

## P0 — SETUP (Opus)

Goal:
Create a condensed summary of architecture playbook.

Output:
docs/ai/context/architecture_summary.md

---

## P1 — GEOMETRY SYSTEM (Opus)

Read:
- full architecture playbook

Write:
- docs/ai/outputs/geometry_system.md

---

## P2 — CORE COMMANDS (Opus)

# PROMPT: CORE COMMAND SYSTEM

MODEL: Claude Opus 4.6

---

## Read:

* docs/ai/context/architecture_summary.md
* docs/ai/outputs/geometry_system.md

---

## Goal

Design the **core command system** that drives all state changes in the cabinet design application.

This is the foundation for:

* ResolutionOrchestrator
* Undo / Redo
* Why Engine (explanation tracing)
* Editor interactions

---

## Architecture Guardrails (MANDATORY)

* All design changes must be expressed as commands
* Commands must be deterministic
* Commands must be immutable
* Commands must NOT perform resolution directly
* Commands must flow through ResolutionOrchestrator
* No primitive dimensions — use geometry types only
* Commands must support explanation tracing
* Commands must support undo/redo

---

## Design Requirements

Define:

### 1. Command Interfaces

* `IDesignCommand`
* `IEditorCommand`

Include:

* metadata
* intent description
* parameters
* traceability hooks

---

### 2. Command Lifecycle

Define the full lifecycle:

* Creation (user interaction)
* Validation (pre-check)
* Execution (via orchestrator)
* Result generation
* Explanation capture
* Undo / Redo integration

---

### 3. Command Result Model

Design a result object that includes:

* success / failure
* validation issues
* state changes (reference, not full mutation)
* explanation references

---

### 4. Command Metadata (CRITICAL)

Each command must carry:

* command type
* timestamp
* user/system origin
* affected entities
* reason / intent

This feeds the Why Engine.

---

### 5. Undo / Redo Strategy

Design:

* reversible commands OR
* delta-based reversal OR
* hybrid approach

Must be:

* deterministic
* auditable

---

### 6. Example Commands

Define a small set of concrete commands:

* `AddCabinetToRunCommand`
* `MoveCabinetCommand`
* `ResizeCabinetCommand`
* `CreateRunCommand`
* `InsertCabinetIntoRunCommand`

Each should:

* use geometry types
* reflect real user intent (not low-level mutations)

---

## Output Format

1. goals
2. design decisions
3. command architecture
4. interfaces (C#)
5. command lifecycle
6. result model
7. undo/redo strategy
8. example commands (C#)
9. risks / edge cases

---

## Write Output To

docs/ai/outputs/commands.md


---

## P3 — DOMAIN MODEL (Opus)

Read:
- geometry
- commands
- summary

Write:
docs/ai/outputs/domain_model.md

---

## P4 — RESOLUTION ORCHESTRATOR (Opus)

Read:
- domain
- commands
- summary

Write:
docs/ai/outputs/orchestrator.md

---

## P5 — WHY ENGINE (Opus)

NEW

Design explanation graph system.

Write:
docs/ai/outputs/why_engine.md

---

## P6 — APPLICATION LAYER (Sonnet)

Write:
docs/ai/outputs/application_layer.md

---

## P7 — EDITOR ENGINE (Sonnet)

Write:
docs/ai/outputs/editor_engine.md

---

## P8 — VALIDATION ENGINE (Opus)

Write:
docs/ai/outputs/validation_engine.md

---

## P9 — PERSISTENCE (Sonnet)

Write:
docs/ai/outputs/persistence_strategy.md

---

## P10 — MANUFACTURING (Opus)

Write:
docs/ai/outputs/manufacturing.md

---

## P11 — INSTALL PLANNING (Opus)

Write:
docs/ai/outputs/install_planning.md

---

## P12 — CROSS-CUTTING (Sonnet)

NEW

Design:
- logging
- autosave
- events
- DI

Write:
docs/ai/outputs/cross_cutting.md

---

## P13 — PRESENTATION (Sonnet)

NEW

Design:
- WPF MVVM
- binding
- UI interaction

Write:
docs/ai/outputs/presentation.md

---

## P14 — RENDERING (Sonnet)

NEW

Design:
- 2D canvas
- hit testing
- preview rendering

Write:
docs/ai/outputs/rendering.md

---

# 🐎 CODE GENERATION PHASE (FIXED)

## RULES

- NEVER one-shot generate large systems
- ALWAYS:
  1. Generate
  2. Review
  3. Refine

---

## CODE PROMPT TEMPLATE

Model: GPT-5.4-mini

```
Read:
- specific output file

Goal:
Implement EXACTLY as defined.

Requirements:
- follow domain model strictly
- no deviations
- include unit tests
- no UI coupling

Output:
- production-ready C#
- tests included
```

---

## 🔁 REQUIRED REVIEW LOOP

After code generation:

Use **Sonnet** to:
- verify alignment with domain model
- detect violations
- refine code

---

# 🧠 FINAL NOTE

This version fixes:
- context overflow risk
- missing architectural systems
- weak prompts
- unsafe code generation

This is now:
👉 safe to run
👉 scalable
👉 production-oriented

Follow the sequence exactly.
