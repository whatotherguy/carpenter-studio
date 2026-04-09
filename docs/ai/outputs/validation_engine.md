# P8 — Validation Engine Design

Source: `cabinet_ai_prompt_pack_v4_1_full.md` (Phase 8)
Context: `architecture_summary.md`, `commands.md`, `domain_model.md`, `orchestrator.md`, `why_engine.md`, `application_layer.md`, `editor_engine.md`

---

## 1. Goals

- Evaluate design, engineering, manufacturing, install, and workflow correctness **without mutating state**
- Provide structured, machine-readable validation results with severity, entity references, and fix suggestions
- Distinguish structural validation (pure, stateless), contextual validation (state-dependent), and cross-cutting validation (multi-layer)
- Support preview-safe validation (lightweight, during drag) and full validation (post-commit, all layers)
- Produce deterministic results — same state + same rules = same issues, always
- Feed the Why Engine with validation decisions so users can ask "why is this flagged?"
- Power future "fix this" workflows through a remediation model that describes what to change
- Aggregate, deduplicate, and index issues for efficient querying by entity, severity, category, and rule

---

## 2. Design Decisions

| Decision | Rationale |
|---|---|
| Validation never mutates state | Guardrail #6 from architecture. Validation observes and reports — the orchestrator and commands own mutation. A validation rule that "fixes" something is a design bug |
| Rules are individually registered, not hardcoded in a monolith | Each rule is a self-contained class implementing `IValidationRule`. Enables independent testing, selective execution, and rule composition without a god-class |
| Three validation tiers: structural, contextual, cross-cutting | Structural runs on the command alone (pure). Contextual runs within a pipeline stage against resolved state. Cross-cutting runs at Stage 10 across all resolved layers. Each tier has different cost, scope, and safety guarantees |
| Severity is a closed enum with explicit blocking semantics | `Info`, `Warning`, `Error`, `ManufactureBlocker`. Only `Error` and `ManufactureBlocker` block pipeline progression. `ManufactureBlocker` additionally prevents release to shop |
| Every issue carries a stable `ValidationIssueId` | Enables deduplication across pipeline runs, issue tracking in the UI, and linking to Why Engine nodes. Composed from rule code + affected entity IDs |
| Rules reference entities by typed ID strings, not object references | Matches the command architecture (§4.6 of `commands.md`). Enables serialization, persistence, and cross-session issue tracking |
| Suggested fixes are data, not executable behavior | A `SuggestedFix` describes what command to issue and what parameters to change. The application layer decides whether to auto-apply or present to the user. No side effects in the validation layer |
| Preview validation is a strict subset of full validation | Only rules marked `PreviewSafe = true` run during drag. These must be O(1) or O(n) in affected entities, never O(n^2) across the scene |

---

## 3. Validation Taxonomy

### 3.1 Three Tiers

```
┌─────────────────────────────────────────────────────────────────────┐
│  Tier 1: Structural Validation (stateless, pure)                     │
│  Runs: Before pipeline (command.ValidateStructure())                 │
│  Scope: Single command in isolation                                  │
│  Examples: required fields, positive dimensions, valid enums          │
│  Cost: Negligible                                                    │
│  Safe in preview: Always                                             │
└─────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Tier 2: Contextual Validation (state-dependent, per-stage)          │
│  Runs: Inside pipeline stages 1–9                                    │
│  Scope: Stage-local resolved state                                   │
│  Examples: entity exists, slot fits in run, material compatible       │
│  Cost: Proportional to affected entities                             │
│  Safe in preview: Stages 1–3 only                                    │
└─────────────────────────────────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────────────────────────┐
│  Tier 3: Cross-Cutting Validation (multi-layer, Stage 10)            │
│  Runs: After all resolution stages, before packaging                 │
│  Scope: Entire resolved state across all realities                   │
│  Examples: collision detection, hardware/opening compat, install      │
│            feasibility, manufacturing limits, workflow state           │
│  Cost: May be O(n^2) for spatial checks                              │
│  Safe in preview: Never                                              │
└─────────────────────────────────────────────────────────────────────┘
```

### 3.2 Rule Categories

Rules are categorized by the reality they primarily check. A rule belongs to exactly one category.

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// The reality/layer a validation rule primarily checks.
/// Maps to the six realities defined in the architecture.
/// </summary>
public enum ValidationRuleCategory
{
    /// <summary>Spatial layout: collisions, overlaps, fit within run/room.</summary>
    Geometry,

    /// <summary>Engineering: assembly resolution, opening compatibility, filler logic.</summary>
    Engineering,

    /// <summary>Material and hardware: thickness, grain, hardware clearances, boring patterns.</summary>
    MaterialHardware,

    /// <summary>Manufacturing: CNC capacity, kerf limits, nesting feasibility, part dimensions.</summary>
    Manufacturing,

    /// <summary>Installation: fastening, stud alignment, access clearance, weight capacity.</summary>
    Installation,

    /// <summary>Workflow: approval state, revision completeness, release prerequisites.</summary>
    Workflow,

    /// <summary>Run-level: capacity, continuity, reveal consistency, end conditions.</summary>
    RunIntegrity,

    /// <summary>Completeness: missing assignments (hardware, edge treatment, material).</summary>
    Completeness
}
```

### 3.3 Rule Scope

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// Defines the scope at which a validation rule operates.
/// Used by the engine to determine what state to provide and how to index results.
/// </summary>
public enum ValidationRuleScope
{
    /// <summary>Validates a single cabinet in isolation.</summary>
    Cabinet,

    /// <summary>Validates a run and its contents as a unit.</summary>
    Run,

    /// <summary>Validates relationships between adjacent cabinets.</summary>
    Adjacency,

    /// <summary>Validates the entire room/scene (collision detection, spatial layout).</summary>
    Scene,

    /// <summary>Validates project-level concerns (workflow state, completeness).</summary>
    Project
}
```

---

## 4. Severity Model

```csharp
namespace CabinetDesigner.Domain.Validation;

// ValidationSeverity is already defined in commands.md (§4.6).
// Re-stated here for completeness — the canonical definition lives in
// CabinetDesigner.Domain.Commands.ValidationSeverity.
//
// public enum ValidationSeverity
// {
//     Info,                // Informational — does not block anything
//     Warning,             // Potential issue — does not block pipeline
//     Error,               // Design error — blocks pipeline progression
//     ManufactureBlocker   // Cannot manufacture — blocks pipeline AND release to shop
// }
```

### 4.1 Blocking Behavior

| Severity | Blocks Pipeline | Blocks Release to Shop | Blocks Approval | Shown in UI |
|---|---|---|---|---|
| `Info` | No | No | No | Status bar count, expandable in issue panel |
| `Warning` | No | No | No | Status bar count, highlighted in issue panel |
| `Error` | Yes | Yes | Yes | Inline on canvas + issue panel + modal on attempt |
| `ManufactureBlocker` | Yes | Yes | Yes | Red badge + inline on canvas + issue panel + blocks export |

### 4.2 Blocking Threshold

The orchestrator checks for blocking issues after each stage and at Stage 10:

```csharp
// In ResolutionOrchestrator — already defined in orchestrator.md
// context.HasBlockingIssues checks: Severity >= ValidationSeverity.Error
```

