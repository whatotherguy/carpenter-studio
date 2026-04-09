using Microsoft.Data.Sqlite;
using CabinetDesigner.Persistence.Mapping;
using CabinetDesigner.Persistence.Models;
using CabinetDesigner.Persistence.UnitOfWork;

namespace CabinetDesigner.Persistence.Repositories;

internal sealed class ProjectRepository : SqliteRepositoryBase, IProjectRepository
{
    public ProjectRepository(IDbConnectionFactory connectionFactory, SqliteSessionAccessor sessionAccessor)
        : base(connectionFactory, sessionAccessor)
    {
    }

    public Task<ProjectRecord?> FindAsync(ProjectId id, CancellationToken ct = default) =>
        WithConnectionAsync<ProjectRecord?>(
            async (connection, transaction) =>
            {
                using var command = CreateCommand(connection, transaction, """
                    SELECT id, name, description, created_at, updated_at, current_state, file_path
                    FROM projects
                    WHERE id = @id;
                    """);
                command.Parameters.AddWithValue("@id", id.Value.ToString());
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                if (!await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    return null;
                }

                return ProjectMapper.ToRecord(new ProjectRow
                {
                    Id = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                    CreatedAt = reader.GetString(3),
                    UpdatedAt = reader.GetString(4),
                    CurrentState = reader.GetString(5),
                    FilePath = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            },
            ct);

    public Task SaveAsync(ProjectRecord project, CancellationToken ct = default) =>
        WithConnectionAsync(
            async (connection, transaction) =>
            {
                var row = ProjectMapper.ToRow(project);
                using var command = CreateCommand(connection, transaction, """
                    INSERT INTO projects(id, name, description, created_at, updated_at, current_state, file_path)
                    VALUES(@id, @name, @description, @createdAt, @updatedAt, @currentState, @filePath)
                    ON CONFLICT(id) DO UPDATE SET
                        name = excluded.name,
                        description = excluded.description,
                        updated_at = excluded.updated_at,
                        current_state = excluded.current_state,
                        file_path = excluded.file_path;
                    """);
                command.Parameters.AddWithValue("@id", row.Id);
                command.Parameters.AddWithValue("@name", row.Name);
                command.Parameters.AddWithValue("@description", (object?)row.Description ?? DBNull.Value);
                command.Parameters.AddWithValue("@createdAt", row.CreatedAt);
                command.Parameters.AddWithValue("@updatedAt", row.UpdatedAt);
                command.Parameters.AddWithValue("@currentState", row.CurrentState);
                command.Parameters.AddWithValue("@filePath", (object?)row.FilePath ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            },
            ct);

    public Task<IReadOnlyList<ProjectRecord>> ListRecentAsync(int limit, CancellationToken ct = default) =>
        WithConnectionAsync<IReadOnlyList<ProjectRecord>>(
            async (connection, transaction) =>
            {
                using var command = CreateCommand(connection, transaction, """
                    SELECT id, name, description, created_at, updated_at, current_state, file_path
                    FROM projects
                    ORDER BY updated_at DESC
                    LIMIT @limit;
                    """);
                command.Parameters.AddWithValue("@limit", limit);
                var results = new List<ProjectRecord>();
                await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
                while (await reader.ReadAsync(ct).ConfigureAwait(false))
                {
                    results.Add(ProjectMapper.ToRecord(new ProjectRow
                    {
                        Id = reader.GetString(0),
                        Name = reader.GetString(1),
                        Description = reader.IsDBNull(2) ? null : reader.GetString(2),
                        CreatedAt = reader.GetString(3),
                        UpdatedAt = reader.GetString(4),
                        CurrentState = reader.GetString(5),
                        FilePath = reader.IsDBNull(6) ? null : reader.GetString(6)
                    }));
                }

                return results;
            },
            ct);
}
