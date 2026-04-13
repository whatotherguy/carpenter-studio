using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Explanation;

public sealed class WhyEngine : IWhyEngine
{
    private static readonly IReadOnlyDictionary<string, string> EmptyContext = new Dictionary<string, string>();
    private static readonly ExplanationEdgeType[] CausalEdgeTypes =
    [
        ExplanationEdgeType.CausedBy,
        ExplanationEdgeType.ConstrainedBy,
        ExplanationEdgeType.ResolvedFrom,
        ExplanationEdgeType.TriggeredBy,
        ExplanationEdgeType.ChildOf
    ];

    private readonly List<ExplanationNode> _nodes = [];
    private readonly Dictionary<ExplanationNodeId, ExplanationNode> _nodeLookup = [];
    private readonly Dictionary<ExplanationNodeId, long> _nodeOrder = [];
    private readonly Dictionary<CommandId, ExplanationNodeId> _commandRoots = [];
    private readonly Dictionary<CommandId, List<ExplanationNodeId>> _commandIndex = [];
    private readonly Dictionary<string, List<ExplanationNodeId>> _entityIndex = [];
    private readonly Dictionary<int, List<ExplanationNodeId>> _stageIndex = [];
    private readonly Dictionary<string, List<ExplanationNodeId>> _ruleIndex = [];
    private readonly ExplanationStatusProjection _statusProjection = new();
    private long _nextSequence;
    private readonly object _lock = new();

    public IReadOnlyList<ExplanationNodeId> RecordCommand(IDesignCommand command, IReadOnlyList<StateDelta> deltas)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(deltas);

