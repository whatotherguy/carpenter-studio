# P8.5 — Install Planning Subsystem Design

Source: `cabinet_ai_prompt_pack_v4_1_full.md`
Context: `architecture_summary.md`, `domain_model.md`, `orchestrator.md`, `validation_engine.md`, `manufacturing.md`

---

## 1. Goals

- Define the install planning subsystem as a **pure projection** of resolved design and manufacturing state
- Model install sequencing, dependency ordering, and fastening concerns for field readiness
- Capture wall conditions (stud locations, blocking zones), access constraints, and clearance checks
- Define shim/scribe/tolerance allowances as install-facing data derived from engineering resolution
- Produce a deterministic install plan — same resolved design state = same install plan, always
- Provide install readiness assessment with blockers and field-actionable issue reporting
- Maintain full traceability from every install step back to its source cabinet, run, revision, and manufacturing artifacts

---

## 2. Design Decisions

| Decision | Rationale |
|---|---|
| Install planning is a projection, not a source of truth | Architecture guardrail: install output is derived only, must never mutate design state. The design is the source of truth; install planning is a deterministic function of it |
| Install plan is computed at Stage 8 of the pipeline | Runs after Manufacturing Planning (Stage 7). Install planning consumes spatial resolution, engineering resolution, and manufacturing plan data to produce field-ready output |
| Install planning does not own wall stud data — it consumes it | Stud locations and blocking zones are spatial/engineering data. Install planning evaluates fastening feasibility against them but does not define them |
| Sequencing is topological, not heuristic | Install order is derived from a dependency graph with explicit edges. No ad-hoc "install base cabinets first" assumptions — the graph encodes those constraints formally |
| Tolerances are install-facing projections of engineering data | Shim allowances, scribe widths, and gap tolerances originate in engineering resolution. Install planning presents them as field-actionable values, not raw engineering math |
| Install plan is frozen with the revision snapshot | When a revision reaches `ReadyForInstall`, the install plan is frozen. Working-state install plans are preview-only |
| Access order considers physical interference | A tall cabinet may block access to an adjacent base cabinet's fastening zone. The dependency graph encodes these physical constraints |

---

## 3. Boundaries

### In Scope

- Install data model (plan, steps, dependencies, fastening zones, access constraints)
- Sequencing and dependency graph derivation from resolved design
- Fastening zone identification and stud/blocking evaluation
- Access and clearance constraint modeling
- Shim, scribe, and tolerance allowance projection
- Install readiness assessment and blocker detection
- Traceability from install artifacts to design/manufacturing artifacts

### Out of Scope

- Modifying design state (projection only)
- UI implementation (presentation layer concern)
- Physical measurement capture workflows (field tool integration)
- Costing logic (Stage 9 concern)
- Persistence implementation (persistence layer concern)
- Actual field installation tracking (post-install workflow)

---

## 4. Install Data Model

### 4.1 Identifiers

```csharp
namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct InstallPlanId(Guid Value)
{
    public static InstallPlanId New() => new(Guid.NewGuid());
}

public readonly record struct InstallStepId(Guid Value)
{
    public static InstallStepId New() => new(Guid.NewGuid());
}

public readonly record struct FasteningZoneId(Guid Value)
{
    public static FasteningZoneId New() => new(Guid.NewGuid());
}

public readonly record struct AccessConstraintId(Guid Value)
{
    public static AccessConstraintId New() => new(Guid.NewGuid());
}
```

### 4.2 InstallPlan

The top-level install output for a revision. Contains sequenced steps, fastening data, and readiness assessment.

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Complete install plan for a revision. Pure projection — computed from
/// resolved design and manufacturing state, never persisted as source of truth.
/// Frozen with the revision snapshot when state reaches ReadyForInstall.
/// </summary>
public sealed class InstallPlan
{
    public InstallPlanId Id { get; }
    public RevisionId RevisionId { get; }
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>Ordered install steps. Topologically sorted by dependency graph.</summary>
    private readonly List<InstallStep> _steps;
    public IReadOnlyList<InstallStep> Steps => _steps;

    /// <summary>Dependency edges between install steps.</summary>
    private readonly List<InstallDependency> _dependencies;
    public IReadOnlyList<InstallDependency> Dependencies => _dependencies;

    /// <summary>Fastening zones across all cabinets in the plan.</summary>
    private readonly List<FasteningZone> _fasteningZones;
    public IReadOnlyList<FasteningZone> FasteningZones => _fasteningZones;

