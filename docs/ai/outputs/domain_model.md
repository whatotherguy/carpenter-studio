# P3 — Domain Model Design

Source: `cabinet_ai_implementation_playbook_v1_1.md` (Phase 3)
Context: `architecture_summary.md`, `geometry_system.md`, `commands.md`

---

## 1. Goals

- Define the core domain entities, value objects, and aggregates for the cabinet design system
- Establish bounded context boundaries with explicit ownership of each entity
- Document invariants that the domain must enforce at all times
- Map entity relationships and aggregate boundaries to guide persistence and command handling
- Use geometry value objects everywhere — no primitive dimensions crossing domain boundaries
- Keep entities UI-independent, persistence-ignorant, and fully testable

---

## 2. Cross-Cutting Domain Abstractions

### 2.1 IClock

Domain entities must never call `DateTimeOffset.UtcNow` directly — doing so couples them to the system clock and makes them non-deterministic. The application layer injects timestamps; the domain declares the abstraction.

```csharp
namespace CabinetDesigner.Domain;

/// <summary>
/// Clock abstraction. Keeps domain entities deterministic and testable.
/// Implementation lives in Infrastructure. Test doubles provide fixed timestamps.
/// The application layer (orchestrator, command handlers) resolves IClock and
/// passes timestamps to domain entity methods as parameters.
/// </summary>
public interface IClock
{
    DateTimeOffset Now { get; }
}
```

### 2.2 OverrideValue

Override values appear in cabinet parameter overrides, shop standards, and template defaults. Raw `object` is prohibited — all override values must be drawn from this constrained union to ensure serialization safety and type correctness.

```csharp
namespace CabinetDesigner.Domain;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Constrained override value. Closed set of types permitted as parameter overrides,
/// shop standard values, or template defaults.
/// No raw object values cross domain boundaries.
/// </summary>
public abstract record OverrideValue
{
    public sealed record OfLength(Length Value) : OverrideValue;
    public sealed record OfThickness(Thickness Value) : OverrideValue;
    public sealed record OfAngle(Angle Value) : OverrideValue;
    public sealed record OfString(string Value) : OverrideValue;
    public sealed record OfBool(bool Value) : OverrideValue;
    public sealed record OfInt(int Value) : OverrideValue;
    public sealed record OfDecimal(decimal Value) : OverrideValue;
    public sealed record OfMaterialId(MaterialId Value) : OverrideValue;
    public sealed record OfHardwareItemId(HardwareItemId Value) : OverrideValue;
}
```

---

## 4. Bounded Contexts & Ownership

Each bounded context owns its entities exclusively. Cross-context references use typed IDs, never object references.

| Bounded Context | Owns | Depends On |
|---|---|---|
| **Project & Revision** | `Project`, `Revision`, `ApprovalState` | — |
| **Spatial Scene** | `Room`, `Wall`, `WallOpening`, `Obstacle` | Project |
| **Run Engine** | `CabinetRun`, `RunSlot`, `Filler`, `EndCondition` | Spatial Scene, Cabinet |
| **Cabinet & Assembly** | `Cabinet`, `CabinetType`, `Opening`, `DoorDrawerAssignment` | Geometry |
| **Material Catalog** | `Material`, `MaterialCategory`, `EdgeBanding` | Geometry (Thickness) |
| **Hardware Catalog** | `HardwareItem`, `HardwareConstraint`, `BoringPattern` | Material Catalog |
| **Template & Library** | `CabinetTemplate`, `StylePreset`, `ShopStandard` | Cabinet, Material |

---

## 5. Entity Identifiers

All entities use strongly typed IDs. Defined in `CabinetDesigner.Domain.Identifiers`.

```csharp
namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct ProjectId(Guid Value)
{
    public static ProjectId New() => new(Guid.NewGuid());
}

public readonly record struct RevisionId(Guid Value)
{
    public static RevisionId New() => new(Guid.NewGuid());
}

public readonly record struct RoomId(Guid Value)
{
    public static RoomId New() => new(Guid.NewGuid());
}

public readonly record struct WallId(Guid Value)
{
    public static WallId New() => new(Guid.NewGuid());
}

public readonly record struct WallOpeningId(Guid Value)
{
    public static WallOpeningId New() => new(Guid.NewGuid());
}

public readonly record struct ObstacleId(Guid Value)
{
    public static ObstacleId New() => new(Guid.NewGuid());
}

// RunId and CabinetId already defined in commands.md
// Re-exported here for completeness:
// public readonly record struct RunId(Guid Value);
// public readonly record struct CabinetId(Guid Value);

public readonly record struct RunSlotId(Guid Value)
{
    public static RunSlotId New() => new(Guid.NewGuid());
}

public readonly record struct FillerId(Guid Value)
{
    public static FillerId New() => new(Guid.NewGuid());
}

public readonly record struct OpeningId(Guid Value)
{
    public static OpeningId New() => new(Guid.NewGuid());
}

public readonly record struct MaterialId(Guid Value)
{
    public static MaterialId New() => new(Guid.NewGuid());
}

public readonly record struct HardwareItemId(Guid Value)
{
    public static HardwareItemId New() => new(Guid.NewGuid());
}

public readonly record struct TemplateId(Guid Value)
{
    public static TemplateId New() => new(Guid.NewGuid());
}
```

