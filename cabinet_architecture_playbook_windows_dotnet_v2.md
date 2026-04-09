# Cabinet Design Software — Windows-First Architecture Prompt Playbook (v2)

Use this playbook to drive AI-assisted architecture, backend implementation, domain modeling, validation logic, editor development, and long-term system consistency for the cabinet design system.

This version is aligned to the chosen stack and tightened with additional architectural safeguards:

- **Primary language:** C#
- **Runtime:** .NET 8+
- **Desktop platform:** Windows
- **Desktop UI:** WPF
- **Local persistence:** SQLite
- **Primary architectural style:** modular monolith with strongly typed domain/application layers
- **Optional future optimization path:** Rust or C++ only for narrowly scoped performance-critical engines if proven necessary

---
## 1. Mission

Build a **Windows desktop cabinet design and production system** that combines **assisted layout authoring** with **shop-grade precision**, so carpenters can design faster, make fewer mistakes, and move confidently from concept to cut list to install.

Core promise:

**Fast to draw. Hard to mess up. Safe to build.**

The product is not primarily competing on pretty rendering or broad catalogs.  
It is competing on:

- trust
- manufacturable accuracy
- install-aware correctness
- fast real-world workflow
- revision safety
- explainability
- low-friction Windows desktop usability

---
## 2. Chosen Tech Stack

### 2.1 Primary stack
- **C#** for core language
- **.NET 8+** for runtime and application platform
- **WPF** for native Windows desktop UI
- **SQLite** for local project storage and snapshots
- **System.Text.Json** for structured serialization where appropriate
- **MVVM** for desktop UI composition
- **Dependency Injection** via .NET built-in container unless project needs grow enough to justify more

### 2.2 Why this stack
This application is primarily:
- a rules-heavy domain application
- a precision design tool
- a desktop interaction tool
- a file/export workflow tool
- a validation and explanation engine
- a Windows-native productivity application

That strongly favors:
- strongly typed domain modeling
- rich desktop UI controls and panels
- good file system access
- low-latency local interactions
- deterministic local execution
- excellent debugging and profiling support in Visual Studio

### 2.3 What not to optimize for initially
Do not design the core around:
- web-first assumptions
- Electron
- browser rendering constraints
- cloud dependency for core design workflows
- premature microservices
- cross-platform UI compromises

### 2.4 Future flexibility
The architecture should still leave room for:
- future cloud sync
- future multi-user workflow services
- future hardware catalog sync
- future cross-platform considerations if ever needed
- optional native performance modules for constrained hotspots

But the initial architecture is **Windows desktop first**.

---
## 3. Product Principles

### 3.1 Precision without friction
The product must be both:
- highly precise for real shop output
- fast and easy for carpenters to use

A correct system that is frustrating will fail.  
An easy system that is wrong will become a liability.

### 3.2 Design intent is not manufacturing output
The system must separate:
- what the user means
- how the cabinet is engineered
- how it is manufactured
- how it is installed
- how it is costed and approved

### 3.3 Runs are first-class
Cabinets are not isolated objects. Important logic must be solved at the run level:
- reveal continuity
- shared stiles
- fillers and scribes
- alignment
- end conditions
- collision relationships
- install dependencies

### 3.4 Assisted authoring is core architecture
Drag/drop, snapping, placement previews, run-aware editing, and guided correction are core architecture concerns, not UI polish.

### 3.5 Real-world constraints drive the truth
The model must account for:
- actual vs nominal thickness
- hardware SKUs
- boring patterns
- clearances
- grain direction
- veneer matching
- machining workflows
- install and fastening realities
- transport/access limitations
- tolerances and wall imperfections

### 3.6 Overrides are allowed, but unsafe states must remain visible
Users need freedom, but the system must still classify outcomes as:
- valid
- warning
- error
- manufacture_blocker

### 3.7 Traceability is a differentiator
The user must be able to understand:
- why a part exists
- why a filler was added
- why a cabinet snapped where it did
- why cost changed
- why a warning fired
- what changed between revisions

