using System.Text.Json;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Domain.Identifiers;
using CabinetDesigner.Domain.SpatialContext;
using CabinetDesigner.Persistence.Migrations;
using CabinetDesigner.Persistence.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CabinetDesigner.Persistence.Tests.Integration;

public sealed class ProjectLifecycleIntegrationTests
{
    [Fact]
    public async Task CreateProject_ThenOpen_RoundTripsAllState()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        await using var provider = BuildProvider(fixture.DatabasePath, new FixedClock(new DateTimeOffset(2026, 4, 18, 12, 0, 0, TimeSpan.Zero)));
        await using var scope = provider.CreateAsyncScope();

        var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
        var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();
        var runService = scope.ServiceProvider.GetRequiredService<IRunService>();
        var currentClock = scope.ServiceProvider.GetRequiredService<IClock>();

        var created = await projectService.CreateProjectAsync("Kitchen Shop");
        var room = await roomService.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
        var wall = await roomService.AddWallAsync(room.Id, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)), default);
        var run = await runService.CreateRunAsync(room.Id, wall.Id, Length.FromInches(12m), Length.FromInches(96m));
        await runService.PlaceCabinetAsync(run.Id, "base-standard-24");
        await CommitCurrentStateAsync(scope.ServiceProvider, currentClock.Now, "lifecycle.roundtrip", room.Id.Value.ToString(), wall.Id.Value.ToString(), run.Id.Value.ToString());
        await projectService.SaveAsync();
        await projectService.CloseAsync();

        var reopened = await projectService.OpenProjectAsync(new ProjectId(created.ProjectId));
        var currentState = scope.ServiceProvider.GetRequiredService<CurrentWorkingRevisionSource>().CurrentState
            ?? throw new InvalidOperationException("Expected current state to be available after reopen.");

        Assert.Equal(created.ProjectId, reopened.ProjectId);
        Assert.Single(currentState.WorkingRevision.Rooms);
        Assert.Single(currentState.WorkingRevision.Walls);
        Assert.Single(currentState.WorkingRevision.Runs);
        Assert.Single(currentState.WorkingRevision.Cabinets);

        var loadedRoom = currentState.WorkingRevision.Rooms.Single();
        Assert.Equal(room.Id, loadedRoom.Id);
        Assert.Equal("Kitchen", loadedRoom.Name);
        Assert.Equal(Length.FromInches(96m), loadedRoom.CeilingHeight);

        var loadedWall = currentState.WorkingRevision.Walls.Single();
        Assert.Equal(wall.StartPoint, loadedWall.StartPoint);
        Assert.Equal(wall.EndPoint, loadedWall.EndPoint);

        var loadedCabinet = currentState.WorkingRevision.Cabinets.Single();
        Assert.Equal("base-standard-24", loadedCabinet.CabinetTypeId);
        Assert.Equal(Length.FromInches(24m), loadedCabinet.NominalWidth);
        Assert.Equal(CabinetCategory.Base, loadedCabinet.Category);
        Assert.Equal(ConstructionMethod.Frameless, loadedCabinet.Construction);
    }

    [Fact]
    public async Task Save_IsIdempotent_AndStableHash()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        await using var provider = BuildProvider(fixture.DatabasePath, new FixedClock(new DateTimeOffset(2026, 4, 18, 13, 0, 0, TimeSpan.Zero)));
        await using var scope = provider.CreateAsyncScope();

        var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
        var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();
        var runService = scope.ServiceProvider.GetRequiredService<IRunService>();
        var currentClock = scope.ServiceProvider.GetRequiredService<IClock>();

        var created = await projectService.CreateProjectAsync("Kitchen Shop");
        var room = await roomService.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
        var wall = await roomService.AddWallAsync(room.Id, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)), default);
        var run = await runService.CreateRunAsync(room.Id, wall.Id, Length.FromInches(12m), Length.FromInches(96m));
        await runService.PlaceCabinetAsync(run.Id, "base-standard-24");
        await CommitCurrentStateAsync(scope.ServiceProvider, currentClock.Now, "lifecycle.hash", room.Id.Value.ToString(), wall.Id.Value.ToString(), run.Id.Value.ToString());

        var firstSignature = await CaptureWorkingStateSignatureAsync(fixture.ConnectionFactory, created.ProjectId);
        await projectService.SaveAsync();
        var secondSignature = await CaptureWorkingStateSignatureAsync(fixture.ConnectionFactory, created.ProjectId);
        await projectService.SaveAsync();
        var thirdSignature = await CaptureWorkingStateSignatureAsync(fixture.ConnectionFactory, created.ProjectId);

        Assert.Equal(firstSignature, secondSignature);
        Assert.Equal(secondSignature, thirdSignature);
    }

    [Fact]
    public async Task CloseWithUnsavedChanges_DropsUnsavedState()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        await using var provider = BuildProvider(fixture.DatabasePath, new FixedClock(new DateTimeOffset(2026, 4, 18, 14, 0, 0, TimeSpan.Zero)));
        await using var scope = provider.CreateAsyncScope();

        var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
        var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();
        var runService = scope.ServiceProvider.GetRequiredService<IRunService>();
        var project = await projectService.CreateProjectAsync("Kitchen Shop");
        var room = await roomService.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
        var wall = await roomService.AddWallAsync(room.Id, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)), default);
        await projectService.SaveAsync();

        var unsavedRun = await runService.CreateRunAsync(room.Id, wall.Id, Length.FromInches(12m), Length.FromInches(72m));
        await runService.PlaceCabinetAsync(unsavedRun.Id, "base-standard-24");
        await projectService.CloseAsync();

        var reopened = await projectService.OpenProjectAsync(new ProjectId(project.ProjectId));
        var currentState = scope.ServiceProvider.GetRequiredService<CurrentWorkingRevisionSource>().CurrentState
            ?? throw new InvalidOperationException("Expected current state to be available after reopen.");

        Assert.Equal(project.ProjectId, reopened.ProjectId);
        Assert.Single(currentState.WorkingRevision.Rooms);
        Assert.Single(currentState.WorkingRevision.Walls);
        Assert.Empty(currentState.WorkingRevision.Runs);
        Assert.Empty(currentState.WorkingRevision.Cabinets);
    }

    [Fact]
    public async Task AutosaveCheckpoint_CreatedOnCommandSuccess()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        await using var provider = BuildProvider(fixture.DatabasePath, new FixedClock(new DateTimeOffset(2026, 4, 18, 15, 0, 0, TimeSpan.Zero)));
        await using var scope = provider.CreateAsyncScope();

        var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
        var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();
        var runService = scope.ServiceProvider.GetRequiredService<IRunService>();
        var checkpointRepository = scope.ServiceProvider.GetRequiredService<IAutosaveCheckpointRepository>();
        var currentClock = scope.ServiceProvider.GetRequiredService<IClock>();

        var project = await projectService.CreateProjectAsync("Kitchen Shop");
        var room = await roomService.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
        var wall = await roomService.AddWallAsync(room.Id, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)), default);
        var run = await runService.CreateRunAsync(room.Id, wall.Id, Length.FromInches(12m), Length.FromInches(96m));
        await runService.PlaceCabinetAsync(run.Id, "base-standard-24");
        var commandMetadata = await CommitCurrentStateAsync(scope.ServiceProvider, currentClock.Now, "lifecycle.checkpoint", room.Id.Value.ToString(), wall.Id.Value.ToString(), run.Id.Value.ToString());

        var checkpoint = await checkpointRepository.FindByProjectAsync(new ProjectId(project.ProjectId));

        Assert.NotNull(checkpoint);
        Assert.Equal(new ProjectId(project.ProjectId), checkpoint!.ProjectId);
        Assert.Equal(commandMetadata.CommandId.Value, checkpoint.LastCommandId!.Value.Value);
        Assert.False(checkpoint.IsClean);
    }

    [Fact]
    public async Task CrashRecovery_ReopensFromLatestCheckpoint()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        await using var provider = BuildProvider(fixture.DatabasePath, new FixedClock(new DateTimeOffset(2026, 4, 18, 16, 0, 0, TimeSpan.Zero)));
        ProjectId projectId;
        PersistedProjectState postAutosaveState;

        await using (var scope = provider.CreateAsyncScope())
        {
            var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
            var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();
            var runService = scope.ServiceProvider.GetRequiredService<IRunService>();
            var currentState = scope.ServiceProvider.GetRequiredService<CurrentWorkingRevisionSource>();

            var project = await projectService.CreateProjectAsync("Kitchen Shop");
            projectId = new ProjectId(project.ProjectId);
            var room = await roomService.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
            var wall = await roomService.AddWallAsync(room.Id, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)), default);
            var run = await runService.CreateRunAsync(room.Id, wall.Id, Length.FromInches(12m), Length.FromInches(96m));
            await runService.PlaceCabinetAsync(run.Id, "base-standard-24");
            await CommitCurrentStateAsync(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IClock>().Now, "lifecycle.crash", room.Id.Value.ToString(), wall.Id.Value.ToString(), run.Id.Value.ToString());

            postAutosaveState = currentState.CaptureCurrentState();
            Assert.Single(postAutosaveState.WorkingRevision.Cabinets);
        }

        await using var restartedProvider = BuildProvider(fixture.DatabasePath, new FixedClock(new DateTimeOffset(2026, 4, 18, 16, 30, 0, TimeSpan.Zero)));
        await using var restartedScope = restartedProvider.CreateAsyncScope();
        var orchestrator = restartedScope.ServiceProvider.GetRequiredService<StartupOrchestrator>();
        var latestCheckpoint = await orchestrator.FindLatestAutosaveCheckpointAsync(projectId);
        var workingRevisionRepository = restartedScope.ServiceProvider.GetRequiredService<IWorkingRevisionRepository>();
        var recovered = await workingRevisionRepository.LoadAsync(projectId);

        Assert.NotNull(latestCheckpoint);
        Assert.Equal(postAutosaveState.Checkpoint!.RevisionId, latestCheckpoint!.RevisionId);
        Assert.False(latestCheckpoint.IsClean);
        Assert.NotNull(recovered);
        Assert.Single(recovered!.Rooms);
        Assert.Single(recovered.Walls);
        Assert.Single(recovered.Runs);
        Assert.Single(recovered.Cabinets);
        Assert.Equal(postAutosaveState.WorkingRevision.Cabinets[0].NominalWidth, recovered.Cabinets[0].NominalWidth);
        Assert.Equal(postAutosaveState.WorkingRevision.Walls[0].StartPoint, recovered.Walls[0].StartPoint);
        Assert.Equal(postAutosaveState.WorkingRevision.Walls[0].EndPoint, recovered.Walls[0].EndPoint);
    }

    [Fact]
    public async Task ConcurrentSaves_SerializedCorrectly()
    {
        await using var fixture = new SqliteTestFixture();
        await fixture.InitializeAsync();

        await using var provider = BuildProvider(fixture.DatabasePath, new FixedClock(new DateTimeOffset(2026, 4, 18, 17, 0, 0, TimeSpan.Zero)));
        await using var scope = provider.CreateAsyncScope();

        var projectService = scope.ServiceProvider.GetRequiredService<IProjectService>();
        var roomService = scope.ServiceProvider.GetRequiredService<IRoomService>();
        var runService = scope.ServiceProvider.GetRequiredService<IRunService>();
        var currentClock = scope.ServiceProvider.GetRequiredService<IClock>();
        var project = await projectService.CreateProjectAsync("Kitchen Shop");
        var room = await roomService.CreateRoomAsync("Kitchen", Length.FromInches(96m), default);
        var wall = await roomService.AddWallAsync(room.Id, Point2D.Origin, new Point2D(120m, 0m), Thickness.Exact(Length.FromInches(4m)), default);
        var run = await runService.CreateRunAsync(room.Id, wall.Id, Length.FromInches(12m), Length.FromInches(96m));
        await runService.PlaceCabinetAsync(run.Id, "base-standard-24");
        await CommitCurrentStateAsync(scope.ServiceProvider, currentClock.Now, "lifecycle.concurrent", room.Id.Value.ToString(), wall.Id.Value.ToString(), run.Id.Value.ToString());

        var save1 = projectService.SaveAsync();
        var save2 = projectService.SaveAsync();
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(save1, save2));

        Assert.Null(exception);

        var checkpointRepository = scope.ServiceProvider.GetRequiredService<IAutosaveCheckpointRepository>();
        var checkpoint = await checkpointRepository.FindByProjectAsync(new ProjectId(project.ProjectId));

        Assert.NotNull(checkpoint);
        Assert.True(checkpoint!.IsClean);
    }

    private static ServiceProvider BuildProvider(string filePath, IClock clock)
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddPersistence(filePath);
        services.AddSingleton(clock);
        services.AddSingleton<IAppLogger, NullAppLogger>();
        return services.BuildServiceProvider();
    }

    private static async Task<string> CaptureWorkingStateSignatureAsync(IDbConnectionFactory connectionFactory, Guid projectId)
    {
        await using var connection = (Microsoft.Data.Sqlite.SqliteConnection)await connectionFactory.OpenConnectionAsync().ConfigureAwait(false);
        var rows = new Dictionary<string, IReadOnlyList<Dictionary<string, object?>>>(StringComparer.Ordinal)
        {
            ["projects"] = await ReadRowsAsync(connection, """
                SELECT id, name, description, created_at, updated_at, current_state, file_path
                FROM projects
                WHERE id = @projectId
                ORDER BY id;
                """, projectId),
            ["revisions"] = await ReadRowsAsync(connection, """
                SELECT id, project_id, revision_number, state, created_at, approved_at, approved_by, label, approval_notes
                FROM revisions
                WHERE project_id = @projectId
                ORDER BY revision_number;
                """, projectId),
            ["rooms"] = await ReadRowsAsync(connection, """
                SELECT id, revision_id, name, shape_json, created_at, updated_at
                FROM rooms
                WHERE revision_id IN (SELECT id FROM revisions WHERE project_id = @projectId)
                ORDER BY id;
                """, projectId),
            ["walls"] = await ReadRowsAsync(connection, """
                SELECT id, revision_id, room_id, start_point, end_point, thickness, created_at, updated_at
                FROM walls
                WHERE revision_id IN (SELECT id FROM revisions WHERE project_id = @projectId)
                ORDER BY id;
                """, projectId),
            ["runs"] = await ReadRowsAsync(connection, """
                SELECT id, revision_id, wall_id, run_index, start_offset, end_offset, end_condition_start, end_condition_end, created_at, updated_at
                FROM runs
                WHERE revision_id IN (SELECT id FROM revisions WHERE project_id = @projectId)
                ORDER BY id;
                """, projectId),
            ["cabinets"] = await ReadRowsAsync(connection, """
                SELECT id, revision_id, run_id, slot_index, cabinet_type_id, category, construction_method, nominal_width, nominal_height, nominal_depth, overrides_json, created_at, updated_at
                FROM cabinets
                WHERE revision_id IN (SELECT id FROM revisions WHERE project_id = @projectId)
                ORDER BY id;
                """, projectId),
            ["parts"] = await ReadRowsAsync(connection, """
                SELECT id, revision_id, cabinet_id, part_type, label, material_id, length, width, thickness, grain_direction, edge_treatment_json, created_at, updated_at
                FROM parts
                WHERE revision_id IN (SELECT id FROM revisions WHERE project_id = @projectId)
                ORDER BY id;
                """, projectId)
        };

        return JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = false });
    }

    private static async Task<IReadOnlyList<Dictionary<string, object?>>> ReadRowsAsync(System.Data.IDbConnection connection, string sql, Guid projectId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@projectId";
        parameter.Value = projectId.ToString();
        command.Parameters.Add(parameter);

        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await ((Microsoft.Data.Sqlite.SqliteCommand)command).ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static async Task<CommandMetadata> CommitCurrentStateAsync(IServiceProvider serviceProvider, DateTimeOffset timestamp, string intentDescription, params string[] affectedEntityIds)
    {
        var persistencePort = serviceProvider.GetRequiredService<ICommandPersistencePort>();
        var command = new TestDesignCommand(
            intentDescription,
            CommandMetadata.Create(timestamp, CommandOrigin.User, intentDescription, affectedEntityIds));
        await persistencePort.CommitCommandAsync(command, CommandResult.Succeeded(command.Metadata, [], []));
        return command.Metadata;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed record TestDesignCommand(string IntentDescription, CommandMetadata Metadata) : IDesignCommand
    {
        public string CommandType => "integration.lifecycle";

        public IReadOnlyList<ValidationIssue> ValidateStructure() => [];
    }

    private sealed class NullAppLogger : IAppLogger
    {
        public void Log(LogEntry entry)
        {
        }
    }
}