Stage 10 (cross-cutting validation) may produce additional `Error` or `ManufactureBlocker` issues. If Stage 10 produces blocking issues, the pipeline halts before Stage 11 (packaging). This means:
- No snapshot is created
- No revision is frozen
- The command still succeeds as a design change (deltas are committed), but the result carries blocking issues
- The UI shows the issues and prevents approval/export until resolved

**Exception:** If the orchestrator is configured for `StrictValidation` mode, Stage 10 blocking issues also roll back deltas. Default is non-strict (issues reported but design state is committed).

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// Controls whether Stage 10 blocking issues cause delta rollback.
/// </summary>
public enum ValidationStrictness
{
    /// <summary>
    /// Default. Stage 10 issues are reported but design state is committed.
    /// User can iterate on the design and fix issues incrementally.
    /// </summary>
    ReportOnly,

    /// <summary>
    /// Stage 10 blocking issues cause the entire command to fail.
    /// Used for release-to-shop and approval workflows.
    /// </summary>
    Strict
}
```

---

## 5. Rule Contracts

### 5.1 IValidationRule

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// A single validation rule. Stateless. Deterministic. Non-mutating.
/// Each rule checks one concern and produces zero or more issues.
/// Rules must be independently testable with no shared mutable state.
/// </summary>
public interface IValidationRule
{
    /// <summary>
    /// Stable, unique identifier for this rule.
    /// Used in issue IDs, deduplication, Why Engine references, and rule filtering.
    /// Convention: "{category}.{specific_check}" — e.g., "geometry.cabinet_overlap",
    /// "manufacturing.part_exceeds_cnc_capacity".
    /// </summary>
    string RuleCode { get; }

    /// <summary>Human-readable rule name for display in issue panel.</summary>
    string RuleName { get; }

    /// <summary>Human-readable description of what this rule checks.</summary>
    string Description { get; }

    /// <summary>The reality/layer this rule primarily validates.</summary>
    ValidationRuleCategory Category { get; }

    /// <summary>The scope at which this rule operates.</summary>
    ValidationRuleScope Scope { get; }

    /// <summary>
    /// Whether this rule is safe to run during preview (drag operations).
    /// Preview-safe rules must be fast (O(1) or O(n) in affected entities)
    /// and must not require deep-path stage results (stages 4–11).
    /// </summary>
    bool PreviewSafe { get; }

    /// <summary>
    /// Evaluate this rule against the provided validation context.
    /// Must be pure — no side effects, no state mutation.
    /// Returns zero issues if the rule passes, one or more if it fails.
    /// </summary>
    IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context);
}
```

### 5.2 ValidationContext

The read-only state provided to rules. Rules never receive mutable domain objects.

```csharp
namespace CabinetDesigner.Domain.Validation;

using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Pipeline.Stages;
using CabinetDesigner.Domain.Commands;

/// <summary>
/// Read-only context passed to validation rules during evaluation.
/// Provides access to all resolved stage results without exposing mutable state.
/// Rules query this context — they never hold direct references to domain entities.
/// </summary>
public sealed class ValidationContext
{
    /// <summary>The command that triggered this validation pass.</summary>
    public required IDesignCommand Command { get; init; }

    /// <summary>Whether this is a preview or full validation pass.</summary>
    public required ResolutionMode Mode { get; init; }

    /// <summary>The strictness level for this validation pass.</summary>
    public required ValidationStrictness Strictness { get; init; }

    // --- Stage results (read-only projections) ---
    // Null for stages that haven't executed (preview mode skips 4–11).

    public SpatialResolutionResult? SpatialResult { get; init; }
    public EngineeringResolutionResult? EngineeringResult { get; init; }
    public ConstraintPropagationResult? ConstraintResult { get; init; }
    public PartGenerationResult? PartResult { get; init; }
    public ManufacturingPlanResult? ManufacturingResult { get; init; }
    public InstallPlanResult? InstallResult { get; init; }
    public CostingResult? CostingResult { get; init; }

    // --- Scene state (read-only snapshots for cross-cutting checks) ---

    /// <summary>All cabinet positions in the scene (for collision detection).</summary>
    public required IReadOnlyList<CabinetPositionSnapshot> CabinetPositions { get; init; }

    /// <summary>All run summaries in the scene.</summary>
    public required IReadOnlyList<RunValidationSnapshot> RunSnapshots { get; init; }

    /// <summary>Current workflow/approval state.</summary>
    public required WorkflowStateSnapshot WorkflowState { get; init; }
}

/// <summary>Read-only position snapshot for collision/spatial validation.</summary>
public sealed record CabinetPositionSnapshot(
    string CabinetId,
    string RunId,
    Rect2D BoundingBox,
    int SlotIndex);

/// <summary>Read-only run state snapshot for run-level validation.</summary>
public sealed record RunValidationSnapshot(
    string RunId,
    Length Capacity,
    Length OccupiedLength,
    int SlotCount,
    bool HasLeftEndCondition,
    bool HasRightEndCondition);

/// <summary>Read-only workflow state for workflow validation.</summary>
public sealed record WorkflowStateSnapshot(
    string ApprovalState,
    bool HasUnapprovedChanges,
    bool HasPendingManufactureBlockers);
```

---

## 6. Result Model

### 6.1 ValidationIssueId

Stable, deterministic identity for deduplication and tracking.

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// Deterministic identifier for a validation issue.
/// Composed from rule code + sorted affected entity IDs.
/// Two issues with the same ID represent the same problem —
/// used for deduplication across pipeline runs.
/// </summary>
public readonly record struct ValidationIssueId
{
    public string Value { get; }

    public ValidationIssueId(string ruleCode, IReadOnlyList<string> affectedEntityIds)
    {
        // Sort entity IDs for deterministic composition regardless of evaluation order
        var sortedIds = affectedEntityIds.OrderBy(id => id, StringComparer.Ordinal);
        Value = $"{ruleCode}:{string.Join("+", sortedIds)}";
    }

    public override string ToString() => Value;
}
```

### 6.2 ValidationIssue (Extended)

The existing `ValidationIssue` from `commands.md` (§4.6) is the canonical issue type used throughout the pipeline. The validation engine extends it with additional fields for cross-cutting validation. Both shapes coexist — the extended version wraps the base.

```csharp
namespace CabinetDesigner.Domain.Validation;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Extended validation issue produced by Stage 10 cross-cutting validation.
/// Wraps the base ValidationIssue (from commands.md) with additional context:
/// stable ID, rule reference, category, fix suggestions, and Why Engine linkage.
/// </summary>
public sealed record ExtendedValidationIssue
{
    /// <summary>Stable, deterministic ID for deduplication and tracking.</summary>
    public required ValidationIssueId IssueId { get; init; }

    /// <summary>The base issue (severity, code, message, affected entities).</summary>
    public required ValidationIssue Issue { get; init; }

    /// <summary>The rule that produced this issue.</summary>
    public required string RuleCode { get; init; }

    /// <summary>Category of the rule that produced this issue.</summary>
    public required ValidationRuleCategory Category { get; init; }

    /// <summary>Scope at which this issue was detected.</summary>
    public required ValidationRuleScope Scope { get; init; }