### 3.8 Version safety protects the shop
The architecture must prevent outdated designs from silently becoming shop output.

---
## 4. System Realities

The system explicitly models six realities.

### 4.1 Interaction reality
What the user is trying to do right now.

Examples:
- drag a cabinet near a wall
- append to a run
- center a sink base under a window
- replace one cabinet with another
- resize a segment

### 4.2 Design intent reality
What the user means to create.

Examples:
- 36 inch base cabinet
- align uppers across run
- use shaker style
- center sink on window
- apply walnut veneer

### 4.3 Engineering reality
How the design resolves into constructible cabinet logic.

Examples:
- opening breakdown
- frame vs frameless logic
- panel layout
- fillers and scribes
- corner strategy
- reveal math

### 4.4 Manufacturing reality
How the design becomes shop output.

Examples:
- parts
- labels
- grain direction
- cut lists
- boring patterns
- machining operations
- nesting / linear cut plans

### 4.5 Install reality
How the product is physically installed in the field.

Examples:
- install order
- dependency graph
- fastening zones
- stud/blocking checks
- shim allowances
- access and fit checks

### 4.6 Commercial / project reality
How the job is costed, versioned, approved, and handed off.

Examples:
- estimate snapshots
- taxes
- discounts
- delivery/install costs
- approval states
- manufacturing release state

---
## 5. Architecture Rules (Non-Negotiable)

1. Separate interaction, design intent, engineering, manufacturing, install, and commercial layers.
2. Treat drag/drop snapping and guided layout as first-class architecture concerns.
3. Capture authoring as intent-driven commands, not raw geometry mutation.
4. Solve important continuity logic at the run level.
5. Treat hardware SKUs, actual thickness, and grain direction as first-class constraints.
6. Separate parts from machining operations.
7. Build validation severity and manufacture blockers into the core.
8. Record explanation lineage during rule resolution.
9. Use immutable approved snapshots for production safety.
10. Optimize for time-to-correct-layout, not just theoretical correctness.
11. Bias the UX toward fast valid layouts without trapping expert users.
12. Preserve manual precision and overrides, but never silently bless invalid results.
13. Keep the initial product as a **modular monolith**, not a microservice system.
14. Keep the domain engine UI-independent and testable outside WPF.
15. Use desktop-native interaction patterns appropriate for a Windows productivity app.
16. Route every design-changing action through a single orchestration choke point in the Application layer.
17. Do not pass raw primitive dimensions freely across domain boundaries; use explicit geometry/value objects.
18. Approved historical revisions must remain durable even as the live working schema evolves.

---
## 6. Windows-First Architectural Shape

### 6.1 Overall style
Build the app as a **Windows desktop modular monolith** with clear internal boundaries.

Recommended high-level layers:
- Presentation layer (WPF)
- Application layer
- Domain layer
- Infrastructure layer
- Persistence layer
- Export/integration layer

### 6.2 Why modular monolith
This product needs:
- deterministic local behavior
- simple deployment
- rich in-process recalculation
- high trust in local data
- low operational complexity

A modular monolith is the right default because:
- most workflows are tightly coupled to the same domain model
- splitting into services early would create friction without clear benefit
- local-first execution matters
- desktop packaging is simpler
- revision snapshots and recalculation pipelines are easier to reason about

### 6.3 WPF role
WPF should own:
- windows, panels, inspectors, and toolbars
- canvas/editor view
- binding and commands
- selection and interaction visuals
- property editing UX
- preview overlays and guides

WPF should **not** contain core domain rules.

### 6.4 Domain engine role
The domain/application core should own:
- commands
- resolution pipeline
- rule execution
- validation
- snapshot creation
- explanation lineage
- costing
- manufacturing/install planning

This core should be executable and testable without the UI shell.

---
## 7. Recommended Solution / Project Structure

Use a Visual Studio solution organized roughly like this:

