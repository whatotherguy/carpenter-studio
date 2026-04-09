using CabinetDesigner.Application.Explanation;
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
        services.AddSingleton<IResolutionOrchestrator>(provider => new ResolutionOrchestrator(
            provider.GetRequiredService<IDeltaTracker>(),
            provider.GetRequiredService<IWhyEngine>(),
            provider.GetRequiredService<IUndoStack>(),
            provider.GetRequiredService<IStateManager>(),
            provider.GetRequiredService<IDesignStateStore>(),
            provider.GetRequiredService<IResolutionOrchestratorLogger>()));

        services.AddScoped<IDesignCommandHandler, DesignCommandHandler>();
        services.AddScoped<IPreviewCommandHandler, PreviewCommandHandler>();
        services.AddScoped<IEditorCommandHandler, EditorCommandHandler>();

        services.AddScoped<IRunService, RunService>();
        services.AddSingleton<IRunSummaryService, RunSummaryService>();
        services.AddScoped<IUndoRedoService, UndoRedoService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ISnapshotService, SnapshotService>();
        services.AddSingleton<ICatalogService, CatalogService>();

        return services;
    }
}