    /// <summary>
    /// Suggested fixes, if any. Ordered by confidence (highest first).
    /// Empty if no automated fix is possible.
    /// </summary>
    public required IReadOnlyList<SuggestedFix> SuggestedFixes { get; init; }

    /// <summary>
    /// Why Engine explanation node ID for this issue, if recorded.
    /// Populated after the Why Engine records the validation decision.
    /// </summary>
    public ExplanationNodeId? ExplanationNodeId { get; init; }
}
```

### 6.3 ValidationResult (Stage 10 Output)

```csharp
namespace CabinetDesigner.Domain.Validation;

using CabinetDesigner.Domain.Commands;

/// <summary>
/// Complete result from Stage 10 cross-cutting validation.
/// Contains both base issues (from per-stage contextual validation)
/// and extended issues (from cross-cutting rules).
/// </summary>
public sealed record FullValidationResult
{
    /// <summary>All issues from cross-cutting validation rules.</summary>
    public required IReadOnlyList<ExtendedValidationIssue> CrossCuttingIssues { get; init; }

    /// <summary>Issues accumulated from stages 1–9 (contextual validation).</summary>
    public required IReadOnlyList<ValidationIssue> ContextualIssues { get; init; }

    /// <summary>All issues combined, sorted by severity descending.</summary>
    public IReadOnlyList<ValidationIssue> AllBaseIssues =>
        ContextualIssues
            .Concat(CrossCuttingIssues.Select(e => e.Issue))
            .OrderByDescending(i => i.Severity)
            .ToList();

    /// <summary>True if no Error or ManufactureBlocker issues exist across all sources.</summary>
    public bool IsValid => !AllBaseIssues.Any(i => i.Severity >= ValidationSeverity.Error);

    /// <summary>True if any ManufactureBlocker issues exist.</summary>
    public bool HasManufactureBlockers =>
        AllBaseIssues.Any(i => i.Severity == ValidationSeverity.ManufactureBlocker);

    /// <summary>Count by severity for status bar display.</summary>
    public ValidationSeverityCounts SeverityCounts => new(
        Info: AllBaseIssues.Count(i => i.Severity == ValidationSeverity.Info),
        Warnings: AllBaseIssues.Count(i => i.Severity == ValidationSeverity.Warning),
        Errors: AllBaseIssues.Count(i => i.Severity == ValidationSeverity.Error),
        ManufactureBlockers: AllBaseIssues.Count(i => i.Severity == ValidationSeverity.ManufactureBlocker));
}

public sealed record ValidationSeverityCounts(
    int Info,
    int Warnings,
    int Errors,
    int ManufactureBlockers);
```

---

## 7. Remediation Model

### 7.1 SuggestedFix

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// A machine-readable suggested fix for a validation issue.
/// Describes what to change, not how to execute it.
/// The application layer or UI decides whether to auto-apply or present as a suggestion.
/// </summary>
public sealed record SuggestedFix
{
    /// <summary>Human-readable description of the fix.</summary>
    public required string Description { get; init; }

    /// <summary>The strategy for applying this fix.</summary>
    public required FixStrategy Strategy { get; init; }

    /// <summary>
    /// The command type that would implement this fix.
    /// Matches IDesignCommand.CommandType values.
    /// Example: "modification.resize_cabinet"
    /// </summary>
    public required string CommandType { get; init; }

    /// <summary>
    /// Parameters for the fix command.
    /// Keys are parameter names; values are the suggested values.
    /// The application layer constructs the actual IDesignCommand from these.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    /// <summary>
    /// Confidence that this fix resolves the issue (0.0–1.0).
    /// 1.0 = guaranteed to resolve. Lower values indicate heuristic suggestions.
    /// </summary>
    public required decimal Confidence { get; init; }

    /// <summary>
    /// Entity IDs that would be modified by this fix.
    /// Used to preview the blast radius before applying.
    /// </summary>
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }
}
```

### 7.2 FixStrategy

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// Describes the nature of a suggested fix.
/// Used by the UI to determine how to present the fix option.
/// </summary>
public enum FixStrategy
{
    /// <summary>Resize an entity to fit constraints.</summary>
    Resize,

    /// <summary>Move an entity to resolve a conflict.</summary>
    Reposition,

    /// <summary>Replace an entity with a compatible alternative.</summary>
    Substitute,

    /// <summary>Remove the problematic entity entirely.</summary>
    Remove,

    /// <summary>Add a missing element (e.g., filler, end condition, edge banding).</summary>
    Add,

    /// <summary>Change a property value (e.g., material, hardware selection).</summary>
    ChangeProperty,

    /// <summary>Reorder elements (e.g., adjust install sequence).</summary>
    Reorder,

    /// <summary>Advance or change workflow state (e.g., re-approve after changes).</summary>
    WorkflowTransition
}
```

### 7.3 Remediation Examples

| Issue | Fix Strategy | Command Type | Parameters |
|---|---|---|---|
| Cabinet exceeds run capacity | `Resize` | `modification.resize_cabinet` | `{ "CabinetId": "...", "NewNominalWidthInches": "30" }` |
| Cabinet overlap detected | `Reposition` | `layout.move_cabinet` | `{ "CabinetId": "...", "TargetIndex": "3" }` |
| Hinge incompatible with door width | `Substitute` | `modification.change_hardware` | `{ "OpeningId": "...", "HardwareItemId": "blum-110deg" }` |
| Missing edge banding on exposed edge | `Add` | `modification.set_edge_treatment` | `{ "PartId": "...", "Edge": "top", "BandingId": "pvc-2mm" }` |
| Unapproved changes before release | `WorkflowTransition` | `workflow.submit_for_review` | `{ "RevisionId": "..." }` |

---

## 8. Validation Engine Interface

### 8.1 IValidationEngine

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// Orchestrates validation rule execution.
/// Owns rule registration, execution ordering, result aggregation, and deduplication.
/// Stateless — all state comes from the ValidationContext.
/// The engine itself is deterministic: same rules + same context = same result.
/// </summary>
public interface IValidationEngine
{
    /// <summary>
    /// Run all registered rules against the provided context.
    /// Used by Stage 10 for cross-cutting validation.
    /// Returns a complete, deduplicated result.
    /// </summary>
    FullValidationResult Validate(ValidationContext context);

    /// <summary>
    /// Run only preview-safe rules against the provided context.
    /// Used during drag operations for fast feedback.
    /// Returns only base ValidationIssues (no extended metadata).
    /// </summary>
    IReadOnlyList<ValidationIssue> ValidatePreview(ValidationContext context);

    /// <summary>
    /// Run a specific subset of rules by category.
    /// Used when only one reality needs revalidation (e.g., after hardware catalog update).
    /// </summary>
    IReadOnlyList<ExtendedValidationIssue> ValidateCategory(
        ValidationContext context,
        ValidationRuleCategory category);

    /// <summary>
    /// Get all registered rules. For diagnostics and rule listing in UI.
    /// </summary>
    IReadOnlyList<IValidationRule> RegisteredRules { get; }
}
```

### 8.2 Rule Registration and Composition

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// Builds and configures the validation engine with rules.
/// Rules are registered at application startup and remain fixed for the session.
/// </summary>
public sealed class ValidationEngineBuilder
{
    private readonly List<IValidationRule> _rules = [];