---

## 4. Project & Revision Context

### 4.1 Project (Aggregate Root)

The top-level container. One project = one job site / one customer engagement.

```csharp
namespace CabinetDesigner.Domain.ProjectContext;

public sealed class Project
{
    public ProjectId Id { get; }
    public string Name { get; private set; }
    public string? CustomerName { get; private set; }
    public string? JobSiteAddress { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset LastModifiedAt { get; private set; }

    private readonly List<Revision> _revisions = [];
    public IReadOnlyList<Revision> Revisions => _revisions;

    public Revision CurrentRevision =>
        _revisions.OrderByDescending(r => r.CreatedAt).First();

    /// <param name="createdAt">Provided by the application layer via IClock. Never call DateTimeOffset.UtcNow here.</param>
    public Project(ProjectId id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        LastModifiedAt = createdAt;

        // Every project starts with a draft revision
        _revisions.Add(Revision.CreateDraft(this.Id, createdAt));
    }

    public void UpdateName(string name, DateTimeOffset modifiedAt)
    {
        Name = name;
        Touch(modifiedAt);
    }

    public Revision CreateNewRevision(DateTimeOffset createdAt)
    {
        var revision = Revision.CreateDraft(Id, createdAt);
        _revisions.Add(revision);
        Touch(createdAt);
        return revision;
    }

    private void Touch(DateTimeOffset modifiedAt) => LastModifiedAt = modifiedAt;
}
```

### 4.2 Revision

A version of the design. Moves through approval states. Approved revisions are immutable snapshots.

```csharp
namespace CabinetDesigner.Domain.ProjectContext;

public enum ApprovalState
{
    Draft,
    UnderReview,
    Approved,
    LockedForManufacture,
    ReleasedToShop,
    ReadyForInstall,
    Installed,
    Superseded
}

public sealed class Revision
{
    public RevisionId Id { get; }
    public ProjectId ProjectId { get; }
    public int VersionNumber { get; }
    public ApprovalState State { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? ApprovalNotes { get; private set; }

    private Revision(RevisionId id, ProjectId projectId, DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        State = ApprovalState.Draft;
        CreatedAt = createdAt;
    }

    public static Revision CreateDraft(ProjectId projectId, DateTimeOffset createdAt)
        => new(RevisionId.New(), projectId, createdAt);

    // --- Invariant: state transitions must follow the defined order ---

    private static readonly Dictionary<ApprovalState, ApprovalState[]> _allowedTransitions = new()
    {
        [ApprovalState.Draft] = [ApprovalState.UnderReview],
        [ApprovalState.UnderReview] = [ApprovalState.Draft, ApprovalState.Approved],
        [ApprovalState.Approved] = [ApprovalState.LockedForManufacture, ApprovalState.Superseded],
        [ApprovalState.LockedForManufacture] = [ApprovalState.ReleasedToShop, ApprovalState.Superseded],
        [ApprovalState.ReleasedToShop] = [ApprovalState.ReadyForInstall],
        [ApprovalState.ReadyForInstall] = [ApprovalState.Installed],
        [ApprovalState.Installed] = [ApprovalState.Superseded],
        [ApprovalState.Superseded] = [],
    };

    /// <param name="transitionedAt">Provided by the application layer via IClock.</param>
    public void TransitionTo(ApprovalState newState, DateTimeOffset transitionedAt, string? notes = null)
    {
        if (!_allowedTransitions[State].Contains(newState))
            throw new InvalidOperationException(
                $"Cannot transition from {State} to {newState}.");

        State = newState;
        ApprovalNotes = notes;

        if (newState == ApprovalState.Approved)
            ApprovedAt = transitionedAt;
    }

    /// <summary>
    /// Invariant: only Draft revisions can be modified by design commands.
    /// </summary>
    public bool IsEditable => State == ApprovalState.Draft;
}
```

**Invariants:**
- A project always has at least one revision
- Only `Draft` revisions accept design commands
- State transitions follow the defined graph — no skipping states
- Approved revisions become immutable snapshots

---

## 5. Spatial Scene Context