        lock (_lock)
        {
            var rootNodeId = ExplanationNodeId.New();
            var commandId = command.Metadata.CommandId;
            var rootNode = new ExplanationNode
            {
                Id = rootNodeId,
                NodeType = ExplanationNodeType.CommandRoot,
                CommandId = commandId,
                Timestamp = NextTimestamp(),
                DecisionType = command.CommandType,
                Description = command.Metadata.IntentDescription,
                AffectedEntityIds = NormalizeStrings(command.Metadata.AffectedEntityIds),
                Edges = BuildCommandRootEdges(rootNodeId, commandId),
                Context = BuildCommandContext(command),
                Rule = null
            };

            AppendNode(rootNode);
            _commandRoots[commandId] = rootNodeId;

            var createdNodeIds = new List<ExplanationNodeId>(deltas.Count + 1) { rootNodeId };
            foreach (var delta in deltas)
            {
                var deltaNodeId = ExplanationNodeId.New();
                var deltaNode = new ExplanationNode
                {
                    Id = deltaNodeId,
                    NodeType = ExplanationNodeType.DeltaReference,
                    CommandId = commandId,
                    Timestamp = NextTimestamp(),
                    DecisionType = $"delta.{delta.Operation.ToString().ToLowerInvariant()}",
                    Description = BuildDeltaDescription(delta),
                    AffectedEntityIds = NormalizeStrings([delta.EntityId]),
                    Edges =
                    [
                        new ExplanationEdge
                        {
                            SourceNodeId = deltaNodeId,
                            TargetNodeId = rootNodeId,
                            EdgeType = ExplanationEdgeType.ChildOf,
                            Label = "command_root"
                        }
                    ],
                    Context = BuildDeltaContext(delta),
                    Rule = null
                };

                AppendNode(deltaNode);
                createdNodeIds.Add(deltaNodeId);
            }

            return createdNodeIds;
        }
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
        lock (_lock)
        {
            return RecordDecisionInternal(
                commandId,
                stageNumber,
                decisionType,
                description,
                [],
                affectedEntityIds,
                context,
                rule);
        }
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
        lock (_lock)
        {
            return RecordDecisionInternal(
                commandId,
                stageNumber,
                decisionType,
                description,
                edges,
                affectedEntityIds,
                context,
                rule);
        }
    }

    public IReadOnlyList<ExplanationNodeId> RecordUndo(
        CommandMetadata commandMetadata,
        IReadOnlyList<StateDelta> deltas,
        IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds)
    {
        ArgumentNullException.ThrowIfNull(commandMetadata);
        ArgumentNullException.ThrowIfNull(priorExplanationNodeIds);

        lock (_lock)
        {
            var affectedNodeIds = OrderNodeIds(priorExplanationNodeIds).ToArray();
            _statusProjection.MarkUndone(affectedNodeIds);

            var markerNodeId = ExplanationNodeId.New();
            var markerNode = new ExplanationNode
            {
                Id = markerNodeId,
                NodeType = ExplanationNodeType.UndoMarker,
                CommandId = commandMetadata.CommandId,
                Timestamp = NextTimestamp(),
                DecisionType = "undo",
                Description = $"Undo applied for '{commandMetadata.IntentDescription}'.",
                AffectedEntityIds = NormalizeStrings(commandMetadata.AffectedEntityIds),
                Edges = affectedNodeIds
                    .Select(targetId => new ExplanationEdge
                    {
                        SourceNodeId = markerNodeId,
                        TargetNodeId = targetId,
                        EdgeType = ExplanationEdgeType.Supersedes,
                        Label = "undo"
                    })
                    .ToArray(),
                Context = BuildUndoRedoContext(commandMetadata, deltas, "undo"),
                Rule = null
            };

            AppendNode(markerNode);
            return [markerNodeId];
        }
    }

    public IReadOnlyList<ExplanationNodeId> RecordRedo(
        CommandMetadata commandMetadata,
        IReadOnlyList<StateDelta> deltas,
        IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds)
    {
        ArgumentNullException.ThrowIfNull(commandMetadata);
        ArgumentNullException.ThrowIfNull(priorExplanationNodeIds);

        lock (_lock)
        {
            var affectedNodeIds = OrderNodeIds(priorExplanationNodeIds).ToArray();
            _statusProjection.MarkRedone(affectedNodeIds);

            var markerNodeId = ExplanationNodeId.New();
            var markerNode = new ExplanationNode
            {
                Id = markerNodeId,
                NodeType = ExplanationNodeType.RedoMarker,
                CommandId = commandMetadata.CommandId,
                Timestamp = NextTimestamp(),
                DecisionType = "redo",
                Description = $"Redo applied for '{commandMetadata.IntentDescription}'.",
                AffectedEntityIds = NormalizeStrings(commandMetadata.AffectedEntityIds),
                Edges = affectedNodeIds
                    .Select(targetId => new ExplanationEdge
                    {
                        SourceNodeId = markerNodeId,
                        TargetNodeId = targetId,
                        EdgeType = ExplanationEdgeType.TriggeredBy,
                        Label = "redo"
                    })
                    .ToArray(),
                Context = BuildUndoRedoContext(commandMetadata, deltas, "redo"),
                Rule = null
            };

            AppendNode(markerNode);
            return [markerNodeId];
        }
    }

    public IReadOnlyList<ExplanationNode> GetEntityHistory(string entityId)
    {
        lock (_lock)
        {
            if (!_entityIndex.TryGetValue(entityId, out var nodeIds))
            {
                return [];
            }

            return OrderNodeIds(nodeIds).Select(GetNode).ToArray();
        }
    }

    public IReadOnlyList<ExplanationNode> GetCommandExplanation(CommandId commandId)
    {
        lock (_lock)
        {
            if (!_commandIndex.TryGetValue(commandId, out var nodeIds))
            {
                return [];
            }

            return OrderNodeIds(nodeIds).Select(GetNode).ToArray();
        }
    }

    public ExplanationNode? GetCommandRoot(CommandId commandId)
    {
        lock (_lock)
        {
            return _commandRoots.TryGetValue(commandId, out var nodeId)
                ? GetNode(nodeId)
                : null;
        }
    }

    public IReadOnlyList<ExplanationNode> GetStageDecisions(int stageNumber)
    {
        lock (_lock)
        {
            if (!_stageIndex.TryGetValue(stageNumber, out var nodeIds))
            {
                return [];
            }

            return OrderNodeIds(nodeIds).Select(GetNode).ToArray();
        }
    }

    public IReadOnlyList<ExplanationNode> GetDecisionsByRule(string ruleId)
    {
        lock (_lock)
        {
            if (!_ruleIndex.TryGetValue(ruleId, out var nodeIds))
            {
                return [];
            }

            return OrderNodeIds(nodeIds).Select(GetNode).ToArray();
        }
    }

    public IReadOnlyList<ExplanationNode> GetDecisionTrail(string entityId)
    {
        lock (_lock)
        {
            if (!_entityIndex.TryGetValue(entityId, out var startingNodeIds))
            {
                return [];
            }

            var visited = new HashSet<ExplanationNodeId>();
            var orderedTrail = new List<ExplanationNode>();

            foreach (var nodeId in OrderNodeIds(startingNodeIds))
            {
                VisitDecisionTrail(nodeId, visited, orderedTrail);
            }

            return orderedTrail;
        }
    }

    public ExplanationNode? GetPropertyExplanation(string entityId, string propertyName)
    {
        lock (_lock)
        {
            if (!_entityIndex.TryGetValue(entityId, out var nodeIds))
            {
                return null;
            }

            var directKey = propertyName;
            var prefixedKey = $"property:{propertyName}";

            return OrderNodeIds(nodeIds)
                .Select(GetNode)
                .Where(node =>
                    _statusProjection.GetStatus(node.Id) != ExplanationNodeStatus.Undone &&
                    node.Context is not null &&
                    (node.Context.ContainsKey(directKey) || node.Context.ContainsKey(prefixedKey)))
                .LastOrDefault();
        }
    }

    public IReadOnlyList<ExplanationNode> GetNodesByStatus(ExplanationNodeStatus status)
    {
        lock (_lock)
        {
            return _nodes.Where(node => _statusProjection.GetStatus(node.Id) == status).ToArray();
        }
    }

    public IReadOnlyList<ExplanationNode> GetAllNodes()
    {
        lock (_lock)
        {
            return _nodes.ToArray();
        }
    }

    private ExplanationNodeId RecordDecisionInternal(
        CommandId commandId,
        int stageNumber,
        string decisionType,
        string description,
        IReadOnlyList<ExplanationEdge> edges,
        IReadOnlyList<string>? affectedEntityIds,
        IReadOnlyDictionary<string, string>? context,
        ExplanationRuleRecord? rule)
    {
        var nodeId = ExplanationNodeId.New();
        var normalizedEdges = NormalizeEdges(nodeId, edges, commandId);
        var node = new ExplanationNode
        {
            Id = nodeId,
            NodeType = ExplanationNodeType.StageDecision,
            CommandId = commandId,
            Timestamp = NextTimestamp(),
            StageNumber = stageNumber,
            DecisionType = decisionType,
            Description = description,
            AffectedEntityIds = NormalizeStrings(affectedEntityIds ?? []),
            Edges = normalizedEdges,
            Context = NormalizeContext(context),
            Rule = rule
        };

        AppendNode(node);
        return nodeId;
    }

    private void AppendNode(ExplanationNode node)
    {
        _nodes.Add(node);
        _nodeLookup[node.Id] = node;
        _nodeOrder[node.Id] = _nextSequence++;

        AddToIndex(_commandIndex, node.CommandId, node.Id);

        foreach (var entityId in node.AffectedEntityIds)
        {
            AddToIndex(_entityIndex, entityId, node.Id);
        }

        if (node.StageNumber is int stageNumber)
        {
            AddToIndex(_stageIndex, stageNumber, node.Id);
        }

        if (!string.IsNullOrWhiteSpace(node.Rule?.RuleId))
        {
            AddToIndex(_ruleIndex, node.Rule.RuleId, node.Id);
        }
    }

    private void VisitDecisionTrail(
        ExplanationNodeId nodeId,
        ISet<ExplanationNodeId> visited,
        IList<ExplanationNode> orderedTrail)
    {
        if (!visited.Add(nodeId))
        {
            return;
        }

        var node = GetNode(nodeId);
        var causalTargets = node.Edges
            .Where(edge => CausalEdgeTypes.Contains(edge.EdgeType))
            .OrderBy(edge => GetNodeOrder(edge.TargetNodeId))
            .Select(edge => edge.TargetNodeId)
            .ToArray();

        foreach (var targetNodeId in causalTargets)
        {
            VisitDecisionTrail(targetNodeId, visited, orderedTrail);
        }

        orderedTrail.Add(node);
    }

    private IReadOnlyList<ExplanationEdge> NormalizeEdges(
        ExplanationNodeId sourceNodeId,
        IEnumerable<ExplanationEdge> edges,
        CommandId commandId)
    {
        var normalized = edges
            .Select(edge => new ExplanationEdge
            {
                SourceNodeId = sourceNodeId,
                TargetNodeId = edge.TargetNodeId,
                EdgeType = edge.EdgeType,
                Label = edge.Label
            })
            .OrderBy(edge => edge.EdgeType)
            .ThenBy(edge => GetNodeOrder(edge.TargetNodeId))
            .ThenBy(edge => edge.Label, StringComparer.Ordinal)
            .ToList();

        if (_commandRoots.TryGetValue(commandId, out var rootNodeId) &&
            normalized.All(edge => edge.EdgeType != ExplanationEdgeType.ChildOf || edge.TargetNodeId != rootNodeId))
        {
            normalized.Add(new ExplanationEdge
            {
                SourceNodeId = sourceNodeId,
                TargetNodeId = rootNodeId,
                EdgeType = ExplanationEdgeType.ChildOf,
                Label = "command_root"
            });
        }

        return normalized;
    }

    private IReadOnlyList<ExplanationEdge> BuildCommandRootEdges(ExplanationNodeId rootNodeId, CommandId commandId)
    {
        if (!_commandIndex.TryGetValue(commandId, out var existingNodeIds))
        {
            return [];
        }

        return OrderNodeIds(existingNodeIds)
            .Select(targetNodeId => new ExplanationEdge
            {
                SourceNodeId = rootNodeId,
                TargetNodeId = targetNodeId,
                EdgeType = ExplanationEdgeType.Produced,
                Label = "stage_decision"
            })
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeStrings(IEnumerable<string> values) =>
        values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyDictionary<string, string> NormalizeContext(IReadOnlyDictionary<string, string>? context)
    {
        if (context is null || context.Count == 0)
        {
            return EmptyContext;
        }

        return context
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> BuildCommandContext(IDesignCommand command)
    {
        var metadata = command.Metadata;
        var context = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["command_id"] = metadata.CommandId.ToString(),
            ["command_type"] = command.CommandType,
            ["origin"] = metadata.Origin.ToString(),
            ["intent"] = metadata.IntentDescription,
            ["affected_entity_count"] = metadata.AffectedEntityIds.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (metadata.ParentCommandId is CommandId parentCommandId)
        {
            context["parent_command_id"] = parentCommandId.ToString();
        }

        return context;
    }

    private static IReadOnlyDictionary<string, string> BuildDeltaContext(StateDelta delta)
    {
        var context = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["entity_id"] = delta.EntityId,
            ["entity_type"] = delta.EntityType,
            ["operation"] = delta.Operation.ToString(),
            ["previous_value_count"] = (delta.PreviousValues?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["new_value_count"] = (delta.NewValues?.Count ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        return context;
    }

    private static IReadOnlyDictionary<string, string> BuildUndoRedoContext(
        CommandMetadata commandMetadata,
        IReadOnlyList<StateDelta> deltas,
        string action)
    {
        var context = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["command_id"] = commandMetadata.CommandId.ToString(),
            ["action"] = action,
            ["delta_count"] = deltas.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        return context;
    }

    private static string BuildDeltaDescription(StateDelta delta) =>
        $"{delta.Operation} {delta.EntityType} '{delta.EntityId}'.";

    private DateTimeOffset NextTimestamp() => DateTimeOffset.UnixEpoch.AddTicks(_nextSequence + 1);

    private long GetNodeOrder(ExplanationNodeId nodeId) =>
        _nodeOrder.TryGetValue(nodeId, out var order) ? order : long.MaxValue;

    private ExplanationNode GetNode(ExplanationNodeId nodeId) => _nodeLookup[nodeId];

    private IEnumerable<ExplanationNodeId> OrderNodeIds(IEnumerable<ExplanationNodeId> nodeIds) =>
        nodeIds
            .Distinct()
            .OrderBy(GetNodeOrder)
            .ThenBy(nodeId => nodeId.Value);

    private static void AddToIndex<TKey>(IDictionary<TKey, List<ExplanationNodeId>> index, TKey key, ExplanationNodeId nodeId)
        where TKey : notnull
    {
        if (!index.TryGetValue(key, out var nodeIds))
        {
            nodeIds = [];
            index[key] = nodeIds;
        }

        nodeIds.Add(nodeId);
    }
}
