# Persistence Layer — Implementation Plan

Source: `cabinet_ai_prompt_pack_v7_plan_build_review.md` (Phase: Persistence Planning)
Context: `code_phase_global_instructions.md`, `persistence_strategy.md`, `domain_model.md`, `orchestrator.md`
Current state: `CabinetDesigner.Persistence` project does not exist. All `ProjectService` and `SnapshotService` methods are `NOT IMPLEMENTED YET` stubs.

---

## 1. IMPLEMENTATION PLAN

### Current State Summary

| Asset | Status |
|---|---|
| `CabinetDesigner.Domain` | Implemented — geometry, identifiers, domain entities |
| `CabinetDesigner.Application` | Implemented — orchestrator, pipeline stages, handlers, event bus |
| `CabinetDesigner.Application.Services.ProjectService` | Stub — all methods `NOT IMPLEMENTED YET` |
| `CabinetDesigner.Application.Services.ISnapshotService` | Interface only — no implementation |
| `CabinetDesigner.Persistence` (project) | Does not exist |
| `CabinetDesigner.Persistence.Tests` (project) | Does not exist |
| Repository interfaces (`IProjectRepository`, etc.) | Do not exist |
| Persistence models (`*Row` types) | Do not exist |
| SQLite schema / migrations | Do not exist |

The `DesignCommandHandler.Execute()` is currently synchronous and calls `_orchestrator.Execute()` which returns `CommandResult`. No persistence occurs on commit.

---

### Implementation Phases

#### Phase 1 — Application-Layer Persistence Contracts

Define all persistence-crossing types and repository interfaces inside the **Application** project. No SQLite code here — pure C# contracts.

**Namespace:** `CabinetDesigner.Application.Persistence`

**Transfer types** (cross Application↔Persistence boundary — not domain entities, not persistence rows):

```
ProjectRecord          — project metadata flat type
RevisionRecord         — revision metadata flat type  
WorkingRevision        — container of all domain entity collections for a revision
CommandJournalEntry    — journal record (carries CommandId, RevisionId, deltas, command JSON)
ApprovedSnapshot       — frozen blob container (holds all serialized blob strings)
SnapshotSummary        — lightweight list record (no blobs)
ExplanationNodeRecord  — flat explanation node
ValidationIssueRecord  — flat validation issue
AutosaveCheckpoint     — checkpoint metadata
```

**Repository interfaces** (async throughout — implementations may use sync SQLite internally):

```
IProjectRepository
IWorkingRevisionRepository
ICommandJournalRepository
ISnapshotRepository
IExplanationRepository
IValidationHistoryRepository
IUnitOfWork               — wraps a SQLite transaction
IDbConnectionFactory      — creates/manages the SQLite connection per open file
ISchemaMigration          — contract for migration classes
```

Full interface signatures defined in `persistence_strategy.md` §4 are authoritative — reproduce verbatim.

---

#### Phase 2 — Persistence Project Scaffold

Create `src/CabinetDesigner.Persistence/CabinetDesigner.Persistence.csproj`:
- Target: `net8.0`
- Package references: `Microsoft.Data.Sqlite` (sync + async), `System.Text.Json` (already in SDK)
- Project references: `CabinetDesigner.Domain`, `CabinetDesigner.Application`
- All internal types marked `internal sealed`
- Add project to `CabinetDesigner.sln`

---

#### Phase 3 — Internal Persistence Models (`*Row` types)

One `internal sealed class *Row` per table. No behavior. No domain types. No public visibility outside the Persistence project.

Tables → Row types:

```
projects                  → ProjectRow
revisions                 → RevisionRow
rooms                     → RoomRow
walls                     → WallRow
runs                      → RunRow
cabinets                  → CabinetRow
parts                     → PartRow
command_journal           → CommandJournalRow
approved_snapshots        → ApprovedSnapshotRow
explanation_nodes         → ExplanationNodeRow
explanation_entity_index  → ExplanationEntityIndexRow
validation_issues         → ValidationIssueRow
validation_entity_index   → ValidationEntityIndexRow
autosave_checkpoints      → AutosaveCheckpointRow
schema_migrations         → SchemaMigrationRow
```

All ID columns: `string` (UUID text).
All `Length`/`Thickness`/`Angle` columns: `string` with unit suffix.
All `DateTimeOffset` columns: `string` (ISO 8601 UTC).
All enum columns: `string` discriminator.
All boolean columns: `int` (1/0).