- `CabinetDesigner.App`
- `CabinetDesigner.Presentation`
- `CabinetDesigner.Application`
- `CabinetDesigner.Domain`
- `CabinetDesigner.Infrastructure`
- `CabinetDesigner.Persistence`
- `CabinetDesigner.Editor`
- `CabinetDesigner.Rendering`
- `CabinetDesigner.Integrations`
- `CabinetDesigner.Exports`
- `CabinetDesigner.Tests`

### 7.1 CabinetDesigner.App
Role:
- WPF startup project
- app bootstrap
- shell window composition
- dependency injection wiring
- desktop startup/configuration

### 7.2 CabinetDesigner.Presentation
Role:
- MVVM view models
- WPF views
- commands and UI adapters
- property panels
- workspace state
- document window behavior

### 7.3 CabinetDesigner.Application
Role:
- application services
- use cases
- command handlers
- orchestration of resolution pipeline
- transaction boundaries
- DTOs for presentation
- snapshot orchestration

Contains the primary choke point:
- `ResolutionOrchestrator`

Suggested shape:
- `IDesignCommand`
- `IEditorCommand`
- `ResolutionOrchestrator`
- `ResolutionContext`
- `ResolutionResult`

Every design-changing action must flow through this layer so the system always:
- enforces pipeline order
- records explanation lineage
- fires validation
- updates state consistently
- integrates undo/redo uniformly
- decides snapshot behavior consistently

### 7.4 CabinetDesigner.Domain
Role:
- core domain entities
- value objects
- domain rules
- invariants
- interfaces for ports
- explanation lineage models
- validation primitives

Include a required geometry foundation:
- `CabinetDesigner.Domain.Geometry`

Suggested geometry/value objects:
- `Length`
- `Angle`
- `Point2D`
- `Vector2D`
- `Rect2D`
- `LineSegment2D`
- `Thickness`

Formatting and conversion concerns should be handled via dedicated services or helpers, not by leaking UI formatting into raw geometry types.

### 7.5 CabinetDesigner.Infrastructure
Role:
- file system access
- logging
- external process access if needed
- background task helpers
- settings
- caching
- clock/environment abstractions

### 7.6 CabinetDesigner.Persistence
Role:
- SQLite access
- repositories
- snapshots persistence
- migrations
- serialization of project state

### 7.7 CabinetDesigner.Editor
Role:
- interaction engine
- snap engine
- placement candidate ranking
- editor commands
- selection state
- drag/drop preview models

### 7.8 CabinetDesigner.Rendering
Role:
- canvas rendering helpers
- geometry-to-screen mapping
- editor adorners
- guides/preview visuals
- hit testing utilities
- seam for possible future orthographic 3D preview

### 7.9 CabinetDesigner.Integrations
Role:
- hardware catalog import adapters
- future measurement device adapters
- future sync connectors
- import pipelines for vendor data

### 7.10 CabinetDesigner.Exports
Role:
- cut list exports
- shop drawing data export
- machine export packaging
- estimate/proposal outputs
- reports

### 7.11 CabinetDesigner.Tests
Role:
- unit tests
- pipeline tests
- snapshot tests
- validation tests
- geometry/rules tests
- property-based tests
- editor interaction tests where feasible

---
## 8. Bounded Contexts / Subsystems

### 8.1 Project & Revision System
Owns:
- project metadata
- revision history
- approvals
- immutable snapshots
- workflow state

Core entities:
- Project
- ProjectRevision
- ApprovalRecord
- ProjectSnapshot
- ProjectState

### 8.2 Spatial Scene Engine
Owns:
- room geometry
- walls, floors, ceilings
- openings
- obstacles
- appliances
- visual countertops/floors
- placement coordinates
- anchors and guides

Core entities:
- Room
- Wall
- Opening
- Obstacle
- FloorSurface
- CountertopVisual
- PlacedAssembly
- ReferencePlane
- MeasurementAnchor

### 8.3 Authoring / Interaction Engine
Owns:
- drag and drop behavior
- selection/edit handles
- interaction state
- placement intent
- suggested actions
- fast editing operations

