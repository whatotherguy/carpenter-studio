using CabinetDesigner.Application.Explanation;
using CabinetDesigner.Application.Costing;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Pipeline;
using CabinetDesigner.Application.Projection;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetDesigner.Application;

public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAppLogger, TextFileAppLogger>();
        services.AddSingleton<IApplicationEventBus, ApplicationEventBus>();
        services.AddSingleton<InMemoryDesignStateStore>();
        services.AddSingleton<IDesignStateStore>(provider => provider.GetRequiredService<InMemoryDesignStateStore>());
        services.AddSingleton<IStateManager>(provider => provider.GetRequiredService<InMemoryDesignStateStore>());
        services.AddSingleton<CurrentWorkingRevisionSource>();
        services.AddSingleton<IWorkingRevisionSource>(provider => provider.GetRequiredService<CurrentWorkingRevisionSource>());
        services.AddSingleton<ICurrentPersistedProjectState>(provider => provider.GetRequiredService<CurrentWorkingRevisionSource>());
        services.AddSingleton<IDeltaTracker, InMemoryDeltaTracker>();
        services.AddSingleton<IUndoStack, InMemoryUndoStack>();
        services.AddSingleton<IWhyEngine, WhyEngine>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IResolutionOrchestratorLogger, ResolutionOrchestratorLogger>();
        services.AddSingleton<IManufacturingProjector, ManufacturingProjector>();
        services.AddSingleton<IInstallProjector, InstallProjector>();
        services.AddSingleton<IValidationResultStore, InMemoryValidationResultStore>();
        services.AddSingleton<IPackagingResultStore, InMemoryPackagingResultStore>();
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<ICostingPolicy, DefaultCostingPolicy>();
        services.AddSingleton<IPreviousApprovedCostLookup>(provider =>
        {
            var snapshots = provider.GetService<Persistence.ISnapshotRepository>();
            if (snapshots is null)
            {
                return new NullPreviousApprovedCostLookup();
            }

            return new SnapshotApprovedCostLookup(
                snapshots,
                provider.GetRequiredService<Persistence.ICurrentPersistedProjectState>());
        });
        services.AddSingleton<IResolutionOrchestrator>(provider => new ResolutionOrchestrator(
            provider.GetRequiredService<IDeltaTracker>(),
            provider.GetRequiredService<IWhyEngine>(),
            provider.GetRequiredService<IUndoStack>(),
            provider.GetRequiredService<IStateManager>(),
            stateStore: provider.GetRequiredService<IDesignStateStore>(),
            logger: provider.GetRequiredService<IResolutionOrchestratorLogger>(),
            stages: null,
            validationResultStore: provider.GetRequiredService<IValidationResultStore>(),
            packagingResultStore: provider.GetRequiredService<IPackagingResultStore>(),
            catalogService: provider.GetRequiredService<ICatalogService>(),
            costingPolicy: provider.GetRequiredService<ICostingPolicy>(),
            currentPersistedProjectState: provider.GetRequiredService<ICurrentPersistedProjectState>(),
            previousCostLookup: provider.GetRequiredService<IPreviousApprovedCostLookup>()));

        services.AddScoped<IDesignCommandHandler, DesignCommandHandler>();
        services.AddScoped<IPreviewCommandHandler, PreviewCommandHandler>();
        services.AddScoped<IEditorCommandHandler, EditorCommandHandler>();

        services.AddScoped<IRunService, RunService>();
        services.AddSingleton<IRunSummaryService, RunSummaryService>();
        services.AddScoped<IUndoRedoService, UndoRedoService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ISnapshotService, SnapshotService>();
        services.AddSingleton<IValidationSummaryService, ValidationSummaryService>();

        return services;
    }
}
