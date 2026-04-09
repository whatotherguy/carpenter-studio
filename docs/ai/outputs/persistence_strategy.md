# P9 — Persistence Strategy

Source: `cabinet_ai_prompt_pack_v4_1_full.md` (Phase 9)
Context: `architecture_summary.md`, `domain_model.md`, `commands.md`, `orchestrator.md`, `validation_engine.md`, `why_engine.md`

---

## 1. Goals

- Provide durable, deterministic storage for working design state, command history, and immutable approved snapshots
- Separate persistence models completely from domain entities — no ORM annotations or storage concerns in the domain
- Ensure approved snapshots survive schema evolution without corruption or forced migration
- Define clean repository contracts the application layer can depend on without knowing SQLite internals
- Support autosave and crash recovery so no committed work is ever silently lost
- Store explanation graph and validation history where needed for audit and UI queries
- Keep transaction boundaries tight: one command commit = one atomic write

---

## 2. Persistence Boundaries

### 2.1 What Persistence Owns

| Concern | Owned By |
|---|---|
| Project metadata and state | Persistence |
| Working revision (mutable design state) | Persistence |
| Command journal (ordered, append-only) | Persistence |
| Undo/redo stack entries (current session) | In-memory (not persisted) |
| Immutable approved snapshots | Persistence (frozen blobs) |
| Explanation graph nodes (session + retained) | Persistence (working) |
| Validation issue history | Persistence (working, per revision) |
| Autosave checkpoints | Persistence |
| Schema migration version | Persistence |

### 2.2 What Persistence Does NOT Own

| Concern | Owned By |
|---|---|
| Domain entity behavior | Domain layer |
| Resolution pipeline logic | Orchestrator |
| In-memory undo stack | Application (in-memory only) |
| Rendering state | Editor/Rendering layer |
| Session-local editor state (selection, zoom, pan) | Editor (not persisted across sessions) |
| Drag preview / lightweight layout graph | Editor (transient, never persisted) |

### 2.3 Session vs Persisted State

| State | Persisted | Notes |
|---|---|---|
| Working revision (design entities) | Yes — continuously autosaved | Mutable until approved |
| Command journal (this session) | Yes — appended per commit | Append-only |
| Undo/redo stack entries | No | Rebuilt from journal on crash recovery if needed |
| Editor selection | No | Ephemeral per session |
| Zoom / pan / viewport | No | Ephemeral per session |
| Explanation graph (per revision) | Yes — written per commit | Queryable across sessions |
| Validation issues (per revision) | Yes — replaced per run | Per-run working record |
| Approved snapshot blobs | Yes — immutable forever | Written once, never mutated |

---

## 3. Storage Model

### 3.1 One File Per Project

Each project is a single `.cabinet` file, which is a standard SQLite database with a custom extension. This provides:

- Single-file portability (share via email, USB, network share)
- Transactional safety from SQLite's WAL mode
- Simple backup: copy the file

### 3.2 Two Storage Tiers

**Tier 1 — Working Records:** Mutable rows representing the live design state. Updated on every command commit. Subject to schema migration.

**Tier 2 — Frozen Snapshot Blobs:** Immutable JSON blobs written at approval time. These are never parsed or re-interpreted by the working schema. They carry their own schema version header and are read only by the snapshot reader, not by the working-record layer.

This separation means working schema can evolve freely without touching approved snapshots.

---

## 4. Repository Contracts

All repositories are interfaces in `CabinetDesigner.Application`. Implementations live in `CabinetDesigner.Persistence`.

Domain entities cross the boundary via explicit mapping — repositories accept and return domain types. Persistence models are internal to `CabinetDesigner.Persistence` and must not appear in application or domain namespaces.