Core entities:
- PlacementIntent
- InteractionState
- PlacementPreview
- EditHandle
- SuggestedAction
- AuthoringConstraint

### 8.4 Snap & Placement Engine
Owns:
- snap anchors
- placement candidates
- ranked snapping
- alignment guides
- drop validity
- quick placement suggestions

Core entities:
- SnapAnchor
- SnapRule
- PlacementCandidate
- AlignmentGuide
- DropValidity
- CandidateScore

Separate lightweight preview structures from fully resolved project structures when appropriate.

### 8.5 Run Engine
Owns:
- grouped cabinet behavior
- ordered runs
- reveal continuity
- shared stile logic
- fillers/scribes
- end conditions
- run dependencies
- run edit operations

Core entities:
- CabinetRun
- RunSegment
- RunAlignmentRule
- RunRevealRule
- RunEndCondition
- RunDependency

### 8.6 Cabinet / Assembly Resolver
Owns:
- cabinet types
- dimensions
- openings
- drawers/doors/interiors
- construction method
- local overrides

Core entities:
- CabinetAssembly
- CabinetType
- Opening
- DoorSet
- DrawerBank
- InteriorOption
- AssemblyOverride
- ConstructionMethod

### 8.7 Material Catalog + Grain Engine
Owns:
- nominal vs actual thickness
- material profiles
- sheet/stock definitions
- grain rules
- veneer matching
- pricing hooks

Core entities:
- MaterialSku
- MaterialCategory
- MaterialThicknessProfile
- SheetStock
- SolidStock
- GrainRule
- VeneerMatchGroup

### 8.8 Hardware Catalog + Constraint Engine
Owns:
- manufacturers
- families
- exact SKUs
- boring patterns
- mounting offsets
- clearances
- compatibility rules
- installation metadata

Core entities:
- HardwareManufacturer
- HardwareFamily
- HardwareSku
- HardwareMountPattern
- HardwareConstraint
- HardwareCompatibilityRule

### 8.9 Part Generation Engine
Owns:
- parts
- geometry
- labels
- material assignment
- orientation
- edge treatment
- exposure metadata

Core entities:
- Part
- PartGeometry
- PartRole
- PartMaterialAssignment
- PartOrientation
- EdgeTreatment
- PartExposureProfile

### 8.10 Manufacturing Planning Engine
Owns:
- cut list generation
- CNC workflows
- table saw / panel saw workflows
- kerf tracking
- tool compensation
- cut sequencing
- machine export packaging

Core entities:
- ManufacturingPlan
- CutOperation
- DrillOperation
- RouteOperation
- ToolProfile
- KerfProfile
- NestPlan
- LinearCutPlan
- MachineExportPackage

### 8.11 Install Planning Engine
Owns:
- install dependency graph
- sequence planning
- fastening zones
- stud/blocking references
- tolerance allowances
- access / fit / transport checks

Core entities:
- InstallPlan
- InstallStep
- InstallDependency
- FasteningZone
- StudMap
- BlockingZone
- ToleranceAllowance
- AccessEnvelope

### 8.12 Costing / Estimating Engine
Owns:
- material cost
- hardware cost
- labor models
- finishing
- install cost
- delivery
- taxes
- discounts
- markup
- re-costing by revision

Core entities:
- Estimate
- EstimateSnapshot
- CostLine
- LaborModel
- InstallCostModel
- DeliveryCostModel
- TaxProfile
- DiscountRule
- MarkupRule

### 8.13 Validation / Error Detection Engine
Owns:
- collision checking
- hardware compatibility
- grain/layout validation
- manufacturability
- installability
- transportability
- override policy checks
- remediation suggestions

Core entities:
- ValidationIssue
- ValidationRule
- IssueSeverity
- IssueEvidence
- RemediationSuggestion

Severity model:
- info
- warning
- error
- manufacture_blocker

### 8.14 Why Engine / Explanation Graph
Owns:
- lineage
- rule application records
- constraint records
- change records
- source-to-output traceability
- authoring outcome explanations