    /// <summary>Access constraints that affect install ordering.</summary>
    private readonly List<AccessConstraint> _accessConstraints;
    public IReadOnlyList<AccessConstraint> AccessConstraints => _accessConstraints;

    /// <summary>Install readiness assessment.</summary>
    public InstallReadinessResult Readiness { get; }

    public InstallPlan(
        InstallPlanId id,
        RevisionId revisionId,
        DateTimeOffset generatedAt,
        IReadOnlyList<InstallStep> steps,
        IReadOnlyList<InstallDependency> dependencies,
        IReadOnlyList<FasteningZone> fasteningZones,
        IReadOnlyList<AccessConstraint> accessConstraints,
        InstallReadinessResult readiness)
    {
        Id = id;
        RevisionId = revisionId;
        GeneratedAt = generatedAt;
        _steps = steps.ToList();
        _dependencies = dependencies.ToList();
        _fasteningZones = fasteningZones.ToList();
        _accessConstraints = accessConstraints.ToList();
        Readiness = readiness;
    }

    /// <summary>Total install steps.</summary>
    public int TotalSteps => _steps.Count;

    /// <summary>Steps that have no unsatisfied dependencies (can start first).</summary>
    public IReadOnlyList<InstallStep> RootSteps =>
        _steps.Where(s => !_dependencies.Any(d => d.DependentStepId == s.Id)).ToList();

    /// <summary>Steps grouped by wall for field crew organization.</summary>
    public ILookup<WallId, InstallStep> StepsByWall =>
        _steps.Where(s => s.WallId.HasValue).ToLookup(s => s.WallId!.Value);
}
```

### 4.3 InstallStep

A single unit of install work — typically one cabinet, one filler, or one preparation task.

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A single install step. Represents one discrete unit of field work.
/// Steps are topologically ordered by the dependency graph.
/// </summary>
public sealed record InstallStep
{
    public InstallStepId Id { get; init; }

    /// <summary>Sequence position in the topological sort (0-based).</summary>
    public required int SequenceIndex { get; init; }

    /// <summary>What kind of install work this step represents.</summary>
    public required InstallStepType StepType { get; init; }

    /// <summary>Human-readable description for field crew.</summary>
    public required string Description { get; init; }

    // --- Traceability ---

    /// <summary>The cabinet being installed (null for preparation steps).</summary>
    public CabinetId? CabinetId { get; init; }

    /// <summary>The run this step belongs to (null for room-level prep).</summary>
    public RunId? RunId { get; init; }

    /// <summary>The wall this step is associated with.</summary>
    public WallId? WallId { get; init; }

    /// <summary>The revision this step belongs to.</summary>
    public required RevisionId RevisionId { get; init; }

    // --- Position and dimensions ---

    /// <summary>
    /// World-space bounding box for this step's installation footprint.
    /// Derived from spatial resolution — not computed here.
    /// </summary>
    public Rect2D? InstallFootprint { get; init; }

    // --- Tolerances ---

    /// <summary>Shim and scribe allowances applicable to this step.</summary>
    public required InstallTolerances Tolerances { get; init; }

    // --- Fastening ---

    /// <summary>Fastening zone IDs associated with this step.</summary>
    public required IReadOnlyList<FasteningZoneId> FasteningZoneIds { get; init; }

    // --- Field notes ---

    /// <summary>Auto-generated notes for field crew (e.g., "check plumb before fastening").</summary>
    public string? FieldNotes { get; init; }
}

/// <summary>
/// The type of work an install step represents.
/// </summary>
public enum InstallStepType
{
    /// <summary>Wall preparation: mark stud locations, install blocking, verify plumb/level.</summary>
    WallPrep,

    /// <summary>Install a ledger board or support rail.</summary>
    LedgerInstall,

    /// <summary>Install a base cabinet.</summary>
    BaseCabinetInstall,

    /// <summary>Install a wall cabinet.</summary>
    WallCabinetInstall,

    /// <summary>Install a tall/pantry cabinet.</summary>
    TallCabinetInstall,

    /// <summary>Install a vanity cabinet.</summary>
    VanityCabinetInstall,

    /// <summary>Install a filler strip.</summary>
    FillerInstall,

    /// <summary>Install an end panel.</summary>
    EndPanelInstall,

    /// <summary>Install a scribe piece.</summary>
    ScribeInstall,

    /// <summary>Install crown molding or trim.</summary>
    TrimInstall,

    /// <summary>Join adjacent cabinets (clamp and screw through stiles/panels).</summary>
    CabinetJoin,

    /// <summary>Final leveling, shimming, and plumb verification.</summary>
    LevelAndShim,

    /// <summary>Hardware installation (doors, drawers, pulls) — post-cabinet-set.</summary>
    HardwareInstall
}
```