```csharp
namespace CabinetDesigner.Application.Persistence;

// ── Project ──────────────────────────────────────────────────────────

public interface IProjectRepository
{
    Task<ProjectRecord?> FindAsync(ProjectId id, CancellationToken ct = default);
    Task SaveAsync(ProjectRecord project, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectRecord>> ListRecentAsync(int limit, CancellationToken ct = default);
}

// ── Working Revision ─────────────────────────────────────────────────

public interface IWorkingRevisionRepository
{
    /// <summary>Load the current working revision for the project.</summary>
    Task<WorkingRevision?> LoadAsync(ProjectId projectId, CancellationToken ct = default);

    /// <summary>
    /// Persist the working revision as part of a command commit.
    /// Always called inside a transaction started by IUnitOfWork.
    /// </summary>
    Task SaveAsync(WorkingRevision revision, CancellationToken ct = default);
}

// ── Command Journal ──────────────────────────────────────────────────

public interface ICommandJournalRepository
{
    /// <summary>Append a committed command entry. Append-only — never update or delete.</summary>
    Task AppendAsync(CommandJournalEntry entry, CancellationToken ct = default);

    /// <summary>Load ordered journal entries for a revision (for audit/replay).</summary>
    Task<IReadOnlyList<CommandJournalEntry>> LoadForRevisionAsync(
        RevisionId revisionId, CancellationToken ct = default);
}

// ── Approved Snapshots ───────────────────────────────────────────────

public interface ISnapshotRepository
{
    /// <summary>Write a frozen snapshot. Called once on approval — never updated.</summary>
    Task WriteAsync(ApprovedSnapshot snapshot, CancellationToken ct = default);

    /// <summary>Read a frozen snapshot by revision ID.</summary>
    Task<ApprovedSnapshot?> ReadAsync(RevisionId revisionId, CancellationToken ct = default);

    /// <summary>List summary headers for all approved revisions in a project.</summary>
    Task<IReadOnlyList<SnapshotSummary>> ListAsync(ProjectId projectId, CancellationToken ct = default);
}

// ── Explanation Graph ─────────────────────────────────────────────────

public interface IExplanationRepository
{
    Task AppendNodeAsync(ExplanationNodeRecord node, CancellationToken ct = default);
    Task<IReadOnlyList<ExplanationNodeRecord>> LoadForEntityAsync(
        string entityId, RevisionId revisionId, CancellationToken ct = default);
    Task<IReadOnlyList<ExplanationNodeRecord>> LoadForCommandAsync(
        CommandId commandId, CancellationToken ct = default);
}

// ── Validation History ────────────────────────────────────────────────

public interface IValidationHistoryRepository
{
    /// <summary>Replace validation issues for a revision (re-run replaces prior set).</summary>
    Task SaveIssuesAsync(RevisionId revisionId,
        IReadOnlyList<ValidationIssueRecord> issues, CancellationToken ct = default);

    Task<IReadOnlyList<ValidationIssueRecord>> LoadAsync(
        RevisionId revisionId, CancellationToken ct = default);
}

// ── Unit of Work ─────────────────────────────────────────────────────

/// <summary>
/// Wraps a SQLite transaction. A command commit begins, writes to all
/// affected repositories, then commits. On exception: rollback.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
```

---

## 5. Persistence Models

Persistence models are internal C# types in `CabinetDesigner.Persistence.Models`. They are flat, SQLite-shaped, and must not appear outside the Persistence project.

Domain-to-persistence mapping is handled by explicit mappers in `CabinetDesigner.Persistence.Mapping`. The domain never sees these types.

### 5.1 Conventions

- All IDs stored as `TEXT` (UUID string) — SQLite has no native UUID type
- All `Length` values stored as `TEXT` with unit suffix (e.g., `"36.000in"`) — never raw `REAL` to avoid float drift
- All `DateTimeOffset` values stored as ISO 8601 `TEXT` in UTC
- All enums stored as `TEXT` (discriminator string) — never integer ordinal (fragile to reordering)
- All JSON blobs stored as `TEXT` with a `schema_version` prefix field inside the JSON
- `created_at` and `updated_at` on every mutable working-record table
- Snapshot tables use `TEXT` blob columns (`snapshot_json`) — schema version embedded in the blob

---

## 6. SQLite Schema Outline