Core entities:
- ExplanationNode
- RuleApplicationRecord
- ConstraintRecord
- SourceInputRecord
- DerivedFromLink
- ChangeRecord

### 8.15 Template / Library System
Owns:
- cabinet templates
- run templates
- style presets
- material packages
- hardware packages
- reusable assemblies
- shop standards

Core entities:
- CabinetTemplate
- RunTemplate
- StylePreset
- ShopStandard
- AssemblyTemplate
- HardwarePackage

---
## 9. Canonical Resolution Pipeline

### Stage 1: Input capture
Sources:
- drag/drop authoring
- numeric input
- templates
- room setup
- hardware/material package selection
- overrides
- field measurement workflows

Output:
- interaction state
- design-intent state

### Stage 2: Interaction interpretation
Translate user actions into intent-driven commands.

Examples:
- AppendCabinetToRun
- PlaceCabinetNearWall
- CenterCabinetOnReference
- ReplaceCabinetInRun
- ResizeRunSegment

Output:
- committed design commands

### Stage 3: Spatial resolution
Resolve:
- scene placement
- run membership
- adjacency
- anchors
- coordinate relationships

Output:
- stable layout graph

### Stage 4: Engineering resolution
Resolve:
- assemblies
- openings
- frame logic
- fillers/scribes
- face-frame math
- run continuity
- corner strategies

Output:
- constructible assembly graph

### Stage 5: Constraint propagation
Apply:
- material thickness realities
- hardware rules
- grain/orientation rules
- clearances
- construction rules
- tolerance presets / reality mode

Output:
- constrained engineering graph

### Stage 6: Part generation
Generate:
- parts
- labels
- dimensions
- material assignments
- grain directions
- edge treatment
- boring references

Output:
- part graph

### Stage 7: Manufacturing planning
Resolve:
- cut lists
- machining operations
- kerf/tool compensation
- nesting / linear cut plans
- workflow-specific outputs

Output:
- manufacturing plan

### Stage 8: Install planning
Resolve:
- install order
- dependencies
- fastening logic
- access/fit checks
- transport/install warnings

Output:
- install plan

### Stage 9: Costing
Resolve:
- estimate lines
- labor and install costs
- delivery
- taxes
- discounts
- markup
- revision delta

Output:
- estimate snapshot

### Stage 10: Validation
Run checks across:
- layout
- engineering
- manufacturing
- install
- costing sanity

Output:
- issue set with severity and fix suggestions

### Stage 11: Packaging / snapshot
If approved:
- freeze immutable snapshot
- generate manufacturing package
- generate install package
- generate estimate package
- bind outputs to revision ID

All design-changing commands must flow through the same orchestration path so pipeline stages cannot be skipped accidentally.

---
## 10. WPF / Desktop Interaction Requirements

### 10.1 UI architecture
Use MVVM.
Keep:
- views thin
- view models focused on UI state and commands
- domain/application logic outside the view layer

### 10.2 Editor architecture
The editor should split into:
- interaction state
- selection state
- preview state
- snap candidate generation
- placement scoring
- commit translator
- deep recalculation trigger

Maintain distinct lightweight and full-resolution models where appropriate, for example:
- `LightweightLayoutGraph` for drag preview and quick snapping
- `ResolvedProject` for fully resolved authoritative state

### 10.3 Desktop UX expectations
Design as a real Windows productivity tool with:
- panel-based workspace patterns
- property inspectors
- keyboard shortcuts
- right-click/context actions
- precise numeric entry
- drag/drop plus keyboard refinement
- multi-select support later
- fast local undo/redo

### 10.4 Fast path vs deep path
During drag:
- lightweight snap evaluation
- quick candidate ranking
- preview likely results
- avoid full deep recompute on every movement

After commit:
- full deterministic resolution
- validations
- explanation records
- cost/install/manufacturing refresh