### 5.1 Room (Aggregate Root)

The physical space being designed. Contains walls, openings, and obstacles.

```csharp
namespace CabinetDesigner.Domain.SpatialContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed class Room
{
    public RoomId Id { get; }
    public RevisionId RevisionId { get; }
    public string Name { get; private set; }
    public Length CeilingHeight { get; private set; }

    private readonly List<Wall> _walls = [];
    public IReadOnlyList<Wall> Walls => _walls;

    private readonly List<Obstacle> _obstacles = [];
    public IReadOnlyList<Obstacle> Obstacles => _obstacles;

    public Room(RoomId id, RevisionId revisionId, string name, Length ceilingHeight)
    {
        Id = id;
        RevisionId = revisionId;
        Name = name;
        CeilingHeight = ceilingHeight;
    }

    public Wall AddWall(Point2D start, Point2D end, Thickness wallThickness)
    {
        var wall = new Wall(WallId.New(), Id, start, end, wallThickness);
        _walls.Add(wall);
        return wall;
    }

    public Obstacle AddObstacle(Rect2D bounds, string description)
    {
        var obstacle = new Obstacle(ObstacleId.New(), Id, bounds, description);
        _obstacles.Add(obstacle);
        return obstacle;
    }

    /// <summary>Closed perimeter formed by wall segments.</summary>
    public bool IsEnclosed =>
        _walls.Count >= 3 && WallsFormClosedLoop();

    private bool WallsFormClosedLoop()
    {
        if (_walls.Count < 3) return false;
        // Simplified: checks that walls connect end-to-start in sequence
        for (int i = 0; i < _walls.Count; i++)
        {
            var current = _walls[i];
            var next = _walls[(i + 1) % _walls.Count];
            if (!GeometryTolerance.ApproximatelyEqual(
                current.EndPoint, next.StartPoint, GeometryTolerance.DefaultShopTolerance))
                return false;
        }
        return true;
    }
}
```

### 5.2 Wall

```csharp
namespace CabinetDesigner.Domain.SpatialContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed class Wall
{
    public WallId Id { get; }
    public RoomId RoomId { get; }
    public Point2D StartPoint { get; }
    public Point2D EndPoint { get; }
    public Thickness WallThickness { get; }

    private readonly List<WallOpening> _openings = [];
    public IReadOnlyList<WallOpening> Openings => _openings;

    public Wall(WallId id, RoomId roomId, Point2D start, Point2D end, Thickness wallThickness)
    {
        Id = id;
        RoomId = roomId;
        StartPoint = start;
        EndPoint = end;
        WallThickness = wallThickness;
    }

    public Length Length => StartPoint.DistanceTo(EndPoint);
    public Vector2D Direction => (EndPoint - StartPoint).Normalized();
    public LineSegment2D Segment => new(StartPoint, EndPoint);

    /// <summary>
    /// Available linear length after subtracting openings.
    /// Used by Run Engine to determine max run length.
    /// </summary>
    public Length AvailableLength
    {
        get
        {
            var totalOpeningWidth = _openings
                .Aggregate(Length.Zero, (sum, o) => sum + o.Width);
            var available = Length - totalOpeningWidth;
            return Length.Max(available, Length.Zero);
        }
    }

    public WallOpening AddOpening(WallOpeningType type, Length offsetFromStart, Length width, Length height, Length sillHeight)
    {
        var opening = new WallOpening(WallOpeningId.New(), Id, type, offsetFromStart, width, height, sillHeight);

        // Invariant: openings must not overlap
        if (HasOverlappingOpening(opening))
            throw new InvalidOperationException("Opening overlaps with an existing opening.");

        // Invariant: opening must fit within wall
        if (offsetFromStart + width > Length)
            throw new InvalidOperationException("Opening extends beyond wall length.");

        _openings.Add(opening);
        return opening;
    }

    private bool HasOverlappingOpening(WallOpening candidate)
    {
        foreach (var existing in _openings)
        {
            var existingEnd = existing.OffsetFromWallStart + existing.Width;
            var candidateEnd = candidate.OffsetFromWallStart + candidate.Width;

            bool overlaps =
                candidate.OffsetFromWallStart.Inches < existingEnd.Inches &&
                candidateEnd.Inches > existing.OffsetFromWallStart.Inches;

            if (overlaps) return true;
        }
        return false;
    }
}
```

### 5.3 WallOpening