---

#### Phase 4 — Schema and Migrations

**`ISchemaMigration`** (lives in `CabinetDesigner.Application.Persistence`):
```csharp
int Version { get; }
string Description { get; }
void Apply(IDbConnection connection);   // synchronous — called at startup
```

**`MigrationRunner`** (lives in `CabinetDesigner.Persistence.Migrations`):
1. Ensure `schema_migrations` table exists (CREATE IF NOT EXISTS)
2. Query applied versions
3. Run pending migrations in ascending version order
4. On failure: throw, leave DB at last clean state, do not auto-recover

**`V1_InitialSchema`**:
Implements `ISchemaMigration` with `Version = 1`.
SQL body: exact `CREATE TABLE IF NOT EXISTS` statements from `persistence_strategy.md` §6.
Includes: all tables, all indexes, and the snapshot immutability trigger:

```sql
CREATE TRIGGER IF NOT EXISTS trg_snapshots_no_update
    BEFORE UPDATE ON approved_snapshots
BEGIN
    SELECT RAISE(ABORT, 'approved_snapshots rows are immutable and may not be updated');
END;
```

**WAL mode pragma** is set by `SqliteConnectionFactory` immediately after opening any connection — not inside a migration.

---

#### Phase 5 — Mappers

Location: `CabinetDesigner.Persistence.Mapping`

One mapper class per entity type. No reflection. No auto-mapper. Explicit property assignments only.

```
CabinetMapper          — Cabinet ↔ CabinetRow
WallMapper             — Wall ↔ WallRow
RoomMapper             — Room ↔ RoomRow
RunMapper              — CabinetRun ↔ RunRow
PartMapper             — Part ↔ PartRow
RevisionMapper         — Revision ↔ RevisionRow
CommandJournalMapper   — CommandJournalEntry ↔ CommandJournalRow
ExplanationNodeMapper  — ExplanationNodeRecord ↔ ExplanationNodeRow
ValidationIssueMapper  — ValidationIssueRecord ↔ ValidationIssueRow
SnapshotMapper         — ApprovedSnapshot ↔ ApprovedSnapshotRow
```

**Serialization conventions (mandatory)**:

| Type | Serialized As |
|---|---|
| `Length` | `value.ToInches().ToString("F3") + "in"` — parsed by `Length.Parse()` |
| `Thickness` | same convention as `Length` |
| `Angle` | decimal degrees text: `value.ToDegrees().ToString("F6") + "deg"` |
| `IReadOnlyDictionary<string, OverrideValue>` | JSON via `System.Text.Json`, custom `OverrideValueJsonConverter` with `"type"` discriminator |
| `IReadOnlyList<StateDelta>` | JSON via `System.Text.Json`, `DeltaValueJsonConverter` |
| `IReadOnlyList<string>` (entity ID arrays) | JSON array of strings |
| `DateTimeOffset` | `dto.UtcDateTime.ToString("O")` — round-trip ISO 8601 |
| Enum | `.ToString()` — stored as discriminator string |
| `bool` | `int` 1/0 |

**Critical rule**: `Length` values must never be stored as `REAL`. A mapper that casts `Length` to `double` and inserts it as a real column is a schema violation.

---

#### Phase 6 — Repository Implementations

Location: `CabinetDesigner.Persistence.Repositories`

Each repository receives a `IDbConnectionFactory` in its constructor. All write operations require the caller to have begun a `IUnitOfWork` transaction first — repositories do not manage their own transactions.

**`WorkingRevisionRepository.SaveAsync`** is the heaviest operation. Strategy:
1. Delete all entity rows for the revision that are no longer present (by ID set diff)
2. Upsert all rows that are new or modified (INSERT OR REPLACE)
3. This is called inside the command-commit UoW transaction

For MVP, a full-replace approach is acceptable:
```
DELETE FROM cabinets WHERE revision_id = @revisionId
-- followed by bulk INSERT for all cabinets in the revision
```
This is correct and simple. Optimize to delta-based upsert in a later phase.

**`SnapshotRepository.WriteAsync`** is INSERT-only — there is no `UpdateAsync` method on the interface or any implementation. Attempt to call `WriteAsync` for a revision that already has a snapshot row must throw `InvalidOperationException` before any SQL executes.

**`CommandJournalRepository.AppendAsync`** is append-only — there is no `DeleteAsync` or `UpdateAsync` on the interface.

