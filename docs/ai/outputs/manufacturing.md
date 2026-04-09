# P7 — Manufacturing Projection Layer Design

Source: `cabinet_ai_prompt_pack_v4_1_full.md` (Phase 7)
Context: `architecture_summary.md`, `domain_model.md`, `orchestrator.md`, `validation_engine.md`

---

## 1. Goals

- Define a manufacturing projection layer that transforms resolved design state into shop-ready output
- Keep manufacturing as a **pure projection** — it derives from design state and never mutates it
- Model the full path from resolved parts to cut lists, machining operations, and export artifacts
- Handle actual vs nominal thickness throughout, ensuring shop output reflects real material dimensions
- Group parts by material, thickness, grain, and process for efficient production
- Define manufacturing readiness criteria and blockers that prevent premature release to shop
- Maintain traceability from every manufactured part back to its source cabinet, run, and revision
- Support deterministic output — same resolved state = same manufacturing plan, always

---

## 2. Design Decisions

| Decision | Rationale |
|---|---|
| Manufacturing is a projection, not a source of truth | Architecture guardrail: manufacturing output is derived only, must never mutate design state. The design is the source of truth; manufacturing is a deterministic function of it |
| Manufacturing plan is computed at Stage 7 of the pipeline | Runs after Part Generation (Stage 6) which provides the resolved part graph. Manufacturing planning transforms parts into shop-ready groupings |
| All dimensions use geometry value objects | No primitive floats or decimals for dimensions. `Length`, `Thickness`, `Angle` throughout. Actual thickness — not nominal — drives all manufacturing math |
| Parts are grouped by material and process | A shop does not cut one cabinet at a time — it batches by sheet good, by solid stock, by edge treatment. The manufacturing model reflects real shop workflow |
| Machining operations are intent-based, not machine-specific | `ToolpathIntent` describes *what* to do (bore hinge holes, route edge profile) not *how* (G-code, specific CNC post). Post-processors live in `CabinetDesigner.Exports`, not here |
| Kerf and waste are modeled explicitly | Saw kerf affects yield. The manufacturing plan accounts for kerf when computing sheet utilization. Kerf is a shop-level parameter, not a material property |
| Manufacturing ties to immutable approved snapshots | Once a revision is approved and locked for manufacture, the manufacturing plan is frozen with it. Working-state manufacturing plans are preview-only |
| Nesting is an abstraction boundary, not a solver | This layer defines nesting *contracts* (parts that need placement on sheets). Actual nesting optimization is a downstream concern in `CabinetDesigner.Exports` or a future integration |

---

## 3. Boundaries

### In Scope

- Manufacturing data model (plans, cut list items, machining operations)
- Projection flow from resolved part graph to manufacturing plan
- Material/thickness/grain grouping logic
- Edge treatment planning
- Machining intent abstraction
- Readiness criteria and blocker detection
- Traceability identifiers
- Export artifact contracts

### Out of Scope

- Changing design state (projection only)
- UI implementation (presentation layer concern)
- Persistence implementation (persistence layer concern)
- Specific machine post-processors (exports layer concern)
- Nesting solver algorithms (exports/integrations concern)
- Actual G-code or CNC program generation

---

## 4. Manufacturing Data Model

### 4.1 Identifiers

```csharp
namespace CabinetDesigner.Domain.Identifiers;

public readonly record struct ManufacturingPlanId(Guid Value)
{
    public static ManufacturingPlanId New() => new(Guid.NewGuid());
}

public readonly record struct CutListItemId(Guid Value)
{
    public static CutListItemId New() => new(Guid.NewGuid());
}

public readonly record struct MachiningOperationId(Guid Value)
{
    public static MachiningOperationId New() => new(Guid.NewGuid());
}
```

### 4.2 ManufacturingPlan

The top-level manufacturing output for a revision. Contains all parts grouped for production.

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Complete manufacturing plan for a revision. Pure projection — computed from
/// resolved part graph, never persisted as source of truth.
/// Frozen with the revision snapshot when approved.
/// </summary>
public sealed class ManufacturingPlan
{
    public ManufacturingPlanId Id { get; }
    public RevisionId RevisionId { get; }
    public DateTimeOffset GeneratedAt { get; }

    /// <summary>Sheet good parts grouped by material/thickness.</summary>
    private readonly List<SheetGoodGroup> _sheetGoodGroups;
    public IReadOnlyList<SheetGoodGroup> SheetGoodGroups => _sheetGoodGroups;

    /// <summary>Solid stock parts grouped by species/dimension.</summary>
    private readonly List<SolidStockGroup> _solidStockGroups;
    public IReadOnlyList<SolidStockGroup> SolidStockGroups => _solidStockGroups;

