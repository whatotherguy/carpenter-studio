using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Tests.Fixtures;
using CabinetDesigner.Persistence.UnitOfWork;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

/// <summary>
/// Regression tests for CommandJournalRepository error paths, missing-data
/// read scenarios, per-revision sequence isolation, and nullable-field
/// round-trips.
/// </summary>
public sealed class CommandJournalRepositoryTests
{
    // -------------------------------------------------------------------------
    // Missing-data / null-result read scenarios
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: LoadForRevisionAsync throwing or returning null instead
    /// of an empty list when no journal entries exist for the given revision.
    /// Callers depend on an always-non-null return value for replay.
    /// </summary>
    [Fact]
    public async Task LoadForRevisionAsync_UnknownRevisionId_ReturnsEmptyList()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var accessor = new SqliteSessionAccessor();
        var repo = new CommandJournalRepository(fixture.ConnectionFactory, accessor);

        var result = await repo.LoadForRevisionAsync(RevisionId.New());

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // -------------------------------------------------------------------------
    // Foreign-key enforcement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: Foreign-key constraints being disabled (PRAGMA
    /// foreign_keys=OFF) or removed from the schema, which would allow journal
    /// entries to reference non-existent revisions and silently corrupt the
    /// audit trail.
    /// </summary>
    [Fact]
    public async Task AppendAsync_MissingRevision_ThrowsForeignKeyViolation()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var accessor = new SqliteSessionAccessor();
        var repo = new CommandJournalRepository(fixture.ConnectionFactory, accessor);

        var orphanEntry = BuildEntry(RevisionId.New());