**Sequence number generation** for `command_journal.sequence_number`: Use `SELECT COALESCE(MAX(sequence_number), 0) + 1 FROM command_journal WHERE revision_id = @revisionId` inside the same transaction. Not a database sequence — SQLite doesn't have them. This is safe because the transaction serializes writes.

---

#### Phase 7 — Unit of Work

**`SqliteUnitOfWork`** wraps a `SqliteConnection` and `SqliteTransaction`.

```csharp
internal sealed class SqliteUnitOfWork : IUnitOfWork
{
    // Stores open connection + active transaction
    // Repositories receive the connection + transaction via ambient context (passed per-call)
    // OR via constructor injection when UoW creates repository instances
}
```

**Decision — ambient vs. explicit transaction passing:**
Repositories must know which transaction to enlist in. Preferred approach: `SqliteUnitOfWork` creates the connection and transaction, then exposes a `Connection` property that repository implementations use directly. The `IUnitOfWork` interface does not expose `Connection` (it's application-layer). The concrete `SqliteUnitOfWork` is passed to repository constructors that accept `IDbConnection` + `IDbTransaction` via a factory or unit-of-work–scoped factory pattern.

Concrete wiring: `SqliteUnitOfWorkScope` creates the UoW + all repositories wired to it, and is disposed on commit or rollback. DI scope is managed by `PersistenceServiceRegistration` using a scoped lifetime.

---

#### Phase 8 — Snapshot Serialization

Location: `CabinetDesigner.Persistence.Snapshots`

```
ISnapshotSerializer     — produces ApprovedSnapshot from resolved domain state
ISnapshotDeserializer   — reads ApprovedSnapshot from stored blob JSON (versioned)
SnapshotBlobReader      — reads schema_version from JSON header, dispatches to deserializer
V1SnapshotSerializer    — writes schema_version = 1 blobs
V1SnapshotDeserializer  — reads schema_version = 1 blobs
```

**Blob format (all blobs)**:
```json
{
  "schema_version": 1,
  "revision_id": "...",
  ...
}
```

`schema_version` must be the first key in every blob object. `SnapshotBlobReader` reads only the first ~32 bytes to extract the version before deserializing the full blob.

**Deserializer registry**: `Dictionary<int, ISnapshotDeserializer>` populated at startup. If `schema_version` is not found, return `SnapshotReadResult.Unreadable(version)` — never throw, never crash.

**Blobs per snapshot** (as per strategy §8.1):
- `design_blob` — full resolved design (rooms, walls, runs, cabinets)
- `parts_blob` — all parts
- `manufacturing_blob` — cut lists, machining plans
- `install_blob` — install plan
- `estimate_blob` — cost estimate
- `validation_blob` — validation issue set at approval
- `explanation_blob` — explanation graph summary

Each blob is independently versioned. `design_blob` at schema version 2 does not force `parts_blob` to version 2.

---

#### Phase 9 — Command Commit Integration

**Integration point**: `DesignCommandHandler.Execute()`

Current flow:
```
handler.Execute(command) → orchestrator.Execute(command) → CommandResult → publish event → return DTO
```

New flow:
```
handler.Execute(command)
  → orchestrator.Execute(command)
  → if result.Success:
      → persistencePort.CommitAsync(command, result)   // ICommandPersistencePort
          → begin UoW
          → save working revision (all entity changes from deltas)
          → append command journal entry
          → append explanation nodes
          → append entity index rows
          → (if validation ran) replace validation issues
          → update autosave checkpoint
          → commit UoW
      → publish DesignChangedEvent
  → return DTO
```

**`ICommandPersistencePort`** (lives in `CabinetDesigner.Application.Persistence`):
```csharp
Task CommitCommandAsync(IDesignCommand command, CommandResult result, CancellationToken ct = default);
```

**Sync/async decision**: `DesignCommandHandler.Execute()` is currently synchronous. Rather than calling `.GetAwaiter().GetResult()`, change `IDesignCommandHandler` to return `Task<CommandResultDto>` and make ViewModels await it. This is the correct approach — synchronous SQLite writes block the UI thread. This is a required API change.

If breaking change is unacceptable in this phase: implement `CommitCommandAsync` and call it with `.GetAwaiter().GetResult()` only, with a `// TODO: make caller async` marker. Document this as technical debt.

**Persistence failure handling**:
- If UoW commit fails: the in-memory domain state is already mutated (orchestrator succeeded). Log the error. Push the command to a failed-commit queue for retry. Do not roll back in-memory state.
- If retry fails: mark session as "unsaved changes pending recovery" and surface to UI.
- Do not silently discard the failure.

---

#### Phase 10 — ProjectService Implementation

Replace `NOT IMPLEMENTED YET` stubs in `ProjectService`:

```
CreateProjectAsync  — generate ProjectId + RevisionId, insert project + revision rows,
                      set WAL pragma, write autosave checkpoint, return ProjectSummaryDto

OpenProjectAsync    — open SQLite connection to .cabinet file,
                      run MigrationRunner (startup migrations),
                      load ProjectRecord + active RevisionRecord,
                      load WorkingRevision (all entity tables for working revision),
                      return ProjectSummaryDto

SaveAsync           — PRAGMA wal_checkpoint(TRUNCATE), update autosave checkpoint is_clean = 1

CloseAsync          — final wal_checkpoint, set is_clean = 1, dispose connection
```

---

#### Phase 11 — SnapshotService Implementation

Replace stub (currently interface-only, `ISnapshotService` in Application):

```
ApproveRevisionAsync  — assert revision is in 'under_review' state,
                        call orchestrator for final canonical resolution,
                        serialize all blobs via ISnapshotSerializer,
                        begin UoW transaction,
                        INSERT INTO approved_snapshots (all blobs),
                        UPDATE revisions SET state = 'approved',
                        commit,
                        return RevisionDto

LoadSnapshotAsync     — read ApprovedSnapshotRow by revision_id,
                        dispatch design_blob to SnapshotBlobReader → V1SnapshotDeserializer,
                        reconstruct domain view from blob,
                        return RevisionDto (read-only view)

GetRevisionHistory    — query revisions table for project_id, return list of RevisionDto
```

---

#### Phase 12 — Service Registration

`PersistenceServiceRegistration.AddPersistence(this IServiceCollection services, string filePath)`:
- Registers `SqliteConnectionFactory` as singleton (holds open connection for the project lifetime)
- Registers all repository implementations as scoped
- Registers `SqliteUnitOfWork` as transient (new instance per command commit)
- Registers `MigrationRunner` as singleton
- Registers versioned snapshot deserializers as singletons

---

## 2. FILES TO CREATE

### In `src/CabinetDesigner.Application/Persistence/`

```
IProjectRepository.cs
IWorkingRevisionRepository.cs
ICommandJournalRepository.cs
ISnapshotRepository.cs
IExplanationRepository.cs
IValidationHistoryRepository.cs
IUnitOfWork.cs
ICommandPersistencePort.cs
IDbConnectionFactory.cs
PersistenceRecords.cs          — ProjectRecord, RevisionRecord, WorkingRevision,
                                  CommandJournalEntry, ApprovedSnapshot, SnapshotSummary,
                                  ExplanationNodeRecord, ValidationIssueRecord, AutosaveCheckpoint
```

### New project `src/CabinetDesigner.Persistence/`

```
CabinetDesigner.Persistence.csproj
SqliteConnectionFactory.cs
PersistenceServiceRegistration.cs

Migrations/
  ISchemaMigration.cs
  MigrationRunner.cs
  V1_InitialSchema.cs

Models/
  ProjectRow.cs
  RevisionRow.cs
  RoomRow.cs
  WallRow.cs
  RunRow.cs
  CabinetRow.cs
  PartRow.cs
  CommandJournalRow.cs
  ApprovedSnapshotRow.cs
  ExplanationNodeRow.cs
  ExplanationEntityIndexRow.cs
  ValidationIssueRow.cs
  ValidationEntityIndexRow.cs
  AutosaveCheckpointRow.cs
  SchemaMigrationRow.cs

Mapping/
  LengthSerializer.cs           — static: Length → string, string → Length
  OverrideValueJsonConverter.cs — System.Text.Json converter for OverrideValue union
  DeltaValueJsonConverter.cs    — System.Text.Json converter for DeltaValue union
  CabinetMapper.cs
  WallMapper.cs
  RoomMapper.cs
  RunMapper.cs
  PartMapper.cs
  RevisionMapper.cs
  CommandJournalMapper.cs
  ExplanationNodeMapper.cs
  ValidationIssueMapper.cs
  SnapshotMapper.cs

Repositories/
  ProjectRepository.cs
  WorkingRevisionRepository.cs
  CommandJournalRepository.cs
  SnapshotRepository.cs
  ExplanationRepository.cs
  ValidationHistoryRepository.cs

Snapshots/
  ISnapshotSerializer.cs
  ISnapshotDeserializer.cs
  SnapshotReadResult.cs
  SnapshotBlobReader.cs
  V1SnapshotSerializer.cs
  V1SnapshotDeserializer.cs

UnitOfWork/
  SqliteUnitOfWork.cs
  CommandPersistenceService.cs   — implements ICommandPersistencePort
```

### New project `tests/CabinetDesigner.Persistence.Tests/`

```
CabinetDesigner.Persistence.Tests.csproj

Fixtures/
  SqliteTestFixture.cs           — creates temp .cabinet file, opens connection, runs migrations

Mapping/
  LengthSerializerTests.cs
  OverrideValueSerializerTests.cs
  DeltaValueSerializerTests.cs
  CabinetMapperRoundTripTests.cs

Repositories/
  ProjectRepositoryTests.cs
  WorkingRevisionRepositoryTests.cs
  CommandJournalRepositoryTests.cs
  SnapshotRepositoryTests.cs
  ExplanationRepositoryTests.cs
  ValidationHistoryRepositoryTests.cs

Integration/
  CommandCommitTransactionTests.cs
  SnapshotImmutabilityTests.cs
  MigrationTests.cs
  CrashRecoveryTests.cs
  AutosaveCheckpointTests.cs
```

---

## 3. FILES TO MODIFY

| File | Change |
|---|---|
| `CabinetDesigner.sln` | Add `CabinetDesigner.Persistence` and `CabinetDesigner.Persistence.Tests` projects |
| `src/CabinetDesigner.Application/CabinetDesigner.Application.csproj` | No package changes needed — contracts only |
| `src/CabinetDesigner.Application/Handlers/DesignCommandHandler.cs` | Inject `ICommandPersistencePort`; call `CommitCommandAsync` after successful orchestrator result |
| `src/CabinetDesigner.Application/Handlers/IDesignCommandHandler.cs` | Change `Execute()` to `Task<CommandResultDto> ExecuteAsync()` — required for async persistence commit |
| `src/CabinetDesigner.Application/Services/ProjectService.cs` | Implement all `NOT IMPLEMENTED YET` stubs using injected repositories |
| `src/CabinetDesigner.Application/ApplicationServiceRegistration.cs` | Register `SnapshotService` implementation; update `ProjectService` binding |
| `tests/CabinetDesigner.Tests/CabinetDesigner.Tests.csproj` | No change (unit tests stay in existing project; integration tests go in new project) |

---

## 4. CODE

Interface contracts are defined in full in `persistence_strategy.md` §4. Reproduce them verbatim when implementing.

**Key signatures to lock in before implementation begins:**

```csharp
// ICommandPersistencePort.cs
namespace CabinetDesigner.Application.Persistence;

public interface ICommandPersistencePort
{
    Task CommitCommandAsync(
        IDesignCommand command,
        CommandResult result,
        RevisionId revisionId,
        CancellationToken ct = default);
}
```

```csharp
// ISchemaMigration.cs (Application layer — testable without Persistence project)
namespace CabinetDesigner.Application.Persistence;

public interface ISchemaMigration
{
    int Version { get; }
    string Description { get; }
    void Apply(IDbConnection connection);
}
```

```csharp
// SnapshotReadResult.cs
namespace CabinetDesigner.Application.Persistence;

public sealed class SnapshotReadResult
{
    public bool IsReadable { get; private init; }
    public int SchemaVersion { get; private init; }
    public ApprovedSnapshot? Snapshot { get; private init; }
    public string? UnreadableReason { get; private init; }

    public static SnapshotReadResult Ok(ApprovedSnapshot snapshot) => ...;
    public static SnapshotReadResult Unreadable(int version, string reason) => ...;
}
```

```csharp
// WorkingRevision.cs (transfer type — not a domain entity)
namespace CabinetDesigner.Application.Persistence;

public sealed class WorkingRevision
{
    public required RevisionId RevisionId { get; init; }
    public required ProjectId ProjectId { get; init; }
    public required IReadOnlyList<Room> Rooms { get; init; }
    public required IReadOnlyList<Wall> Walls { get; init; }
    public required IReadOnlyList<CabinetRun> Runs { get; init; }
    public required IReadOnlyList<Cabinet> Cabinets { get; init; }
    public required IReadOnlyList<Part> Parts { get; init; }
}
```

---

## 5. TESTS

### Unit Tests (in `CabinetDesigner.Persistence.Tests`)

| Test | Assertion |
|---|---|
| `LengthSerializer_RoundTrip_AllUnits` | Parse(Serialize(x)) == x for all supported Length units |
| `LengthSerializer_NeverProducesRealColumn` | Serialized string contains unit suffix — never a bare decimal |
| `OverrideValue_AllCases_RoundTrip` | Every `OverrideValue` discriminated union case serializes and deserializes identically |
| `DeltaValue_AllCases_RoundTrip` | Every `DeltaValue` case survives JSON round-trip |
| `CabinetMapper_DomainToRow_AndBack` | Domain Cabinet → CabinetRow → Domain Cabinet produces value-identical entity |
| `SnapshotDeserializer_V1_ReadsCorrectly` | V1 blob JSON deserializes to correct `ApprovedSnapshot` values |
| `SnapshotDeserializer_UnknownVersion_ReturnsUnreadable` | schema_version = 99 returns `SnapshotReadResult.Unreadable`, never throws |

### Integration Tests (require real SQLite — use `SqliteTestFixture`)

| Test | Assertion |
|---|---|
| `CommandCommit_WritesAllExpectedRows_Atomically` | After `CommitCommandAsync`: journal row exists, entity rows updated, explanation nodes written — all in same transaction |
| `CommandCommit_FailedTransaction_LeavesNoPartialState` | Simulate commit failure mid-write; verify DB state matches pre-command state |
| `CommandJournal_SequenceNumber_MonotonicallyIncreases` | Append 5 commands; sequence_numbers are 1,2,3,4,5 with no gaps |
| `WorkingRevision_FullSaveAndLoad_RoundTrip` | Save revision with 3 rooms, 10 cabinets; reload; verify all entities present with correct field values |
| `ExplanationRepository_QueryByEntityId_ReturnsCorrectNodes` | Write nodes for 3 commands; query by CabinetId; only nodes referencing that cabinet returned |
| `ValidationHistory_ReplaceOnRerun_RemovesPriorIssues` | Write 5 issues; run again with 2 issues; verify only 2 remain |

### Snapshot Immutability Tests

| Test | Assertion |
|---|---|
| `Snapshot_WriteOnce_SecondWriteSameRevision_Throws` | `SnapshotRepository.WriteAsync()` for an already-snapshotted revision throws `InvalidOperationException` before any SQL executes |
| `Snapshot_DatabaseTrigger_BlocksUpdateSQL` | Execute raw `UPDATE approved_snapshots SET ...` on the live DB; assert SQLite raises ABORT |
| `Snapshot_SurvivesWorkingMigration` | Write snapshot at V1; run V2+V3 migrations; read back snapshot; blob content is byte-identical |
| `Snapshot_NoUpdatePathInCodebase` (convention test) | Scan all `.cs` files in `CabinetDesigner.Persistence` namespace for regex `UPDATE\s+approved_snapshots`; assert zero matches |

### Migration Tests

| Test | Assertion |
|---|---|
| `Migration_V1_AppliesCleanly` | Fresh file; run V1 migration; all expected tables and indexes exist |
| `Migration_Idempotent_SecondRun_NoOps` | Run migrations twice; no exception; `schema_migrations` has exactly one row per version |
| `Migration_Failed_DoesNotCorruptFile` | Inject failure in mid-migration; verify prior version state is intact |

---

## 6. RATIONALE

### Why Application-layer persistence contracts, not interfaces in Domain?
Domain must be persistence-ignorant. Repository interfaces in `CabinetDesigner.Application.Persistence` keep domain clean while giving the application layer stable contracts to depend on. Implementations inject via DI.

### Why two-tier storage (working rows + frozen blobs)?
Working-record schema must be free to evolve without touching historical snapshots. A snapshot written at schema version 1 must remain readable after 40 working-schema migrations. The blob approach with `schema_version` header inside each blob achieves this cleanly. Attempting to store snapshots as normalized rows would force all prior snapshots to migrate with every schema change — a maintenance trap.

### Why SQLite trigger for snapshot immutability?
Code-level enforcement (no `UpdateAsync` on the interface) is the primary control. The trigger is a defense-in-depth layer that catches bugs where raw SQL or future developers bypass the repository interface. It is inexpensive and testable.

### Why keep `DesignCommandHandler.Execute()` → `ExecuteAsync()`?
SQLite writes on the UI thread block responsiveness. Making the handler async ensures the commit I/O happens off the UI thread. The current sync design is acceptable temporarily; making it async is the correct final state for a WPF app that needs to remain responsive.

### Why `ISchemaMigration` in Application layer?
Placing it in Application allows the test project to reference migrations without coupling to the Persistence project's SQLite internals. It also keeps migration contracts testable against an `IDbConnection` abstraction rather than a concrete `SqliteConnection`.

### Why full-replace strategy for `WorkingRevisionRepository.SaveAsync`?
For MVP with a single working revision and small-to-medium designs (< 500 entities), the full DELETE + bulk INSERT approach is correct, simple, and fast enough (< 5ms for typical designs). Delta-based upserts add complexity without proportional benefit at this scale. Add delta tracking when benchmarks prove it's needed.

---

## 7. FOLLOW-UP NOTES

### Risks Codex Must Avoid

| Risk | Avoidance Rule |
|---|---|
| `Length` stored as `REAL` | All dimensional mappers must produce `TEXT` with unit suffix. Add a failing test that checks serialized output is not a bare floating-point string. |
| Domain types leaking into `*Row` models | `*Row` files must import only system types and `System.Text.Json`. No `using CabinetDesigner.Domain` in any file under `Persistence.Models`. |
| Snapshot `UPDATE` SQL path | `SnapshotRepository` has no `UpdateAsync` method. `WriteAsync` must assert no existing row before inserting. |
| Mapper using reflection / auto-mapper | All mappers are explicit property assignments. No `AutoMapper`, no `ObjectMapper`, no reflection-based mapping. |
| `Length.Parse()` drift | `LengthSerializer` is the single entry point for Length round-trips. All mappers must route through it — no inline `double.Parse()` or raw `decimal` parsing. |
| Migration auto-rollback | Migrations are forward-only. No rollback methods. On failure: exit and display error. Do not attempt to undo a partial migration. |
| Journal row mutability | `ICommandJournalRepository` has no `DeleteAsync` or `UpdateAsync`. Any PR adding these must be rejected. |
| Undo stack reconstruction on crash | Undo stack is intentionally empty after crash recovery. Do not attempt to replay command journal on startup unless explicitly configured — this is a future feature. |
| Concurrent file access | SQLite busy-timeout is set to 0 on open. Second `OpenProjectAsync` for the same file must fail immediately with a clear user-facing error. Do not silently retry. |
| Schema migration touching snapshot blobs | Inspect every migration before merge. No `UPDATE approved_snapshots` or `ALTER TABLE approved_snapshots` is ever acceptable. |

### Open Decisions to Resolve Before Implementation

1. **Sync vs. async `DesignCommandHandler`**: Recommend making `IDesignCommandHandler.ExecuteAsync()` async in this phase. Confirm with project owner before starting.

2. **`WorkingRevision` load granularity**: Does `LoadAsync` always load all entity tables (rooms, walls, cabinets, parts)? Or is lazy-loading per entity type needed? Recommendation: always load all — single-project, single-revision, small-to-medium datasets.

3. **`IStateManager` integration**: `ResolutionOrchestrator` uses `IStateManager` for in-memory state. `WorkingRevisionRepository.SaveAsync` needs the full state graph. Clarify whether `WorkingRevision` is built from `IStateManager` or from the orchestrator's `CommandResult.Deltas`. Recommendation: build it from `IStateManager.GetCurrentRevision()` — authoritative source of truth.

4. **Part persistence scope for MVP**: The `parts` table is populated by Stage 6 (Part Generation). Confirm whether parts are persisted in the working revision (as generated by the orchestrator) or only in approved snapshots. Recommendation: persist in working revision — enables crash recovery of the full resolved state.

### Suggested Build Order

1. Phase 1 (contracts) → compile-check against existing Application code
2. Phase 2 (project scaffold) + Phase 3 (row models) → no logic yet
3. Phase 4 (migrations + schema) + Phase 5 (mappers) → enables mapping tests
4. Phase 6 (repositories) + Phase 7 (UoW) → enables integration tests
5. Phase 8 (snapshot serialization) → enables snapshot tests
6. Phase 9 (command commit integration) → enables end-to-end commit tests
7. Phase 10 + 11 (ProjectService, SnapshotService) → enables full project lifecycle tests
8. Phase 12 (registration) → enables DI wiring in WPF shell