    /// <summary>Edge banding requirements aggregated across all parts.</summary>
    private readonly List<EdgeBandingRequirement> _edgeBandingRequirements;
    public IReadOnlyList<EdgeBandingRequirement> EdgeBandingRequirements => _edgeBandingRequirements;

    /// <summary>All machining operations across all parts.</summary>
    private readonly List<MachiningOperation> _machiningOperations;
    public IReadOnlyList<MachiningOperation> MachiningOperations => _machiningOperations;

    /// <summary>Manufacturing readiness assessment.</summary>
    public ManufacturingReadinessResult Readiness { get; }

    /// <summary>Flat cut list for export.</summary>
    public IReadOnlyList<CutListItem> CutList { get; }

    /// <summary>Shop-level parameters used to generate this plan.</summary>
    public ShopParameters ShopParameters { get; }

    public ManufacturingPlan(
        ManufacturingPlanId id,
        RevisionId revisionId,
        DateTimeOffset generatedAt,
        IReadOnlyList<SheetGoodGroup> sheetGoodGroups,
        IReadOnlyList<SolidStockGroup> solidStockGroups,
        IReadOnlyList<EdgeBandingRequirement> edgeBandingRequirements,
        IReadOnlyList<MachiningOperation> machiningOperations,
        ManufacturingReadinessResult readiness,
        IReadOnlyList<CutListItem> cutList,
        ShopParameters shopParameters)
    {
        Id = id;
        RevisionId = revisionId;
        GeneratedAt = generatedAt;
        _sheetGoodGroups = sheetGoodGroups.ToList();
        _solidStockGroups = solidStockGroups.ToList();
        _edgeBandingRequirements = edgeBandingRequirements.ToList();
        _machiningOperations = machiningOperations.ToList();
        Readiness = readiness;
        CutList = cutList;
        ShopParameters = shopParameters;
    }

    /// <summary>Total distinct sheet good parts across all groups.</summary>
    public int TotalSheetParts => _sheetGoodGroups.Sum(g => g.Parts.Count);

    /// <summary>Total distinct solid stock parts across all groups.</summary>
    public int TotalSolidParts => _solidStockGroups.Sum(g => g.Parts.Count);
}
```

### 4.3 ShopParameters

Shop-level configuration that affects manufacturing output. Not part of design state — injected by the application layer.

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Shop-level parameters that affect manufacturing calculations.
/// Injected at plan generation time, not stored in design state.
/// </summary>
public sealed record ShopParameters
{
    /// <summary>Saw blade kerf width. Affects yield calculations.</summary>
    public required Length SawKerf { get; init; }

    /// <summary>CNC tool diameter for routing operations.</summary>
    public required Length CncToolDiameter { get; init; }

    /// <summary>Minimum part dimension the shop can reliably cut.</summary>
    public required Length MinPartDimension { get; init; }

    /// <summary>Maximum sheet dimension the shop's saw can handle.</summary>
    public required Length MaxSawCapacity { get; init; }

    /// <summary>Edge trimming allowance per edge for sheet goods.</summary>
    public required Length EdgeTrimAllowance { get; init; }
}
```

---

## 5. Projection Flow

The manufacturing projection runs as Stage 7 of the resolution pipeline. It consumes the output of Stage 6 (Part Generation) and produces a `ManufacturingPlan`.

```
Stage 6: Part Generation
    │
    │  PartGenerationResult
    │  (resolved parts with material assignments,
    │   dimensions, edge treatments, hardware)
    │
    ▼
┌─────────────────────────────────────────────────────────┐
│  Stage 7: Manufacturing Planning                         │
│                                                          │
│  Step 1: Classify parts by process                       │
│          → SheetGoodPart vs SolidStockPart                │
│                                                          │
│  Step 2: Group by material + actual thickness + grain     │
│          → SheetGoodGroup / SolidStockGroup               │
│                                                          │
│  Step 3: Compute edge banding requirements               │
│          → EdgeBandingRequirement per group               │
│                                                          │
│  Step 4: Generate machining operations                   │
│          → MachiningOperation with ToolpathIntent         │
│                                                          │
│  Step 5: Build flat cut list                             │
│          → CutListItem per part (for export)             │
│                                                          │
│  Step 6: Assess manufacturing readiness                  │
│          → ManufacturingReadinessResult                   │
│                                                          │
│  Step 7: Assemble ManufacturingPlan                      │
│          → Complete plan with traceability                │
└─────────────────────────────────────────────────────────┘
    │
    │  ManufacturingPlanResult
    │  (set on ResolutionContext.ManufacturingResult)
    │
    ▼
Stage 8: Install Planning
```