    /// <summary>Register a single rule.</summary>
    public ValidationEngineBuilder AddRule(IValidationRule rule)
    {
        if (_rules.Any(r => r.RuleCode == rule.RuleCode))
            throw new InvalidOperationException(
                $"Duplicate rule code: {rule.RuleCode}. Each rule must have a unique code.");

        _rules.Add(rule);
        return this;
    }

    /// <summary>Register all rules from an assembly by convention (implements IValidationRule).</summary>
    public ValidationEngineBuilder AddRulesFromAssembly(System.Reflection.Assembly assembly)
    {
        var ruleTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IValidationRule).IsAssignableFrom(t));

        foreach (var type in ruleTypes)
        {
            var rule = (IValidationRule)Activator.CreateInstance(type)!;
            AddRule(rule);
        }

        return this;
    }

    public IValidationEngine Build() => new ValidationEngine(_rules);
}
```

### 8.3 Engine Implementation

```csharp
namespace CabinetDesigner.Domain.Validation;

using CabinetDesigner.Domain.Commands;

public sealed class ValidationEngine : IValidationEngine
{
    private readonly IReadOnlyList<IValidationRule> _rules;

    // Pre-computed rule subsets for fast lookup
    private readonly IReadOnlyList<IValidationRule> _previewSafeRules;
    private readonly IReadOnlyDictionary<ValidationRuleCategory, IReadOnlyList<IValidationRule>> _rulesByCategory;

