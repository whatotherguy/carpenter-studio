using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class ExplanationRepositoryTests
{
    [Fact]
    public async Task LoadForEntityAsync_ReturnsNodesAcrossCommandsInCreatedOrder()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var explanationRepository = new ExplanationRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var sharedEntityId = state.WorkingRevision.Cabinets[0].Id.Value.ToString();
        var firstCommandId = CommandId.New();
        var secondCommandId = CommandId.New();
        var firstNode = new ExplanationNodeRecord(
            ExplanationNodeId.New(),
            state.Revision.Id,
            firstCommandId,
            1,
            ExplanationNodeType.CommandRoot,
            "Capture",
            "First explanation",
            [sharedEntityId],
            null,
            null,
            ExplanationNodeStatus.Active,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"));
        var secondNode = new ExplanationNodeRecord(
            ExplanationNodeId.New(),
            state.Revision.Id,
            secondCommandId,
            2,
            ExplanationNodeType.StageDecision,
            "Resolve",
            "Second explanation",
            [sharedEntityId, "other-entity"],
            firstNode.Id,
            "depends_on",
            ExplanationNodeStatus.Active,
            DateTimeOffset.Parse("2026-04-08T17:01:00Z"));

        await explanationRepository.AppendNodeAsync(firstNode);
        await explanationRepository.AppendNodeAsync(secondNode);

        var loaded = await explanationRepository.LoadForEntityAsync(sharedEntityId, state.Revision.Id);

        Assert.Equal(new[] { firstNode.Id, secondNode.Id }, loaded.Select(node => node.Id).ToArray());
        Assert.Equal(
            new[] { firstCommandId.Value, secondCommandId.Value },
            loaded.Select(node => node.CommandId!.Value.Value).ToArray());
    }
}
