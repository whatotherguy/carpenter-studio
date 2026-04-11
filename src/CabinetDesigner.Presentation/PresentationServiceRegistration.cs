using CabinetDesigner.Presentation.Projection;
using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Editor;
using CabinetDesigner.Editor.Snap;
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
        services.AddScoped<IEditorCanvasHost, WpfEditorCanvasHost>();
        services.AddScoped<IEditorCanvasSession, EditorCanvasSessionAdapter>();
        services.AddScoped<IHitTester, DefaultHitTester>();
        services.AddScoped<ISceneProjector, SceneProjector>();
        services.AddScoped<IEditorSceneGraph, ApplicationEditorSceneGraph>();
        services.AddScoped<IPreviewCommandExecutor, ApplicationPreviewCommandExecutor>();
        services.AddScoped<ICommitCommandExecutor, ApplicationCommitCommandExecutor>();
        services.AddScoped<ISnapResolver>(provider => new DefaultSnapResolver(
        [
            new RunEndpointSnapCandidateSource(),
            new CabinetFaceSnapCandidateSource(),
            new GridSnapCandidateSource()
        ]));
        services.AddScoped<IEditorInteractionService, EditorInteractionService>();
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
