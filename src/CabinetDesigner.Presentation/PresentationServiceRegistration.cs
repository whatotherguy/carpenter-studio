using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Editor;
using CabinetDesigner.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetDesigner.Presentation;

public static class PresentationServiceRegistration
{
    public static IServiceCollection AddPresentationServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<EditorSession>();
        services.AddScoped<EditorCanvas>();
        services.AddScoped<IEditorCanvasHost, EditorCanvasHost>();
        services.AddScoped<IEditorCanvasSession, EditorCanvasSessionAdapter>();
        services.AddScoped<IHitTester, DefaultHitTester>();
        services.AddScoped<ISceneProjector, SceneProjector>();
        services.AddScoped<EditorCanvasViewModel>();
        services.AddScoped<CatalogPanelViewModel>();
        services.AddScoped<PropertyInspectorViewModel>();
        services.AddScoped<RunSummaryPanelViewModel>();
        services.AddScoped<IssuePanelViewModel>();
        services.AddScoped<StatusBarViewModel>();
        services.AddScoped<ShellViewModel>();

        return services;
    }
}