```csharp
namespace CabinetDesigner.Domain.SpatialContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum WallOpeningType
{
    Window,
    Door,
    Passthrough,
    Archway
}

public sealed class WallOpening
{
    public WallOpeningId Id { get; }
    public WallId WallId { get; }
    public WallOpeningType Type { get; }
    public Length OffsetFromWallStart { get; }
    public Length Width { get; }
    public Length Height { get; }
    public Length SillHeight { get; }

    public WallOpening(
        WallOpeningId id, WallId wallId, WallOpeningType type,
        Length offsetFromStart, Length width, Length height, Length sillHeight)
    {
        Id = id;
        WallId = wallId;
        Type = type;
        OffsetFromWallStart = offsetFromStart;
        Width = width;
        Height = height;
        SillHeight = sillHeight;
    }

    /// <summary>
    /// True if cabinets can be placed below this opening (e.g., under a window).
    /// </summary>
    public bool AllowsCabinetsBelow => Type == WallOpeningType.Window;
}
```

### 5.4 Obstacle

```csharp
namespace CabinetDesigner.Domain.SpatialContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed class Obstacle
{
    public ObstacleId Id { get; }
    public RoomId RoomId { get; }
    public Rect2D Bounds { get; }
    public string Description { get; }

    public Obstacle(ObstacleId id, RoomId roomId, Rect2D bounds, string description)
    {
        Id = id;
        RoomId = roomId;
        Bounds = bounds;
        Description = description;
    }
}
```

**Spatial Scene Invariants:**
- Walls have non-zero length
- Openings must not overlap on the same wall
- Openings must fit within the wall's length
- A room belongs to exactly one revision

---

## 6. Run Engine Context

### 6.1 CabinetRun (Aggregate Root)

A run is a linear sequence of cabinets along a wall. It is the primary organizational unit for layout. The Run Engine resolves continuity, reveals, fillers, and end conditions at the run level.

```csharp
namespace CabinetDesigner.Domain.RunContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum EndConditionType
{
    Open,           // Run ends with exposed panel
    AgainstWall,    // Run terminates at a wall return
    Filler,         // Run ends with a filler strip
    Scribe          // Run ends with a scribe piece against irregular surface
}

public sealed class CabinetRun
{
    public RunId Id { get; }
    public WallId WallId { get; }

    /// <summary>
    /// Available length for this run, set by the spatial layer when the run is
    /// associated with a wall segment. The run itself does not store world-space
    /// endpoints — those belong to the spatial resolution stage.
    /// </summary>
    public Length Capacity { get; private set; }

    public EndCondition LeftEndCondition { get; private set; }
    public EndCondition RightEndCondition { get; private set; }

    private readonly List<RunSlot> _slots = [];
    public IReadOnlyList<RunSlot> Slots => _slots;

    public CabinetRun(RunId id, WallId wallId, Length capacity)
    {
        Id = id;
        WallId = wallId;
        Capacity = capacity;
        LeftEndCondition = EndCondition.Open();
        RightEndCondition = EndCondition.Open();
    }

    public void UpdateCapacity(Length newCapacity)
    {
        Capacity = newCapacity;
    }

    public Length OccupiedLength =>
        _slots.Aggregate(Length.Zero, (sum, s) => sum + s.OccupiedWidth);

    public Length RemainingLength
    {
        get
        {
            var remaining = Capacity - OccupiedLength;
            return Length.Max(remaining.Abs(), Length.Zero);
        }
    }

    public int CabinetCount => _slots.Count(s => s.SlotType == RunSlotType.Cabinet);

    // --- Slot management ---

    public RunSlot AppendCabinet(CabinetId cabinetId, Length nominalWidth)
    {
        var slot = RunSlot.ForCabinet(RunSlotId.New(), Id, cabinetId, nominalWidth, _slots.Count);
        ValidateSlotFits(slot);
        _slots.Add(slot);
        return slot;
    }

    public RunSlot InsertCabinetAt(int index, CabinetId cabinetId, Length nominalWidth)
    {
        if (index < 0 || index > _slots.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        var slot = RunSlot.ForCabinet(RunSlotId.New(), Id, cabinetId, nominalWidth, index);
        ValidateSlotFits(slot);

        _slots.Insert(index, slot);
        ReindexSlots();
        return slot;
    }

    public void RemoveSlot(RunSlotId slotId)
    {
        var index = _slots.FindIndex(s => s.Id == slotId);
        if (index < 0) throw new InvalidOperationException($"Slot {slotId} not found in run.");
        _slots.RemoveAt(index);
        ReindexSlots();
    }

    public RunSlot InsertFiller(int index, Length fillerWidth)
    {
        var slot = RunSlot.ForFiller(RunSlotId.New(), Id, fillerWidth, index);
        ValidateSlotFits(slot);
        _slots.Insert(index, slot);
        ReindexSlots();
        return slot;
    }

    public void SetLeftEndCondition(EndConditionType type, Length? fillerWidth = null)
    {
        LeftEndCondition = new EndCondition(type, fillerWidth);
    }

    public void SetRightEndCondition(EndConditionType type, Length? fillerWidth = null)
    {
        RightEndCondition = new EndCondition(type, fillerWidth);
    }

    // --- Queries ---

    /// <summary>
    /// Compute the run-local offset of a slot's left edge from slot 0.
    /// This is a run-internal calculation only — not world-space.
    /// World-space position is computed by the spatial resolution stage using RunPlacement.
    /// </summary>
    public Length SlotOffset(int slotIndex)
    {
        return _slots
            .Take(slotIndex)
            .Aggregate(Length.Zero, (sum, s) => sum + s.OccupiedWidth);
    }

    // --- Invariants ---

    private void ValidateSlotFits(RunSlot slot)
    {
        var projectedOccupied = OccupiedLength + slot.OccupiedWidth;
        if (projectedOccupied.Inches > Capacity.Inches)
            throw new InvalidOperationException(
                $"Slot width ({slot.OccupiedWidth}) exceeds remaining run capacity ({RemainingLength}).");
    }

    private void ReindexSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i] = _slots[i] with { SlotIndex = i };
    }
}
```

