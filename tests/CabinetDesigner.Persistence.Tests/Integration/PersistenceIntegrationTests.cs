using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using CabinetDesigner.Persistence.UnitOfWork;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class PersistenceIntegrationTests
{
    [Fact]
    public async Task WorkingRevision_SaveAndLoad_RoundTripsDeterministically()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var workingRevisionRepository = new WorkingRevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var checkpointRepository = new AutosaveCheckpointRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);
        await checkpointRepository.SaveAsync(state.Checkpoint);
        await workingRevisionRepository.SaveAsync(state.WorkingRevision);

        var loaded = await workingRevisionRepository.LoadAsync(state.Project.Id);

        Assert.NotNull(loaded);
        Assert.Equal(state.WorkingRevision.Revision.Id, loaded!.Revision.Id);
        Assert.Single(loaded.Rooms);
        Assert.Single(loaded.Walls);
        Assert.Single(loaded.Runs);
        Assert.Single(loaded.Cabinets);
        Assert.Single(loaded.Parts);
        Assert.Equal(state.WorkingRevision.Cabinets[0].Category, loaded.Cabinets[0].Category);
        Assert.Equal(state.WorkingRevision.Cabinets[0].Construction, loaded.Cabinets[0].Construction);
        Assert.Equal(state.WorkingRevision.Parts[0].GrainDirection, loaded.Parts[0].GrainDirection);
        Assert.Equal(state.WorkingRevision.Cabinets[0].Overrides, loaded.Cabinets[0].Overrides);
    }

    [Fact]
    public async Task SnapshotRepository_WriteOnce_AndTriggerBlocksUpdates()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var snapshotRepository = new SnapshotRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision with { State = ApprovalState.Approved, ApprovedAt = DateTimeOffset.UtcNow, ApprovedBy = "tester" });

        var snapshot = new ApprovedSnapshot(state.Revision.Id, state.Project.Id, 1, DateTimeOffset.UtcNow, "tester", "Rev 1", """{"schema_version":1}""", """{"schema_version":1}""", """{"schema_version":1}""", """{"schema_version":1}""", """{"schema_version":1}""", """{"schema_version":1}""", """{"schema_version":1}""");
        await snapshotRepository.WriteAsync(snapshot);
        await Assert.ThrowsAsync<InvalidOperationException>(() => snapshotRepository.WriteAsync(snapshot));

        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var update = connection.CreateCommand();
        update.CommandText = "UPDATE approved_snapshots SET approved_by = 'mutated' WHERE revision_id = @revisionId;";
        update.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        var exception = await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => update.ExecuteNonQueryAsync());
        Assert.Contains("immutable", exception.Message, StringComparison.OrdinalIgnoreCase);

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM approved_snapshots WHERE revision_id = @revisionId;";
        delete.Parameters.AddWithValue("@revisionId", state.Revision.Id.Value.ToString());
        var deleteException = await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => delete.ExecuteNonQueryAsync());
        Assert.Contains("immutable", deleteException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransactionFailure_RollsBack_PartialWrites()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();
        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        await using var transaction = (Microsoft.Data.Sqlite.SqliteTransaction)await connection.BeginTransactionAsync();
        try
        {
            using var insertProject = connection.CreateCommand();
            insertProject.Transaction = transaction;
            insertProject.CommandText = """
                INSERT INTO projects(id, name, description, created_at, updated_at, current_state)
                VALUES(@id, @name, @description, @createdAt, @updatedAt, @currentState);
                """;
            insertProject.Parameters.AddWithValue("@id", state.Project.Id.Value.ToString());
            insertProject.Parameters.AddWithValue("@name", state.Project.Name);
            insertProject.Parameters.AddWithValue("@description", (object?)state.Project.Description ?? DBNull.Value);
            insertProject.Parameters.AddWithValue("@createdAt", state.Project.CreatedAt.ToString("O"));
            insertProject.Parameters.AddWithValue("@updatedAt", state.Project.UpdatedAt.ToString("O"));
            insertProject.Parameters.AddWithValue("@currentState", state.Project.CurrentState.ToString());
            await insertProject.ExecuteNonQueryAsync();

            throw new InvalidOperationException("boom");
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync();
        }

        await using var verification = (Microsoft.Data.Sqlite.SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var countCommand = verification.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM projects WHERE id = @id;";
        countCommand.Parameters.AddWithValue("@id", state.Project.Id.Value.ToString());
        var count = Convert.ToInt32(await countCommand.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CommandJournal_AppendsMonotonicSequenceNumbers()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var journalRepository = new CommandJournalRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var first = new CommandJournalEntry(CommandId.New(), state.Revision.Id, 0, "first", CommandOrigin.User, "First", [], null, DateTimeOffset.UtcNow, "{}", [], true);
        var second = new CommandJournalEntry(CommandId.New(), state.Revision.Id, 0, "second", CommandOrigin.User, "Second", [], null, DateTimeOffset.UtcNow, "{}", [], true);
        await journalRepository.AppendAsync(first);
        await journalRepository.AppendAsync(second);

        var loaded = await journalRepository.LoadForRevisionAsync(state.Revision.Id);
        Assert.Equal(new[] { 1, 2 }, loaded.Select(entry => entry.SequenceNumber).ToArray());
    }

    [Fact]
    public async Task ForeignKeys_AreEnforced()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await fixture.ConnectionFactory.OpenConnectionAsync();
        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO revisions(id, project_id, revision_number, state, created_at, approved_at, approved_by, label, approval_notes)
            VALUES(@id, @projectId, 1, 'Draft', @createdAt, NULL, NULL, 'Rev 1', NULL);
            """;
        insert.Parameters.AddWithValue("@id", RevisionId.New().Value.ToString());
        insert.Parameters.AddWithValue("@projectId", ProjectId.New().Value.ToString());
        insert.Parameters.AddWithValue("@createdAt", DateTimeOffset.UtcNow.ToString("O"));

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() => insert.ExecuteNonQueryAsync());
    }

    [Fact]
    public void PersistenceCode_HasNoApprovedSnapshotUpdatePath()
    {
        var root = FindPersistenceProjectRoot();
        var files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
        var matches = files
            .SelectMany(file => File.ReadAllLines(file).Select((line, index) => (file, line, index)))
            .Where(item => item.line.Contains("UPDATE approved_snapshots", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Empty(matches);
    }

    private static string FindPersistenceProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "CabinetDesigner.Persistence");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/CabinetDesigner.Persistence from the test host base directory.");
    }

    private sealed class RecordingWorkingRevisionSource((ProjectRecord Project, RevisionRecord Revision, WorkingRevision WorkingRevision, AutosaveCheckpoint Checkpoint) state) : IWorkingRevisionSource
    {
        public PersistedProjectState CaptureCurrentState(PartGenerationResult? partResult = null) =>
            new(state.Project, state.Revision, state.WorkingRevision, state.Checkpoint);
    }

}