        await Assert.ThrowsAsync<SqliteException>(() => repo.AppendAsync(orphanEntry));
    }

    // -------------------------------------------------------------------------
    // Sequence-number isolation per revision
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: The sequence-number subquery losing its
    /// <c>WHERE revision_id = @revisionId</c> predicate, which would cause a
    /// single global counter shared across all revisions and produce
    /// non-consecutive (or duplicate) sequence numbers within each revision.
    /// </summary>
    [Fact]
    public async Task AppendAsync_SequenceNumbers_AreIsolatedPerRevision()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);

        // Create two independent projects/revisions.
        var projectIdA = ProjectId.New();
        var revisionIdA = RevisionId.New();
        var projectIdB = ProjectId.New();
        var revisionIdB = RevisionId.New();
        var createdAt = DateTimeOffset.Parse("2026-04-08T17:00:00Z");

        await projectRepository.SaveAsync(new ProjectRecord(projectIdA, "Project A", null, createdAt, createdAt, ApprovalState.Draft));
        await projectRepository.SaveAsync(new ProjectRecord(projectIdB, "Project B", null, createdAt, createdAt, ApprovalState.Draft));
        await revisionRepository.SaveAsync(new RevisionRecord(revisionIdA, projectIdA, 1, ApprovalState.Draft, createdAt, null, null, "Rev A1"));
        await revisionRepository.SaveAsync(new RevisionRecord(revisionIdB, projectIdB, 1, ApprovalState.Draft, createdAt, null, null, "Rev B1"));

        var accessorA = new SqliteSessionAccessor();
        var repoA = new CommandJournalRepository(fixture.ConnectionFactory, accessorA);
        var accessorB = new SqliteSessionAccessor();
        var repoB = new CommandJournalRepository(fixture.ConnectionFactory, accessorB);

        // Interleave appends to revision A and B.
        await repoA.AppendAsync(BuildEntry(revisionIdA));
        await repoB.AppendAsync(BuildEntry(revisionIdB));
        await repoA.AppendAsync(BuildEntry(revisionIdA));
        await repoB.AppendAsync(BuildEntry(revisionIdB));
        await repoB.AppendAsync(BuildEntry(revisionIdB));

        var verifyAccessor = new SqliteSessionAccessor();
        var verifyRepo = new CommandJournalRepository(fixture.ConnectionFactory, verifyAccessor);

        var entriesA = await verifyRepo.LoadForRevisionAsync(revisionIdA);
        var entriesB = await verifyRepo.LoadForRevisionAsync(revisionIdB);

        // Revision A: two entries, sequences 1 and 2.
        Assert.Equal(2, entriesA.Count);
        Assert.Equal(new[] { 1, 2 }, entriesA.Select(e => e.SequenceNumber).OrderBy(n => n).ToArray());

        // Revision B: three entries, sequences 1, 2 and 3 — independent of A.
        Assert.Equal(3, entriesB.Count);
        Assert.Equal(new[] { 1, 2, 3 }, entriesB.Select(e => e.SequenceNumber).OrderBy(n => n).ToArray());
    }

    // -------------------------------------------------------------------------
    // Nullable-field round-trips
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: A null <see cref="CommandId"/> for
    /// <see cref="CommandJournalEntry.ParentCommandId"/> not being mapped to
    /// <c>DBNull.Value</c> on write (causing a constraint failure) or not
    /// being read back as <c>null</c> (causing a parse error on a stored empty
    /// string).
    /// </summary>
    [Fact]
    public async Task AppendAsync_NullParentCommandId_PersistsAndRoundTrips()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        var accessor = new SqliteSessionAccessor();
        var repo = new CommandJournalRepository(fixture.ConnectionFactory, accessor);

        // Build an entry that has no parent command (top-level user action).
        var entry = new CommandJournalEntry(
            CommandId.New(),
            state.Revision.Id,
            0,
            "layout.add_cabinet",
            CommandOrigin.User,
            "Add cabinet",
            [CabinetId.New().Value.ToString()],
            ParentCommandId: null,
            DateTimeOffset.UtcNow,
            "{}",
            [],
            true);

        await repo.AppendAsync(entry);

        var verifyAccessor = new SqliteSessionAccessor();
        var verifyRepo = new CommandJournalRepository(fixture.ConnectionFactory, verifyAccessor);
        var loaded = await verifyRepo.LoadForRevisionAsync(state.Revision.Id);

        Assert.Single(loaded);
        Assert.Null(loaded[0].ParentCommandId);
        Assert.Equal(entry.Id, loaded[0].Id);
    }

    // -------------------------------------------------------------------------
    // Concurrent appends within the same revision
    // -------------------------------------------------------------------------

    /// <summary>
    /// Guards against: A race condition where two concurrent appends to the
    /// same revision both read the same MAX(sequence_number) and produce
    /// duplicate sequence numbers.  SQLite's single-writer model combined with
    /// the atomic scalar-subquery INSERT prevents this; this test makes the
    /// guarantee explicit and will catch any future change that breaks it (e.g.
    /// splitting the SELECT and INSERT into two statements).
    /// </summary>
    [Fact]
    public async Task AppendAsync_ConcurrentCallsSameRevision_ProducesNoDuplicateSequenceNumbers()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();
        var state = TestData.CreatePersistedState();

        var projectRepository = new ProjectRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        var revisionRepository = new RevisionRepository(fixture.ConnectionFactory, fixture.SessionAccessor);
        await projectRepository.SaveAsync(state.Project);
        await revisionRepository.SaveAsync(state.Revision);

        const int concurrency = 12;
        var tasks = Enumerable.Range(0, concurrency)
            .Select(_ =>
            {
                var accessor = new SqliteSessionAccessor();
                var repo = new CommandJournalRepository(fixture.ConnectionFactory, accessor);
                return repo.AppendAsync(BuildEntry(state.Revision.Id));
            })
            .ToArray();

        await Task.WhenAll(tasks);

        var verifyAccessor = new SqliteSessionAccessor();
        var verifyRepo = new CommandJournalRepository(fixture.ConnectionFactory, verifyAccessor);
        var entries = await verifyRepo.LoadForRevisionAsync(state.Revision.Id);

        var seqNumbers = entries.Select(e => e.SequenceNumber).OrderBy(n => n).ToList();

        Assert.Equal(concurrency, seqNumbers.Count);
        Assert.Equal(seqNumbers.Distinct().Count(), seqNumbers.Count);
        for (var i = 0; i < concurrency; i++)
        {
            Assert.Equal(i + 1, seqNumbers[i]);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static CommandJournalEntry BuildEntry(RevisionId revisionId) => new(
        CommandId.New(),
        revisionId,
        0,
        "test.command",
        CommandOrigin.User,
        "Test intent",
        [],
        null,
        DateTimeOffset.UtcNow,
        "{}",
        [],
        true);
}
