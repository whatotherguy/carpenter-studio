using CabinetDesigner.Domain.Explanation;
using CabinetDesigner.Domain.Identifiers;
using Xunit;

namespace CabinetDesigner.Tests.Explanation;

public sealed class ExplanationStatusProjectionTests
{
    [Fact]
    public void DefaultStatus_IsActive()
    {
        var projection = new ExplanationStatusProjection();

        Assert.Equal(ExplanationNodeStatus.Active, projection.GetStatus(ExplanationNodeId.New()));
    }

    [Fact]
    public void MarkUndone_SetsStatusToUndone()
    {
        var projection = new ExplanationStatusProjection();
        var nodeId = ExplanationNodeId.New();

        projection.MarkUndone([nodeId]);

        Assert.Equal(ExplanationNodeStatus.Undone, projection.GetStatus(nodeId));
    }

    [Fact]
    public void MarkRedone_OnUndoneNode_SetsStatusToRedone()
    {
        var projection = new ExplanationStatusProjection();
        var nodeId = ExplanationNodeId.New();
        projection.MarkUndone([nodeId]);

        projection.MarkRedone([nodeId]);

        Assert.Equal(ExplanationNodeStatus.Redone, projection.GetStatus(nodeId));
    }

    [Fact]
    public void MarkRedone_OnActiveNode_NoChange()
    {
        var projection = new ExplanationStatusProjection();
        var nodeId = ExplanationNodeId.New();

        projection.MarkRedone([nodeId]);

        Assert.Equal(ExplanationNodeStatus.Active, projection.GetStatus(nodeId));
    }
}
