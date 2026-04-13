using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Repositories;

public sealed class AutosaveCheckpointRepositoryTests
{
    [Fact]
    public async Task MarkCleanAsync_WithNoExistingCheckpoint_DoesNotThrow()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var projectId = ProjectId.New();

        var repository = new AutosaveCheckpointRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var savedAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");

        // Act & Assert: Should not throw even though no checkpoint exists for this project
        await repository.MarkCleanAsync(projectId, savedAt);

        // Verify: No checkpoint should be created
        var checkpoint = await repository.FindByProjectAsync(projectId);
        Assert.Null(checkpoint);
    }
}