### 6.2 RunSlot

A slot in a run — either a cabinet or a filler. Slots are ordered and contiguous.

```csharp
namespace CabinetDesigner.Domain.RunContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum RunSlotType
{
    Cabinet,
    Filler
}

public sealed record RunSlot
{
    public RunSlotId Id { get; init; }
    public RunId RunId { get; init; }
    public RunSlotType SlotType { get; init; }
    public int SlotIndex { get; init; }
    public Length OccupiedWidth { get; init; }

    /// <summary>Non-null when SlotType == Cabinet.</summary>
    public CabinetId? CabinetId { get; init; }

    public static RunSlot ForCabinet(RunSlotId id, RunId runId, CabinetId cabinetId, Length width, int index) => new()
    {
        Id = id,
        RunId = runId,
        SlotType = RunSlotType.Cabinet,
        SlotIndex = index,
        OccupiedWidth = width,
        CabinetId = cabinetId
    };

    public static RunSlot ForFiller(RunSlotId id, RunId runId, Length width, int index) => new()
    {
        Id = id,
        RunId = runId,
        SlotType = RunSlotType.Filler,
        SlotIndex = index,
        OccupiedWidth = width,
        CabinetId = null
    };
}
```

### 6.3 EndCondition

```csharp
namespace CabinetDesigner.Domain.RunContext;

using CabinetDesigner.Domain.Geometry;

public sealed record EndCondition
{
    public EndConditionType Type { get; init; }
    public Length? FillerWidth { get; init; }

    public EndCondition(EndConditionType type, Length? fillerWidth = null)
    {
        Type = type;
        FillerWidth = fillerWidth;

        if (type == EndConditionType.Filler && fillerWidth is null)
            throw new ArgumentException("Filler end condition requires a filler width.");
    }

    public static EndCondition Open() => new(EndConditionType.Open);
    public static EndCondition AgainstWall() => new(EndConditionType.AgainstWall);
    public static EndCondition WithFiller(Length width) => new(EndConditionType.Filler, width);
    public static EndCondition WithScribe(Length width) => new(EndConditionType.Scribe, width);
}
```

**Run Engine Invariants:**
- Slots are contiguous — no gaps in the slot sequence
- Total occupied width of all slots cannot exceed run capacity
- Slot indices are always sequential (0..n-1)
- Filler end conditions require a width
- A run belongs to exactly one wall
- A cabinet can occupy at most one slot across all runs
- World-space position is NOT owned by `CabinetRun` — it belongs to the spatial resolution stage (see `RunPlacement` in `orchestrator.md`)

---

## 7. Cabinet & Assembly Context

### 7.1 CabinetType (Reference Data)

Defines a class of cabinet — not a placed instance. Think of it as the catalog entry.