### 4.4 InstallTolerances

Field-actionable tolerance values projected from engineering resolution.

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Tolerance allowances for an install step. Derived from engineering
/// resolution and presented as field-actionable values.
/// All values use geometry value objects — no primitives.
/// </summary>
public sealed record InstallTolerances
{
    /// <summary>Maximum allowed gap between cabinet and wall (triggers shimming).</summary>
    public required Length MaxWallGap { get; init; }

    /// <summary>Scribe allowance for irregular wall surfaces.</summary>
    public required Length ScribeAllowance { get; init; }

    /// <summary>Maximum plumb deviation before correction is required.</summary>
    public required Length MaxPlumbDeviation { get; init; }

    /// <summary>Maximum level deviation before shimming is required.</summary>
    public required Length MaxLevelDeviation { get; init; }

    /// <summary>
    /// Reveal tolerance — acceptable deviation from specified reveal between adjacent cabinets.
    /// </summary>
    public required Length RevealTolerance { get; init; }

    /// <summary>Gap tolerance between adjacent cabinet faces in a run.</summary>
    public required Length AdjacentCabinetGapTolerance { get; init; }

    /// <summary>Default tolerances for standard residential installation.</summary>
    public static InstallTolerances ResidentialDefaults => new()
    {
        MaxWallGap = Length.FromFraction(1, 4),           // 1/4"
        ScribeAllowance = Length.FromFraction(3, 8),      // 3/8"
        MaxPlumbDeviation = Length.FromFraction(1, 8),    // 1/8" per 4'
        MaxLevelDeviation = Length.FromFraction(1, 8),    // 1/8" per 8'
        RevealTolerance = Length.FromFraction(1, 16),     // 1/16"
        AdjacentCabinetGapTolerance = Length.FromFraction(1, 16) // 1/16"
    };
}
```

### 4.5 InstallDependency

An edge in the install dependency graph.

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A dependency edge in the install sequence graph.
/// "DependentStepId cannot begin until PrerequisiteStepId is complete."
/// </summary>
public sealed record InstallDependency
{
    /// <summary>The step that must be completed first.</summary>
    public required InstallStepId PrerequisiteStepId { get; init; }

    /// <summary>The step that depends on the prerequisite.</summary>
    public required InstallStepId DependentStepId { get; init; }

    /// <summary>Why this dependency exists.</summary>
    public required InstallDependencyReason Reason { get; init; }

    /// <summary>Human-readable explanation for the field crew.</summary>
    public required string Explanation { get; init; }
}

/// <summary>
/// Why one install step depends on another.
/// </summary>
public enum InstallDependencyReason
{
    /// <summary>Structural: prerequisite provides physical support (e.g., base before countertop).</summary>
    StructuralSupport,

    /// <summary>Access: prerequisite must be in place before dependent can be reached.</summary>
    AccessOrder,

    /// <summary>Alignment: prerequisite establishes reference line/level for dependent.</summary>
    AlignmentReference,

    /// <summary>Fastening: dependent fastens through or to the prerequisite.</summary>
    FasteningTarget,

    /// <summary>Interference: installing dependent first would block access to prerequisite's fastening zone.</summary>
    InterferenceClearance,

    /// <summary>Wall prep must precede any cabinet installation on that wall.</summary>
    WallPrepRequired,

    /// <summary>Run continuity: cabinets in a run are installed in sequence for reveal alignment.</summary>
    RunSequence,

    /// <summary>Joining: adjacent cabinets must both be positioned before they are joined.</summary>
    JoinOrder
}
```

### 4.6 FasteningZone

A region on a cabinet that requires fastening to the wall structure.

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A zone on a cabinet requiring fastening to wall structure.
/// Evaluated against stud/blocking locations during install planning.
/// </summary>
public sealed record FasteningZone
{
    public FasteningZoneId Id { get; init; }

    /// <summary>The cabinet this zone belongs to.</summary>
    public required CabinetId CabinetId { get; init; }

    /// <summary>The install step that installs this cabinet.</summary>
    public required InstallStepId InstallStepId { get; init; }

    /// <summary>Type of fastening required.</summary>
    public required FasteningType FasteningType { get; init; }

    /// <summary>
    /// Zone bounds relative to the cabinet's origin (not world-space).
    /// The install projection computes world-space positions from
    /// spatial resolution data.
    /// </summary>
    public required Rect2D ZoneBounds { get; init; }