```sql
-- ═══════════════════════════════════════════════
--  SCHEMA VERSIONING
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS schema_migrations (
    version         INTEGER PRIMARY KEY,
    applied_at      TEXT    NOT NULL,   -- ISO 8601 UTC
    description     TEXT    NOT NULL
);

-- ═══════════════════════════════════════════════
--  PROJECT
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS projects (
    id              TEXT    PRIMARY KEY,  -- ProjectId GUID
    name            TEXT    NOT NULL,
    description     TEXT,
    created_at      TEXT    NOT NULL,
    updated_at      TEXT    NOT NULL,
    current_state   TEXT    NOT NULL      -- 'draft' | 'under_review' | 'approved' | 'locked_for_manufacture' | ...
);

-- ═══════════════════════════════════════════════
--  REVISIONS
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS revisions (
    id              TEXT    PRIMARY KEY,  -- RevisionId GUID
    project_id      TEXT    NOT NULL REFERENCES projects(id),
    revision_number INTEGER NOT NULL,
    state           TEXT    NOT NULL,     -- 'working' | 'approved' | 'superseded'
    created_at      TEXT    NOT NULL,
    approved_at     TEXT,                 -- NULL until approved
    approved_by     TEXT,                 -- user identifier
    label           TEXT                  -- optional user label ("Rev A", "Client Review")
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_revisions_project_number
    ON revisions(project_id, revision_number);

CREATE INDEX IF NOT EXISTS ix_revisions_project_state
    ON revisions(project_id, state);

-- ═══════════════════════════════════════════════
--  WORKING REVISION ENTITIES
--  (mutable working records — one active set per project)
-- ═══════════════════════════════════════════════

-- Rooms

CREATE TABLE IF NOT EXISTS rooms (
    id              TEXT    PRIMARY KEY,
    revision_id     TEXT    NOT NULL REFERENCES revisions(id),
    name            TEXT,
    shape_json      TEXT    NOT NULL,     -- serialized room outline (polygon points)
    created_at      TEXT    NOT NULL,
    updated_at      TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_rooms_revision ON rooms(revision_id);

-- Walls

CREATE TABLE IF NOT EXISTS walls (
    id              TEXT    PRIMARY KEY,
    revision_id     TEXT    NOT NULL REFERENCES revisions(id),
    room_id         TEXT    NOT NULL REFERENCES rooms(id),
    start_point     TEXT    NOT NULL,     -- "x,y" in Length units
    end_point       TEXT    NOT NULL,
    thickness       TEXT    NOT NULL,     -- Length
    created_at      TEXT    NOT NULL,
    updated_at      TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_walls_revision   ON walls(revision_id);
CREATE INDEX IF NOT EXISTS ix_walls_room       ON walls(room_id);

-- Cabinet Runs

CREATE TABLE IF NOT EXISTS runs (
    id              TEXT    PRIMARY KEY,
    revision_id     TEXT    NOT NULL REFERENCES revisions(id),
    wall_id         TEXT    NOT NULL REFERENCES walls(id),
    run_index       INTEGER NOT NULL,     -- ordering within wall
    start_offset    TEXT    NOT NULL,     -- Length along wall
    end_offset      TEXT    NOT NULL,     -- Length along wall
    end_condition_start TEXT,             -- 'wall' | 'scribe' | 'open' | ...
    end_condition_end   TEXT,
    created_at      TEXT    NOT NULL,
    updated_at      TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_runs_revision ON runs(revision_id);
CREATE INDEX IF NOT EXISTS ix_runs_wall     ON runs(wall_id);

-- Cabinets

CREATE TABLE IF NOT EXISTS cabinets (
    id              TEXT    PRIMARY KEY,
    revision_id     TEXT    NOT NULL REFERENCES revisions(id),
    run_id          TEXT    NOT NULL REFERENCES runs(id),
    slot_index      INTEGER NOT NULL,
    cabinet_type_id TEXT    NOT NULL,
    nominal_width   TEXT    NOT NULL,     -- Length
    nominal_height  TEXT    NOT NULL,
    nominal_depth   TEXT    NOT NULL,
    overrides_json  TEXT,                 -- serialized IReadOnlyDictionary<string, OverrideValue>
    created_at      TEXT    NOT NULL,
    updated_at      TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_cabinets_revision ON cabinets(revision_id);
CREATE INDEX IF NOT EXISTS ix_cabinets_run      ON cabinets(run_id);

-- Parts

CREATE TABLE IF NOT EXISTS parts (
    id              TEXT    PRIMARY KEY,
    revision_id     TEXT    NOT NULL REFERENCES revisions(id),
    cabinet_id      TEXT    NOT NULL REFERENCES cabinets(id),
    part_type       TEXT    NOT NULL,     -- 'panel' | 'stile' | 'rail' | 'door' | ...
    label           TEXT    NOT NULL,
    material_id     TEXT    NOT NULL,
    length          TEXT    NOT NULL,
    width           TEXT    NOT NULL,
    thickness       TEXT    NOT NULL,
    grain_direction TEXT,                 -- 'length' | 'width' | 'none'
    edge_treatment_json TEXT,
    created_at      TEXT    NOT NULL,
    updated_at      TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_parts_revision ON parts(revision_id);
CREATE INDEX IF NOT EXISTS ix_parts_cabinet  ON parts(cabinet_id);

-- ═══════════════════════════════════════════════
--  COMMAND JOURNAL
--  (append-only, ordered record of all committed commands)
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS command_journal (
    id                  TEXT    PRIMARY KEY,  -- CommandId GUID
    revision_id         TEXT    NOT NULL REFERENCES revisions(id),
    sequence_number     INTEGER NOT NULL,     -- monotonically increasing per revision
    command_type        TEXT    NOT NULL,
    origin              TEXT    NOT NULL,     -- CommandOrigin discriminator
    intent_description  TEXT    NOT NULL,
    affected_entity_ids TEXT    NOT NULL,     -- JSON array of strings
    parent_command_id   TEXT,
    timestamp           TEXT    NOT NULL,
    command_json        TEXT    NOT NULL,     -- full serialized command payload
    deltas_json         TEXT    NOT NULL,     -- serialized list of StateDelta
    succeeded           INTEGER NOT NULL      -- 1 = success, 0 = failure (rejected commands not journaled)
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_revision_seq
    ON command_journal(revision_id, sequence_number);

CREATE INDEX IF NOT EXISTS ix_journal_command_type
    ON command_journal(revision_id, command_type);

-- ═══════════════════════════════════════════════
--  IMMUTABLE APPROVED SNAPSHOTS
--  (frozen blobs — written once, never mutated)
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS approved_snapshots (
    revision_id         TEXT    PRIMARY KEY REFERENCES revisions(id),
    snapshot_schema_ver INTEGER NOT NULL,     -- version of the snapshot blob format
    approved_at         TEXT    NOT NULL,
    approved_by         TEXT    NOT NULL,

    -- Independent frozen blobs per concern. Each blob carries its own schema_version.
    design_blob         TEXT    NOT NULL,     -- full resolved design state
    parts_blob          TEXT    NOT NULL,     -- all parts at time of approval
    manufacturing_blob  TEXT    NOT NULL,     -- cut lists, machining plans
    install_blob        TEXT    NOT NULL,     -- install plan
    estimate_blob       TEXT    NOT NULL,     -- cost estimate
    validation_blob     TEXT    NOT NULL,     -- validation issue set at approval
    explanation_blob    TEXT    NOT NULL      -- explanation graph summary
);

-- No secondary indexes — snapshots are always queried by revision_id (PK).

-- ═══════════════════════════════════════════════
--  EXPLANATION GRAPH (working)
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS explanation_nodes (
    id                  TEXT    PRIMARY KEY,  -- ExplanationNodeId GUID
    revision_id         TEXT    NOT NULL REFERENCES revisions(id),
    command_id          TEXT,                 -- CommandId GUID (nullable — some nodes are system)
    stage_number        INTEGER,
    node_type           TEXT    NOT NULL,     -- 'command_root' | 'stage_decision' | 'constraint' | ...
    decision_type       TEXT    NOT NULL,
    description         TEXT    NOT NULL,
    affected_entity_ids TEXT    NOT NULL,     -- JSON array
    parent_node_id      TEXT,                 -- ExplanationNodeId for graph traversal
    edge_type           TEXT,                 -- 'CausedBy' | 'ProducedBy' | 'ConstrainedBy' | ...
    status              TEXT    NOT NULL DEFAULT 'active',  -- 'active' | 'undone'
    created_at          TEXT    NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_explanation_revision  ON explanation_nodes(revision_id);
CREATE INDEX IF NOT EXISTS ix_explanation_command   ON explanation_nodes(command_id);
CREATE INDEX IF NOT EXISTS ix_explanation_stage     ON explanation_nodes(revision_id, stage_number);

-- Entity index: allows "show me all decisions for cabinet X"
CREATE TABLE IF NOT EXISTS explanation_entity_index (
    node_id     TEXT    NOT NULL REFERENCES explanation_nodes(id),
    entity_id   TEXT    NOT NULL,
    PRIMARY KEY (node_id, entity_id)
);

CREATE INDEX IF NOT EXISTS ix_expidx_entity ON explanation_entity_index(entity_id);

-- ═══════════════════════════════════════════════
--  VALIDATION ISSUE HISTORY (working, per revision)
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS validation_issues (
    id                  TEXT    PRIMARY KEY,  -- stable ValidationIssueId
    revision_id         TEXT    NOT NULL REFERENCES revisions(id),
    run_at              TEXT    NOT NULL,     -- timestamp of the validation run that produced this issue
    severity            TEXT    NOT NULL,     -- 'Info' | 'Warning' | 'Error' | 'ManufactureBlocker'
    rule_code           TEXT    NOT NULL,
    message             TEXT    NOT NULL,
    affected_entity_ids TEXT    NOT NULL,     -- JSON array
    suggested_fix_json  TEXT                  -- nullable serialized SuggestedFix
);

CREATE INDEX IF NOT EXISTS ix_validation_revision ON validation_issues(revision_id);
CREATE INDEX IF NOT EXISTS ix_validation_severity ON validation_issues(revision_id, severity);

-- Entity index for issue queries ("show issues for cabinet X")
CREATE TABLE IF NOT EXISTS validation_entity_index (
    issue_id    TEXT    NOT NULL REFERENCES validation_issues(id),
    entity_id   TEXT    NOT NULL,
    PRIMARY KEY (issue_id, entity_id)
);

CREATE INDEX IF NOT EXISTS ix_validx_entity ON validation_entity_index(entity_id);

-- ═══════════════════════════════════════════════
--  AUTOSAVE CHECKPOINTS
-- ═══════════════════════════════════════════════

CREATE TABLE IF NOT EXISTS autosave_checkpoints (
    id              TEXT    PRIMARY KEY,
    project_id      TEXT    NOT NULL REFERENCES projects(id),
    revision_id     TEXT    NOT NULL REFERENCES revisions(id),
    saved_at        TEXT    NOT NULL,
    last_command_id TEXT,                 -- CommandId of the most recently committed command
    is_clean        INTEGER NOT NULL      -- 1 if no in-flight changes since last checkpoint
);

CREATE INDEX IF NOT EXISTS ix_autosave_project ON autosave_checkpoints(project_id);
```