    internal ValidationEngine(IReadOnlyList<IValidationRule> rules)
    {
        _rules = rules;
        _previewSafeRules = rules.Where(r => r.PreviewSafe).ToList();
        _rulesByCategory = rules
            .GroupBy(r => r.Category)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<IValidationRule>)g.ToList());
    }

    public IReadOnlyList<IValidationRule> RegisteredRules => _rules;

    public FullValidationResult Validate(ValidationContext context)
    {
        var crossCuttingIssues = new List<ExtendedValidationIssue>();
        var seen = new HashSet<string>(); // deduplication by ValidationIssueId.Value

        foreach (var rule in _rules)
        {
            var issues = rule.Evaluate(context);
            foreach (var issue in issues)
            {
                var issueId = new ValidationIssueId(
                    rule.RuleCode,
                    issue.AffectedEntityIds ?? []);

                if (!seen.Add(issueId.Value))
                    continue; // duplicate — skip

                crossCuttingIssues.Add(new ExtendedValidationIssue
                {
                    IssueId = issueId,
                    Issue = issue,
                    RuleCode = rule.RuleCode,
                    Category = rule.Category,
                    Scope = rule.Scope,
                    SuggestedFixes = GenerateFixes(rule, issue, context)
                });
            }
        }

        return new FullValidationResult
        {
            CrossCuttingIssues = crossCuttingIssues,
            ContextualIssues = [] // populated by the orchestrator from accumulated stage issues
        };
    }

    public IReadOnlyList<ValidationIssue> ValidatePreview(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();
        foreach (var rule in _previewSafeRules)
            issues.AddRange(rule.Evaluate(context));
        return issues;
    }

    public IReadOnlyList<ExtendedValidationIssue> ValidateCategory(
        ValidationContext context,
        ValidationRuleCategory category)
    {
        if (!_rulesByCategory.TryGetValue(category, out var rules))
            return [];

        var results = new List<ExtendedValidationIssue>();
        foreach (var rule in rules)
        {
            foreach (var issue in rule.Evaluate(context))
            {
                results.Add(new ExtendedValidationIssue
                {
                    IssueId = new ValidationIssueId(rule.RuleCode, issue.AffectedEntityIds ?? []),
                    Issue = issue,
                    RuleCode = rule.RuleCode,
                    Category = rule.Category,
                    Scope = rule.Scope,
                    SuggestedFixes = GenerateFixes(rule, issue, context)
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Generate suggested fixes for an issue. Rules that implement IFixSuggestingRule
    /// provide their own fix generation. Otherwise, returns empty.
    /// </summary>
    private static IReadOnlyList<SuggestedFix> GenerateFixes(
        IValidationRule rule,
        ValidationIssue issue,
        ValidationContext context)
    {
        if (rule is IFixSuggestingRule fixRule)
            return fixRule.SuggestFixes(issue, context);
        return [];
    }
}

/// <summary>
/// Optional interface for rules that can suggest fixes.
/// Not all rules need fix suggestions — only implement when
/// a deterministic remediation exists.
/// </summary>
public interface IFixSuggestingRule : IValidationRule
{
    /// <summary>
    /// Generate suggested fixes for the given issue.
    /// Must be pure — no side effects.
    /// </summary>
    IReadOnlyList<SuggestedFix> SuggestFixes(
        ValidationIssue issue,
        ValidationContext context);
}
```

---

## 9. Pipeline Integration

### 9.1 Where Validation Runs in the Pipeline

```
Stage 1 (Input Capture)
  └─ Contextual: entity exists, references valid
Stage 2 (Interaction Interpretation)
  └─ Contextual: operation feasible (slot index in bounds, etc.)
Stage 3 (Spatial Resolution)
  └─ Contextual: slot fits in run, no immediate overlap
  └─ Preview-safe rules run here during drag (via ValidatePreview)
Stage 4 (Engineering Resolution)
  └─ Contextual: assembly resolution valid, openings compatible
Stage 5 (Constraint Propagation)
  └─ Contextual: material/hardware constraints satisfied
Stage 6–9 (Parts, Manufacturing, Install, Costing)
  └─ Contextual: stage-specific feasibility checks

Stage 10 (Validation) ← CROSS-CUTTING VALIDATION
  └─ IValidationEngine.Validate() runs ALL registered rules
  └─ Checks interactions BETWEEN layers that no single stage can see
  └─ Produces FullValidationResult
  └─ Records issues in Why Engine

Stage 11 (Packaging)
  └─ Proceeds only if Stage 10 allows (based on strictness mode)
```

### 9.2 Stage 10 Implementation Contract

```csharp
namespace CabinetDesigner.Application.Pipeline.Stages;

using CabinetDesigner.Domain.Validation;

/// <summary>
/// Stage 10: Cross-cutting validation.
/// Runs all registered validation rules against the fully resolved state.
/// </summary>
public sealed class ValidationStage : IResolutionStage
{
    private readonly IValidationEngine _validationEngine;
    private readonly IWhyEngine _whyEngine;

    public int StageNumber => 10;
    public string StageName => "Validation";

    public ValidationStage(IValidationEngine validationEngine, IWhyEngine whyEngine)
    {
        _validationEngine = validationEngine;
        _whyEngine = whyEngine;
    }

    public bool ShouldExecute(ResolutionMode mode) => mode == ResolutionMode.Full;

    public StageResult Execute(ResolutionContext context)
    {
        // 1. Build validation context from pipeline stage results
        var validationContext = BuildValidationContext(context);

        // 2. Run all cross-cutting rules
        var result = _validationEngine.Validate(validationContext);

        // 3. Merge contextual issues from prior stages
        result = result with
        {
            ContextualIssues = context.AccumulatedIssues.ToList()
        };

        // 4. Record in Why Engine
        var explanationNodeIds = RecordValidationDecisions(context, result);

        // 5. Set on context
        context.ValidationResult = new ValidationResult
        {
            Issues = result.AllBaseIssues
        };

        // 6. Determine success based on strictness
        if (validationContext.Strictness == ValidationStrictness.Strict && !result.IsValid)
        {
            return StageResult.Failed(
                StageNumber,
                result.AllBaseIssues.Where(i => i.Severity >= ValidationSeverity.Error).ToList(),
                explanationNodeIds);
        }

        // In ReportOnly mode, always succeed — issues are carried as warnings
        return StageResult.Succeeded(
            StageNumber,
            explanationNodeIds,
            result.AllBaseIssues.ToList());
    }

    private ValidationContext BuildValidationContext(ResolutionContext context)
    {
        return new ValidationContext
        {
            Command = context.Command,
            Mode = context.Mode,
            Strictness = ValidationStrictness.ReportOnly, // default; overridden for approval/release
            SpatialResult = context.SpatialResult,
            EngineeringResult = context.EngineeringResult,
            ConstraintResult = context.ConstraintResult,
            PartResult = context.PartResult,
            ManufacturingResult = context.ManufacturingResult,
            InstallResult = context.InstallResult,
            CostingResult = context.CostingResult,
            CabinetPositions = BuildCabinetPositions(context),
            RunSnapshots = BuildRunSnapshots(context),
            WorkflowState = BuildWorkflowState(context)
        };
    }

    private IReadOnlyList<ExplanationNodeId> RecordValidationDecisions(
        ResolutionContext context,
        FullValidationResult result)
    {
        var nodeIds = new List<ExplanationNodeId>();

        foreach (var issue in result.CrossCuttingIssues)
        {
            var nodeId = _whyEngine.RecordDecision(
                context.Command.Metadata.CommandId,
                StageNumber,
                "validation_issue",
                $"[{issue.Issue.Severity}] {issue.RuleCode}: {issue.Issue.Message}",
                issue.Issue.AffectedEntityIds,
                new Dictionary<string, string>
                {
                    ["rule_code"] = issue.RuleCode,
                    ["severity"] = issue.Issue.Severity.ToString(),
                    ["category"] = issue.Category.ToString(),
                    ["has_fix"] = (issue.SuggestedFixes.Count > 0).ToString()
                });

            nodeIds.Add(nodeId);
        }

        return nodeIds;
    }

    // Builder methods — extract read-only snapshots from resolved state
    private static IReadOnlyList<CabinetPositionSnapshot> BuildCabinetPositions(
        ResolutionContext context) =>
        // Implementation reads from SpatialResult.SlotUpdates and
        // builds bounding boxes from resolved cabinet dimensions.
        // Elided — populated from stage 3 + stage 4 results.
        [];

    private static IReadOnlyList<RunValidationSnapshot> BuildRunSnapshots(
        ResolutionContext context) =>
        // Implementation reads from SpatialResult.AffectedRuns.
        [];

    private static WorkflowStateSnapshot BuildWorkflowState(
        ResolutionContext context) =>
        // Implementation reads from project state.
        new("draft", false, false);
}
```

### 9.3 Preview Validation Integration

During drag (fast path), preview validation runs after Stage 3 inside the editor layer:

```csharp
// In IPreviewCommandHandler (application_layer.md §4.2)
// After stages 1–3 complete, run preview-safe rules:

var previewContext = new ValidationContext
{
    Command = command,
    Mode = ResolutionMode.Preview,
    Strictness = ValidationStrictness.ReportOnly,
    SpatialResult = context.SpatialResult,
    CabinetPositions = BuildLightweightPositions(),
    RunSnapshots = BuildLightweightRunSnapshots(),
    WorkflowState = currentWorkflowState,
    // All deep-path results are null — preview-safe rules must not access them
};

var previewIssues = _validationEngine.ValidatePreview(previewContext);
// Issues surfaced as warnings in PreviewResultDto.Warnings
```

---

## 10. Aggregation, Deduplication, and Indexing

### 10.1 Deduplication

Issues are deduplicated by `ValidationIssueId` — composed from rule code + sorted affected entity IDs. If the same rule fires for the same entities across multiple pipeline runs (e.g., before and after a command), only the latest instance is kept in the active issue set.

### 10.2 Issue Index

```csharp
namespace CabinetDesigner.Domain.Validation;

/// <summary>
/// In-memory index of active validation issues.
/// Updated after every full pipeline run.
/// Provides efficient lookup by entity, severity, category, and rule.
/// </summary>
public sealed class ValidationIssueIndex
{
    private readonly Dictionary<string, ExtendedValidationIssue> _byIssueId = [];
    private readonly Dictionary<string, List<string>> _byEntityId = [];     // entity → issue IDs
    private readonly Dictionary<ValidationSeverity, List<string>> _bySeverity = [];
    private readonly Dictionary<ValidationRuleCategory, List<string>> _byCategory = [];
    private readonly Dictionary<string, List<string>> _byRuleCode = [];

    /// <summary>
    /// Replace the full issue set. Called after each Stage 10 execution.
    /// Clears previous state and rebuilds all indexes.
    /// </summary>
    public void ReplaceAll(IReadOnlyList<ExtendedValidationIssue> issues)
    {
        _byIssueId.Clear();
        _byEntityId.Clear();
        _bySeverity.Clear();
        _byCategory.Clear();
        _byRuleCode.Clear();

        foreach (var issue in issues)
        {
            var id = issue.IssueId.Value;
            _byIssueId[id] = issue;

            // Entity index
            foreach (var entityId in issue.Issue.AffectedEntityIds ?? [])
            {
                if (!_byEntityId.TryGetValue(entityId, out var entityList))
                    _byEntityId[entityId] = entityList = [];
                entityList.Add(id);
            }

            // Severity index
            if (!_bySeverity.TryGetValue(issue.Issue.Severity, out var sevList))
                _bySeverity[issue.Issue.Severity] = sevList = [];
            sevList.Add(id);

            // Category index
            if (!_byCategory.TryGetValue(issue.Category, out var catList))
                _byCategory[issue.Category] = catList = [];
            catList.Add(id);

            // Rule index
            if (!_byRuleCode.TryGetValue(issue.RuleCode, out var ruleList))
                _byRuleCode[issue.RuleCode] = ruleList = [];
            ruleList.Add(id);
        }
    }

    public IReadOnlyList<ExtendedValidationIssue> GetAll() =>
        _byIssueId.Values.ToList();

    public IReadOnlyList<ExtendedValidationIssue> GetByEntity(string entityId) =>
        LookupIssues(_byEntityId, entityId);

    public IReadOnlyList<ExtendedValidationIssue> GetBySeverity(ValidationSeverity severity) =>
        LookupIssues(_bySeverity, severity);

    public IReadOnlyList<ExtendedValidationIssue> GetByCategory(ValidationRuleCategory category) =>
        LookupIssues(_byCategory, category);

    public IReadOnlyList<ExtendedValidationIssue> GetByRuleCode(string ruleCode) =>
        LookupIssues(_byRuleCode, ruleCode);

    public bool HasBlockingIssues =>
        _bySeverity.ContainsKey(ValidationSeverity.Error) ||
        _bySeverity.ContainsKey(ValidationSeverity.ManufactureBlocker);

    public ValidationSeverityCounts Counts => new(
        Info: CountForSeverity(ValidationSeverity.Info),
        Warnings: CountForSeverity(ValidationSeverity.Warning),
        Errors: CountForSeverity(ValidationSeverity.Error),
        ManufactureBlockers: CountForSeverity(ValidationSeverity.ManufactureBlocker));

    private int CountForSeverity(ValidationSeverity severity) =>
        _bySeverity.TryGetValue(severity, out var list) ? list.Count : 0;

    private IReadOnlyList<ExtendedValidationIssue> LookupIssues<TKey>(
        Dictionary<TKey, List<string>> index,
        TKey key) where TKey : notnull
    {
        if (!index.TryGetValue(key, out var issueIds))
            return [];
        return issueIds
            .Where(id => _byIssueId.ContainsKey(id))
            .Select(id => _byIssueId[id])
            .ToList();
    }
}
```

### 10.3 Integration with IValidationSummaryService

The `ValidationIssueIndex` backs the `IValidationSummaryService` defined in `application_layer.md` (§3.4):

```csharp
// In ValidationSummaryService implementation:
public IReadOnlyList<ValidationIssueSummaryDto> GetAllIssues() =>
    _index.GetAll()
        .OrderByDescending(i => i.Issue.Severity)
        .Select(MapToDto)
        .ToList();

public IReadOnlyList<ValidationIssueSummaryDto> GetIssuesFor(string entityId) =>
    _index.GetByEntity(entityId)
        .Select(MapToDto)
        .ToList();

public bool HasManufactureBlockers =>
    _index.Counts.ManufactureBlockers > 0;
```

---

## 11. Why Engine Integration

### 11.1 What Gets Recorded

Every cross-cutting validation issue is recorded as an `ExplanationNode` with:
- `NodeType = StageDecision`
- `StageNumber = 10`
- `DecisionType = "validation_issue"`
- `AffectedEntityIds` from the issue
- `Context` dictionary containing rule code, severity, category, and fix availability

### 11.2 Edge Linkage

Validation nodes link back to the stage decisions that produced the problematic state:

```
Stage 3 decision: "Placed cabinet C-123 at slot index 4"
      │
      │ ConstrainedBy
      ▼
Stage 10 decision: "geometry.cabinet_overlap — C-123 overlaps C-456"
      │
      │ Produced
      ▼
SuggestedFix reference: "Resize C-123 to 30 inches"
```

### 11.3 Querying Validation History

Users can ask:
- "Why is this cabinet flagged?" → `whyEngine.GetEntityHistory(cabinetId)` filtered by `DecisionType == "validation_issue"`
- "What rule flagged this?" → issue's `RuleCode` → `whyEngine.GetDecisionsByRule(ruleCode)`
- "What changed to cause this issue?" → traverse `ConstrainedBy` edges from the validation node to the stage decision that introduced the problem

---

## 12. Example Rules

### 12.1 Geometry: Cabinet Overlap

```csharp
namespace CabinetDesigner.Domain.Validation.Rules.Geometry;

public sealed class CabinetOverlapRule : IValidationRule, IFixSuggestingRule
{
    public string RuleCode => "geometry.cabinet_overlap";
    public string RuleName => "Cabinet Overlap Detection";
    public string Description => "Detects cabinets whose bounding boxes overlap within a run or across adjacent runs.";
    public ValidationRuleCategory Category => ValidationRuleCategory.Geometry;
    public ValidationRuleScope Scope => ValidationRuleScope.Scene;
    public bool PreviewSafe => false; // O(n^2) — not safe for drag

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();
        var positions = context.CabinetPositions;

        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                if (positions[i].BoundingBox.Intersects(positions[j].BoundingBox))
                {
                    issues.Add(new ValidationIssue(
                        ValidationSeverity.Error,
                        "geometry.cabinet_overlap",
                        $"Cabinet {positions[i].CabinetId} overlaps with {positions[j].CabinetId}.",
                        [positions[i].CabinetId, positions[j].CabinetId]));
                }
            }
        }

        return issues;
    }

    public IReadOnlyList<SuggestedFix> SuggestFixes(
        ValidationIssue issue,
        ValidationContext context)
    {
        if (issue.AffectedEntityIds is not { Count: 2 })
            return [];

        return
        [
            new SuggestedFix
            {
                Description = $"Reposition cabinet {issue.AffectedEntityIds[1]} to eliminate overlap.",
                Strategy = FixStrategy.Reposition,
                CommandType = "layout.move_cabinet",
                Parameters = new Dictionary<string, string>
                {
                    ["CabinetId"] = issue.AffectedEntityIds[1]
                },
                Confidence = 0.7m,
                AffectedEntityIds = [issue.AffectedEntityIds[1]]
            }
        ];
    }
}
```

### 12.2 Run Integrity: Capacity Exceeded

```csharp
namespace CabinetDesigner.Domain.Validation.Rules.RunIntegrity;

public sealed class RunCapacityExceededRule : IValidationRule, IFixSuggestingRule
{
    public string RuleCode => "run.capacity_exceeded";
    public string RuleName => "Run Capacity Exceeded";
    public string Description => "Checks that total occupied width does not exceed run capacity.";
    public ValidationRuleCategory Category => ValidationRuleCategory.RunIntegrity;
    public ValidationRuleScope Scope => ValidationRuleScope.Run;
    public bool PreviewSafe => true; // O(n) in runs — safe for preview

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();

        foreach (var run in context.RunSnapshots)
        {
            if (run.OccupiedLength > run.Capacity)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Error,
                    "run.capacity_exceeded",
                    $"Run {run.RunId} exceeds capacity by {(run.OccupiedLength - run.Capacity)}.",
                    [run.RunId]));
            }
        }

        return issues;
    }

    public IReadOnlyList<SuggestedFix> SuggestFixes(
        ValidationIssue issue,
        ValidationContext context)
    {
        // Suggest removing the last cabinet in the run
        return
        [
            new SuggestedFix
            {
                Description = "Remove the most recently added cabinet from the run.",
                Strategy = FixStrategy.Remove,
                CommandType = "layout.remove_cabinet",
                Parameters = new Dictionary<string, string>
                {
                    ["RunId"] = issue.AffectedEntityIds?.FirstOrDefault() ?? ""
                },
                Confidence = 0.5m,
                AffectedEntityIds = issue.AffectedEntityIds ?? []
            }
        ];
    }
}
```

### 12.3 Material/Hardware: Hinge Incompatible with Door Width

```csharp
namespace CabinetDesigner.Domain.Validation.Rules.MaterialHardware;

