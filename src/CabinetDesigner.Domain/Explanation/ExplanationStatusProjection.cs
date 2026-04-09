using System.Collections.Generic;
using CabinetDesigner.Domain.Identifiers;

namespace CabinetDesigner.Domain.Explanation;

public sealed class ExplanationStatusProjection
{
    private readonly Dictionary<ExplanationNodeId, ExplanationNodeStatus> _statuses = [];

    public ExplanationNodeStatus GetStatus(ExplanationNodeId nodeId) =>
        _statuses.GetValueOrDefault(nodeId, ExplanationNodeStatus.Active);

    public void MarkUndone(IEnumerable<ExplanationNodeId> nodeIds)
    {
        foreach (var nodeId in nodeIds)
        {
            _statuses[nodeId] = ExplanationNodeStatus.Undone;
        }
    }

    public void MarkRedone(IEnumerable<ExplanationNodeId> nodeIds)
    {
        foreach (var nodeId in nodeIds)
        {
            if (_statuses.GetValueOrDefault(nodeId) == ExplanationNodeStatus.Undone)
            {
                _statuses[nodeId] = ExplanationNodeStatus.Redone;
            }
        }
    }
}