### 10.5 Required authoring commands
Support commands such as:
- CreateRunFromSelection
- AppendCabinetToRun
- InsertCabinetIntoRun
- ReplaceCabinetInRun
- SplitRun
- MergeRuns
- PlaceCabinetNearWall
- AlignCabinetsToReference
- CenterCabinetOnReference
- ApplyEndCondition
- CreateFillerAtGap
- RebalanceRunReveals

### 10.6 Snap types
Support:
- spatial snapping
- run snapping
- functional snapping
- constraint-aware snapping

Examples:
- wall snap
- run append
- insert between cabinets
- center under window
- align to appliance centerline
- reject hardware-invalid placement

### 10.7 Undo / redo
Plan for explicit command-based undo/redo.
Do not treat undo/redo as an afterthought.

Use deterministic command handling through the Application layer.
Implementation may use:
- reversible minimal deltas
- command journals
- periodic checkpoints
- snapshot hybrids

Do not prematurely hard-lock the system into only one undo storage strategy, but require deterministic results and auditable change history.

---
## 11. Geometry, Measurement, and Precision Rules

### 11.1 Geometry foundation
Create a required `CabinetDesigner.Domain.Geometry` namespace with immutable value objects and operators.

At minimum, define:
- `Length`
- `Angle`
- `Point2D`
- `Vector2D`
- `Rect2D`
- `LineSegment2D`

Optional later:
- `Polygon2D`
- `Transform2D`
- wrappers or adapters for more advanced geometry libraries if ever needed

Do not pass naked decimals, tuples, or ambiguous primitive coordinate data freely across domain boundaries.

### 11.2 Internal storage
Use precise canonical dimensional storage with deterministic behavior.

In C#, prefer decimal-oriented modeling for authoritative measured values.  
Do not rely on binary floating point for authoritative dimensional truth.

### 11.3 Input/output
Support:
- fractional inches
- decimal inches
- configurable shop formatting
- context-dependent display precision
- project-level imperial / metric display preference

### 11.4 Measurement system
Support a project-level:
- `MeasurementSystem` (Imperial / Metric)
- `DisplayFormat`

Keep one canonical internal measurement representation.  
Do not allow competing internal measurement truths.

### 11.5 Actual vs nominal
Material records must store both nominal and actual thickness.

Actual thickness drives:
- joinery
- reveals
- hardware fit
- machining
- part generation

### 11.6 Determinism
All derived outputs must be reproducible from source inputs and rules.

---
## 12. Parameter Hierarchy

Parameters must be structured and hierarchical, not ad hoc.

Recommended resolution order:
1. global shop standards
2. project defaults
3. room defaults
4. run-level rules
5. cabinet-level settings
6. opening/interior settings
7. local overrides

Derived values should be generated from authoritative inputs, not manually duplicated.

---
## 13. Real-World Constraint Requirements

### 13.1 Hardware
Support real manufacturer-aware hardware behavior:
- manufacturer-specific rules
- exact SKUs
- boring patterns
- minimum clearances
- slide length vs cabinet depth
- soft-close impact
- compatibility rules

Hardware data should be ingested through normalized internal catalogs, optionally synced from vendor APIs.

### 13.2 Grain / veneer
Support:
- grain direction on every material-bearing part
- sheet optimization respecting grain
- rotation restrictions
- veneer matching groups
- visual sequence awareness for premium work

### 13.3 Manufacturing workflows
Support:
- CNC nested workflow
- panel saw / table saw linear workflow
- hybrid workflow
- kerf tracking
- tool diameter compensation
- cut sequence planning

### 13.4 Install reality
Support:
- install sequencing
- dependency graph
- studs/blocking
- fastening zones
- shim/tolerance allowances
- fit warnings
- transport/access warnings

### 13.5 Reality Mode
Provide a mode that increases realism by considering:
- tolerances
- imperfect walls
- tighter fit thresholds
- real-world install risk
- transport constraints

---
## 14. Non-Functional Requirements

### 14.1 Performance budget
The system must include explicit performance targets.

Target guidance:
- drag preview updates should feel real-time for normal kitchen editing
- common preview updates should target roughly one frame budget
- ordinary kitchen commit/recalc operations should complete quickly enough to feel responsive to a working carpenter