```csharp
namespace CabinetDesigner.Domain.CabinetContext;

using CabinetDesigner.Domain.Geometry;

public enum CabinetCategory
{
    Base,
    Wall,
    Tall,
    Vanity,
    Specialty
}

public enum ConstructionMethod
{
    Frameless,      // European / full-overlay
    FaceFrame       // Traditional American
}

public sealed record CabinetType
{
    public string TypeId { get; init; }
    public string Name { get; init; }
    public CabinetCategory Category { get; init; }
    public ConstructionMethod Construction { get; init; }

    /// <summary>Standard nominal widths available for this type.</summary>
    public IReadOnlyList<Length> AvailableWidths { get; init; }

    /// <summary>Default depth for this category.</summary>
    public Length DefaultDepth { get; init; }

    /// <summary>Default height for this category.</summary>
    public Length DefaultHeight { get; init; }

    /// <summary>Number of openings (door/drawer bays) by default.</summary>
    public int DefaultOpeningCount { get; init; }

    /// <summary>True if this type can be placed under a window (e.g., sink base).</summary>
    public bool AllowsBelowOpening { get; init; }

    public CabinetType(
        string typeId, string name, CabinetCategory category,
        ConstructionMethod construction, IReadOnlyList<Length> availableWidths,
        Length defaultDepth, Length defaultHeight, int defaultOpeningCount,
        bool allowsBelowOpening = false)
    {
        TypeId = typeId;
        Name = name;
        Category = category;
        Construction = construction;
        AvailableWidths = availableWidths;
        DefaultDepth = defaultDepth;
        DefaultHeight = defaultHeight;
        DefaultOpeningCount = defaultOpeningCount;
        AllowsBelowOpening = allowsBelowOpening;
    }
}
```

### 7.2 Cabinet (Entity)

A placed instance of a cabinet type within a design. Owned by a revision, positioned via a run slot.

```csharp
namespace CabinetDesigner.Domain.CabinetContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed class Cabinet
{
    public CabinetId Id { get; }
    public RevisionId RevisionId { get; }
    public string CabinetTypeId { get; }
    public CabinetCategory Category { get; }
    public ConstructionMethod Construction { get; }

    // --- Dimensions (all geometry types, no primitives) ---
    public Length NominalWidth { get; private set; }
    public Length Depth { get; private set; }
    public Length Height { get; private set; }

    // --- Overrides (parameter hierarchy: cabinet-level) ---
    private readonly Dictionary<string, OverrideValue> _overrides = [];
    public IReadOnlyDictionary<string, OverrideValue> Overrides => _overrides;

    // --- Openings ---
    private readonly List<CabinetOpening> _openings = [];
    public IReadOnlyList<CabinetOpening> Openings => _openings;

    public Cabinet(
        CabinetId id, RevisionId revisionId, string cabinetTypeId,
        CabinetCategory category, ConstructionMethod construction,
        Length nominalWidth, Length depth, Length height)
    {
        Id = id;
        RevisionId = revisionId;
        CabinetTypeId = cabinetTypeId;
        Category = category;
        Construction = construction;
        NominalWidth = nominalWidth;
        Depth = depth;
        Height = height;
    }

    public void Resize(Length newWidth)
    {
        if (newWidth <= Length.Zero)
            throw new InvalidOperationException("Width must be positive.");
        NominalWidth = newWidth;
    }

    public CabinetOpening AddOpening(Length width, Length height, OpeningType type)
    {
        var opening = new CabinetOpening(OpeningId.New(), Id, width, height, type, _openings.Count);
        _openings.Add(opening);
        return opening;
    }

    public void SetOverride(string key, OverrideValue value)
    {
        _overrides[key] = value;
    }

    public void RemoveOverride(string key)
    {
        _overrides.Remove(key);
    }
}
```

### 7.3 CabinetOpening

An opening within a cabinet — a bay that receives a door, drawer, or remains open.

```csharp
namespace CabinetDesigner.Domain.CabinetContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum OpeningType
{
    SingleDoor,
    DoubleDoor,
    Drawer,
    DrawerBank,
    Open,
    Appliance,
    FalseFront
}

public sealed class CabinetOpening
{
    public OpeningId Id { get; }
    public CabinetId CabinetId { get; }
    public Length Width { get; private set; }
    public Length Height { get; private set; }
    public OpeningType Type { get; private set; }
    public int Index { get; }

    /// <summary>Hardware assignments for this opening (hinges, slides, etc.).</summary>
    private readonly List<HardwareItemId> _hardwareIds = [];
    public IReadOnlyList<HardwareItemId> HardwareIds => _hardwareIds;

    /// <summary>Material for the door/drawer front assigned to this opening.</summary>
    public MaterialId? FrontMaterialId { get; private set; }

    public CabinetOpening(
        OpeningId id, CabinetId cabinetId, Length width, Length height,
        OpeningType type, int index)
    {
        Id = id;
        CabinetId = cabinetId;
        Width = width;
        Height = height;
        Type = type;
        Index = index;
    }

    public void ChangeType(OpeningType newType)
    {
        Type = newType;
        // Hardware may need re-evaluation — but that's resolution's job, not ours
    }

    public void AssignFrontMaterial(MaterialId materialId)
    {
        FrontMaterialId = materialId;
    }

    public void AssignHardware(HardwareItemId hardwareId)
    {
        if (!_hardwareIds.Contains(hardwareId))
            _hardwareIds.Add(hardwareId);
    }
}
```

