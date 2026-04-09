using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Xunit;

namespace CabinetDesigner.Persistence.Tests;

public sealed class JournalReplayTests
{
    [Fact]
    public async Task LoadForRevisionAsync_ReplaysDeterministicallyAcrossRepeatedReads()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var state = TestData.CreatePersistedState();
        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var journalRepository = new CommandJournalRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        await journalRepository.AppendAsync(CreateEntry(state.Revision.Id, DateTimeOffset.Parse("2026-04-08T17:01:00Z"), "layout.add_cabinet", "cabinet-1"));
        await journalRepository.AppendAsync(CreateEntry(state.Revision.Id, DateTimeOffset.Parse("2026-04-08T17:02:00Z"), "layout.move_cabinet", "cabinet-1"));

        var firstReplay = await journalRepository.LoadForRevisionAsync(state.Revision.Id);
        var secondReplay = await journalRepository.LoadForRevisionAsync(state.Revision.Id);

        Assert.Equal(ProjectReplay(firstReplay), ProjectReplay(secondReplay));
    }

    private static CommandJournalEntry CreateEntry(RevisionId revisionId, DateTimeOffset timestamp, string commandType, string entityId) =>
        new(
            CommandId.New(),
            revisionId,
            0,
            commandType,
            CommandOrigin.User,
            commandType,
            [entityId],
            null,
            timestamp,
            "{}",
            [new StateDelta(entityId, "Cabinet", DeltaOperation.Modified, null, new Dictionary<string, DeltaValue> { ["Timestamp"] = new DeltaValue.OfString(timestamp.ToString("O")) })],
            true);

    private static IReadOnlyList<string> ProjectReplay(IReadOnlyList<CommandJournalEntry> entries) =>
        entries
            .SelectMany(entry => entry.Deltas.Select(delta => $"{entry.SequenceNumber}:{entry.CommandType}:{delta.EntityId}:{delta.Operation}"))
            .ToArray();
}
