# P5 — Why Engine Design

Source: `cabinet_ai_prompt_pack_v3_final.md` (Phase 5)
Context: `commands.md`, `orchestrator.md`

---

## 1. Goals

- Trace **every design decision** from user intent through resolution to final state
- Answer "why does this cabinet look like this?" at any point in the design — dimensions, material, hardware, position, cost
- Provide structured, queryable explanation data — not free-text logs
- Link commands → pipeline stage decisions → state deltas into a traversable graph
- Support the UI's ability to show explanation panels, decision trails, and "what changed and why" diffs
- Record without distorting — the Why Engine observes and records, it never influences resolution logic
- Keep explanation data lightweight enough for real-time querying during interactive design sessions
- Survive undo/redo — explanation history is append-only; undone decisions are marked, not deleted

---

## 2. Design Decisions

| Decision | Rationale |
|---|---|
| Explanation graph, not a log | A flat log can answer "what happened in order" but not "why is this entity in this state." A graph with typed edges enables both temporal and causal traversal |
| Append-only storage | Never delete or mutate explanation nodes. Undone commands get an `Undone` status marker. This preserves full audit history and enables "what was tried and reverted" queries |
| Nodes are lightweight value objects | Each node stores IDs, a decision type string, a description, and references — not full entity snapshots. The entities themselves hold the current state |
| Per-stage recording, not post-hoc | Stages call `IWhyEngine.RecordDecision()` as decisions happen. Post-hoc reconstruction is fragile and drifts from actual decision logic |
| Explanation is opt-in per decision, not per entity | Not every micro-mutation needs explanation. Stages record decisions at the **reasoning boundary** — the point where a choice was made that could have gone differently |
| Graph edges are typed | `CausedBy`, `ProducedBy`, `TriggeredBy`, `ResolvedFrom`, `ConstrainedBy` — typed edges enable targeted queries ("show me all constraint-driven decisions for this cabinet") |
| Command-level root nodes | Every command execution gets a root `CommandExplanationNode`. Stage decisions hang off it. This gives every explanation a clear entry point |
| Queryable by entity ID | The primary query pattern is "given entity X, show me all decisions that affected it." The graph supports reverse-traversal from entity → decision nodes |

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  ResolutionOrchestrator                                         │
│                                                                 │
│  ┌──────────┐  ┌──────────┐       ┌──────────┐                 │
│  │ Stage 1  │→ │ Stage 2  │→ ... →│ Stage 11 │                 │
│  └────┬─────┘  └────┬─────┘       └────┬─────┘                 │
│       │              │                  │                        │
│       │ RecordDecision()                │                        │
│       ▼              ▼                  ▼                        │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  IWhyEngine                                             │    │
│  │                                                         │    │
│  │  RecordDecision() ← called by stages during execution   │    │
│  │  RecordCommand()  ← called by orchestrator after stages │    │
│  │  RecordUndo()     ← called on undo                      │    │
│  │  RecordRedo()     ← called on redo                      │    │
│  └────────────────────────┬────────────────────────────────┘    │
│                           │                                      │
└───────────────────────────┼──────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Explanation Graph (in-memory, append-only)                     │
│                                                                 │
│  CommandExplanationNode (root)                                  │
│   ├── StageDecisionNode (stage 1: entity resolved)              │
│   ├── StageDecisionNode (stage 2: interpreted as slot insert)   │
│   ├── StageDecisionNode (stage 3: placed at index 4)            │
│   ├── StageDecisionNode (stage 4: opening resolved as 2-door)  │
│   ├── StageDecisionNode (stage 5: maple assigned, grain OK)    │
│   ├── ...                                                       │
│   ├── StateDeltaReference (what changed)                        │
│   └── ValidationIssueReference (what was flagged)               │
│                                                                 │
│  Entity Index: CabinetId → [node1, node2, ...]                 │
│  Stage Index:  StageNumber → [node1, node2, ...]               │
│  Command Index: CommandId → CommandExplanationNode              │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  UI / Query Layer                                               │
│                                                                 │
│  "Why is this cabinet 36 inches wide?"                          │
│  "What decisions affected this run?"                            │
│  "Show me the full trail for this command"                      │
│  "What was tried and reverted?"                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 4. Core Types (C#)

### 4.1 Explanation Node

The fundamental unit of the explanation graph. Each node represents a single recorded decision or event.

```csharp
namespace CabinetDesigner.Domain.Explanation;

using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A single node in the explanation graph.
/// Immutable after creation. Represents one decision, event, or linkage.
/// </summary>
public sealed record ExplanationNode
{
    public required ExplanationNodeId Id { get; init; }
    public required ExplanationNodeType NodeType { get; init; }
    public required CommandId CommandId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Pipeline stage that produced this node (null for command-level nodes).</summary>
    public int? StageNumber { get; init; }

    /// <summary>
    /// Machine-readable decision type discriminator.
    /// Examples: "entity_resolved", "slot_placed", "material_assigned",
    ///           "filler_inserted", "constraint_violated", "cost_calculated"
    /// </summary>
    public required string DecisionType { get; init; }

    /// <summary>Human-readable description of the decision and its rationale.</summary>
    public required string Description { get; init; }

    /// <summary>Entity IDs affected by this decision.</summary>
    public required IReadOnlyList<string> AffectedEntityIds { get; init; }

    /// <summary>Edges connecting this node to other nodes in the graph.</summary>
    public required IReadOnlyList<ExplanationEdge> Edges { get; init; }

    /// <summary>
    /// Structured key-value context for this decision.
    /// Stores the specific values that drove the decision (not the full entity state).
    /// Examples: { "selected_width": "36in", "reason": "nearest_standard_size" }
    /// </summary>
    public IReadOnlyDictionary<string, string>? Context { get; init; }

    // Note: node status (Active, Undone, Redone) is NOT stored here.
    // Status is a derived projection maintained by ExplanationStatusProjection,
    // rebuilt from UndoMarker/RedoMarker nodes in the history.
    // Nodes are immutable after creation — append-only history.
}

public enum ExplanationNodeType
{
    /// <summary>Root node for a command execution.</summary>
    CommandRoot,

    /// <summary>A decision made by a pipeline stage.</summary>
    StageDecision,

    /// <summary>A reference to a state delta produced by the command.</summary>
    DeltaReference,

    /// <summary>A reference to a validation issue.</summary>
    ValidationReference,

    /// <summary>An undo event marker.</summary>
    UndoMarker,

    /// <summary>A redo event marker.</summary>
    RedoMarker
}

public enum ExplanationNodeStatus
{
    /// <summary>Decision is currently in effect.</summary>
    Active,

    /// <summary>Decision was undone. State was reverted but the node persists for history.</summary>
    Undone,

    /// <summary>Decision was undone then redone. Currently in effect again.</summary>
    Redone
}

/// <summary>
/// Mutable projection that maps node IDs to their effective status.
/// Derived from UndoMarker/RedoMarker nodes in the append-only history.
/// Nodes themselves are never mutated — only this projection changes.
/// </summary>
public sealed class ExplanationStatusProjection
{
    private readonly Dictionary<ExplanationNodeId, ExplanationNodeStatus> _statuses = [];

    public ExplanationNodeStatus GetStatus(ExplanationNodeId nodeId) =>
        _statuses.GetValueOrDefault(nodeId, ExplanationNodeStatus.Active);

    public void MarkUndone(IEnumerable<ExplanationNodeId> nodeIds)
    {
        foreach (var id in nodeIds)
            _statuses[id] = ExplanationNodeStatus.Undone;
    }

    public void MarkRedone(IEnumerable<ExplanationNodeId> nodeIds)
    {
        foreach (var id in nodeIds)
        {
            if (_statuses.GetValueOrDefault(id) == ExplanationNodeStatus.Undone)
                _statuses[id] = ExplanationNodeStatus.Redone;
        }
    }
}
```

### 4.2 Explanation Edge

Typed edges connect nodes in the graph. Each edge describes how two decisions or events relate.

```csharp
namespace CabinetDesigner.Domain.Explanation;

using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// A directed edge in the explanation graph.
/// Connects a source node to a target node with a typed relationship.
/// </summary>
public sealed record ExplanationEdge
{
    public required ExplanationNodeId SourceNodeId { get; init; }
    public required ExplanationNodeId TargetNodeId { get; init; }
    public required ExplanationEdgeType EdgeType { get; init; }

    /// <summary>Optional label for the edge (e.g., "material_thickness_required").</summary>
    public string? Label { get; init; }
}

public enum ExplanationEdgeType
{
    /// <summary>This decision was caused by the target decision.</summary>
    CausedBy,

    /// <summary>This decision produced the target artifact (delta, part, etc.).</summary>
    Produced,

    /// <summary>This decision was triggered by the target event (command, undo, etc.).</summary>
    TriggeredBy,

    /// <summary>This decision resolved an ambiguity introduced by the target.</summary>
    ResolvedFrom,

    /// <summary>This decision was constrained by the target rule or catalog entry.</summary>
    ConstrainedBy,

    /// <summary>This decision superseded (replaced) the target decision.</summary>
    Supersedes,

    /// <summary>This decision validated the target state or artifact.</summary>
    Validated,

    /// <summary>Parent-child: this node is a child of the target (e.g., stage decision → command root).</summary>
    ChildOf
}
```

### 4.3 Explanation Rule Record

When a decision is driven by a specific rule (shop standard, hardware constraint, material rule), the rule itself is captured as a first-class record. This enables "show me all decisions driven by rule X" queries.

```csharp
namespace CabinetDesigner.Domain.Explanation;

/// <summary>
/// A rule that drove a decision. Captured alongside the decision node
/// so that users can understand what constraint or standard applied.
/// </summary>
public sealed record ExplanationRuleRecord
{
    /// <summary>Machine-readable rule identifier.</summary>
    public required string RuleId { get; init; }

    /// <summary>Human-readable rule name.</summary>
    public required string RuleName { get; init; }

    /// <summary>Category of the rule.</summary>
    public required ExplanationRuleCategory Category { get; init; }

    /// <summary>The source of the rule (e.g., "shop_standard.default", "hardware_catalog.blum_hinges").</summary>
    public required string Source { get; init; }

    /// <summary>Human-readable description of what the rule enforces.</summary>
    public required string Description { get; init; }

    /// <summary>The specific parameters the rule evaluated.</summary>
    public IReadOnlyDictionary<string, string>? EvaluatedParameters { get; init; }
}

public enum ExplanationRuleCategory
{
    ShopStandard,
    MaterialConstraint,
    HardwareConstraint,
    GeometryConstraint,
    ManufacturabilityRule,
    InstallRequirement,
    CostRule,
    BusinessRule
}
```

---

## 5. IWhyEngine Interface

The contract consumed by the `ResolutionOrchestrator` and pipeline stages. Defined in `orchestrator.md` section 8, fully specified here.

```csharp
namespace CabinetDesigner.Application;

using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;

/// <summary>
/// The Why Engine records the lineage of every design decision.
/// Append-only. Never mutates or deletes nodes.
/// Provides queryable access to the explanation graph.
/// </summary>
public interface IWhyEngine
{
    // --- Recording (write path) ---

    /// <summary>
    /// Record a completed command execution. Creates a CommandRoot node
    /// and links it to all stage decisions and state deltas.
    /// Called by the orchestrator after all stages complete successfully.
    /// </summary>
    IReadOnlyList<ExplanationNodeId> RecordCommand(
        IDesignCommand command,
        IReadOnlyList<StateDelta> deltas);

    /// <summary>
    /// Record a stage-level decision during pipeline execution.
    /// Called by individual stages as they make choices.
    /// </summary>
    ExplanationNodeId RecordDecision(
        CommandId commandId,
        int stageNumber,
        string decisionType,
        string description,
        IReadOnlyList<string>? affectedEntityIds = null,
        IReadOnlyDictionary<string, string>? context = null,
        ExplanationRuleRecord? rule = null);

    /// <summary>
    /// Record a decision with explicit edges to prior decisions.
    /// Used when a stage decision is directly caused by or constrained by
    /// a prior decision in the same pipeline run.
    /// </summary>
    ExplanationNodeId RecordDecisionWithEdges(
        CommandId commandId,
        int stageNumber,
        string decisionType,
        string description,
        IReadOnlyList<ExplanationEdge> edges,
        IReadOnlyList<string>? affectedEntityIds = null,
        IReadOnlyDictionary<string, string>? context = null,
        ExplanationRuleRecord? rule = null);

    /// <summary>Record an undo operation. Marks affected nodes as Undone.</summary>
    void RecordUndo(CommandMetadata metadata, IReadOnlyList<StateDelta> deltas);

    /// <summary>Record a redo operation. Marks affected nodes as Redone.</summary>
    void RecordRedo(CommandMetadata metadata, IReadOnlyList<StateDelta> deltas);

    // --- Querying (read path) ---

    /// <summary>Get all explanation nodes for a specific entity.</summary>
    IReadOnlyList<ExplanationNode> GetEntityHistory(string entityId);

    /// <summary>Get all explanation nodes for a specific command execution.</summary>
    IReadOnlyList<ExplanationNode> GetCommandExplanation(CommandId commandId);

    /// <summary>Get the root node for a command.</summary>
    ExplanationNode? GetCommandRoot(CommandId commandId);

    /// <summary>Get all decisions from a specific pipeline stage across all commands.</summary>
    IReadOnlyList<ExplanationNode> GetStageDecisions(int stageNumber);

    /// <summary>Get all decisions driven by a specific rule.</summary>
    IReadOnlyList<ExplanationNode> GetDecisionsByRule(string ruleId);

    /// <summary>
    /// Get the full decision trail for an entity: all nodes that affected it,
    /// in causal order (following edges), across all commands.
    /// </summary>
    IReadOnlyList<ExplanationNode> GetDecisionTrail(string entityId);

    /// <summary>
    /// Get the explanation for a specific property of an entity.
    /// Returns the most recent decision that set or influenced this property.
    /// </summary>
    ExplanationNode? GetPropertyExplanation(string entityId, string propertyName);

    /// <summary>Get all nodes with a given status (Active, Undone, Redone).</summary>
    IReadOnlyList<ExplanationNode> GetNodesByStatus(ExplanationNodeStatus status);

    /// <summary>Get all nodes in the graph. Use sparingly — prefer targeted queries.</summary>
    IReadOnlyList<ExplanationNode> GetAllNodes();
}
```

---

## 6. Implementation

### 6.1 WhyEngine Core

```csharp
namespace CabinetDesigner.Application.Explanation;

using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;

public sealed class WhyEngine : IWhyEngine
{
    private readonly IClock _clock;

    // --- Primary storage: append-only node list ---
    private readonly List<ExplanationNode> _nodes = [];

    // --- Status projection (mutable; derived from marker nodes) ---
    private readonly ExplanationStatusProjection _statusProjection = new();

    // --- Indexes for fast querying ---
    private readonly Dictionary<string, List<ExplanationNodeId>> _entityIndex = [];
    private readonly Dictionary<CommandId, List<ExplanationNodeId>> _commandIndex = [];
    private readonly Dictionary<CommandId, ExplanationNodeId> _commandRoots = [];
    private readonly Dictionary<int, List<ExplanationNodeId>> _stageIndex = [];
    private readonly Dictionary<string, List<ExplanationNodeId>> _ruleIndex = [];

    public WhyEngine(IClock clock)
    {
        _clock = clock;
    }

    // --- Recording ---

    public IReadOnlyList<ExplanationNodeId> RecordCommand(
        IDesignCommand command,
        IReadOnlyList<StateDelta> deltas)
    {
        var nodeIds = new List<ExplanationNodeId>();

        // 1. Create the command root node.
        // BuildCommandRootEdges needs the root ID upfront so edges are correctly sourced.
        var rootNodeId = ExplanationNodeId.New();
        var rootNode = new ExplanationNode
        {
            Id = rootNodeId,
            NodeType = ExplanationNodeType.CommandRoot,
            CommandId = command.Metadata.CommandId,
            Timestamp = _clock.Now,
            StageNumber = null,
            DecisionType = "command_executed",
            Description = command.Metadata.IntentDescription,
            AffectedEntityIds = command.Metadata.AffectedEntityIds,
            Edges = BuildCommandRootEdges(rootNodeId, command.Metadata.CommandId),
            Context = new Dictionary<string, string>
            {
                ["command_type"] = command.CommandType,
                ["origin"] = command.Metadata.Origin.ToString()
            }
        };

        AppendNode(rootNode);
        _commandRoots[command.Metadata.CommandId] = rootNode.Id;
        nodeIds.Add(rootNode.Id);

        // 2. Create delta reference nodes
        foreach (var delta in deltas)
        {
            var deltaNodeId = ExplanationNodeId.New();
            var deltaNode = new ExplanationNode
            {
                Id = deltaNodeId,
                NodeType = ExplanationNodeType.DeltaReference,
                CommandId = command.Metadata.CommandId,
                Timestamp = _clock.Now,
                StageNumber = null,
                DecisionType = $"delta_{delta.Operation.ToString().ToLowerInvariant()}",
                Description = $"{delta.Operation} {delta.EntityType} {delta.EntityId}",
                AffectedEntityIds = [delta.EntityId],
                Edges = [new ExplanationEdge
                {
                    SourceNodeId = deltaNodeId,
                    TargetNodeId = rootNode.Id,
                    EdgeType = ExplanationEdgeType.ChildOf
                }]
            };

            AppendNode(deltaNode);
            nodeIds.Add(deltaNode.Id);
        }

        return nodeIds;
    }

    public ExplanationNodeId RecordDecision(
        CommandId commandId,
        int stageNumber,
        string decisionType,
        string description,
        IReadOnlyList<string>? affectedEntityIds = null,
        IReadOnlyDictionary<string, string>? context = null,
        ExplanationRuleRecord? rule = null)
    {
        var nodeId = ExplanationNodeId.New();
        var edges = new List<ExplanationEdge>();

        // Link to command root if it exists (it may not yet during pipeline execution)
        if (_commandRoots.TryGetValue(commandId, out var rootId))
        {
            edges.Add(new ExplanationEdge
            {
                SourceNodeId = nodeId,
                TargetNodeId = rootId,
                EdgeType = ExplanationEdgeType.ChildOf
            });
        }

        var node = new ExplanationNode
        {
            Id = nodeId,
            NodeType = ExplanationNodeType.StageDecision,
            CommandId = commandId,
            Timestamp = _clock.Now,
            StageNumber = stageNumber,
            DecisionType = decisionType,
            Description = description,
            AffectedEntityIds = affectedEntityIds ?? [],
            Edges = edges,
            Context = context
        };

        AppendNode(node);

        // Index by rule if present
        if (rule is not null)
        {
            if (!_ruleIndex.TryGetValue(rule.RuleId, out var ruleNodes))
            {
                ruleNodes = [];
                _ruleIndex[rule.RuleId] = ruleNodes;
            }
            ruleNodes.Add(nodeId);
        }

        return nodeId;
    }

    public ExplanationNodeId RecordDecisionWithEdges(
        CommandId commandId,
        int stageNumber,
        string decisionType,
        string description,
        IReadOnlyList<ExplanationEdge> edges,
        IReadOnlyList<string>? affectedEntityIds = null,
        IReadOnlyDictionary<string, string>? context = null,
        ExplanationRuleRecord? rule = null)
    {
        var nodeId = ExplanationNodeId.New();

        // Merge caller-provided edges with standard ChildOf edge
        var allEdges = new List<ExplanationEdge>(edges);
        if (_commandRoots.TryGetValue(commandId, out var rootId))
        {
            allEdges.Add(new ExplanationEdge
            {
                SourceNodeId = nodeId,
                TargetNodeId = rootId,
                EdgeType = ExplanationEdgeType.ChildOf
            });
        }

        var node = new ExplanationNode
        {
            Id = nodeId,
            NodeType = ExplanationNodeType.StageDecision,
            CommandId = commandId,
            Timestamp = _clock.Now,
            StageNumber = stageNumber,
            DecisionType = decisionType,
            Description = description,
            AffectedEntityIds = affectedEntityIds ?? [],
            Edges = allEdges,
            Context = context
        };

        AppendNode(node);

        if (rule is not null)
        {
            if (!_ruleIndex.TryGetValue(rule.RuleId, out var ruleNodes))
            {
                ruleNodes = [];
                _ruleIndex[rule.RuleId] = ruleNodes;
            }
            ruleNodes.Add(nodeId);
        }

        return nodeId;
    }

    public void RecordUndo(CommandMetadata metadata, IReadOnlyList<StateDelta> deltas)
    {
        // Update the status projection — nodes themselves are never mutated.
        if (_commandIndex.TryGetValue(metadata.CommandId, out var commandNodeIds))
            _statusProjection.MarkUndone(commandNodeIds);

        // Append an UndoMarker node to the immutable history.
        var undoNodeId = ExplanationNodeId.New();
        var undoNode = new ExplanationNode
        {
            Id = undoNodeId,
            NodeType = ExplanationNodeType.UndoMarker,
            CommandId = metadata.CommandId,
            Timestamp = _clock.Now,
            StageNumber = null,
            DecisionType = "undo",
            Description = $"Undid: {metadata.IntentDescription}",
            AffectedEntityIds = metadata.AffectedEntityIds,
            Edges = _commandRoots.TryGetValue(metadata.CommandId, out var rootId)
                ? [new ExplanationEdge
                {
                    SourceNodeId = undoNodeId,
                    TargetNodeId = rootId,
                    EdgeType = ExplanationEdgeType.Supersedes
                }]
                : []
        };

        AppendNode(undoNode);
    }

    public void RecordRedo(CommandMetadata metadata, IReadOnlyList<StateDelta> deltas)
    {
        // Update the status projection — nodes themselves are never mutated.
        if (_commandIndex.TryGetValue(metadata.CommandId, out var commandNodeIds))
            _statusProjection.MarkRedone(commandNodeIds);

        // Append a RedoMarker node to the immutable history.
        var redoNodeId = ExplanationNodeId.New();
        var redoNode = new ExplanationNode
        {
            Id = redoNodeId,
            NodeType = ExplanationNodeType.RedoMarker,
            CommandId = metadata.CommandId,
            Timestamp = _clock.Now,
            StageNumber = null,
            DecisionType = "redo",
            Description = $"Redid: {metadata.IntentDescription}",
            AffectedEntityIds = metadata.AffectedEntityIds,
            Edges = _commandRoots.TryGetValue(metadata.CommandId, out var rootId)
                ? [new ExplanationEdge
                {
                    SourceNodeId = redoNodeId,
                    TargetNodeId = rootId,
                    EdgeType = ExplanationEdgeType.Supersedes
                }]
                : []
        };

        AppendNode(redoNode);
    }

    // --- Querying ---

    public IReadOnlyList<ExplanationNode> GetEntityHistory(string entityId)
    {
        if (!_entityIndex.TryGetValue(entityId, out var nodeIds))
            return [];

        return nodeIds
            .Select(id => _nodes.First(n => n.Id == id))
            .OrderBy(n => n.Timestamp)
            .ToList();
    }

    public IReadOnlyList<ExplanationNode> GetCommandExplanation(CommandId commandId)
    {
        if (!_commandIndex.TryGetValue(commandId, out var nodeIds))
            return [];

        return nodeIds
            .Select(id => _nodes.First(n => n.Id == id))
            .OrderBy(n => n.StageNumber ?? 0)
            .ThenBy(n => n.Timestamp)
            .ToList();
    }

    public ExplanationNode? GetCommandRoot(CommandId commandId)
    {
        if (!_commandRoots.TryGetValue(commandId, out var rootId))
            return null;

        return _nodes.FirstOrDefault(n => n.Id == rootId);
    }

    public IReadOnlyList<ExplanationNode> GetStageDecisions(int stageNumber)
    {
        if (!_stageIndex.TryGetValue(stageNumber, out var nodeIds))
            return [];

        return nodeIds
            .Select(id => _nodes.First(n => n.Id == id))
            .OrderBy(n => n.Timestamp)
            .ToList();
    }

    public IReadOnlyList<ExplanationNode> GetDecisionsByRule(string ruleId)
    {
        if (!_ruleIndex.TryGetValue(ruleId, out var nodeIds))
            return [];

        return nodeIds
            .Select(id => _nodes.First(n => n.Id == id))
            .OrderBy(n => n.Timestamp)
            .ToList();
    }

    public IReadOnlyList<ExplanationNode> GetDecisionTrail(string entityId)
    {
        if (!_entityIndex.TryGetValue(entityId, out var nodeIds))
            return [];

        // Walk the graph: start from entity's nodes, follow CausedBy / ConstrainedBy
        // edges backwards to find the full causal chain.
        var visited = new HashSet<ExplanationNodeId>();
        var trail = new List<ExplanationNode>();
        var queue = new Queue<ExplanationNodeId>(nodeIds);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!visited.Add(currentId)) continue;

            var node = _nodes.FirstOrDefault(n => n.Id == currentId);
            if (node is null) continue;

            trail.Add(node);

            // Follow causal edges backward
            foreach (var edge in node.Edges)
            {
                if (edge.EdgeType is ExplanationEdgeType.CausedBy
                    or ExplanationEdgeType.ConstrainedBy
                    or ExplanationEdgeType.ResolvedFrom
                    or ExplanationEdgeType.TriggeredBy)
                {
                    queue.Enqueue(edge.TargetNodeId);
                }
            }
        }

        return trail.OrderBy(n => n.Timestamp).ToList();
    }

    public ExplanationNode? GetPropertyExplanation(string entityId, string propertyName)
    {
        if (!_entityIndex.TryGetValue(entityId, out var nodeIds))
            return null;

        // Find the most recent active decision that mentions this property in its context
        return nodeIds
            .Select(id => _nodes.FirstOrDefault(n => n.Id == id))
            .Where(n => n is not null
                && _statusProjection.GetStatus(n.Id) != ExplanationNodeStatus.Undone
                && n.Context is not null
                && n.Context.ContainsKey(propertyName))
            .OrderByDescending(n => n!.Timestamp)
            .FirstOrDefault();
    }

    public IReadOnlyList<ExplanationNode> GetNodesByStatus(ExplanationNodeStatus status)
    {
        return _nodes
            .Where(n => _statusProjection.GetStatus(n.Id) == status)
            .ToList();
    }

    public IReadOnlyList<ExplanationNode> GetAllNodes() => _nodes.ToList();

    // --- Internal helpers ---

    private void AppendNode(ExplanationNode node)
    {
        _nodes.Add(node);

        // Update command index
        if (!_commandIndex.TryGetValue(node.CommandId, out var commandNodes))
        {
            commandNodes = [];
            _commandIndex[node.CommandId] = commandNodes;
        }
        commandNodes.Add(node.Id);

        // Update entity index
        foreach (var entityId in node.AffectedEntityIds)
        {
            if (!_entityIndex.TryGetValue(entityId, out var entityNodes))
            {
                entityNodes = [];
                _entityIndex[entityId] = entityNodes;
            }
            entityNodes.Add(node.Id);
        }

        // Update stage index
        if (node.StageNumber.HasValue)
        {
            if (!_stageIndex.TryGetValue(node.StageNumber.Value, out var stageNodes))
            {
                stageNodes = [];
                _stageIndex[node.StageNumber.Value] = stageNodes;
            }
            stageNodes.Add(node.Id);
        }
    }

    /// <summary>
    /// Build edges from the root node to all stage decisions already recorded for this command.
    /// Stages record before the root is created, so we link them retroactively.
    /// The root node ID is passed in so edges are correctly sourced.
    /// </summary>
    private IReadOnlyList<ExplanationEdge> BuildCommandRootEdges(
        ExplanationNodeId rootNodeId, CommandId commandId)
    {
        if (!_commandIndex.TryGetValue(commandId, out var existingNodeIds))
            return [];

        return existingNodeIds
            .Select(id => new ExplanationEdge
            {
                SourceNodeId = rootNodeId,
                TargetNodeId = id,
                EdgeType = ExplanationEdgeType.Produced
            })
            .ToList();
    }
}
```

---

## 7. Stage Integration Patterns

Each pipeline stage integrates with the Why Engine by calling `RecordDecision()` or `RecordDecisionWithEdges()` at decision points. Below are the decision types each stage is expected to emit.

### 7.1 Decision Type Catalog

| Stage | Decision Type | When Emitted |
|---|---|---|
| 1 — Input Capture | `entity_resolved` | Entity loaded by typed ID |
| 1 — Input Capture | `template_expanded` | Template reference expanded to concrete parameters |
| 1 — Input Capture | `parameter_defaulted` | A missing parameter was filled with a default value |
| 2 — Interaction Interpretation | `intent_interpreted` | Abstract command intent mapped to concrete domain operation |
| 2 — Interaction Interpretation | `placement_resolved` | `RunPlacement.EndOfRun` → concrete slot index |
| 2 — Interaction Interpretation | `neighbor_identified` | Adjacent cabinets identified for the operation |
| 3 — Spatial Resolution | `slot_placed` | Cabinet placed into a run slot at a specific index |
| 3 — Spatial Resolution | `slot_repositioned` | Existing slot moved due to insert/remove |
| 3 — Spatial Resolution | `adjacency_updated` | Left/right neighbor relationships changed |
| 3 — Spatial Resolution | `run_capacity_checked` | Remaining run length evaluated against requested width |
| 4 — Engineering Resolution | `assembly_resolved` | Cabinet openings, construction details determined |
| 4 — Engineering Resolution | `filler_required` | Filler strip needed at a specific slot position |
| 4 — Engineering Resolution | `end_condition_set` | Run end condition determined (wall, filler, open, scribe) |
| 4 — Engineering Resolution | `reveal_calculated` | Overlay/reveal amount computed for construction method |
| 4 — Engineering Resolution | `shared_stile_applied` | Adjacent frameless cabinets share a stile |
| 5 — Constraint Propagation | `material_assigned` | Material selected for a part |
| 5 — Constraint Propagation | `grain_direction_set` | Grain direction determined for material on part |
| 5 — Constraint Propagation | `hardware_assigned` | Hardware items selected for an opening |
| 5 — Constraint Propagation | `boring_pattern_set` | Boring pattern propagated from hardware to part |
| 5 — Constraint Propagation | `constraint_violated` | A constraint rule was not satisfied |
| 6 — Part Generation | `part_generated` | A concrete part created from assembly resolution |
| 6 — Part Generation | `edge_treatment_assigned` | Edge banding assigned to exposed edges |
| 6 — Part Generation | `dimension_adjusted` | Part dimension adjusted for material thickness / joinery |
| 7 — Manufacturing Planning | `cut_list_entry_created` | Part added to cut list with kerf allowance |
| 7 — Manufacturing Planning | `machining_op_planned` | CNC operation planned for a part |
| 7 — Manufacturing Planning | `nesting_optimized` | Sheet nesting layout computed |
| 8 — Install Planning | `install_order_determined` | Cabinet's position in install sequence set |
| 8 — Install Planning | `fastening_planned` | Fastening requirements identified |
| 9 — Costing | `cost_calculated` | Cost computed for material, hardware, or labor |
| 9 — Costing | `revision_delta_computed` | Cost difference from previous revision calculated |
| 10 — Validation | `validation_passed` | A validation rule passed |
| 10 — Validation | `validation_failed` | A validation rule failed (with severity and suggestion) |
| 11 — Packaging | `snapshot_created` | Immutable snapshot frozen and bound to revision |

### 7.2 Example: Stage 5 Recording a Material Assignment

```csharp
// Inside ConstraintPropagationStage.Execute()

var explanationNodeId = _whyEngine.RecordDecision(
    commandId: context.Command.Metadata.CommandId,
    stageNumber: 5,
    decisionType: "material_assigned",
    description: $"Assigned {material.Name} ({material.SheetThickness}) to left side panel " +
                 $"of cabinet {cabinetId}. Source: style preset '{preset.Name}'.",
    affectedEntityIds: [cabinetId.Value.ToString(), partId],
    context: new Dictionary<string, string>
    {
        ["material_id"] = material.Id.Value.ToString(),
        ["material_name"] = material.Name,
        ["nominal_thickness"] = material.SheetThickness.Nominal.ToString(),
        ["actual_thickness"] = material.SheetThickness.Actual.ToString(),
        ["grain_direction"] = GrainDirection.LengthWise.ToString(),
        ["source"] = "style_preset",
        ["preset_name"] = preset.Name
    },
    rule: new ExplanationRuleRecord
    {
        RuleId = "material.style_preset_default",
        RuleName = "Style Preset Default Material",
        Category = ExplanationRuleCategory.MaterialConstraint,
        Source = $"style_preset.{preset.PresetId}",
        Description = "When no cabinet-level override exists, assign the default case material from the active style preset.",
        EvaluatedParameters = new Dictionary<string, string>
        {
            ["has_cabinet_override"] = "false",
            ["preset_default_material"] = material.Name
        }
    });
```

### 7.3 Example: Stage 4 Recording a Constrained Decision with Edges

```csharp
// Inside EngineeringResolutionStage.Execute()
// A filler is required because the run gap doesn't match any standard cabinet width.

var gapDecisionId = _whyEngine.RecordDecision(
    commandId: context.Command.Metadata.CommandId,
    stageNumber: 4,
    decisionType: "filler_required",
    description: $"Run {runId} has a {gap} gap after cabinet placement. " +
                 $"No standard cabinet width fits. Inserting filler strip.",
    affectedEntityIds: [runId.Value.ToString()],
    context: new Dictionary<string, string>
    {
        ["gap_width"] = gap.ToString(),
        ["nearest_standard_width"] = nearestStandard.ToString(),
        ["filler_width"] = gap.ToString()
    });

// Later, when the filler's material is assigned in stage 5, link back:
var materialDecisionId = _whyEngine.RecordDecisionWithEdges(
    commandId: context.Command.Metadata.CommandId,
    stageNumber: 5,
    decisionType: "material_assigned",
    description: $"Assigned {material.Name} to filler strip in run {runId}.",
    edges: [new ExplanationEdge
    {
        SourceNodeId = ExplanationNodeId.New(), // will be replaced by engine
        TargetNodeId = gapDecisionId,
        EdgeType = ExplanationEdgeType.CausedBy,
        Label = "filler_required_material"
    }],
    affectedEntityIds: [runId.Value.ToString(), fillerPartId]);
```

---

## 8. Query Patterns

The Why Engine supports the following user-facing query patterns. The UI consumes these via the `IWhyEngine` query methods.

### 8.1 "Why is this cabinet 36 inches wide?"

```csharp
// User clicks a cabinet and asks "why this width?"
var explanation = _whyEngine.GetPropertyExplanation(
    entityId: cabinetId.Value.ToString(),
    propertyName: "nominal_width");

// Returns the most recent active decision that set or influenced the width.
// UI renders: "Width set to 36\" by command 'Add 36\" base cabinet to end of kitchen run'
//              (user action, 2 minutes ago)"
```

### 8.2 "What happened to this run?"

```csharp
// User selects a run and opens the explanation panel
var history = _whyEngine.GetEntityHistory(runId.Value.ToString());

// Returns all decisions affecting this run, in chronological order.
// UI renders a timeline:
//   1. Run created along north wall (command: CreateRunCommand)
//   2. Cabinet B-36 placed at slot 0 (command: AddCabinetToRunCommand)
//   3. Cabinet B-24 placed at slot 1 (command: AddCabinetToRunCommand)
//   4. Filler required: 1.5" gap (stage 4: engineering resolution)
//   5. Material assigned to filler: maple plywood (stage 5: constraint propagation)
```

### 8.3 "Show me the full decision trail for this cabinet"

```csharp
// User wants the complete causal chain
var trail = _whyEngine.GetDecisionTrail(cabinetId.Value.ToString());

// Returns all nodes reachable by following CausedBy/ConstrainedBy edges:
//   1. User dragged cabinet type B-36 (input capture)
//   2. Interpreted as AddCabinetToRun at slot 3 (interaction interpretation)
//   3. Placed at position (120", 0") in run R-1 (spatial resolution)
//   4. Resolved as 2-door base cabinet (engineering resolution)
//   5. Maple assigned to case (constraint propagation, rule: style_preset_default)
//   6. Blum 110° hinge assigned (constraint propagation, rule: hardware_compatibility)
//   7. Left side panel: 23.25" x 34.5" (part generation)
//   8. Cut list entry: 23.5" x 34.75" with 1/8" kerf (manufacturing planning)
```

### 8.4 "What was tried and reverted?"

```csharp
// User wants to see undone decisions
var undone = _whyEngine.GetNodesByStatus(ExplanationNodeStatus.Undone);

// Returns all decisions that were undone but still preserved in the graph.
// UI can render: "You tried moving cabinet B-36 to slot 5 but undid it."
```

### 8.5 "What decisions were driven by the shop standard?"

```csharp
// Shop manager wants to audit which decisions came from shop standards
var standardDriven = _whyEngine.GetDecisionsByRule("shop_standard.default_reveal");

// Returns all decisions where the default reveal shop standard was applied.
```

---

## 9. Explanation Graph Lifecycle

### 9.1 Per-Command Lifecycle

```
1. PIPELINE BEGINS
   → DeltaTracker.Begin()
   → (No Why Engine call yet)

2. STAGES 1-11 EXECUTE
   → Each stage calls RecordDecision() / RecordDecisionWithEdges()
   → Stage decisions are appended to the graph immediately
   → Decisions reference the CommandId but no root node exists yet

3. PIPELINE COMPLETES SUCCESSFULLY
   → Orchestrator calls RecordCommand()
   → CommandRoot node created, linked to all stage decisions via edges
   → Delta reference nodes created for each state delta

4. PIPELINE FAILS
   → Orchestrator does NOT call RecordCommand()
   → Stage decisions already recorded remain in the graph (for debugging)
   → No CommandRoot node — orphaned stage decisions are identifiable by missing root

5. UNDO
   → Orchestrator calls RecordUndo()
   → All nodes for this command marked as Undone
   → UndoMarker node appended with Supersedes edge to command root

6. REDO
   → Orchestrator calls RecordRedo()
   → Undone nodes restored to Redone status
   → RedoMarker node appended
```

### 9.2 Session Lifecycle

```
1. SESSION START
   → WhyEngine initialized with empty graph
   → If loading a saved project, prior session's explanation graph can be restored from snapshot

2. DURING SESSION
   → Graph grows append-only as commands execute
   → Indexes maintained incrementally

3. SESSION END / PROJECT CLOSE
   → Graph can be serialized for persistence (optional, for audit trail)
   → UndoStack.Clear() does NOT clear the Why Engine — explanation persists
   → On project close, the graph is discarded from memory

4. SNAPSHOT PERSISTENCE (optional)
   → Stage 11 (packaging) can serialize the current explanation graph as part of the snapshot
   → Enables post-hoc audit of how a specific revision was produced
```

---

## 10. Performance Considerations

| Concern | Strategy |
|---|---|
| **Node count growth** | A typical command produces 5-15 explanation nodes (1 root + stage decisions + delta refs). A 100-command session ≈ 500-1500 nodes. Well within memory for desktop |
| **Index maintenance** | All indexes are dictionary-based with list values. Append is O(1). Lookup is O(1) to the bucket, O(n) within the bucket (small n per entity) |
| **Graph traversal** | `GetDecisionTrail` does BFS over edges. Depth is bounded by pipeline stage count (11) × command count. For interactive sessions, this is sub-millisecond |
| **Memory footprint** | Each node is ~200-400 bytes (IDs, strings, small dictionaries). 1500 nodes ≈ 300-600 KB. Negligible |
| **Serialization** | Nodes are record types with primitive/string fields. System.Text.Json handles them. Polymorphic serialization not needed — all nodes are `ExplanationNode` |
| **Fast path (preview)** | Preview does NOT record to the Why Engine. Zero overhead during drag operations |

---

## 11. Risks & Edge Cases

| Risk | Mitigation |
|---|---|
| **Stage decisions recorded before command root exists** | By design. Stages record during execution; the root is created after. The `_commandIndex` groups them by `CommandId`. Root edges are built retroactively via `BuildCommandRootEdges()` |
| **Failed pipeline leaves orphaned stage decisions** | Orphaned nodes (no root) are identifiable and useful for debugging. They can be garbage-collected on session end or retained for diagnostics |
| **Undo/Redo status** | Status is NOT stored on nodes. `ExplanationStatusProjection` maintains a separate dictionary updated by `RecordUndo`/`RecordRedo`. Node list is never mutated. Append-only claim is now honest. |
| **Context dictionary stores strings only** | By design. Strongly typed values would require polymorphic serialization. Strings are universally serializable, human-readable, and sufficient for explanation display. Parsing for specific values is the query layer's job |
| **Rule records not centrally registered** | Rules are recorded as embedded data on explanation nodes, not in a separate registry. This is sufficient for MVP. If rule management becomes a feature (e.g., "edit shop standard, see all affected decisions"), extract a rule registry |
| **Graph size in long sessions** | Desktop sessions rarely exceed 500 commands. If commercial/batch use cases emerge, add a compaction strategy: prune nodes for superseded/undone commands older than N minutes |
| **Explanation accuracy depends on stages recording correctly** | Each stage is responsible for its own recording. Integration tests should verify that expected decision types are emitted for each command type. Missing explanation is a test failure, not a runtime error |
| **GetDecisionTrail may be slow for heavily interconnected graphs** | BFS with visited set prevents infinite loops. Depth is bounded. If performance degrades, add a depth limit parameter to the query |
| **Concurrent reads during pipeline execution** | Single-threaded execution guarantee from the orchestrator. No concurrent writes or reads during a pipeline run. Queries only happen between commands (user interaction time) |
