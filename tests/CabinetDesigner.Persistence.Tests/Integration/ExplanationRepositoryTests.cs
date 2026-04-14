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

    [Fact]
    public async Task LoadForEntityAsync_ReturnsDeterministicOrderWhenMultipleNodesShareSameTimestamp()
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
        var commandId = CommandId.New();
        var sameTimestamp = DateTimeOffset.Parse("2026-04-08T17:00:00Z");

        // Use fixed IDs that sort lexicographically as first < second < third
        var firstId = new ExplanationNodeId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondId = new ExplanationNodeId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var thirdId = new ExplanationNodeId(Guid.Parse("00000000-0000-0000-0000-000000000003"));

        // Create three nodes with the same timestamp
        var firstNode = new ExplanationNodeRecord(
            firstId,
            state.Revision.Id,
            commandId,
            1,
            ExplanationNodeType.CommandRoot,
            "Capture",
            "First explanation",
            [sharedEntityId],
            null,
            null,
            ExplanationNodeStatus.Active,
            sameTimestamp);
        var secondNode = new ExplanationNodeRecord(
            secondId,
            state.Revision.Id,
            commandId,
            2,
            ExplanationNodeType.StageDecision,
            "Resolve",
            "Second explanation",
            [sharedEntityId],
            firstNode.Id,
            "depends_on",
            ExplanationNodeStatus.Active,
            sameTimestamp);
        var thirdNode = new ExplanationNodeRecord(
            thirdId,
            state.Revision.Id,
            commandId,
            3,
            ExplanationNodeType.StageDecision,
            "Resolve",
            "Third explanation",
            [sharedEntityId],
            secondNode.Id,
            "depends_on",
            ExplanationNodeStatus.Active,
            sameTimestamp);

        // Insert in reverse order to ensure sorting by ID works correctly
        await explanationRepository.AppendNodeAsync(thirdNode);
        await explanationRepository.AppendNodeAsync(secondNode);
        await explanationRepository.AppendNodeAsync(firstNode);

        var loaded = await explanationRepository.LoadForEntityAsync(sharedEntityId, state.Revision.Id);

        // Should be sorted by ID (used as tiebreaker when timestamps are equal)
        Assert.Equal(3, loaded.Count);
        Assert.Equal(firstNode.Id, loaded[0].Id);
        Assert.Equal(secondNode.Id, loaded[1].Id);
        Assert.Equal(thirdNode.Id, loaded[2].Id);
    }

    [Fact]
    public async Task LoadForCommandAsync_ReturnsDeterministicOrderWhenMultipleNodesShareSameTimestamp()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var explanationRepository = new ExplanationRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var commandId = CommandId.New();
        var sameTimestamp = DateTimeOffset.Parse("2026-04-08T17:00:00Z");

        // Use fixed IDs that sort lexicographically as first < second < third
        var firstId = new ExplanationNodeId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var secondId = new ExplanationNodeId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var thirdId = new ExplanationNodeId(Guid.Parse("00000000-0000-0000-0000-000000000003"));

        // Create three nodes with the same timestamp for the same command
        var firstNode = new ExplanationNodeRecord(
            firstId,
            state.Revision.Id,
            commandId,
            1,
            ExplanationNodeType.CommandRoot,
            "Capture",
            "First explanation",
            ["entity1"],
            null,
            null,
            ExplanationNodeStatus.Active,
            sameTimestamp);
        var secondNode = new ExplanationNodeRecord(
            secondId,
            state.Revision.Id,
            commandId,
            2,
            ExplanationNodeType.StageDecision,
            "Resolve",
            "Second explanation",
            ["entity2"],
            firstNode.Id,
            "depends_on",
            ExplanationNodeStatus.Active,
            sameTimestamp);
        var thirdNode = new ExplanationNodeRecord(
            thirdId,
            state.Revision.Id,
            commandId,
            3,
            ExplanationNodeType.StageDecision,
            "Resolve",
            "Third explanation",
            ["entity3"],
            secondNode.Id,
            "depends_on",
            ExplanationNodeStatus.Active,
            sameTimestamp);

        // Insert in reverse order to ensure sorting by ID works correctly
        await explanationRepository.AppendNodeAsync(thirdNode);
        await explanationRepository.AppendNodeAsync(secondNode);
        await explanationRepository.AppendNodeAsync(firstNode);

        var loaded = await explanationRepository.LoadForCommandAsync(commandId);

        // Should be sorted by ID (used as tiebreaker when timestamps are equal)
        Assert.Equal(3, loaded.Count);
        Assert.Equal(firstNode.Id, loaded[0].Id);
        Assert.Equal(secondNode.Id, loaded[1].Id);
        Assert.Equal(thirdNode.Id, loaded[2].Id);
    }
}