    /// <summary>
    /// World-space position of this zone on the wall.
    /// Computed during install planning from cabinet position + zone bounds.
    /// </summary>
    public required Rect2D WorldBounds { get; init; }

    /// <summary>Whether a stud or blocking was found behind this zone.</summary>
    public required FasteningBackingStatus BackingStatus { get; init; }

    /// <summary>
    /// Distance from zone center to nearest stud center.
    /// Null if no stud data is available.
    /// </summary>
    public Length? DistanceToNearestStud { get; init; }

    /// <summary>Minimum number of fasteners required in this zone.</summary>
    public required int MinFastenerCount { get; init; }

    /// <summary>Field notes for this zone.</summary>
    public string? Notes { get; init; }
}

/// <summary>
/// How a cabinet is fastened to the wall.
/// </summary>
public enum FasteningType
{
    /// <summary>Through back panel into stud (standard base/wall cabinet).</summary>
    ThroughBack,

    /// <summary>Through nailing strip at top of cabinet.</summary>
    ThroughNailingStrip,

    /// <summary>Through hanging rail/cleat system.</summary>
    HangingRail,

    /// <summary>Through cabinet side into adjacent cabinet.</summary>
    CabinetToCabinet,

    /// <summary>Through ledger board (for wall cabinets).</summary>
    ThroughLedger,

    /// <summary>Toggle bolt or anchor (no stud available).</summary>
    WallAnchor
}

/// <summary>
/// What structural backing exists behind a fastening zone.
/// </summary>
public enum FasteningBackingStatus
{
    /// <summary>Stud confirmed at this location.</summary>
    StudConfirmed,

    /// <summary>Blocking confirmed at this location.</summary>
    BlockingConfirmed,

    /// <summary>No stud or blocking — wall anchor required.</summary>
    NoStructuralBacking,

    /// <summary>Stud data not available — cannot assess.</summary>
    Unknown
}
```

### 4.7 AccessConstraint

A physical constraint that affects install ordering due to clearance or reach requirements.

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A physical access constraint that affects install sequencing.
/// Encodes interference between cabinets during installation —
/// e.g., a tall cabinet blocks screw access to the adjacent base.
/// </summary>
public sealed record AccessConstraint
{
    public AccessConstraintId Id { get; init; }

    /// <summary>The cabinet that creates the access restriction.</summary>
    public required CabinetId BlockingCabinetId { get; init; }

    /// <summary>The cabinet whose install access is restricted.</summary>
    public required CabinetId BlockedCabinetId { get; init; }

    /// <summary>What type of access is blocked.</summary>
    public required AccessBlockType BlockType { get; init; }

    /// <summary>The region where access is blocked (world-space).</summary>
    public required Rect2D BlockedRegion { get; init; }

    /// <summary>Human-readable description for field crew.</summary>
    public required string Description { get; init; }
}

/// <summary>
/// What kind of access is restricted by a cabinet's presence.
/// </summary>
public enum AccessBlockType
{
    /// <summary>Cannot reach fastening zone on adjacent cabinet.</summary>
    FasteningAccess,

    /// <summary>Cannot physically position the cabinet (path blocked).</summary>
    PlacementAccess,

    /// <summary>Cannot reach wall for leveling/shimming behind the cabinet.</summary>
    WallAccess,

    /// <summary>Cannot open doors/drawers for final adjustment if neighbor is in place.</summary>
    AdjustmentAccess
}
```

---

## 5. Sequencing Model

Install sequencing is derived from a directed acyclic graph (DAG) of `InstallDependency` edges. The topological sort of this graph produces the `SequenceIndex` on each `InstallStep`.

### 5.1 Dependency Graph Construction

The install projector builds the dependency graph by applying these rules in order:

```
1. Wall Prep Dependencies
   For each wall with cabinets:
     WallPrep(wall) → every cabinet install on that wall

2. Ledger Dependencies
   For each wall with wall-mounted cabinets:
     WallPrep(wall) → LedgerInstall(wall) → every wall cabinet on that wall

3. Category Ordering (per wall)
   Base cabinets before wall cabinets on the same wall
   (reason: bases establish the level reference line)

4. Run Sequence Dependencies
   Within a run, cabinets install left-to-right (or from the reference end)
   step[n] → step[n+1] (reason: reveal alignment)

5. Interference Dependencies
   For each pair of adjacent cabinets:
     If cabinet A blocks fastening access to cabinet B:
       B installs before A (reason: interference clearance)

6. Join Dependencies
   For each pair of adjacent cabinets in a run:
     Both positioned → CabinetJoin step
     (reason: both must be in place before joining)

7. Filler/Scribe Dependencies
   Filler steps depend on both adjacent cabinets being installed
   Scribe steps depend on the cabinet being installed and positioned

8. Trim Dependencies
   Trim/crown steps depend on all cabinets on that wall being installed

9. Hardware Dependencies
   Hardware install steps depend on cabinet set and joining being complete
```

