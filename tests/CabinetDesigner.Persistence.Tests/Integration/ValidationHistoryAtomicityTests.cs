using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class ValidationHistoryAtomicityTests
{
    [Fact]
    public async Task SaveIssuesAsync_NormalOperation_PersistsIssuesAndIndexesCorrectly()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var validationRepository = new ValidationHistoryRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var issue = new ValidationIssueRecord(
            new ValidationIssueId("Overlap", ["cab-1"]),
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"),
            ValidationSeverity.Warning,
            "Overlap",
            "Cabinet overlap detected",
            ["cab-1"],
            null);

        await validationRepository.SaveIssuesAsync(state.Revision.Id, [issue]);

        var loaded = await validationRepository.LoadAsync(state.Revision.Id);

        Assert.Single(loaded);
        Assert.Equal(issue.Id, loaded[0].Id);
        Assert.Equal(issue.RevisionId, loaded[0].RevisionId);
        Assert.Equal(issue.Severity, loaded[0].Severity);
        Assert.Equal(issue.RuleCode, loaded[0].RuleCode);
        Assert.Equal(issue.Message, loaded[0].Message);
    }

    [Fact]
    public async Task SaveIssuesAsync_MidSaveConstraintViolation_DoesNotLosePriorIssueData()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var validationRepository = new ValidationHistoryRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        // Establish baseline: save one valid issue.
        var existingIssue = new ValidationIssueRecord(
            new ValidationIssueId("Clearance", ["cab-1"]),
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"),
            ValidationSeverity.Error,
            "Clearance",
            "Insufficient clearance",
            ["cab-1"],
            null);
        await validationRepository.SaveIssuesAsync(state.Revision.Id, [existingIssue]);

        // Build a bad batch: two issues with the same ValidationIssueId produce the same primary-key
        // value in validation_issues, so the second INSERT fails with a UNIQUE constraint violation.
        var duplicateId = new ValidationIssueId("Overlap", ["cab-2"]);
        var duplicateIssueA = new ValidationIssueRecord(
            duplicateId,
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:01:00Z"),
            ValidationSeverity.Warning,
            "Overlap",
            "First copy",
            ["cab-2"],
            null);
        var duplicateIssueB = new ValidationIssueRecord(
            duplicateId,
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:01:30Z"),
            ValidationSeverity.Warning,
            "Overlap",
            "Second copy — same id, will violate UNIQUE constraint",
            ["cab-2"],
            null);

        await Assert.ThrowsAsync<SqliteException>(() =>
            validationRepository.SaveIssuesAsync(state.Revision.Id, [duplicateIssueA, duplicateIssueB]));

        // After the failed (rolled-back) save the original issue must still be present.
        var loaded = await validationRepository.LoadAsync(state.Revision.Id);

        Assert.Single(loaded);
        Assert.Equal(existingIssue.Id, loaded[0].Id);
    }

    [Fact]
    public async Task SaveIssuesAsync_MidSaveConstraintViolation_TransactionRollbackLeavesRowCountIntact()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var validationRepository = new ValidationHistoryRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var existingIssue = new ValidationIssueRecord(
            new ValidationIssueId("Clearance", ["cab-1"]),
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:00:00Z"),
            ValidationSeverity.Error,
            "Clearance",
            "Insufficient clearance",
            ["cab-1"],
            null);
        await validationRepository.SaveIssuesAsync(state.Revision.Id, [existingIssue]);

        var (issuesBefore, indexBefore) = await CountValidationRowsAsync(fixture, state.Revision.Id);

        var duplicateId = new ValidationIssueId("Overlap", ["cab-2"]);
        var duplicateIssueA = new ValidationIssueRecord(
            duplicateId,
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:01:00Z"),
            ValidationSeverity.Warning,
            "Overlap",
            "First copy",
            ["cab-2"],
            null);
        var duplicateIssueB = new ValidationIssueRecord(
            duplicateId,
            state.Revision.Id,
            DateTimeOffset.Parse("2026-04-08T17:01:30Z"),
            ValidationSeverity.Warning,
            "Overlap",
            "Second copy — same id, will violate UNIQUE constraint",
            ["cab-2"],
            null);

        await Assert.ThrowsAsync<SqliteException>(() =>
            validationRepository.SaveIssuesAsync(state.Revision.Id, [duplicateIssueA, duplicateIssueB]));

        // Row counts must be identical to before: the rollback restored all deleted rows.
        var (issuesAfter, indexAfter) = await CountValidationRowsAsync(fixture, state.Revision.Id);

        Assert.Equal(issuesBefore, issuesAfter);
        Assert.Equal(indexBefore, indexAfter);
    }

    private static async Task<(int issues, int index)> CountValidationRowsAsync(
        SqliteTestFixture fixture,
        RevisionId revisionId)
    {
        await using var connection = (SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        var id = revisionId.Value.ToString();

        using var issuesCmd = connection.CreateCommand();
        issuesCmd.CommandText = "SELECT COUNT(*) FROM validation_issues WHERE revision_id = @id;";
        issuesCmd.Parameters.AddWithValue("@id", id);
        var issues = Convert.ToInt32(await issuesCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        using var indexCmd = connection.CreateCommand();
        indexCmd.CommandText = """
            SELECT COUNT(*)
            FROM validation_entity_index
            WHERE issue_id IN (SELECT id FROM validation_issues WHERE revision_id = @id);
            """;
        indexCmd.Parameters.AddWithValue("@id", id);
        var index = Convert.ToInt32(await indexCmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);

        return (issues, index);
    }
}
