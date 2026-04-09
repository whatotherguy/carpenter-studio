namespace CabinetDesigner.Persistence.Migrations;

public sealed class V1_InitialSchema : ISchemaMigration
{
    public int Version => 1;

    public string Description => "Initial working persistence schema with immutable approved snapshots.";

    public void Apply(IDbConnection connection, IDbTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL,
                description TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS projects (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                description TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                current_state TEXT NOT NULL,
                file_path TEXT
            );

            CREATE TABLE IF NOT EXISTS revisions (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL REFERENCES projects(id),
                revision_number INTEGER NOT NULL,
                state TEXT NOT NULL,
                created_at TEXT NOT NULL,
                approved_at TEXT,
                approved_by TEXT,
                label TEXT,
                approval_notes TEXT
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_revisions_project_number
                ON revisions(project_id, revision_number);
            CREATE INDEX IF NOT EXISTS ix_revisions_project_state
                ON revisions(project_id, state);

            CREATE TABLE IF NOT EXISTS rooms (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                name TEXT,
                shape_json TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_rooms_revision ON rooms(revision_id);

            CREATE TABLE IF NOT EXISTS walls (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                room_id TEXT NOT NULL REFERENCES rooms(id),
                start_point TEXT NOT NULL,
                end_point TEXT NOT NULL,
                thickness TEXT NOT NULL,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_walls_revision ON walls(revision_id);
            CREATE INDEX IF NOT EXISTS ix_walls_room ON walls(room_id);

            CREATE TABLE IF NOT EXISTS runs (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                wall_id TEXT NOT NULL REFERENCES walls(id),
                run_index INTEGER NOT NULL,
                start_offset TEXT NOT NULL,
                end_offset TEXT NOT NULL,
                end_condition_start TEXT,
                end_condition_end TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_runs_revision ON runs(revision_id);
            CREATE INDEX IF NOT EXISTS ix_runs_wall ON runs(wall_id);

            CREATE TABLE IF NOT EXISTS cabinets (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                run_id TEXT NOT NULL REFERENCES runs(id),
                slot_index INTEGER NOT NULL,
                cabinet_type_id TEXT NOT NULL,
                category TEXT NOT NULL,
                construction_method TEXT NOT NULL,
                nominal_width TEXT NOT NULL,
                nominal_height TEXT NOT NULL,
                nominal_depth TEXT NOT NULL,
                overrides_json TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_cabinets_revision ON cabinets(revision_id);
            CREATE INDEX IF NOT EXISTS ix_cabinets_run ON cabinets(run_id);

            CREATE TABLE IF NOT EXISTS parts (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                cabinet_id TEXT NOT NULL REFERENCES cabinets(id),
                part_type TEXT NOT NULL,
                label TEXT NOT NULL,
                material_id TEXT NOT NULL,
                length TEXT NOT NULL,
                width TEXT NOT NULL,
                thickness TEXT NOT NULL,
                grain_direction TEXT,
                edge_treatment_json TEXT,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_parts_revision ON parts(revision_id);
            CREATE INDEX IF NOT EXISTS ix_parts_cabinet ON parts(cabinet_id);

            CREATE TABLE IF NOT EXISTS command_journal (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                sequence_number INTEGER NOT NULL,
                command_type TEXT NOT NULL,
                origin TEXT NOT NULL,
                intent_description TEXT NOT NULL,
                affected_entity_ids TEXT NOT NULL,
                parent_command_id TEXT,
                timestamp TEXT NOT NULL,
                command_json TEXT NOT NULL,
                deltas_json TEXT NOT NULL,
                succeeded INTEGER NOT NULL
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_journal_revision_seq
                ON command_journal(revision_id, sequence_number);
            CREATE INDEX IF NOT EXISTS ix_journal_command_type
                ON command_journal(revision_id, command_type);

            CREATE TABLE IF NOT EXISTS approved_snapshots (
                revision_id TEXT PRIMARY KEY REFERENCES revisions(id),
                snapshot_schema_ver INTEGER NOT NULL,
                approved_at TEXT NOT NULL,
                approved_by TEXT NOT NULL,
                design_blob TEXT NOT NULL,
                parts_blob TEXT NOT NULL,
                manufacturing_blob TEXT NOT NULL,
                install_blob TEXT NOT NULL,
                estimate_blob TEXT NOT NULL,
                validation_blob TEXT NOT NULL,
                explanation_blob TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS explanation_nodes (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                command_id TEXT,
                stage_number INTEGER,
                node_type TEXT NOT NULL,
                decision_type TEXT NOT NULL,
                description TEXT NOT NULL,
                affected_entity_ids TEXT NOT NULL,
                parent_node_id TEXT,
                edge_type TEXT,
                status TEXT NOT NULL DEFAULT 'active',
                created_at TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_explanation_revision ON explanation_nodes(revision_id);
            CREATE INDEX IF NOT EXISTS ix_explanation_command ON explanation_nodes(command_id);
            CREATE INDEX IF NOT EXISTS ix_explanation_stage ON explanation_nodes(revision_id, stage_number);

            CREATE TABLE IF NOT EXISTS explanation_entity_index (
                node_id TEXT NOT NULL REFERENCES explanation_nodes(id),
                entity_id TEXT NOT NULL,
                PRIMARY KEY(node_id, entity_id)
            );

            CREATE INDEX IF NOT EXISTS ix_expidx_entity ON explanation_entity_index(entity_id);

            CREATE TABLE IF NOT EXISTS validation_issues (
                id TEXT PRIMARY KEY,
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                run_at TEXT NOT NULL,
                severity TEXT NOT NULL,
                rule_code TEXT NOT NULL,
                message TEXT NOT NULL,
                affected_entity_ids TEXT NOT NULL,
                suggested_fix_json TEXT
            );

            CREATE INDEX IF NOT EXISTS ix_validation_revision ON validation_issues(revision_id);
            CREATE INDEX IF NOT EXISTS ix_validation_severity ON validation_issues(revision_id, severity);

            CREATE TABLE IF NOT EXISTS validation_entity_index (
                issue_id TEXT NOT NULL REFERENCES validation_issues(id),
                entity_id TEXT NOT NULL,
                PRIMARY KEY(issue_id, entity_id)
            );

            CREATE INDEX IF NOT EXISTS ix_validx_entity ON validation_entity_index(entity_id);

            CREATE TABLE IF NOT EXISTS autosave_checkpoints (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL REFERENCES projects(id),
                revision_id TEXT NOT NULL REFERENCES revisions(id),
                saved_at TEXT NOT NULL,
                last_command_id TEXT,
                is_clean INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS ix_autosave_project ON autosave_checkpoints(project_id);

            CREATE TRIGGER IF NOT EXISTS trg_snapshots_no_update
                BEFORE UPDATE ON approved_snapshots
            BEGIN
                SELECT RAISE(ABORT, 'approved_snapshots rows are immutable and may not be updated');
            END;

            CREATE TRIGGER IF NOT EXISTS trg_snapshots_no_delete
                BEFORE DELETE ON approved_snapshots
            BEGIN
                SELECT RAISE(ABORT, 'approved_snapshots rows are immutable and may not be deleted');
            END;
            """;
        command.ExecuteNonQuery();
    }
}