**Cabinet & Assembly Invariants:**
- Cabinet width, depth, height must be positive
- Openings belong to exactly one cabinet
- Opening indices are sequential within a cabinet
- A cabinet's type ID must reference a valid `CabinetType`

---

## 8. Material Catalog Context

### 8.1 Material

```csharp
namespace CabinetDesigner.Domain.MaterialContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum MaterialCategory
{
    Plywood,
    Melamine,
    MDF,
    Particleboard,
    HardwoodSolid,
    Veneer,
    Laminate
}

public enum GrainDirection
{
    None,           // No grain (melamine, laminate, MDF)
    LengthWise,     // Grain runs along the long dimension
    WidthWise       // Grain runs across — rare, but sometimes needed
}

public sealed class Material
{
    public MaterialId Id { get; }
    public string Name { get; }
    public string? Sku { get; }
    public MaterialCategory Category { get; }
    public Thickness SheetThickness { get; }
    public GrainDirection Grain { get; }

    /// <summary>Standard sheet size (for nesting / yield calculations).</summary>
    public Length SheetWidth { get; }
    public Length SheetHeight { get; }

    public Material(
        MaterialId id, string name, string? sku,
        MaterialCategory category, Thickness sheetThickness,
        GrainDirection grain, Length sheetWidth, Length sheetHeight)
    {
        Id = id;
        Name = name;
        Sku = sku;
        Category = category;
        SheetThickness = sheetThickness;
        Grain = grain;
        SheetWidth = sheetWidth;
        SheetHeight = sheetHeight;
    }
}
```

### 8.2 EdgeBanding

```csharp
namespace CabinetDesigner.Domain.MaterialContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record EdgeBanding(
    string EdgeBandingId,
    string Name,
    Thickness Thickness,
    Length Width,
    MaterialId? MatchesMaterialId);
```

**Material Catalog Invariants:**
- Thickness uses `Thickness` value object (nominal + actual) — never raw decimals
- Sheet dimensions must be positive
- Grain direction must be consistent with material category (e.g., MDF = None)

---

## 9. Hardware Catalog Context

```csharp
namespace CabinetDesigner.Domain.HardwareContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public enum HardwareCategory
{
    Hinge,
    DrawerSlide,
    Pull,
    Knob,
    Shelf,
    ShelfPin,
    Bracket,
    Connector
}

public sealed class HardwareItem
{
    public HardwareItemId Id { get; }
    public string Name { get; }
    public string? ManufacturerSku { get; }
    public HardwareCategory Category { get; }

    /// <summary>Minimum opening width/height this hardware supports.</summary>
    public Length? MinOpeningWidth { get; }
    public Length? MaxOpeningWidth { get; }

    /// <summary>Required boring pattern (for hinges, slides).</summary>
    public BoringPattern? BoringPattern { get; }

    /// <summary>Required clearance from edges.</summary>
    public Length? RequiredClearance { get; }

    public HardwareItem(
        HardwareItemId id, string name, string? sku,
        HardwareCategory category,
        Length? minOpeningWidth = null, Length? maxOpeningWidth = null,
        BoringPattern? boringPattern = null, Length? requiredClearance = null)
    {
        Id = id;
        Name = name;
        ManufacturerSku = sku;
        Category = category;
        MinOpeningWidth = minOpeningWidth;
        MaxOpeningWidth = maxOpeningWidth;
        BoringPattern = boringPattern;
        RequiredClearance = requiredClearance;
    }
}

public sealed record BoringPattern(
    Length HoleSpacing,
    Length EdgeSetback,
    int HoleCount);
```

**Hardware Invariants:**
- If min/max opening width are both set, min <= max
- Boring pattern hole spacing and edge setback must be positive
- Hardware constraints are evaluated during resolution, not at entity level

---

## 10. Template & Library Context

```csharp
namespace CabinetDesigner.Domain.TemplateContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

public sealed record CabinetTemplate(
    TemplateId Id,
    string Name,
    string CabinetTypeId,
    Length DefaultWidth,
    Length DefaultDepth,
    Length DefaultHeight,
    IReadOnlyDictionary<string, OverrideValue> DefaultOverrides);

public sealed record StylePreset(
    string PresetId,
    string Name,
    MaterialId? DefaultCaseMaterialId,
    MaterialId? DefaultFrontMaterialId,
    string? DefaultEdgeBandingId,
    string? DefaultHardwareProfileId);

public sealed record ShopStandard(
    string StandardId,
    string Name,
    IReadOnlyDictionary<string, OverrideValue> Parameters);
```

---

## 11. Entity Relationship Map

