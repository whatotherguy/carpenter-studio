# Cabinet Design Software — AI Prompt Pack (v4.1 COMPLETE FULL CONTRACTS)

This is the COMPLETE final prompt system.

Every prompt is:
- Fully executable
- Fully constrained
- Zero ambiguity
- Copy/paste ready

Use EXACTLY as written.

---

# GLOBAL SYSTEM CONTEXT (COPY INTO EVERY PROMPT)

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

# GLOBAL ARCHITECTURE GUARDRAILS (COPY INTO EVERY PROMPT)

- All design changes go through ResolutionOrchestrator
- Commands are the ONLY way to change state
- No primitive dimensions — geometry value objects only
- Deterministic outputs only
- Immutable approved snapshots
- Full explanation traceability (Why Engine)
- Domain must be UI-independent
- Runs are first-class

---

# CONTEXT RULE

- Only load required files
- Never load entire history
- Always include architecture_summary.md

---

#######################################################################
########################### PROMPT SET #################################
#######################################################################

=======================================================================
P0 — ARCHITECTURE SUMMARY
MODEL: Opus 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/context/cabinet_architecture_playbook_windows_dotnet_v2.md

GOAL:
Create a compressed working architecture summary used in ALL future prompts.

RESPONSIBILITIES:
- Extract only critical system truths
- Reduce size while preserving constraints
- Ensure reuse efficiency

OUTPUT FORMAT:
1. mission
2. stack
3. guardrails
4. six realities
5. resolution pipeline
6. core truths

WRITE:
docs/ai/context/architecture_summary.md

=======================================================================
P1 — GEOMETRY SYSTEM
MODEL: Opus 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/context/architecture_summary.md

GOAL:
Define the deterministic geometry foundation.

RESPONSIBILITIES:
- Define all dimensional primitives
- Enforce non-primitive usage
- Provide safe math

NON-RESPONSIBILITIES:
- UI formatting
- persistence
- domain logic

REQUIRED STRUCTURES:
- Length
- Offset
- Point2D
- Vector2D
- Rect2D
- tolerance system
- equality rules

OUTPUT FORMAT:
1. goals
2. design decisions
3. C# types
4. invariants
5. tolerance rules
6. edge cases

WRITE:
docs/ai/outputs/geometry_system.md

=======================================================================
P2 — COMMAND SYSTEM
MODEL: Opus 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/context/architecture_summary.md
docs/ai/outputs/geometry_system.md

GOAL:
Define the command system for ALL state changes.

RESPONSIBILITIES:
- represent intent
- define lifecycle
- enable undo/redo
- enable traceability

NON-RESPONSIBILITIES:
- resolution logic
- domain mutation

REQUIRED:
- IDesignCommand
- IEditorCommand
- command lifecycle
- metadata
- undo/redo strategy
- concrete examples

OUTPUT FORMAT:
1. goals
2. design
3. interfaces (C#)
4. lifecycle
5. undo strategy
6. examples

WRITE:
docs/ai/outputs/commands.md

=======================================================================
P3 — DOMAIN MODEL
MODEL: Opus 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/context/architecture_summary.md
docs/ai/outputs/geometry_system.md
docs/ai/outputs/commands.md

GOAL:
Define full domain model.

RESPONSIBILITIES:
- entities
- aggregates
- invariants
- relationships

NON-RESPONSIBILITIES:
- UI
- persistence

OUTPUT FORMAT:
1. goals
2. bounded contexts
3. entities (C#)
4. relationships
5. invariants

WRITE:
docs/ai/outputs/domain_model.md

=======================================================================
P4 — RESOLUTION ORCHESTRATOR
MODEL: Opus 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/outputs/commands.md
docs/ai/outputs/domain_model.md

GOAL:
Define ResolutionOrchestrator.

RESPONSIBILITIES:
- execute commands
- enforce pipeline
- manage state transitions

REQUIRED:
- 11 stages
- execution contract
- preview vs commit

OUTPUT FORMAT:
1. goals
2. architecture
3. pipeline
4. C# design
5. execution flow

WRITE:
docs/ai/outputs/orchestrator.md

=======================================================================
P5 — WHY ENGINE
MODEL: Opus 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/outputs/commands.md
docs/ai/outputs/orchestrator.md

GOAL:
Trace every system decision.

RESPONSIBILITIES:
- explanation graph
- trace linking
- auditability

OUTPUT FORMAT:
1. goals
2. architecture
3. data model
4. query patterns

WRITE:
docs/ai/outputs/why_engine.md

=======================================================================
P6 — APPLICATION LAYER
MODEL: Sonnet 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/context/architecture_summary.md
docs/ai/outputs/commands.md
docs/ai/outputs/domain_model.md
docs/ai/outputs/orchestrator.md
docs/ai/outputs/why_engine.md

GOAL:
Define application layer.

RESPONSIBILITIES:
- services
- handlers
- orchestration bridge

NON-RESPONSIBILITIES:
- domain logic
- UI
- persistence

REQUIRED:
- services
- handlers
- DTOs
- event flow
- preview vs commit

OUTPUT FORMAT:
1. goals
2. architecture
3. services
4. handlers
5. DTOs
6. integration

WRITE:
docs/ai/outputs/application_layer.md

=======================================================================
P7 — EDITOR ENGINE
MODEL: Sonnet 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/outputs/commands.md
docs/ai/outputs/geometry_system.md
docs/ai/outputs/application_layer.md

GOAL:
Define snapping + editor system.

REQUIRED:
- lightweight model
- snap candidates
- scoring

WRITE:
docs/ai/outputs/editor_engine.md

=======================================================================
P8 — VALIDATION ENGINE
MODEL: Opus 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/outputs/domain_model.md
docs/ai/outputs/orchestrator.md

GOAL:
Define validation system.

REQUIRED:
- severity levels
- rules
- remediation

WRITE:
docs/ai/outputs/validation_engine.md

=======================================================================
P9 — PERSISTENCE
MODEL: Sonnet 4.6
=======================================================================

SYSTEM CONTEXT:
(copy global context)

READ:
docs/ai/outputs/domain_model.md

GOAL:
Define persistence.

REQUIRED:
- SQLite schema
- snapshot strategy

WRITE:
docs/ai/outputs/persistence_strategy.md

=======================================================================
P10 — MANUFACTURING
MODEL: Opus 4.6
=======================================================================

READ:
docs/ai/outputs/domain_model.md

WRITE:
docs/ai/outputs/manufacturing.md

=======================================================================
P11 — INSTALL
MODEL: Opus 4.6
=======================================================================

READ:
docs/ai/outputs/domain_model.md

WRITE:
docs/ai/outputs/install_planning.md

=======================================================================
P12 — CROSS CUTTING
MODEL: Sonnet 4.6
=======================================================================

WRITE:
docs/ai/outputs/cross_cutting.md

=======================================================================
P13 — PRESENTATION
MODEL: Sonnet 4.6
=======================================================================

WRITE:
docs/ai/outputs/presentation.md

=======================================================================
P14 — RENDERING
MODEL: Sonnet 4.6
=======================================================================

WRITE:
docs/ai/outputs/rendering.md

#######################################################################
######################## CODE PHASE ###################################
#######################################################################

MODEL: GPT-5.4-mini

RULES:
- Implement ONE subsystem at a time
- Follow architecture EXACTLY
- Include tests

#######################################################################
FINAL RULE:
If a prompt is unclear, it is WRONG.
#######################################################################