### 5.1 Manufacturing Planning Stage

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.ManufacturingContext;

/// <summary>
/// Stage 7: Manufacturing Planning.
/// Transforms resolved parts into a shop-ready manufacturing plan.
/// Pure projection — reads from PartResult, writes ManufacturingResult.
/// </summary>
public sealed class ManufacturingPlanningStage : IResolutionStage
{
    public int StageNumber => 7;
    public string StageName => "Manufacturing Planning";

    private readonly IManufacturingProjector _projector;

    public ManufacturingPlanningStage(IManufacturingProjector projector)
    {
        _projector = projector;
    }

    public StageResult Execute(ResolutionContext context)
    {
        var plan = _projector.Project(
            context.PartResult,
            context.ConstraintResult,
            context.Command);

        context.ManufacturingResult = new ManufacturingPlanResult(plan);

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

### 5.2 Manufacturing Projector Interface

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;

/// <summary>
/// Transforms resolved part data into a manufacturing plan.
/// Stateless projection — no side effects, deterministic output.
/// </summary>
public interface IManufacturingProjector
{
    ManufacturingPlan Project(
        PartGenerationResult partResult,
        ConstraintPropagationResult constraintResult,
        IDesignCommand sourceCommand);
}
```

---

## 6. Cut List Model

### 6.1 CutListItem

The flat, exportable representation of a single part for the cut list.

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A single item on the cut list. Flat representation optimized for
/// shop floor readability and export to spreadsheet/label systems.
/// Uses actual dimensions — not nominal — for all measurements.
/// </summary>
public sealed record CutListItem
{
    public CutListItemId Id { get; init; }

    // --- Traceability ---
    public required PartTraceability Traceability { get; init; }

    // --- Part identification ---
    public required string PartLabel { get; init; }
    public required string Description { get; init; }
    public required int Quantity { get; init; }

    // --- Dimensions (actual, not nominal) ---
    public required Length ActualLength { get; init; }
    public required Length ActualWidth { get; init; }
    public required Thickness MaterialThickness { get; init; }

    // --- Material ---
    public required MaterialId MaterialId { get; init; }
    public required string MaterialName { get; init; }
    public required GrainDirection GrainDirection { get; init; }

    // --- Edge treatment ---
    public required EdgeTreatment EdgeTreatment { get; init; }

    // --- Process routing ---
    public required ProcessType PrimaryProcess { get; init; }
    public required bool RequiresMachining { get; init; }

    // --- Notes ---
    public string? ShopNotes { get; init; }
}

/// <summary>
/// Describes which edges of a rectangular part receive edge banding.
/// </summary>
public sealed record EdgeTreatment
{
    public required EdgeBandingSpec? Top { get; init; }
    public required EdgeBandingSpec? Bottom { get; init; }
    public required EdgeBandingSpec? Left { get; init; }
    public required EdgeBandingSpec? Right { get; init; }

    public bool HasAnyBanding =>
        Top is not null || Bottom is not null ||
        Left is not null || Right is not null;

    public int BandedEdgeCount =>
        (Top is not null ? 1 : 0) + (Bottom is not null ? 1 : 0) +
        (Left is not null ? 1 : 0) + (Right is not null ? 1 : 0);

    public static EdgeTreatment None => new()
    {
        Top = null, Bottom = null, Left = null, Right = null
    };
}

/// <summary>
/// Edge banding specification for a single edge.
/// </summary>
public sealed record EdgeBandingSpec(
    string EdgeBandingId,
    string Name,
    Thickness BandingThickness);

/// <summary>
/// Primary manufacturing process for a part.
/// </summary>
public enum ProcessType
{
    /// <summary>Table saw / panel saw cut from sheet goods.</summary>
    SawCut,

    /// <summary>CNC router — for parts requiring boring, profiling, or shaping.</summary>
    CncRoute,

    /// <summary>Solid stock milling — rip, crosscut, plane, joint.</summary>
    SolidMill,

    /// <summary>Edge banding machine — automated edge application.</summary>
    EdgeBand,

    /// <summary>Assembly — parts that are assembled from sub-components.</summary>
    Assembly
}
```

### 6.2 PartTraceability

Every manufactured part traces back to its source in the design.

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Links a manufactured part back to its source in the design.
/// Every CutListItem, SheetGoodPart, and SolidStockPart carries this.
/// Enables "where did this part come from?" queries on the shop floor.
/// </summary>
public sealed record PartTraceability
{
    /// <summary>The revision this part belongs to.</summary>
    public required RevisionId RevisionId { get; init; }

    /// <summary>The cabinet this part belongs to.</summary>
    public required CabinetId CabinetId { get; init; }

    /// <summary>The run containing the cabinet (if applicable).</summary>
    public RunId? RunId { get; init; }

    /// <summary>
    /// The specific opening this part is associated with (doors, drawer fronts).
    /// Null for case parts (sides, top, bottom, back).
    /// </summary>
    public OpeningId? OpeningId { get; init; }

    /// <summary>
    /// Human-readable label for shop floor identification.
    /// Format: "{ProjectName}-{RevisionNumber}-{CabinetLabel}-{PartRole}"
    /// Example: "KitchenReno-R3-B12-LeftSide"
    /// </summary>
    public required string ShopLabel { get; init; }
}
```

---

## 7. Sheet Good & Solid Stock Models

### 7.1 SheetGoodPart

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A part cut from sheet material (plywood, melamine, MDF, etc.).
/// Dimensions are actual — actual thickness from the material, actual
/// length/width after accounting for construction method and joinery.
/// </summary>
public sealed record SheetGoodPart
{
    public CutListItemId Id { get; init; }
    public required PartTraceability Traceability { get; init; }

    public required string PartLabel { get; init; }
    public required PartRole Role { get; init; }

    // --- Dimensions (actual) ---
    public required Length ActualLength { get; init; }
    public required Length ActualWidth { get; init; }

    // --- Material ---
    public required MaterialId MaterialId { get; init; }
    public required Thickness MaterialThickness { get; init; }
    public required GrainDirection GrainDirection { get; init; }

    /// <summary>
    /// If grain is directional, this indicates whether the grain runs
    /// along the length or width of this specific part.
    /// Critical for nesting: grain-sensitive parts constrain rotation.
    /// </summary>
    public required GrainOrientation GrainOrientation { get; init; }

    // --- Edge treatment ---
    public required EdgeTreatment EdgeTreatment { get; init; }

    // --- Machining ---
    public required IReadOnlyList<MachiningOperationId> MachiningOperationIds { get; init; }

    /// <summary>Area in square inches for yield/waste calculation.</summary>
    public decimal AreaSquareInches =>
        ActualLength.Inches * ActualWidth.Inches;
}

/// <summary>
/// What structural role this part plays in the cabinet.
/// </summary>
public enum PartRole
{
    LeftSide,
    RightSide,
    Top,
    Bottom,
    Back,
    Shelf,
    FixedShelf,
    DoorFront,
    DrawerFront,
    DrawerBox_Side,
    DrawerBox_Front,
    DrawerBox_Back,
    DrawerBox_Bottom,
    FaceFrame_Rail,
    FaceFrame_Stile,
    FillerStrip,
    EndPanel,
    KickBoard,
    Stretcher,
    NailingStrip
}

/// <summary>
/// How grain aligns on this specific part instance.
/// </summary>
public enum GrainOrientation
{
    /// <summary>No grain constraint — part can be rotated freely during nesting.</summary>
    None,

    /// <summary>Grain runs along the part's length dimension.</summary>
    AlongLength,

    /// <summary>Grain runs along the part's width dimension.</summary>
    AlongWidth
}
```

### 7.2 SheetGoodGroup

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Parts grouped by material, actual thickness, and grain direction.
/// One group = one batch on the saw or CNC router.
/// Grouping key: (MaterialId, ActualThickness, GrainDirection).
/// </summary>
public sealed class SheetGoodGroup
{
    public required MaterialId MaterialId { get; init; }
    public required string MaterialName { get; init; }
    public required Thickness MaterialThickness { get; init; }
    public required GrainDirection GrainDirection { get; init; }

    private readonly List<SheetGoodPart> _parts;
    public IReadOnlyList<SheetGoodPart> Parts => _parts;

    /// <summary>Standard sheet size for this material.</summary>
    public required Length SheetWidth { get; init; }
    public required Length SheetHeight { get; init; }

    public SheetGoodGroup(IEnumerable<SheetGoodPart> parts)
    {
        _parts = parts.ToList();
    }

    /// <summary>Total area of all parts in this group (square inches).</summary>
    public decimal TotalPartArea => _parts.Sum(p => p.AreaSquareInches);

    /// <summary>Usable area of a single sheet (square inches).</summary>
    public decimal SheetArea => SheetWidth.Inches * SheetHeight.Inches;

    /// <summary>
    /// Minimum sheets required (naive estimate — does not account for
    /// kerf, waste, or grain constraints). Actual sheet count comes
    /// from the nesting solver in the export layer.
    /// </summary>
    public int MinimumSheetsEstimate =>
        SheetArea > 0 ? (int)Math.Ceiling(TotalPartArea / SheetArea) : 0;
}
```

### 7.3 SolidStockPart

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A part milled from solid stock (hardwood rails, stiles, edge strips, etc.).
/// Dimensions include milling allowance — the part starts oversized and is
/// milled to final dimension.
/// </summary>
public sealed record SolidStockPart
{
    public CutListItemId Id { get; init; }
    public required PartTraceability Traceability { get; init; }

    public required string PartLabel { get; init; }
    public required PartRole Role { get; init; }

    // --- Finished dimensions ---
    public required Length FinishedLength { get; init; }
    public required Length FinishedWidth { get; init; }
    public required Thickness FinishedThickness { get; init; }

    // --- Rough dimensions (includes milling allowance) ---
    public required Length RoughLength { get; init; }
    public required Length RoughWidth { get; init; }
    public required Thickness RoughThickness { get; init; }

    // --- Material ---
    public required MaterialId MaterialId { get; init; }
    public required string Species { get; init; }

    // --- Machining ---
    public required IReadOnlyList<MachiningOperationId> MachiningOperationIds { get; init; }
}
```

### 7.4 SolidStockGroup

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Solid stock parts grouped by species and rough thickness.
/// One group = one batch at the planer/jointer.
/// </summary>
public sealed class SolidStockGroup
{
    public required MaterialId MaterialId { get; init; }
    public required string Species { get; init; }
    public required Thickness RoughThickness { get; init; }

    private readonly List<SolidStockPart> _parts;
    public IReadOnlyList<SolidStockPart> Parts => _parts;

    public SolidStockGroup(IEnumerable<SolidStockPart> parts)
    {
        _parts = parts.ToList();
    }

    /// <summary>Total linear feet of rough stock needed.</summary>
    public decimal TotalLinearFeet =>
        _parts.Sum(p => p.RoughLength.Inches) / 12m;
}
```

---

## 8. Machining Model

### 8.1 MachiningOperation

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A machining operation to be performed on a part.
/// Describes intent, not machine-specific instructions.
/// Post-processors in the export layer translate intent to machine code.
/// </summary>
public sealed record MachiningOperation
{
    public MachiningOperationId Id { get; init; }

    /// <summary>The part this operation applies to.</summary>
    public required CutListItemId PartId { get; init; }

    /// <summary>What to do.</summary>
    public required ToolpathIntent Intent { get; init; }

    /// <summary>Traceability back to the design element that requires this operation.</summary>
    public required PartTraceability Traceability { get; init; }

    /// <summary>Order of operations for this part (0-based).</summary>
    public required int OperationSequence { get; init; }
}
```

### 8.2 ToolpathIntent

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Describes what a machining operation should achieve, not how.
/// Each variant carries the parameters needed to define the operation.
/// Post-processors translate these to machine-specific instructions.
/// </summary>
public abstract record ToolpathIntent
{
    /// <summary>Drill a pattern of holes (hinge boring, shelf pins, system holes).</summary>
    public sealed record BoreHoles(
        IReadOnlyList<HoleSpec> Holes) : ToolpathIntent;

    /// <summary>Route a dado or groove (for backs, dividers, drawer bottoms).</summary>
    public sealed record RouteDado(
        Length OffsetFromEdge,
        Length Width,
        Length Depth,
        DadoOrientation Orientation) : ToolpathIntent;

    /// <summary>Route a rabbet (for back panels, lid lips).</summary>
    public sealed record RouteRabbet(
        RabbetEdge Edge,
        Length Width,
        Length Depth) : ToolpathIntent;

    /// <summary>Profile an edge (roundover, chamfer, ogee).</summary>
    public sealed record ProfileEdge(
        ProfileEdgeLocation Edge,
        EdgeProfileType ProfileType,
        Length ProfileDepth) : ToolpathIntent;

    /// <summary>Cut a shape (curved components, arched doors).</summary>
    public sealed record ContourCut(
        IReadOnlyList<Point2D> ContourPoints) : ToolpathIntent;

    /// <summary>Pocket cut (recessed hardware, cable management).</summary>
    public sealed record PocketCut(
        Point2D Origin,
        Length PocketWidth,
        Length PocketHeight,
        Length PocketDepth) : ToolpathIntent;
}

/// <summary>
/// Specification for a single hole in a boring pattern.
/// </summary>
public sealed record HoleSpec(
    Point2D Position,
    Length Diameter,
    Length Depth,
    bool ThroughHole);

public enum DadoOrientation
{
    Horizontal,
    Vertical
}

public enum RabbetEdge
{
    Top,
    Bottom,
    Left,
    Right
}

public enum ProfileEdgeLocation
{
    Top,
    Bottom,
    Left,
    Right,
    All
}

public enum EdgeProfileType
{
    Roundover,
    Chamfer,
    Ogee,
    Bevel,
    Bullnose
}
```

---

## 9. Edge Banding Requirements

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Geometry;

/// <summary>
/// Aggregated edge banding requirement for a material group.
/// Summarizes total linear footage of each edge banding type needed.
/// </summary>
public sealed record EdgeBandingRequirement
{
    public required string EdgeBandingId { get; init; }
    public required string Name { get; init; }
    public required Thickness BandingThickness { get; init; }

    /// <summary>Total linear inches of banding needed.</summary>
    public required Length TotalLinearLength { get; init; }

    /// <summary>Number of individual edges to band.</summary>
    public required int EdgeCount { get; init; }

    /// <summary>Total linear feet (convenience).</summary>
    public decimal TotalLinearFeet => TotalLinearLength.Inches / 12m;
}
```

---

## 10. Readiness & Blockers

### 10.1 ManufacturingReadinessResult

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// Assessment of whether a design is ready for manufacturing.
/// Produced during Stage 7. Blockers prevent release to shop.
/// </summary>
public sealed class ManufacturingReadinessResult
{
    public required bool IsReady { get; init; }
    public required IReadOnlyList<ManufacturingBlocker> Blockers { get; init; }
    public required IReadOnlyList<ManufacturingWarning> Warnings { get; init; }

    /// <summary>
    /// Summary statistics for the manufacturing plan.
    /// Useful for quick shop-floor assessment.
    /// </summary>
    public required ManufacturingSummary Summary { get; init; }
}

/// <summary>
/// A condition that prevents manufacturing. Maps to ManufactureBlocker severity
/// in the validation engine.
/// </summary>
public sealed record ManufacturingBlocker
{
    public required ManufacturingBlockerCode Code { get; init; }
    public required string Message { get; init; }

    /// <summary>The entity that caused the blocker.</summary>
    public required string AffectedEntityId { get; init; }

    /// <summary>Convert to a ValidationIssue for pipeline integration.</summary>
    public ValidationIssue ToValidationIssue() => new()
    {
        RuleCode = $"MFG_{Code}",
        Severity = ValidationSeverity.ManufactureBlocker,
        Message = Message,
        AffectedEntityIds = [AffectedEntityId],
        Category = ValidationRuleCategory.Manufacturing
    };
}

public enum ManufacturingBlockerCode
{
    /// <summary>Part has no material assigned.</summary>
    MissingMaterial,

    /// <summary>Part dimension is below shop minimum.</summary>
    PartTooSmall,

    /// <summary>Part dimension exceeds shop saw capacity.</summary>
    PartTooLarge,

    /// <summary>Hardware requires boring but no boring pattern is defined.</summary>
    MissingBoringPattern,

    /// <summary>Material thickness is zero or undefined.</summary>
    InvalidThickness,

    /// <summary>Grain direction conflicts with part orientation.</summary>
    GrainConflict,

    /// <summary>Edge banding specified but no banding material assigned.</summary>
    MissingEdgeBanding,

    /// <summary>Required hardware clearance is violated by adjacent parts.</summary>
    HardwareClearanceViolation,

    /// <summary>Revision is not in an approved state.</summary>
    RevisionNotApproved
}

/// <summary>
/// A non-blocking manufacturing concern.
/// </summary>
public sealed record ManufacturingWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public required string AffectedEntityId { get; init; }
}

/// <summary>
/// Aggregate statistics for quick shop-floor assessment.
/// </summary>
public sealed record ManufacturingSummary
{
    public required int TotalParts { get; init; }
    public required int SheetGoodParts { get; init; }
    public required int SolidStockParts { get; init; }
    public required int PartsRequiringMachining { get; init; }
    public required int PartsRequiringEdgeBanding { get; init; }
    public required int DistinctMaterials { get; init; }
    public required int EstimatedSheets { get; init; }
    public required decimal EstimatedSolidStockLinearFeet { get; init; }
}
```

---

## 11. Traceability

Every manufactured artifact carries a `PartTraceability` record (defined in §6.2) that links back to:

```
ManufacturingPlan
  └── RevisionId ──────────► Revision (immutable snapshot)
       └── SheetGoodGroup
            └── SheetGoodPart
                 └── PartTraceability
                      ├── RevisionId ──► which revision
                      ├── CabinetId ───► which cabinet
                      ├── RunId ───────► which run (optional)
                      ├── OpeningId ───► which opening (optional)
                      └── ShopLabel ───► human-readable floor ID
```

**Traceability guarantees:**
- Every `CutListItem` has a non-null `PartTraceability`
- Every `SheetGoodPart` and `SolidStockPart` has a non-null `PartTraceability`
- `ShopLabel` follows the format `{ProjectName}-{RevisionNumber}-{CabinetLabel}-{PartRole}`
- When a revision is locked for manufacture, the `ManufacturingPlan` is frozen with the snapshot — traceability IDs are stable forever
- A part on the shop floor can always be traced back to the exact cabinet, run, and revision it belongs to

---

## 12. Export Boundaries

The manufacturing layer defines *what* to export but not *how*. Export implementations live in `CabinetDesigner.Exports`.

```csharp
namespace CabinetDesigner.Domain.ManufacturingContext;

/// <summary>
/// Defines the contract for manufacturing export artifacts.
/// Implementations live in CabinetDesigner.Exports.
/// </summary>
public interface IManufacturingExporter
{
    /// <summary>Export cut list to CSV/spreadsheet format.</summary>
    ExportResult ExportCutList(ManufacturingPlan plan, ExportFormat format);