### 5.2 Topological Sort

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Produces a topological ordering of install steps from the dependency graph.
/// Deterministic: ties are broken by (WallId, RunId, SlotIndex, StepType) to
/// ensure identical output for identical input.
/// </summary>
public static class InstallSequencer
{
    /// <summary>
    /// Topologically sort install steps based on dependencies.
    /// Returns steps in install order with SequenceIndex assigned.
    /// Throws if the dependency graph contains a cycle (design bug).
    /// </summary>
    public static IReadOnlyList<InstallStep> Sequence(
        IReadOnlyList<InstallStep> steps,
        IReadOnlyList<InstallDependency> dependencies)
    {
        // Kahn's algorithm with deterministic tie-breaking
        var inDegree = steps.ToDictionary(s => s.Id, _ => 0);
        var adjacency = steps.ToDictionary(s => s.Id, _ => new List<InstallStepId>());

        foreach (var dep in dependencies)
        {
            inDegree[dep.DependentStepId]++;
            adjacency[dep.PrerequisiteStepId].Add(dep.DependentStepId);
        }

        // Priority queue with deterministic ordering for tie-breaking
        var ready = new SortedSet<InstallStep>(
            steps.Where(s => inDegree[s.Id] == 0),
            InstallStepOrderComparer.Instance);

        var result = new List<InstallStep>();
        var index = 0;

        while (ready.Count > 0)
        {
            var current = ready.Min!;
            ready.Remove(current);

            result.Add(current with { SequenceIndex = index++ });

            foreach (var dependentId in adjacency[current.Id])
            {
                inDegree[dependentId]--;
                if (inDegree[dependentId] == 0)
                {
                    var dependent = steps.First(s => s.Id == dependentId);
                    ready.Add(dependent);
                }
            }
        }

        if (result.Count != steps.Count)
            throw new InvalidOperationException(
                $"Install dependency graph contains a cycle. " +
                $"Sequenced {result.Count} of {steps.Count} steps.");

        return result;
    }
}

/// <summary>
/// Deterministic tie-breaking for install steps with equal dependency depth.
/// Order: WallId → StepType priority → RunId → SlotIndex-derived position.
/// </summary>
internal sealed class InstallStepOrderComparer : IComparer<InstallStep>
{
    public static readonly InstallStepOrderComparer Instance = new();

    private static readonly Dictionary<InstallStepType, int> _typePriority = new()
    {
        [InstallStepType.WallPrep] = 0,
        [InstallStepType.LedgerInstall] = 1,
        [InstallStepType.BaseCabinetInstall] = 2,
        [InstallStepType.VanityCabinetInstall] = 2,
        [InstallStepType.TallCabinetInstall] = 3,
        [InstallStepType.WallCabinetInstall] = 4,
        [InstallStepType.FillerInstall] = 5,
        [InstallStepType.EndPanelInstall] = 5,
        [InstallStepType.ScribeInstall] = 5,
        [InstallStepType.CabinetJoin] = 6,
        [InstallStepType.LevelAndShim] = 7,
        [InstallStepType.TrimInstall] = 8,
        [InstallStepType.HardwareInstall] = 9,
    };

    public int Compare(InstallStep? x, InstallStep? y)
    {
        if (x is null || y is null) return 0;

        // 1. Wall ordering
        var wallCompare = string.Compare(
            x.WallId?.Value.ToString(), y.WallId?.Value.ToString(), StringComparison.Ordinal);
        if (wallCompare != 0) return wallCompare;

        // 2. Step type priority
        var xPriority = _typePriority.GetValueOrDefault(x.StepType, 99);
        var yPriority = _typePriority.GetValueOrDefault(y.StepType, 99);
        if (xPriority != yPriority) return xPriority.CompareTo(yPriority);

        // 3. Run ordering
        var runCompare = string.Compare(
            x.RunId?.Value.ToString(), y.RunId?.Value.ToString(), StringComparison.Ordinal);
        if (runCompare != 0) return runCompare;

        // 4. Step ID as final tiebreaker (deterministic)
        return string.Compare(
            x.Id.Value.ToString(), y.Id.Value.ToString(), StringComparison.Ordinal);
    }
}
```

---

## 6. Fastening and Access Model

### 6.1 Fastening Zone Derivation

Fastening zones are derived during install planning based on cabinet category and construction method:

| Cabinet Category | Fastening Zones |
|---|---|
| Base | Top nailing strip (through back), bottom (optional shim point) |
| Wall | Top nailing strip, bottom nailing strip (through back into studs) |
| Tall | Top nailing strip, mid-height nailing strip, bottom (3-point minimum) |
| Vanity | Top nailing strip (through back) |

Each zone is evaluated against wall stud data (when available) to determine `FasteningBackingStatus`:

```
For each FasteningZone:
  1. Compute world-space bounds from cabinet position + zone offset
  2. Query stud locations within zone horizontal range
  3. If stud center falls within zone bounds → StudConfirmed
  4. If blocking region overlaps zone bounds → BlockingConfirmed
  5. Otherwise → NoStructuralBacking (flag for wall anchor)
  6. If no stud data for this wall → Unknown (flag for field verification)