Suggested working benchmark:
- maintain smooth preview interaction on a normal modern shop laptop
- separate preview-time data structures from full resolved state

### 14.2 Autosave and crash recovery
Desktop reliability is a core requirement.

Add:
- autosave at a regular interval
- recovery from the last known good working state
- user-visible crash recovery flow when needed

### 14.3 Structured events and diagnostics
Every command pathway should be able to emit structured application events for:
- diagnostics
- logging
- telemetry if added later
- troubleshooting
- crash analysis

---
## 15. Validation Targets

The validation engine should eventually detect:
- drawer cannot open fully
- adjacent doors collide
- hinge or slide invalid for geometry
- boring pattern invalid
- cabinet depth invalid for selected slide
- run reveal inconsistency
- impossible machining with current tool profile
- grain direction violating visual requirements
- corner hardware collision / inaccessibility
- install fastening misses studs/blocking
- assembly too large to fit through a doorway
- approved design changed without regenerated outputs

Validation output must be actionable and understandable.

---
## 16. Costing / Estimating Requirements

Support:
- sheet goods cost
- solid wood cost
- hardware cost
- doors
- drawers
- boxes
- finish
- machining
- shop labor
- install labor
- delivery
- taxes
- discounts
- markup

Pricing methods should support:
- square foot pricing
- linear foot pricing
- cabinet-based pricing
- door/drawer-based pricing
- labor-hour models
- flat fees

Estimate outputs should include:
- internal costing view
- client-facing proposal view
- category breakdown
- revision delta breakdown

---
## 17. Versioning / Workflow Requirements

### 16.1 Revision model
Use:
- editable working revision
- immutable approved revision snapshots

### 16.2 Snapshot types
On approval, freeze:
- design snapshot
- part snapshot
- manufacturing snapshot
- install snapshot
- estimate snapshot

### 17.3 Working schema vs approved snapshots
The live working project schema may evolve through migrations.

Approved historical snapshots must be stored in a stable versioned serialized form that is insulated from working-schema drift.

Reasonable options include:
- compressed JSON
- MessagePack
- protobuf
- other versioned serialized payloads

Do not let working database schema evolution corrupt historical approved designs.

### 17.4 Workflow states
Recommended:
- draft
- under_review
- approved
- locked_for_manufacture
- released_to_shop
- ready_for_install
- installed
- superseded

### 17.5 Diff support
Support diffs for:
- layout
- runs
- cabinets
- parts
- hardware
- manufacturing outputs
- install plan
- estimate

---
## 18. Persistence Strategy

### 18.1 Primary local persistence
Use SQLite as the primary local database for:
- projects
- revisions
- snapshots
- templates
- cost data
- catalog cache metadata
- settings as appropriate

### 18.2 Serialized payloads
Use structured serialized payloads selectively for:
- immutable revision snapshots
- export payloads
- explanation records if appropriate
- imported catalog source blobs where useful

### 18.3 Important rule
Do not let persistence shape the domain incorrectly.
The domain model should stay clear and intentional, with persistence adapters mapping to storage concerns.

---
## 19. Why Engine Requirements

The Why Engine must explain:
- why a part exists
- why its dimension is what it is
- which rules influenced it
- why a filler was inserted
- why a cabinet snapped to a run
- why a warning was raised
- why cost changed
- what changed between revisions

Record explanation lineage during resolution, not later as a reconstruction exercise.

---
## 20. Testing Strategy

Prioritize automated testing heavily because correctness is a business-critical feature.

### 19.1 Test categories
- domain unit tests
- rule engine tests
- run resolution tests
- hardware compatibility tests
- manufacturing planning tests
- install planning tests
- validation tests
- snapshot/version tests
- serialization tests
- editor command tests
- undo/redo tests

### 20.2 Property-based testing
Use property-based testing for dimensional invariants and subtle rule interactions, especially around:
- run continuity
- filler math
- reveal consistency
- thickness propagation
- opening/part constraints
- non-negative dimensions
- width/depth consistency invariants