```
Project (1)
 └── Revision (many)
      └── Room (many)
           ├── Wall (many)
           │    ├── WallOpening (many)
           │    └── CabinetRun (many) ←── Run Engine aggregate root
           │         └── RunSlot (ordered list)
           │              └── refers to → Cabinet (by CabinetId)
           │
           └── Obstacle (many)

Cabinet (1)
 ├── refers to → CabinetType (by TypeId)
 ├── CabinetOpening (many, ordered)
 │    ├── refers to → Material (by FrontMaterialId)
 │    └── refers to → HardwareItem (by HardwareIds)
 └── Overrides (key-value)

Material (standalone catalog)
 └── EdgeBanding (associated)

HardwareItem (standalone catalog)
 └── BoringPattern (value object)

CabinetTemplate (standalone library)
StylePreset (standalone library)
ShopStandard (standalone library)
```

### Cross-Context References

| From | To | Via |
|---|---|---|
| `CabinetRun` | `Wall` | `WallId` |
| `RunSlot` | `Cabinet` | `CabinetId` |
| `Cabinet` | `CabinetType` | `CabinetTypeId` (string) |
| `CabinetOpening` | `Material` | `MaterialId` |
| `CabinetOpening` | `HardwareItem` | `HardwareItemId` |
| `CabinetTemplate` | `CabinetType` | `CabinetTypeId` (string) |
| `StylePreset` | `Material` | `MaterialId` |

All cross-context references are by typed ID — never by object reference.

---

## 12. Aggregate Boundaries

| Aggregate Root | Contains | Consistency Boundary |
|---|---|---|
| `Project` | `Revision` | Project metadata + revision list |
| `Room` | `Wall`, `WallOpening`, `Obstacle` | Room geometry + wall/opening layout |
| `CabinetRun` | `RunSlot`, `EndCondition` | Slot ordering, total width, end conditions |
| `Cabinet` | `CabinetOpening` | Cabinet dimensions, openings, overrides |

**Rules:**
- State changes within an aggregate are atomic
- Cross-aggregate changes require the orchestrator to coordinate
- Aggregates reference each other only by ID
- The `ResolutionOrchestrator` is the only code that may modify multiple aggregates in a single operation

---

## 13. Invariant Summary

| Context | Invariant | Enforced By |
|---|---|---|
| Project | At least one revision exists | `Project` constructor |
| Revision | Only `Draft` revisions accept design commands | `Revision.IsEditable` check |
| Revision | State transitions follow defined graph | `Revision.TransitionTo()` |
| Spatial | Openings do not overlap on a wall | `Wall.AddOpening()` |
| Spatial | Openings fit within wall length | `Wall.AddOpening()` |
| Run | Total slot width <= run capacity | `CabinetRun.ValidateSlotFits()` |
| Run | Slot indices are contiguous 0..n-1 | `CabinetRun.ReindexSlots()` |
| Run | Filler end conditions require width | `EndCondition` constructor |
| Cabinet | Dimensions are positive | `Cabinet.Resize()` / constructor |
| Material | Thickness uses `Thickness` (nominal + actual) | Type system |
| Hardware | Min opening width <= max if both set | Constructor validation |
| All | No primitive dimensions at domain boundaries | Geometry type system |
| All | Cross-context references by typed ID only | Typed ID structs |

---

## 14. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| **Run slot reindexing on insert/remove** | Slots use a mutable `SlotIndex` via `with` expression on the record. Reindexing is O(n) but runs are small (typically <20 slots) |
| **Cabinet referenced by multiple runs** | Invariant: a cabinet occupies at most one slot. Enforced at the application layer by the orchestrator, not by the cabinet entity itself (it has no knowledge of runs) |
| **CabinetType as string ID vs typed ID** | String chosen because types are reference/catalog data loaded from templates/config, not user-created entities. If type management becomes dynamic, migrate to a typed ID |
| **Material assignment before catalog is loaded** | `MaterialId` references are nullable on openings. Resolution pipeline validates that assigned materials exist. Missing materials produce validation warnings |
| **Aggregate size — Room with many walls** | Rooms in residential cabinet work rarely exceed 8-10 walls. If commercial use cases emerge, consider splitting wall storage |
| **Override dictionary types** | Resolved via `OverrideValue` discriminated union. Non-serializable types are a compile-time error. |
| **Concurrent modification of the same aggregate** | Single-user desktop app — not a concern for MVP. If multi-user editing is added later, optimistic concurrency on aggregate version numbers |
| **Run capacity change after wall geometry changes** | When a wall segment's available length changes, `CabinetRun.UpdateCapacity()` is called by the orchestrator. Run-local slot math remains valid; spatial layer recomputes world-space positions. |
