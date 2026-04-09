using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Application.Pipeline;

public interface IWhyEngine
{
    IReadOnlyList<ExplanationNodeId> RecordCommand(IDesignCommand command, IReadOnlyList<StateDelta> deltas);

    ExplanationNodeId RecordDecision(
        CommandId commandId,
        int stageNumber,
        string decisionType,
        string description,
        IReadOnlyList<string>? affectedEntityIds = null,
        IReadOnlyDictionary<string, string>? context = null,
        ExplanationRuleRecord? rule = null);

    ExplanationNodeId RecordDecisionWithEdges(
        CommandId commandId,
        int stageNumber,
        string decisionType,
        string description,
        IReadOnlyList<ExplanationEdge> edges,
        IReadOnlyList<string>? affectedEntityIds = null,
        IReadOnlyDictionary<string, string>? context = null,
        ExplanationRuleRecord? rule = null);

    IReadOnlyList<ExplanationNodeId> RecordUndo(
        CommandMetadata commandMetadata,
        IReadOnlyList<StateDelta> deltas,
        IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds);

    IReadOnlyList<ExplanationNodeId> RecordRedo(
        CommandMetadata commandMetadata,
        IReadOnlyList<StateDelta> deltas,
        IReadOnlyList<ExplanationNodeId> priorExplanationNodeIds);

    IReadOnlyList<ExplanationNode> GetEntityHistory(string entityId);

    IReadOnlyList<ExplanationNode> GetCommandExplanation(CommandId commandId);

    ExplanationNode? GetCommandRoot(CommandId commandId);

    IReadOnlyList<ExplanationNode> GetStageDecisions(int stageNumber);

    IReadOnlyList<ExplanationNode> GetDecisionsByRule(string ruleId);

    IReadOnlyList<ExplanationNode> GetDecisionTrail(string entityId);

    ExplanationNode? GetPropertyExplanation(string entityId, string propertyName);

    IReadOnlyList<ExplanationNode> GetNodesByStatus(ExplanationNodeStatus status);

    IReadOnlyList<ExplanationNode> GetAllNodes();
}