### 20.3 Snapshot-style testing
Use snapshot-style assertions where appropriate for:
- generated part sets
- validation issue lists
- estimate outputs
- revision diffs
- explanation records

### 20.4 UI testing posture
Keep core correctness out of the UI so most critical logic can be tested without WPF automation.

---
## 21. AI Prompt Playbook Usage

Use the following prompt structure when asking an AI to design or implement parts of the system.

### 20.1 Prompt skeleton
Use this exact framing:

1. State the subsystem or deliverable.
2. Restate the core product promise:
   - Fast to draw. Hard to mess up. Safe to build.
3. State that the implementation target is:
   - C#
   - .NET 8+
   - Windows desktop
   - WPF
   - SQLite
4. State the non-negotiable architecture rules.
5. Define the bounded contexts involved.
6. Require separation of interaction, design intent, engineering, manufacturing, install, and commercial concerns where relevant.
7. Require traceability / explanation lineage.
8. Require validation severity modeling.
9. Require deterministic outputs.
10. Require testability independent of WPF where possible.
11. Require future extensibility for hardware catalogs, install logic, and revisions.
12. Ask for implementation output in a structured format.

### 20.2 Example structured asks
Use prompts like:

- design the C# domain model for the Run Engine
- propose .NET application services for the Manufacturing Planning Engine
- define C# records/classes for Part Generation outputs
- design a validation pipeline for hardware compatibility and install blockers
- propose event flow for WPF drag/drop commit and recalculation
- design SQLite persistence strategy for revisions and snapshots
- define explanation lineage schema for rule application in C#
- design the `CabinetDesigner.Domain.Geometry` namespace
- design the `ResolutionOrchestrator` service and its command flow

---
## 22. Standard AI Output Format

When prompting an AI for implementation, ask it to return:

1. goals
2. responsibilities
3. boundaries / what this subsystem does not own
4. domain entities
5. C# project/module layout
6. command flow
7. event flow
8. validation rules
9. persistence strategy
10. explanation lineage records
11. testing strategy
12. future extension points
13. risks / edge cases

---
## 23. Preferred Engineering Style for AI Outputs

Ask the AI to:
- keep domains explicit
- avoid collapsing all logic into giant services
- keep rules modular
- separate WPF concerns from application/domain concerns
- preserve deterministic recalculation
- make invalid states visible
- prefer explicit commands and resolution stages over ad hoc mutation
- preserve traceability across outputs
- design for future API ingestion without vendor lock-in
- favor clear C# types and explicit value objects over vague dynamic structures

---
## 24. MVP Priorities

MVP must include:
- precise dimensional engine
- geometry value-object foundation
- room scene
- drag/drop and snap architecture foundation
- run-based cabinet modeling
- engineering resolution
- actual vs nominal thickness support
- curated hardware packages
- part generation
- cut list generation
- revision snapshots
- estimate snapshots
- validation framework
- why/explanation trace foundation
- `ResolutionOrchestrator`
- WPF desktop shell with core editor workflow
- autosave and crash recovery baseline

Near-next:
- hardware import adapters
- grain-aware optimization
- install dependency engine
- fastening/stud logic
- smarter guided placement
- improved shop drawing intelligence
- field measurement cleanup workflows

Later:
- advanced CNC integrations
- multi-user permissions/workflow
- laser measurement integrations
- installer-facing workflow tooling
- richer client demo visuals
- fabricated countertop scope

---
## 25. Final North Star

Build a system where:
- a carpenter can lay out a kitchen quickly on Windows desktop
- snapping and drag/drop feel intelligent and predictable
- the system preserves alignment, continuity, and standards automatically
- outputs are precise enough to trust in the shop
- install issues are caught early
- estimates are revision-safe
- the user can always understand why the software did what it did

North star:

**A Windows desktop cabinet design and production system that combines assisted layout authoring with shop-grade precision, so carpenters can design faster, make fewer mistakes, and move confidently from concept to cut list to install.**