public sealed class HingeDoorWidthCompatibilityRule : IValidationRule
{
    public string RuleCode => "hardware.hinge_door_width_incompatible";
    public string RuleName => "Hinge/Door Width Compatibility";
    public string Description => "Checks that assigned hinges support the door width.";
    public ValidationRuleCategory Category => ValidationRuleCategory.MaterialHardware;
    public ValidationRuleScope Scope => ValidationRuleScope.Cabinet;
    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();

        if (context.ConstraintResult is null || context.EngineeringResult is null)
            return issues;

        foreach (var assignment in context.ConstraintResult.HardwareAssignments)
        {
            // Rule: hinge max door width check
            // Actual implementation would look up hardware specs from catalog.
            // Shown here as the structural pattern — catalog lookup is injected.
            var opening = context.EngineeringResult.Assemblies
                .SelectMany(a => a.Openings)
                .FirstOrDefault(o => o.OpeningId == assignment.OpeningId);

            if (opening is null) continue;

            // Example: flag if opening width exceeds hardware-supported maximum
            // Real implementation queries the hardware catalog constraints.
        }

        return issues;
    }
}
```

### 12.4 Manufacturing: Part Exceeds CNC Capacity

```csharp
namespace CabinetDesigner.Domain.Validation.Rules.Manufacturing;

