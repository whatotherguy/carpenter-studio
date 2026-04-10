using CabinetDesigner.Persistence.Migrations;
using CabinetDesigner.Persistence.Repositories;
using CabinetDesigner.Persistence.Snapshots;
using CabinetDesigner.Persistence.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetDesigner.Persistence;

public static class PersistenceServiceRegistration
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string filePath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        services.AddSingleton<IDbConnectionFactory>(_ => new SqliteConnectionFactory(filePath));
        services.AddSingleton<SqliteSessionAccessor>();
        services.AddSingleton<ISchemaMigration, V1_InitialSchema>();
        services.AddSingleton<ISchemaMigration, V2_RepairSchemaDrift>();
        services.AddSingleton<MigrationRunner>();
        services.AddSingleton<StartupOrchestrator>();
        services.AddSingleton<ISnapshotSerializer, V1SnapshotSerializer>();
        services.AddSingleton<ISnapshotDeserializer, V1SnapshotDeserializer>();
        services.AddSingleton<SnapshotBlobReader>();

        services.AddScoped<IUnitOfWork, SqliteUnitOfWork>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IRevisionRepository, RevisionRepository>();
        services.AddScoped<IWorkingRevisionRepository, WorkingRevisionRepository>();
        services.AddScoped<ICommandJournalRepository, CommandJournalRepository>();
        services.AddScoped<IExplanationRepository, ExplanationRepository>();
        services.AddScoped<IValidationHistoryRepository, ValidationHistoryRepository>();
        services.AddScoped<IAutosaveCheckpointRepository, AutosaveCheckpointRepository>();
        services.AddScoped<ISnapshotRepository, SnapshotRepository>();
        services.AddScoped<ICommandPersistencePort, CommandPersistenceService>();

        return services;
    }
}