---

## 7. Transactions and Save Flow

### 7.1 Command Commit Transaction

Every successful command execution results in exactly one atomic write. The orchestrator calls `IUnitOfWork` after the pipeline completes and before returning `CommandResult` to the caller.

```
BEGIN TRANSACTION
  1. UPDATE or INSERT working revision entities (rooms, walls, runs, cabinets, parts)
     — rows changed according to StateDelta from the pipeline
  2. INSERT INTO command_journal (new journal entry)
  3. INSERT INTO explanation_nodes (new nodes from this command's Why Engine records)
  4. INSERT INTO explanation_entity_index (entity→node mappings)
  5. (if validation ran) DELETE FROM validation_issues WHERE revision_id = ?
                         INSERT INTO validation_issues + validation_entity_index
  6. UPDATE autosave_checkpoints (update last_command_id, set is_clean = 0)
COMMIT
```

On any failure: `ROLLBACK`. The domain state in memory remains unchanged (the orchestrator only commits domain state after a successful pipeline result, and only persists inside the transaction).

### 7.2 Approval Snapshot Transaction

When a revision transitions to `approved`, a separate transaction writes the frozen blobs and updates the revision row. No working records are modified.

```
BEGIN TRANSACTION
  1. UPDATE revisions SET state = 'approved', approved_at = ?, approved_by = ? WHERE id = ?
  2. INSERT INTO approved_snapshots (all blob columns — never updated after this)
COMMIT
```