public sealed class PartExceedsCncCapacityRule : IValidationRule
{
    public string RuleCode => "manufacturing.part_exceeds_cnc_capacity";
    public string RuleName => "Part Exceeds CNC Capacity";
    public string Description => "Checks that generated part dimensions are within CNC machine limits.";
    public ValidationRuleCategory Category => ValidationRuleCategory.Manufacturing;
    public ValidationRuleScope Scope => ValidationRuleScope.Cabinet;
    public bool PreviewSafe => false;

    // These would come from shop standards configuration
    private static readonly Length MaxCncWidth = Length.FromInches(96);
    private static readonly Length MaxCncHeight = Length.FromInches(48);

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();

        if (context.PartResult is null) return issues;

        foreach (var part in context.PartResult.Parts)
        {
            if (part.Width > MaxCncWidth || part.Height > MaxCncHeight)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.ManufactureBlocker,
                    "manufacturing.part_exceeds_cnc_capacity",
                    $"Part {part.PartId} ({part.Width} x {part.Height}) exceeds CNC capacity ({MaxCncWidth} x {MaxCncHeight}).",
                    [part.PartId, part.CabinetId.Value.ToString()]));
            }
        }

        return issues;
    }
}
```

### 12.5 Installation: Fastening Not Possible

```csharp
namespace CabinetDesigner.Domain.Validation.Rules.Installation;

public sealed class FasteningFeasibilityRule : IValidationRule
{
    public string RuleCode => "install.fastening_not_possible";
    public string RuleName => "Fastening Feasibility";
    public string Description => "Checks that planned fastening points are physically achievable.";
    public ValidationRuleCategory Category => ValidationRuleCategory.Installation;
    public ValidationRuleScope Scope => ValidationRuleScope.Cabinet;
    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();

        if (context.InstallResult is null) return issues;

        foreach (var req in context.InstallResult.FasteningRequirements)
        {
            // Check for wall-mounted cabinets that don't align with studs
            // and don't have toggle bolt alternatives specified.
            // Real implementation checks stud locations from the room model.
            if (req.FasteningType == "wall_screw" &&
                req.Requirements.Contains("must hit stud") &&
                !HasStudAtLocation(context, req))
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "install.fastening_not_possible",
                    $"Cabinet {req.CabinetId} fastening at {req.Location} may not align with studs. " +
                    "Consider toggle bolts or blocking.",
                    [req.CabinetId.Value.ToString()]));
            }
        }

        return issues;
    }

    private static bool HasStudAtLocation(ValidationContext context, FasteningRequirement req)
    {
        // Stud detection logic would query room/wall model
        // Placeholder — real implementation checks spatial data
        return true;
    }
}
```

### 12.6 Workflow: Unapproved Changes Block Release

```csharp
namespace CabinetDesigner.Domain.Validation.Rules.Workflow;

public sealed class UnapprovedChangesBlockReleaseRule : IValidationRule
{
    public string RuleCode => "workflow.unapproved_changes_block_release";
    public string RuleName => "Unapproved Changes Block Release";
    public string Description => "Prevents release to shop when unapproved design changes exist.";
    public ValidationRuleCategory Category => ValidationRuleCategory.Workflow;
    public ValidationRuleScope Scope => ValidationRuleScope.Project;
    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        if (context.WorkflowState.HasUnapprovedChanges &&
            context.WorkflowState.ApprovalState == "locked_for_manufacture")
        {
            return
            [
                new ValidationIssue(
                    ValidationSeverity.ManufactureBlocker,
                    "workflow.unapproved_changes_block_release",
                    "Design has unapproved changes. Re-approval required before release to shop.",
                    [])
            ];
        }

        return [];
    }
}
```

### 12.7 Completeness: Missing Edge Banding

```csharp
namespace CabinetDesigner.Domain.Validation.Rules.Completeness;

public sealed class MissingEdgeBandingRule : IValidationRule, IFixSuggestingRule
{
    public string RuleCode => "completeness.missing_edge_banding";
    public string RuleName => "Missing Edge Banding on Exposed Edge";
    public string Description => "Checks that all exposed edges have edge treatment assigned.";
    public ValidationRuleCategory Category => ValidationRuleCategory.Completeness;
    public ValidationRuleScope Scope => ValidationRuleScope.Cabinet;
    public bool PreviewSafe => false;

    public IReadOnlyList<ValidationIssue> Evaluate(ValidationContext context)
    {
        var issues = new List<ValidationIssue>();

        if (context.PartResult is null) return issues;

        foreach (var part in context.PartResult.Parts)
        {
            // Check each edge — exposed edges must have banding
            var edges = part.Edges;
            var missing = new List<string>();

            if (IsExposed(part, "top") && edges.TopEdgeBandingId is null) missing.Add("top");
            if (IsExposed(part, "bottom") && edges.BottomEdgeBandingId is null) missing.Add("bottom");
            if (IsExposed(part, "left") && edges.LeftEdgeBandingId is null) missing.Add("left");
            if (IsExposed(part, "right") && edges.RightEdgeBandingId is null) missing.Add("right");

            if (missing.Count > 0)
            {
                issues.Add(new ValidationIssue(
                    ValidationSeverity.Warning,
                    "completeness.missing_edge_banding",
                    $"Part {part.PartId} has exposed edges without banding: {string.Join(", ", missing)}.",
                    [part.PartId, part.CabinetId.Value.ToString()]));
            }
        }

        return issues;
    }

    public IReadOnlyList<SuggestedFix> SuggestFixes(
        ValidationIssue issue,
        ValidationContext context)
    {
        return
        [
            new SuggestedFix
            {
                Description = "Apply default edge banding to exposed edges.",
                Strategy = FixStrategy.Add,
                CommandType = "modification.set_edge_treatment",
                Parameters = new Dictionary<string, string>
                {
                    ["PartId"] = issue.AffectedEntityIds?.FirstOrDefault() ?? "",
                    ["BandingId"] = "shop_default"
                },
                Confidence = 0.9m,
                AffectedEntityIds = issue.AffectedEntityIds ?? []
            }
        ];
    }