    /// <summary>Export machining operations for CNC post-processing.</summary>
    ExportResult ExportMachiningOperations(ManufacturingPlan plan, string postProcessorId);

    /// <summary>Export parts for nesting solver input.</summary>
    ExportResult ExportNestingInput(ManufacturingPlan plan, NestingFormat format);

    /// <summary>Export part labels for shop floor identification.</summary>
    ExportResult ExportPartLabels(ManufacturingPlan plan, LabelFormat format);
}

public sealed record ExportResult
{
    public required bool Success { get; init; }
    public required string? FilePath { get; init; }
    public required string? ErrorMessage { get; init; }
}

public enum ExportFormat
{
    Csv,
    Excel,
    Pdf
}

public enum NestingFormat
{
    /// <summary>Generic rectangular nesting input.</summary>
    Generic,

    /// <summary>CutRite-compatible format.</summary>
    CutRite,

    /// <summary>Mozaik-compatible format.</summary>
    Mozaik
}

public enum LabelFormat
{
    /// <summary>Standard part labels with barcodes.</summary>
    Standard,

    /// <summary>Avery label sheet format.</summary>
    AverySheet,

    /// <summary>Direct thermal printer format.</summary>
    ThermalPrinter
}
```

---

## 13. Interaction with Validation

Manufacturing validation rules run at two points:

1. **Stage 7 (inline):** The manufacturing projector itself detects blockers during projection (missing material, invalid dimensions). These become `ManufacturingBlocker` entries in `ManufacturingReadinessResult` and are converted to `ValidationIssue` instances on the `ResolutionContext`.

2. **Stage 10 (cross-cutting):** Validation rules in the `Manufacturing` category run against the completed `ManufacturingPlanResult`. These rules check cross-layer concerns:

| Rule | Category | Scope | Severity |
|---|---|---|---|
| `PartBelowMinDimension` | Manufacturing | Cabinet | ManufactureBlocker |
| `PartExceedsSawCapacity` | Manufacturing | Cabinet | ManufactureBlocker |
| `MissingMaterialAssignment` | Completeness | Cabinet | ManufactureBlocker |
| `GrainDirectionConflict` | Manufacturing | Cabinet | Error |
| `EdgeBandingUnassigned` | Completeness | Cabinet | Warning |
| `HighWasteRatio` | Manufacturing | Scene | Warning |
| `SolidStockExcessiveWaste` | Manufacturing | Scene | Warning |
| `MachiningComplexityHigh` | Manufacturing | Cabinet | Info |

---

## 14. Actual vs Nominal Thickness

This is a critical concern throughout the manufacturing layer.

**Nominal thickness** is what the material catalog says (e.g., 3/4" plywood). **Actual thickness** is what you measure with calipers (e.g., 0.703" for "3/4" plywood).

The manufacturing layer **always** uses actual thickness for:
- Part dimensions in cut lists
- Dado and rabbet depth calculations
- Machining operation parameters
- Sheet utilization calculations

The `Thickness` value object (defined in `geometry_system.md`) carries both:

```csharp
// From geometry_system.md — referenced here for clarity
public readonly record struct Thickness
{
    public Length Nominal { get; }
    public Length Actual { get; }
    // ...
}
```

**Flow:**
1. Design intent specifies nominal thickness (user selects "3/4 plywood")
2. Material catalog resolves actual thickness (0.703")
3. Engineering resolution uses actual thickness for joinery math
4. Part generation emits parts with actual dimensions
5. Manufacturing planning receives parts with actual dimensions — no further conversion needed
6. Cut list exports actual dimensions — what the shop floor measures

---

## 15. Invariants

1. A `ManufacturingPlan` is always associated with exactly one `RevisionId`
2. Every part in the plan has a non-null `PartTraceability` with a valid `CabinetId`
3. All dimensions in the manufacturing layer use actual values, never nominal
4. Parts are grouped by `(MaterialId, ActualThickness, GrainDirection)` — no part appears in multiple groups
5. `ManufacturingPlan` output is deterministic: same `PartGenerationResult` + same `ShopParameters` = same plan
6. Manufacturing never mutates design state — it is a pure function of resolved parts
7. Machining operations reference valid `CutListItemId` values — no orphaned operations
8. `ManufacturingBlocker` issues always map to `ValidationSeverity.ManufactureBlocker`
9. Locked/released revisions carry frozen manufacturing plans — the plan does not change after lock
10. `ShopParameters` are injected, not stored in design state

---

## 16. Testing Strategy

### Unit Tests

| Test | Validates |
|---|---|
| `SheetGoodGrouping_GroupsByMaterialAndThickness` | Parts with same material/thickness land in same group |
| `SheetGoodGrouping_SeparatesByGrain` | Grain-sensitive materials create separate groups |
| `CutListItem_UsesActualThickness` | Cut list never contains nominal thickness values |
| `ManufacturingBlocker_MissingMaterial_Detected` | Part without material assignment produces blocker |
| `ManufacturingBlocker_PartTooSmall_Detected` | Part below shop minimum produces blocker |
| `ManufacturingBlocker_PartTooLarge_Detected` | Part exceeding saw capacity produces blocker |
| `EdgeTreatment_ComputesLinearFootage` | Total edge banding footage is correct |
| `ToolpathIntent_BoreHoles_CreatesCorrectSpec` | Hinge boring produces correct hole specs |
| `PartTraceability_AlwaysPresent` | Every manufactured part carries traceability |
| `ManufacturingPlan_Deterministic` | Same input produces identical output |
| `ManufacturingPlan_NeverMutatesInput` | Part graph is unchanged after projection |

### Integration Tests

| Test | Validates |
|---|---|
| `Pipeline_Stage7_ProducesManufacturingPlan` | Stage 7 writes result to `ResolutionContext` |
| `Pipeline_Stage7_BlockerHaltsPipeline` | Manufacturing blocker prevents Stage 8+ execution |
| `Pipeline_FullRun_ManufacturingMatchesParts` | Every part from Stage 6 appears in manufacturing plan |
| `Snapshot_FrozenManufacturingPlan_Immutable` | Approved plan cannot be regenerated or modified |
| `ActualVsNominal_FlowsThroughPipeline` | Actual thickness from material catalog reaches cut list |

### Property-Based Tests

| Property | Validates |
|---|---|
| `AllParts_AppearInExactlyOneGroup` | No duplicates, no orphans across groups |
| `TotalPartCount_EqualsInputPartCount` | Projection does not create or lose parts |
| `CutListQuantities_MatchGroupParts` | Cut list aggregation is consistent |
| `AllMachiningOps_ReferenceValidParts` | No orphaned machining operations |

---

## 17. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| Actual thickness data is missing or zero | `ManufacturingBlocker.InvalidThickness` — detected during projection, blocks release |
| Grain direction not set for grain-sensitive material | `ManufacturingBlocker.GrainConflict` — detected during grouping |
| Part dimensions exceed shop equipment capacity | `ManufacturingBlocker.PartTooLarge` — checked against `ShopParameters.MaxSawCapacity` |
| Edge banding specified but material not in catalog | `ManufacturingBlocker.MissingEdgeBanding` — detected during edge treatment planning |
| Nesting yields extremely high waste (>40%) | `ManufacturingWarning` — does not block but flags for user review |
| Revision locked but design subsequently changed | Impossible — locked revisions are immutable. New changes create new revisions |
| Hardware boring pattern conflicts with joinery | Cross-cutting validation at Stage 10 (`HardwareClearanceViolation`) |
| Same part appears in multiple cabinets (shared shelves, etc.) | Each part instance gets its own `CutListItemId` and `PartTraceability` — no sharing |
| Very large projects (1000+ parts) | Manufacturing projection is O(n) in parts — grouping and sorting, no combinatorial explosion. Nesting is deferred to export |
| Shop parameters change between plan generation and cutting | Manufacturing plan is frozen with the revision snapshot. Re-cutting requires a new revision |
