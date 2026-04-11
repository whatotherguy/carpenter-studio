using CabinetDesigner.Persistence.Mapping;
using CabinetDesigner.Persistence.Models;
using CabinetDesigner.Persistence.UnitOfWork;
using Microsoft.Data.Sqlite;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class WorkingRevisionRepository : SqliteRepositoryBase, IWorkingRevisionRepository
{
    public WorkingRevisionRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task<WorkingRevision?> LoadAsync(ProjectId projectId, CancellationToken ct = default) =>
        WithConnectionAsync<WorkingRevision?>(
            async (connection, transaction) =>
            {
                var revision = await LoadRevisionAsync(connection, transaction, projectId, ct).ConfigureAwait(false);
                if (revision is null)
                {
                    return null;
                }

                var rooms = await LoadRoomsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);
                var walls = await LoadWallsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);
                var runs = await LoadRunsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);
                var cabinets = await LoadCabinetsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);
                var parts = await LoadPartsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);

                var runsById = runs.ToDictionary(run => run.Id);
                var cabinetsById = cabinets.ToDictionary(cabinet => cabinet.Id);
                var cabinetRows = await LoadCabinetRowsAsync(connection, transaction, revision.Id, ct).ConfigureAwait(false);

                foreach (var row in cabinetRows.OrderBy(row => row.SlotIndex))
                {
                    if (!runsById.TryGetValue(new RunId(Guid.Parse(row.RunId)), out var run))
                    {
                        continue;
                    }

                    if (!cabinetsById.TryGetValue(new CabinetId(Guid.Parse(row.Id)), out var cabinet))
                    {
                        continue;
                    }

                    run.AppendCabinet(cabinet.Id, cabinet.NominalWidth);
                }

                return new WorkingRevision(revision, rooms, walls, runsById.Values.ToArray(), cabinets, parts);
            },
            ct);

    public Task SaveAsync(WorkingRevision revision, CancellationToken ct = default) =>
        WithConnectionAsync(
            async (connection, transaction) =>
            {
                if (transaction is not null)
                {
                    await SaveCoreAsync(connection, transaction, revision, ct).ConfigureAwait(false);
                    return;
                }

                await using var localTransaction = (SqliteTransaction)await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
                try
                {
                    await SaveCoreAsync(connection, localTransaction, revision, ct).ConfigureAwait(false);
                    await localTransaction.CommitAsync(ct).ConfigureAwait(false);
                }
                catch
                {
                    await localTransaction.RollbackAsync(ct).ConfigureAwait(false);
                    throw;
                }
            },
            ct);

    private static async Task SaveCoreAsync(SqliteConnection connection, SqliteTransaction transaction, WorkingRevision revision, CancellationToken ct)
    {
        var timestamp = revision.Revision.CreatedAt;
        await DeleteExistingRowsAsync(connection, transaction, revision.Revision.Id, ct).ConfigureAwait(false);

        foreach (var room in revision.Rooms)
        {
            await InsertRoomAsync(connection, transaction, RoomMapper.ToRow(room, timestamp), ct).ConfigureAwait(false);
        }

        foreach (var wall in revision.Walls)
        {
            await InsertWallAsync(connection, transaction, WallMapper.ToRow(wall, revision.Revision.Id, timestamp), ct).ConfigureAwait(false);
        }

        foreach (var run in revision.Runs.Select((run, index) => (run, index)))
        {
            await InsertRunAsync(connection, transaction, RunMapper.ToRow(run.run, revision.Revision.Id, run.index, timestamp), ct).ConfigureAwait(false);
        }

        var runByCabinetId = revision.Runs
            .SelectMany(run => run.Slots.Where(slot => slot.CabinetId is not null).Select(slot => (run.Id, slot.CabinetId!.Value, slot.SlotIndex)))
            .ToDictionary(item => item.Value, item => (item.Id, item.SlotIndex));

        foreach (var cabinet in revision.Cabinets)
        {
            if (!runByCabinetId.TryGetValue(cabinet.Id, out var placement))
            {
                continue;
            }

            await InsertCabinetAsync(connection, transaction, CabinetMapper.ToRow(cabinet, revision.Revision.Id, placement.Id, placement.SlotIndex, timestamp), ct).ConfigureAwait(false);
        }

        foreach (var part in revision.Parts)
        {
            await InsertPartAsync(connection, transaction, PartMapper.ToRow(part, revision.Revision.Id, timestamp), ct).ConfigureAwait(false);
        }
    }

    private static async Task<RevisionRecord?> LoadRevisionAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        ProjectId projectId,
        CancellationToken ct)
    {
        using var command = CreateCommand(connection, transaction, """
            SELECT id, project_id, revision_number, state, created_at, approved_at, approved_by, label, approval_notes
            FROM revisions
            WHERE project_id = @projectId AND state = 'Draft'
            ORDER BY revision_number DESC
            LIMIT 1;
            """);
        command.Parameters.AddWithValue("@projectId", projectId.Value.ToString());
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if (!await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            return null;
        }

        return RevisionMapper.ToRecord(new RevisionRow
        {
            Id = reader.GetString(0),
            ProjectId = reader.GetString(1),
            RevisionNumber = reader.GetInt32(2),
            State = reader.GetString(3),
            CreatedAt = reader.GetString(4),
            ApprovedAt = reader.IsDBNull(5) ? null : reader.GetString(5),
            ApprovedBy = reader.IsDBNull(6) ? null : reader.GetString(6),
            Label = reader.IsDBNull(7) ? null : reader.GetString(7),
            ApprovalNotes = reader.IsDBNull(8) ? null : reader.GetString(8)
        });
    }

    private static async Task<IReadOnlyList<Room>> LoadRoomsAsync(SqliteConnection connection, SqliteTransaction? transaction, RevisionId revisionId, CancellationToken ct)
    {
        using var command = CreateCommand(connection, transaction, "SELECT id, revision_id, name, shape_json, created_at, updated_at FROM rooms WHERE revision_id = @revisionId ORDER BY id;");
        command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        var rooms = new List<Room>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rooms.Add(RoomMapper.ToDomain(new RoomRow
            {
                Id = reader.GetString(0),
                RevisionId = reader.GetString(1),
                Name = reader.IsDBNull(2) ? null : reader.GetString(2),
                ShapeJson = reader.GetString(3),
                CreatedAt = reader.GetString(4),
                UpdatedAt = reader.GetString(5)
            }));
        }

        return rooms;
    }

    private static async Task<IReadOnlyList<Wall>> LoadWallsAsync(SqliteConnection connection, SqliteTransaction? transaction, RevisionId revisionId, CancellationToken ct)
    {
        using var command = CreateCommand(connection, transaction, "SELECT id, revision_id, room_id, start_point, end_point, thickness, created_at, updated_at FROM walls WHERE revision_id = @revisionId ORDER BY id;");
        command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        var walls = new List<Wall>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            walls.Add(WallMapper.ToDomain(new WallRow
            {
                Id = reader.GetString(0),
                RevisionId = reader.GetString(1),
                RoomId = reader.GetString(2),
                StartPoint = reader.GetString(3),
                EndPoint = reader.GetString(4),
                Thickness = reader.GetString(5),
                CreatedAt = reader.GetString(6),
                UpdatedAt = reader.GetString(7)
            }));
        }

        return walls;
    }

    private static async Task<IReadOnlyList<CabinetRun>> LoadRunsAsync(SqliteConnection connection, SqliteTransaction? transaction, RevisionId revisionId, CancellationToken ct)
    {
        using var command = CreateCommand(connection, transaction, "SELECT id, revision_id, wall_id, run_index, start_offset, end_offset, end_condition_start, end_condition_end, created_at, updated_at FROM runs WHERE revision_id = @revisionId ORDER BY run_index;");
        command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        var runs = new List<CabinetRun>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            runs.Add(RunMapper.ToDomain(new RunRow
            {
                Id = reader.GetString(0),
                RevisionId = reader.GetString(1),
                WallId = reader.GetString(2),
                RunIndex = reader.GetInt32(3),
                StartOffset = reader.GetString(4),
                EndOffset = reader.GetString(5),
                EndConditionStart = reader.IsDBNull(6) ? null : reader.GetString(6),
                EndConditionEnd = reader.IsDBNull(7) ? null : reader.GetString(7),
                CreatedAt = reader.GetString(8),
                UpdatedAt = reader.GetString(9)
            }));
        }

        return runs;
    }

    private static async Task<IReadOnlyList<Cabinet>> LoadCabinetsAsync(SqliteConnection connection, SqliteTransaction? transaction, RevisionId revisionId, CancellationToken ct)
    {
        var rows = await LoadCabinetRowsAsync(connection, transaction, revisionId, ct).ConfigureAwait(false);
        return rows.Select(CabinetMapper.ToDomain).ToArray();
    }

    private static async Task<List<CabinetRow>> LoadCabinetRowsAsync(SqliteConnection connection, SqliteTransaction? transaction, RevisionId revisionId, CancellationToken ct)
    {
        using var command = CreateCommand(connection, transaction, "SELECT id, revision_id, run_id, slot_index, cabinet_type_id, category, construction_method, nominal_width, nominal_height, nominal_depth, overrides_json, created_at, updated_at FROM cabinets WHERE revision_id = @revisionId ORDER BY slot_index;");
        command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        var cabinets = new List<CabinetRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            cabinets.Add(new CabinetRow
            {
                Id = reader.GetString(0),
                RevisionId = reader.GetString(1),
                RunId = reader.GetString(2),
                SlotIndex = reader.GetInt32(3),
                CabinetTypeId = reader.GetString(4),
                Category = reader.GetString(5),
                ConstructionMethod = reader.GetString(6),
                NominalWidth = reader.GetString(7),
                NominalHeight = reader.GetString(8),
                NominalDepth = reader.GetString(9),
                OverridesJson = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt = reader.GetString(11),
                UpdatedAt = reader.GetString(12)
            });
        }

        return cabinets;
    }

    private static async Task<IReadOnlyList<CabinetDesigner.Application.Pipeline.StageResults.GeneratedPart>> LoadPartsAsync(SqliteConnection connection, SqliteTransaction? transaction, RevisionId revisionId, CancellationToken ct)
    {
        using var command = CreateCommand(connection, transaction, "SELECT id, revision_id, cabinet_id, part_type, label, material_id, length, width, thickness, grain_direction, edge_treatment_json, created_at, updated_at FROM parts WHERE revision_id = @revisionId ORDER BY id;");
        command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
        var parts = new List<CabinetDesigner.Application.Pipeline.StageResults.GeneratedPart>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            parts.Add(PartMapper.ToDomain(new PartRow
            {
                Id = reader.GetString(0),
                RevisionId = reader.GetString(1),
                CabinetId = reader.GetString(2),
                PartType = reader.GetString(3),
                Label = reader.GetString(4),
                MaterialId = reader.GetString(5),
                Length = reader.GetString(6),
                Width = reader.GetString(7),
                Thickness = reader.GetString(8),
                GrainDirection = reader.IsDBNull(9) ? null : reader.GetString(9),
                EdgeTreatmentJson = reader.IsDBNull(10) ? null : reader.GetString(10),
                CreatedAt = reader.GetString(11),
                UpdatedAt = reader.GetString(12)
            }));
        }

        return parts;
    }

    private static async Task DeleteExistingRowsAsync(SqliteConnection connection, SqliteTransaction? transaction, RevisionId revisionId, CancellationToken ct)
    {
        // Table names are compile-time string constants drawn from this hardcoded array.
        // String interpolation here is intentionally safe: there is no user input involved
        // and no SQL-injection vector exists.
        foreach (var table in new[] { "parts", "cabinets", "runs", "walls", "rooms" })
        {
            using var command = CreateCommand(connection, transaction, $"DELETE FROM {table} WHERE revision_id = @revisionId;");
            command.Parameters.AddWithValue("@revisionId", revisionId.Value.ToString());
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
    }

    private static Task InsertRoomAsync(SqliteConnection connection, SqliteTransaction? transaction, RoomRow row, CancellationToken ct) =>
        ExecuteInsertAsync(connection, transaction, "INSERT INTO rooms(id, revision_id, name, shape_json, created_at, updated_at) VALUES(@id, @revisionId, @name, @shapeJson, @createdAt, @updatedAt);",
            ct, ("@id", row.Id), ("@revisionId", row.RevisionId), ("@name", (object?)row.Name ?? DBNull.Value), ("@shapeJson", row.ShapeJson), ("@createdAt", row.CreatedAt), ("@updatedAt", row.UpdatedAt));

    private static Task InsertWallAsync(SqliteConnection connection, SqliteTransaction? transaction, WallRow row, CancellationToken ct) =>
        ExecuteInsertAsync(connection, transaction, "INSERT INTO walls(id, revision_id, room_id, start_point, end_point, thickness, created_at, updated_at) VALUES(@id, @revisionId, @roomId, @startPoint, @endPoint, @thickness, @createdAt, @updatedAt);",
            ct, ("@id", row.Id), ("@revisionId", row.RevisionId), ("@roomId", row.RoomId), ("@startPoint", row.StartPoint), ("@endPoint", row.EndPoint), ("@thickness", row.Thickness), ("@createdAt", row.CreatedAt), ("@updatedAt", row.UpdatedAt));

    private static Task InsertRunAsync(SqliteConnection connection, SqliteTransaction? transaction, RunRow row, CancellationToken ct) =>
        ExecuteInsertAsync(connection, transaction, "INSERT INTO runs(id, revision_id, wall_id, run_index, start_offset, end_offset, end_condition_start, end_condition_end, created_at, updated_at) VALUES(@id, @revisionId, @wallId, @runIndex, @startOffset, @endOffset, @startCondition, @endCondition, @createdAt, @updatedAt);",
            ct, ("@id", row.Id), ("@revisionId", row.RevisionId), ("@wallId", row.WallId), ("@runIndex", row.RunIndex), ("@startOffset", row.StartOffset), ("@endOffset", row.EndOffset), ("@startCondition", (object?)row.EndConditionStart ?? DBNull.Value), ("@endCondition", (object?)row.EndConditionEnd ?? DBNull.Value), ("@createdAt", row.CreatedAt), ("@updatedAt", row.UpdatedAt));

    private static Task InsertCabinetAsync(SqliteConnection connection, SqliteTransaction? transaction, CabinetRow row, CancellationToken ct) =>
        ExecuteInsertAsync(connection, transaction, "INSERT INTO cabinets(id, revision_id, run_id, slot_index, cabinet_type_id, category, construction_method, nominal_width, nominal_height, nominal_depth, overrides_json, created_at, updated_at) VALUES(@id, @revisionId, @runId, @slotIndex, @cabinetTypeId, @category, @constructionMethod, @nominalWidth, @nominalHeight, @nominalDepth, @overridesJson, @createdAt, @updatedAt);",
            ct, ("@id", row.Id), ("@revisionId", row.RevisionId), ("@runId", row.RunId), ("@slotIndex", row.SlotIndex), ("@cabinetTypeId", row.CabinetTypeId), ("@category", row.Category), ("@constructionMethod", row.ConstructionMethod), ("@nominalWidth", row.NominalWidth), ("@nominalHeight", row.NominalHeight), ("@nominalDepth", row.NominalDepth), ("@overridesJson", (object?)row.OverridesJson ?? DBNull.Value), ("@createdAt", row.CreatedAt), ("@updatedAt", row.UpdatedAt));

    private static Task InsertPartAsync(SqliteConnection connection, SqliteTransaction? transaction, PartRow row, CancellationToken ct) =>
        ExecuteInsertAsync(connection, transaction, "INSERT INTO parts(id, revision_id, cabinet_id, part_type, label, material_id, length, width, thickness, grain_direction, edge_treatment_json, created_at, updated_at) VALUES(@id, @revisionId, @cabinetId, @partType, @label, @materialId, @length, @width, @thickness, @grainDirection, @edgeJson, @createdAt, @updatedAt);",
            ct, ("@id", row.Id), ("@revisionId", row.RevisionId), ("@cabinetId", row.CabinetId), ("@partType", row.PartType), ("@label", row.Label), ("@materialId", row.MaterialId), ("@length", row.Length), ("@width", row.Width), ("@thickness", row.Thickness), ("@grainDirection", (object?)row.GrainDirection ?? DBNull.Value), ("@edgeJson", (object?)row.EdgeTreatmentJson ?? DBNull.Value), ("@createdAt", row.CreatedAt), ("@updatedAt", row.UpdatedAt));

    private static async Task ExecuteInsertAsync(SqliteConnection connection, SqliteTransaction? transaction, string sql, CancellationToken ct, params (string Name, object Value)[] parameters)
    {
        using var command = CreateCommand(connection, transaction, sql);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