    private static bool IsExposed(GeneratedPart part, string edge)
    {
        // Determine if edge is exposed based on part type and position.
        // Example: top edge of a shelf is always exposed; back edge of a side panel is not.
        // Real implementation uses assembly topology from engineering resolution.
        return part.PartType is "shelf" or "door" or "drawer_front";
    }
}
```

---

## 13. Invariants

1. **No mutation**: No validation rule may modify any state. Rules receive a read-only `ValidationContext`. If a rule attempts mutation, it is a defect.
2. **Deterministic output**: Same `ValidationContext` + same rules = same `FullValidationResult`. No randomness, no clock-dependent logic, no external I/O.
3. **Unique rule codes**: Every `IValidationRule.RuleCode` is unique within the registered set. The builder enforces this at registration time.
4. **Stable issue IDs**: `ValidationIssueId` is deterministic given rule code + affected entity IDs. Sorting entity IDs ensures evaluation order independence.
5. **Preview safety contract**: Rules with `PreviewSafe = true` must not access any stage result beyond stages 1–3. The `ValidationContext` exposes nullable stage results — preview-safe rules must only access `SpatialResult`, `CabinetPositions`, and `RunSnapshots`.
6. **Severity ordering**: `Info < Warning < Error < ManufactureBlocker`. Blocking checks use `>=` comparisons against `Error`.
7. **Fix suggestions are non-binding**: `SuggestedFix` is data. The application layer decides whether and how to present it. No fix is ever auto-applied without user consent (unless explicitly configured in shop standards).
8. **Index freshness**: `ValidationIssueIndex` is rebuilt on every full pipeline run. Between runs, the index reflects the last validated state.

---

## 14. Testing Strategy

### 14.1 Rule-Level Tests

Every `IValidationRule` is independently testable. Tests construct a `ValidationContext` with the minimum required state and assert on the issues produced.

```csharp
[Fact]
public void RunCapacityExceededRule_WhenOccupiedExceedsCapacity_ProducesError()
{
    var rule = new RunCapacityExceededRule();
    var context = new ValidationContext
    {
        Command = CreateDummyCommand(),
        Mode = ResolutionMode.Full,
        Strictness = ValidationStrictness.ReportOnly,
        CabinetPositions = [],
        RunSnapshots =
        [
            new RunValidationSnapshot(
                RunId: "run-1",
                Capacity: Length.FromInches(120),
                OccupiedLength: Length.FromInches(130),
                SlotCount: 4,
                HasLeftEndCondition: true,
                HasRightEndCondition: true)
        ],
        WorkflowState = new WorkflowStateSnapshot("draft", false, false)
    };

    var issues = rule.Evaluate(context);

    Assert.Single(issues);
    Assert.Equal(ValidationSeverity.Error, issues[0].Severity);
    Assert.Equal("run.capacity_exceeded", issues[0].Code);
    Assert.Contains("run-1", issues[0].AffectedEntityIds!);
}
```

### 14.2 Engine-Level Tests

Test the engine's aggregation, deduplication, and category filtering:

```csharp
[Fact]
public void ValidationEngine_DeduplicatesIdenticalIssues()
{
    // Register a rule that produces the same issue twice for the same entities
    var engine = new ValidationEngineBuilder()
        .AddRule(new DuplicateProducingTestRule())
        .Build();

    var result = engine.Validate(CreateTestContext());

    // Deduplication should collapse into one
    Assert.Single(result.CrossCuttingIssues);
}

[Fact]
public void ValidationEngine_PreviewOnlyRunsPreviewSafeRules()
{
    var engine = new ValidationEngineBuilder()
        .AddRule(new PreviewSafeTestRule())    // PreviewSafe = true
        .AddRule(new FullOnlyTestRule())       // PreviewSafe = false
        .Build();

    var issues = engine.ValidatePreview(CreatePreviewContext());

    // Only preview-safe rule should have executed
    Assert.All(issues, i => Assert.Equal("preview_safe_rule", i.Code));
}
```

### 14.3 Fix Suggestion Tests

```csharp
[Fact]
public void CabinetOverlapRule_SuggestsRepositionFix()
{
    var rule = new CabinetOverlapRule();
    var issue = new ValidationIssue(
        ValidationSeverity.Error,
        "geometry.cabinet_overlap",
        "Overlap detected.",
        ["cab-1", "cab-2"]);

    var fixes = rule.SuggestFixes(issue, CreateTestContext());

    Assert.Single(fixes);
    Assert.Equal(FixStrategy.Reposition, fixes[0].Strategy);
    Assert.Equal("layout.move_cabinet", fixes[0].CommandType);
}
```

### 14.4 Integration Tests

Test Stage 10 within the full pipeline:

```csharp
[Fact]
public void Stage10_WithBlockingIssues_InStrictMode_FailsPipeline()
{
    // Arrange: set up a context with a collision
    var validationEngine = new ValidationEngineBuilder()
        .AddRule(new CabinetOverlapRule())
        .Build();
    var stage = new ValidationStage(validationEngine, new TestWhyEngine());

    var context = CreateContextWithOverlap();
    // Override strictness
    // ...

    var result = stage.Execute(context);

    Assert.False(result.Success);
    Assert.Contains(result.Issues, i => i.Code == "geometry.cabinet_overlap");
}
```

### 14.5 Property-Based Tests

For rules that operate on geometric state, property-based tests verify that:
- Non-overlapping cabinets never produce overlap issues
- A run with `OccupiedLength <= Capacity` never triggers capacity exceeded
- Issue IDs are stable across evaluation order permutations

---

## 15. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| **O(n^2) collision detection slows large projects** | Spatial indexing (grid-based or quadtree) for cabinet positions in future optimization. Initial implementation uses brute-force pairwise check — acceptable for MVP cabinet counts (<200) |
| **Rule explosion — too many rules overwhelm the user** | Category-based filtering in the UI. Info/Warning collapsed by default. Status bar shows counts only. Users expand what they need |
| **Stale validation state after undo/redo** | Undo/redo re-runs the full pipeline (or applies deltas). Stage 10 re-executes and rebuilds the `ValidationIssueIndex`. No stale issues persist |
| **Fix suggestions that conflict with each other** | Fixes are suggestions, not commands. The UI presents them individually. If two fixes conflict, the user resolves the conflict. No automatic multi-fix application |
| **Preview-safe rule accidentally accesses deep-path data** | `ValidationContext` nullable stage results enforce this at compile time. A preview-safe rule that accesses `PartResult` will get `null` and must handle it (or produce no issues). Tests verify preview-safe rules work with null deep-path state |
| **Rule ordering affects results** | Rules must be order-independent. Each rule evaluates against the same immutable `ValidationContext`. No rule depends on another rule's output. The engine does not guarantee execution order |
| **Validation during workflow transitions** | Approval and release-to-shop workflows run validation in `Strict` mode. The orchestrator receives the strictness setting from the workflow transition command |
| **Custom shop rules (future)** | The `IValidationRule` interface and builder pattern support dynamic rule registration. A future "shop rules" feature can register rules from configuration without changing the engine |
| **ManufactureBlocker on parts that exist across multiple cabinets** | Issue `AffectedEntityIds` can reference both the part and the cabinet. The index supports multi-entity lookup. The UI highlights all affected entities |
| **Validation rules needing catalog data (material specs, hardware limits)** | Rules that need catalog data receive it through constructor injection (DI). The `IValidationRule` interface is stateless in evaluation, but rules can hold injected read-only references to catalogs |
