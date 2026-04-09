using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class ValidationHistoryReplaceTests
{
    [Fact]
    public async Task SaveIssuesAsync_ReplacesPriorRunIssuesAndIndexes()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var validationRepository = new ValidationHistoryRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var firstIssue = new ValidationIssueRecord(
            new ValidationIssueId("Overlap", ["cab-1"]),
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"),
            ValidationSeverity.Warning,
            "Overlap",
            "Original issue",
            ["cab-1"],
            null);
        var secondIssue = new ValidationIssueRecord(
            new ValidationIssueId("Clearance", ["cab-2"]),
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:01:00Z"),
            ValidationSeverity.Error,
            "Clearance",
            "Replacement issue",
            ["cab-2"],
            """{"action":"move"}""");

        await validationRepository.SaveIssuesAsync(state.Revision.Id, [firstIssue]);
        await validationRepository.SaveIssuesAsync(state.Revision.Id, [secondIssue]);

        var loaded = await validationRepository.LoadAsync(state.Revision.Id);

        Assert.Single(loaded);
        Assert.Equal(secondIssue.Id, loaded[0].Id);
        Assert.Equal(secondIssue.RevisionId, loaded[0].RevisionId);
        Assert.Equal(secondIssue.RunAt, loaded[0].RunAt);
        Assert.Equal(secondIssue.Severity, loaded[0].Severity);
        Assert.Equal(secondIssue.RuleCode, loaded[0].RuleCode);
        Assert.Equal(secondIssue.Message, loaded[0].Message);
        Assert.Equal(secondIssue.AffectedEntityIds, loaded[0].AffectedEntityIds);
        Assert.Equal(secondIssue.SuggestedFixJson, loaded[0].SuggestedFixJson);

        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var issuesCount = connection.CreateCommand();
        issuesCount.CommandText = "SELECT COUNT(*) FROM validation_issues WHERE revision_id = @revisionId;";
        issuesCount.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        Assert.Equal(1, Convert.ToInt32(await issuesCount.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

        using var indexCount = connection.CreateCommand();
        indexCount.CommandText = """
            SELECT COUNT(*)
            FROM validation_entity_index
            WHERE issue_id NOT IN (SELECT id FROM validation_issues WHERE revision_id = @revisionId);
            """;
        indexCount.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        Assert.Equal(0, Convert.ToInt32(await indexCount.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));
    }
}