```

### 6.2 Access Constraint Detection

Access constraints are detected by checking interference between adjacent cabinets:

```
For each pair of cabinets (A, B) where A and B are adjacent:
  1. If A is Tall and B is Base:
     Check if A's depth blocks screw access to B's rear nailing strip
     → AccessConstraint(blocking=A, blocked=B, type=FasteningAccess)

  2. If A is Wall and B is Wall, different depths:
     Check if deeper cabinet blocks shallower cabinet's side fastening
     → AccessConstraint(blocking=deeper, blocked=shallower, type=FasteningAccess)

  3. If A blocks the path needed to physically slide B into position:
     → AccessConstraint(blocking=A, blocked=B, type=PlacementAccess)
```

---

## 7. Readiness and Blockers

### 7.1 InstallReadinessResult

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Assessment of whether a design is ready for field installation.
/// Produced during Stage 8. Blockers prevent transition to ReadyForInstall.
/// </summary>
public sealed class InstallReadinessResult
{
    public required bool IsReady { get; init; }
    public required IReadOnlyList<InstallBlocker> Blockers { get; init; }
    public required IReadOnlyList<InstallWarning> Warnings { get; init; }
    public required InstallSummary Summary { get; init; }
}

/// <summary>
/// A condition that prevents installation. Maps to Error severity
/// in the validation engine.
/// </summary>
public sealed record InstallBlocker
{
    public required InstallBlockerCode Code { get; init; }
    public required string Message { get; init; }
    public required string AffectedEntityId { get; init; }

    public ValidationIssue ToValidationIssue() => new()
    {
        RuleCode = $"INSTALL_{Code}",
        Severity = ValidationSeverity.Error,
        Message = Message,
        AffectedEntityIds = [AffectedEntityId],
        Category = ValidationRuleCategory.Installation
    };
}

public enum InstallBlockerCode
{
    /// <summary>Dependency graph contains a cycle — cannot determine install order.</summary>
    CyclicDependency,

    /// <summary>No fastening zone has structural backing and no anchor fallback is viable.</summary>
    NoFasteningOption,

    /// <summary>Cabinet cannot be physically placed — blocked by room geometry or other cabinets.</summary>
    PlacementImpossible,

    /// <summary>Required wall prep data is missing (no stud layout for load-bearing cabinets).</summary>
    MissingWallPrepData,

    /// <summary>Cabinet weight exceeds wall anchor capacity and no stud is available.</summary>
    WeightExceedsAnchorCapacity,

    /// <summary>Manufacturing plan is not ready — cannot plan install without manufactured parts.</summary>
    ManufacturingNotReady,

    /// <summary>Revision is not in an appropriate state for install planning.</summary>
    InvalidRevisionState
}

/// <summary>
/// A non-blocking install concern.
/// </summary>
public sealed record InstallWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string AffectedEntityId { get; init; }
}

/// <summary>
/// Aggregate statistics for install planning assessment.
/// </summary>
public sealed record InstallSummary
{
    public required int TotalSteps { get; init; }
    public required int WallPrepSteps { get; init; }
    public required int CabinetInstallSteps { get; init; }
    public required int FillerAndScribeSteps { get; init; }
    public required int JoinSteps { get; init; }
    public required int TrimSteps { get; init; }
    public required int HardwareSteps { get; init; }
    public required int FasteningZonesWithStuds { get; init; }
    public required int FasteningZonesWithoutStuds { get; init; }
    public required int FasteningZonesUnknown { get; init; }
    public required int AccessConstraintCount { get; init; }
    public required int WallsInvolved { get; init; }
}
```

---

## 8. Integration with Validation and Manufacturing