The blobs are serialized before the transaction begins. If serialization fails, the transaction is never started and the revision remains in its prior state.

### 7.3 Autosave

Autosave does not write a separate record per change. The autosave checkpoint row is updated as part of every command commit transaction (step 6 above). The autosave is always current because working state is always written on commit.

A periodic timer (default: 30 seconds) flips `is_clean = 1` on the checkpoint row after verifying the in-memory undo stack matches the journal. This is a metadata-only write, not a data write.

---

## 8. Revision and Snapshot Strategy

### 8.1 Working Revision

There is exactly one working revision per project at any time. Its `state` column is `'working'`. All mutable working tables (`cabinets`, `runs`, `walls`, `parts`, etc.) carry a `revision_id` foreign key pointing to this row.

When a new revision is created (e.g., after approval, when starting revision B), a new `revisions` row is inserted. Working entity rows for the new revision are either blank (new design) or copied from the prior revision (branch-from-approved). The prior revision's rows remain and are not deleted.

### 8.2 Immutable Approved Snapshots

At approval time, the orchestrator:

1. Runs the full resolution pipeline one final time (canonical state)
2. Serializes five independent blobs: design, parts, manufacturing, install, estimate
3. Serializes validation_blob and explanation_blob
4. Writes to `approved_snapshots` inside a transaction (never updated again)
5. Marks the revision as `approved`