### 8.1 Pipeline Stage

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.InstallContext;

/// <summary>
/// Stage 8: Install Planning.
/// Transforms resolved design + manufacturing state into an install plan.
/// Pure projection — reads from SpatialResult, EngineeringResult, and
/// ManufacturingResult. Writes InstallResult.
/// </summary>
public sealed class InstallPlanningStage : IResolutionStage
{
    public int StageNumber => 8;
    public string StageName => "Install Planning";

    private readonly IInstallProjector _projector;

    public InstallPlanningStage(IInstallProjector projector)
    {
        _projector = projector;
    }

    public StageResult Execute(ResolutionContext context)
    {
        var plan = _projector.Project(
            context.SpatialResult,
            context.EngineeringResult,
            context.ManufacturingResult,
            context.Command);

        context.InstallResult = new InstallPlanResult(plan);

        var issues = plan.Readiness.Blockers
            .Select(b => b.ToValidationIssue())
            .ToList();

        context.AccumulatedIssues.AddRange(issues);

        return issues.Any(i => i.Severity >= ValidationSeverity.Error)
            ? StageResult.Failed(StageNumber, issues)
            : StageResult.Succeeded(StageNumber, warnings: issues);
    }

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;
}
```

### 8.2 Install Projector Interface

```csharp
namespace CabinetDesigner.Domain.InstallContext;

using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;

/// <summary>
/// Transforms resolved design and manufacturing data into an install plan.
/// Stateless projection — no side effects, deterministic output.
/// </summary>
public interface IInstallProjector
{
    InstallPlan Project(
        SpatialResolutionResult spatialResult,
        EngineeringResolutionResult engineeringResult,
        ManufacturingPlanResult manufacturingResult,
        IDesignCommand sourceCommand);
}
```

### 8.3 Cross-Cutting Validation Rules (Stage 10)

Install validation rules run at Stage 10 against the completed `InstallPlanResult`:

| Rule | Category | Scope | Severity |
|---|---|---|---|
| `FasteningZoneNoStudOrAnchor` | Installation | Cabinet | Error |
| `CabinetWeightExceedsAnchorCapacity` | Installation | Cabinet | Error |
| `InstallAccessBlocked` | Installation | Adjacency | Warning |
| `NoStudDataForWall` | Installation | Scene | Warning |
| `ScribeExceedsAllowance` | Installation | Cabinet | Warning |
| `HighShimRequirement` | Installation | Run | Warning |
| `InstallSequenceCycle` | Installation | Scene | Error |
| `MissingLedgerForWallCabinets` | Installation | Run | Error |

---

## 9. Traceability

Every install artifact traces back to its source in the design and manufacturing layers:

```
InstallPlan
  └── RevisionId ──────────► Revision (immutable snapshot)
       └── InstallStep
            ├── CabinetId ───► Cabinet (which cabinet)
            ├── RunId ───────► CabinetRun (which run)
            ├── WallId ──────► Wall (which wall)
            ├── RevisionId ──► Revision (which revision)
            └── FasteningZoneIds ──► FasteningZone
                 └── CabinetId ──► Cabinet

InstallDependency
  ├── PrerequisiteStepId ──► InstallStep
  └── DependentStepId ─────► InstallStep

AccessConstraint
  ├── BlockingCabinetId ──► Cabinet
  └── BlockedCabinetId ───► Cabinet
```

**Traceability guarantees:**
- Every `InstallStep` with `StepType` that involves a cabinet has a non-null `CabinetId`
- Every `FasteningZone` has a valid `CabinetId` and `InstallStepId`
- Every `InstallDependency` references valid `InstallStepId` values — no orphaned edges
- Every `AccessConstraint` references valid `CabinetId` values
- When a revision reaches `ReadyForInstall`, the `InstallPlan` is frozen with the snapshot — step IDs and sequence are stable
- A field crew member can trace any install step back to the exact cabinet, run, wall, and revision

---

## 10. Invariants

1. An `InstallPlan` is always associated with exactly one `RevisionId`
2. The dependency graph is a DAG — cycles are detected and reported as `InstallBlocker.CyclicDependency`
3. Every cabinet in the design has at least one corresponding `InstallStep`
4. `InstallStep.SequenceIndex` values are contiguous (0..n-1) and reflect valid topological order
5. Every cabinet install step has at least one `FasteningZone`
6. `InstallPlan` output is deterministic: same resolved state = same plan, always
7. Install planning never mutates design state — it is a pure function of resolved inputs
8. Tie-breaking in topological sort is deterministic (by WallId, StepType priority, RunId, StepId)
9. Locked revisions (at `ReadyForInstall` or beyond) carry frozen install plans
10. All dimensional values use geometry value objects — no primitives

---

## 11. Testing Strategy

### Unit Tests

| Test | Validates |
|---|---|
| `Sequencer_SimpleRun_ProducesCorrectOrder` | Linear run of 3 base cabinets sequences left-to-right |
| `Sequencer_WallPrepBeforeCabinets` | Wall prep step precedes all cabinet installs on that wall |
| `Sequencer_BasesBeforeWallCabinets` | Base cabinets install before wall cabinets on same wall |
| `Sequencer_CycleDetected_Throws` | Circular dependency produces exception with diagnostic message |
| `Sequencer_DeterministicOutput` | Same input produces identical sequence on repeated runs |
| `FasteningZone_StudWithinBounds_Confirmed` | Stud location within zone bounds sets StudConfirmed |
| `FasteningZone_NoStud_FlagsNoStructuralBacking` | Zone with no stud sets NoStructuralBacking |
| `FasteningZone_NoStudData_FlagsUnknown` | Missing stud data for wall sets Unknown status |
| `AccessConstraint_TallBlocksBase_Detected` | Tall cabinet adjacent to base creates FasteningAccess constraint |
| `Tolerances_ResidentialDefaults_UsesFractions` | Default tolerances use correct fractional values |
| `InstallBlocker_MapsToCorrectSeverity` | All blockers map to Error severity validation issues |
| `InstallPlan_NeverMutatesInput` | Spatial and engineering results unchanged after projection |

### Integration Tests

| Test | Validates |
|---|---|
| `Pipeline_Stage8_ProducesInstallPlan` | Stage 8 writes result to `ResolutionContext.InstallResult` |
| `Pipeline_Stage8_BlockerHaltsPipeline` | Install blocker prevents Stage 9+ execution |
| `Pipeline_FullRun_EveryCAbinetHasInstallStep` | Every cabinet from design has a corresponding install step |
| `Snapshot_FrozenInstallPlan_Immutable` | Plan at ReadyForInstall cannot be regenerated |
| `Tolerances_FlowFromEngineering` | Engineering shim/scribe values reach install tolerances |

### Property-Based Tests

| Property | Validates |
|---|---|
| `AllCabinets_HaveAtLeastOneStep` | No cabinet is orphaned from the install plan |
| `AllDependencies_ReferenceValidSteps` | No orphaned dependency edges |
| `SequenceIndices_AreContiguous` | Indices are 0..n-1 with no gaps |
| `TopologicalOrder_RespectsAllDependencies` | For every dependency edge, prerequisite.SequenceIndex < dependent.SequenceIndex |
| `DeterministicPlan_SameInputSameOutput` | Repeated projections produce identical plans |

---

## 12. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| **Stud data not available for a wall** | `FasteningBackingStatus.Unknown` — does not block install planning but produces a warning. Field crew must verify before installation |
| **Circular dependency in graph** | Detected by topological sort (Kahn's algorithm). Reported as `InstallBlocker.CyclicDependency`. This indicates a logic bug in the dependency rule engine, not a user error |
| **Very large projects (100+ cabinets)** | Topological sort is O(V + E). Dependency graph construction is O(n^2) for adjacency checks but n is bounded by realistic room sizes (~50 cabinets max per room) |
| **Tall cabinet blocks fastening access to multiple neighbors** | Each interference pair creates its own `AccessConstraint` and dependency edge. The graph handles multi-fan dependencies naturally |
| **Corner cabinets with complex access** | Corner cabinets (lazy susan, blind corner) generate fastening zones on two walls. Each wall's zones are evaluated independently |
| **Filler between cabinets requires both neighbors installed** | Filler install steps depend on both adjacent cabinet steps via `InstallDependency`. The graph encodes this two-parent dependency |
| **Wall cabinets with no ledger board** | `InstallBlocker.MissingLedgerForWallCabinets` if the run includes wall cabinets but no ledger step is generated. Some shops use French cleats — the fastening type enum accommodates this via `HangingRail` |
| **Manufacturing not ready when install is planned** | `InstallBlocker.ManufacturingNotReady` — install planning requires manufacturing to be complete (Stage 7 must succeed before Stage 8) |
| **Revision state changes after install plan is frozen** | Impossible — frozen revisions at `ReadyForInstall` are immutable. Design changes create new revisions |
| **Same cabinet appears in multiple rooms** | Not possible by domain model — a cabinet belongs to exactly one run on exactly one wall |