**Blob format:** Each blob is a JSON object with a `schema_version` field as its first key. The snapshot reader checks this version header before deserializing. Old snapshot versions are handled by versioned deserializer implementations registered at startup — not by schema migration.

```json
{
  "schema_version": 3,
  "revision_id": "...",
  "approved_at": "...",
  "design": { ... }
}
```

**Blob schema versions evolve independently** of the working-record schema. A working-schema migration at version 42 does not change existing snapshot blobs at version 3. Blobs remain readable as long as the version-3 snapshot deserializer is registered.

### 8.3 Revision State Machine

```
working → under_review → approved → locked_for_manufacture → released_to_shop → installed → superseded
```

Transitions are recorded in the `revisions` table. Only `approved` triggers snapshot creation. `locked_for_manufacture` and beyond are metadata state changes only — the snapshot is not re-written.

---

## 9. Migration and Versioning Strategy

### 9.1 Working Schema Migrations

Migrations are applied at application startup before any repository operation. The `schema_migrations` table tracks applied versions. Migrations are forward-only — no rollback migrations. If a migration fails, the application exits and the file is left at the last clean state (SQLite's WAL ensures partial writes are not committed).

Each migration is a C# class implementing:

```csharp
namespace CabinetDesigner.Persistence.Migrations;

public interface ISchemaMigration
{
    int Version { get; }
    string Description { get; }
    void Apply(IDbConnection connection);
}
```

Migrations run in version order. New working-record tables and columns are added via `ALTER TABLE ... ADD COLUMN` (SQLite-safe). Renames require a table rebuild migration.

### 9.2 Snapshot Blob Versioning

Snapshot blobs are never migrated. They are read by the version they were written at, using a versioned deserializer. New versions of the snapshot format are registered alongside old ones.

```csharp
namespace CabinetDesigner.Persistence.Snapshots;

public interface ISnapshotDeserializer
{
    int SchemaVersion { get; }
    ApprovedSnapshot Deserialize(string json);
}
```

The snapshot reader selects the deserializer by `schema_version` from the blob header. If no deserializer is registered for a version, the snapshot is treated as unreadable (not corrupt — unreadable) and the application shows an appropriate message.

### 9.3 Commandment

> Working schema evolves. Snapshot blobs are forever.

Do not write code that reads snapshot blobs with the working-record mappers. Do not write code that migrates snapshot blob content. Snapshot deserializer code is additive only.

---

## 10. Autosave and Crash Recovery

### 10.1 Normal Operation

Every committed command writes its state changes to SQLite atomically within a transaction (see §7.1). The on-disk file is always consistent with the last committed command. There is no separate "dirty buffer" — every commit is a real write.

SQLite WAL mode (`PRAGMA journal_mode=WAL`) is required. This provides:
- Read concurrency (UI can query while a write is in progress)
- Crash safety (WAL ensures partial writes never corrupt the database)

### 10.2 Crash Recovery

On next open, the persistence layer:

1. Opens the database (WAL auto-recovers any interrupted transaction)
2. Reads the working revision and all its entity rows — this is the last committed state
3. Reads the command journal for the working revision — provides audit trail
4. The in-memory undo stack is **not** reconstructed from the journal by default (it would be expensive and requires re-materializing state deltas)
5. The undo stack starts empty; the user's session begins from the recovered state

If the application is configured for full undo recovery, the command journal and `deltas_json` can be replayed to reconstruct the undo stack. This is optional and off by default for MVP.

### 10.3 Autosave Checkpoint Semantics

The `autosave_checkpoints` table is not a separate copy of data — it is a metadata record pointing to the current revision and its last committed command. The "checkpoint" is always implicit in the working revision rows themselves.

If `is_clean = 0` on the checkpoint row at startup, the application knows the previous session ended without a clean shutdown. The working state is still valid (SQLite WAL guarantees commit integrity), but the application may display a recovery notice.

### 10.4 Periodic Flush

After each command commit, the application signals the autosave service. The autosave service:
- Updates `last_command_id` on the checkpoint (part of the commit transaction already)
- After a configurable idle period (default: 30s), calls `PRAGMA wal_checkpoint(TRUNCATE)` to force WAL pages to the main database file and reduce recovery time on next open

---

## 11. Explanation and Validation Persistence

### 11.1 Explanation Nodes

Explanation nodes are written as part of the command commit transaction (§7.1 step 3–4). They are append-only. An undone command's nodes are not deleted — their `status` column is updated to `'undone'` in a subsequent transaction when undo is committed.

Nodes are queryable by:
- `revision_id` (all decisions for a revision)
- `command_id` (decisions from a specific command)
- `stage_number` (decisions from a pipeline stage)
- `entity_id` via `explanation_entity_index` (all decisions affecting a cabinet, run, etc.)

The explanation graph is not stored as a graph database. The `parent_node_id` and `edge_type` columns encode the graph structure within the flat table. Traversal queries use recursive CTEs where needed.

### 11.2 Validation Issues

Validation issues for the working revision are **replaced** on each validation run. They are not append-only. The replace strategy:

```sql
DELETE FROM validation_issues WHERE revision_id = ?;
DELETE FROM validation_entity_index WHERE issue_id NOT IN (SELECT id FROM validation_issues);
-- then insert the new issue set
```

This is part of the command commit transaction when validation runs after every deep-path commit.

At approval time, validation issues are serialized into `approved_snapshots.validation_blob` (frozen). The working `validation_issues` rows remain as the current working state.

---

## 12. Persistence Model Conventions (Internal)

These types exist only in `CabinetDesigner.Persistence.Models`. They are never exposed.

```csharp
namespace CabinetDesigner.Persistence.Models;

// Example — flat persistence record, no behavior
internal sealed class CabinetRow
{
    public string Id { get; set; } = "";
    public string RevisionId { get; set; } = "";
    public string RunId { get; set; } = "";
    public int SlotIndex { get; set; }
    public string CabinetTypeId { get; set; } = "";
    public string NominalWidth { get; set; } = "";   // "36.000in"
    public string NominalHeight { get; set; } = "";
    public string NominalDepth { get; set; } = "";
    public string? OverridesJson { get; set; }
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
}
```

Mapper conventions:
- `Domain → Row`: explicit mapper class in `CabinetDesigner.Persistence.Mapping`
- `Row → Domain`: explicit factory call on the domain entity's constructor or a static factory — never a reflection-based auto-mapper
- `Length` serialized as: `value.ToInches().ToString("F4") + "in"` — parseable by `Length.Parse()`
- `IReadOnlyList<T>` serialized as JSON arrays via `System.Text.Json`
- All discriminated union types (`OverrideValue`, `DeltaValue`) use a `type` field as JSON discriminator

---

## 13. Invariants

1. **Working records reference only the active revision.** No working entity row may have a `revision_id` pointing to an approved or superseded revision. Enforced by repository on write.

2. **Approved snapshots are write-once.** The `approved_snapshots` table has no `UPDATE` path in the codebase. INSERT only.

3. **Command journal is append-only.** No DELETE or UPDATE on `command_journal` rows. Soft-marking an entry is not permitted — journal rows are permanent.

4. **Snapshot blobs carry their schema version.** Any blob without a valid `schema_version` field is rejected on read.

5. **Every command commit is a single SQLite transaction.** Working state, journal, and explanation nodes commit together or not at all.

6. **Lengths are never stored as REAL.** All dimensional values use TEXT with unit suffix. REAL columns for dimensions are a schema violation.

7. **Approved snapshot blobs are never mutated by migration.** `schema_migrations` scripts must not UPDATE or touch `approved_snapshots`.

8. **Domain types do not appear in persistence models.** No geometry value objects, no domain entities in `CabinetDesigner.Persistence.Models`.

9. **Session state (selection, zoom, pan) is never persisted.** These are reconstructed from scratch on project open.

10. **One working revision per project at a time.** Only one `revisions` row with `state = 'working'` per `project_id`.

---

## 14. Testing Strategy

### Unit Tests (in `CabinetDesigner.Tests`)

- Mapper round-trip: `domain entity → row → domain entity` produces identical values
- Length serialization: all unit combinations round-trip without drift
- Override value serialization: all `OverrideValue` discriminated union cases round-trip
- Delta serialization: all `DeltaValue` union cases round-trip
- Snapshot blob versioning: old-version blobs deserialize correctly via registered deserializer

### Integration Tests (require real SQLite file)

- Full command commit transaction writes all expected rows atomically
- Transaction rollback on failure leaves no partial state
- Command journal append is ordered correctly (sequence_number monotonically increases)
- Approved snapshot is unmodified after two subsequent working-schema migrations
- Crash simulation (truncate WAL mid-write) recovers to last clean committed state
- Explanation node query by entity ID returns correct nodes across multiple commands
- Validation issue replace correctly removes prior run's issues and inserts new set
- Autosave checkpoint is_clean semantics on clean vs interrupted shutdown

### Snapshot Immutability Tests

- Write a snapshot at schema version N, run schema migrations to N+3, read back the snapshot — content is identical
- Assert no UPDATE SQL paths touch `approved_snapshots` (static analysis / convention test)

---

## 15. Risks and Edge Cases

| Risk | Mitigation |
|---|---|
| **SQLite file grows unboundedly (old revision rows never deleted)** | Accept for MVP. Implement archive/prune feature as a future phase. Periodic `VACUUM` recommended. |
| **Snapshot deserializer missing for old version** | Return `SnapshotReadResult.Unreadable` with version number. UI displays a recovery notice. Never crash. |
| **Migration failure on startup** | Log the failed migration version, leave the database at its last clean state, display error to user. Do not attempt to auto-recover. |
| **Large command JSON / delta JSON payloads (bulk operations)** | Cap delta arrays at a reasonable size. For bulk commands (resize 50 cabinets), store a summary delta with a `bulk` flag. Full payloads remain in memory for the session's undo stack. |
| **Concurrent access from two process instances** | Not supported. SQLite busy-timeout set to 0 — second attempt to open fails immediately. Show a "project is already open" error. |
| **Length parsing drift across versions** | `Length.Parse()` is the single parsing entry point, tested for all known formats. Any format change increments the working schema version and adds a migration. |
| **Explanation graph grows very large for long sessions** | Explanation nodes are lightweight (no entity snapshots). For very long sessions, a truncation policy (retain last N commands' nodes) is a future phase option. |
| **Undo after crash recovery** | In-memory undo stack is empty after recovery. User is notified. No phantom undo entries. |
| **Snapshot approval fails mid-write (power loss)** | Transactional write: either fully committed or fully rolled back. Revision state remains `under_review`. User re-triggers approval. |
