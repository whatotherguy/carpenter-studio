using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CabinetDesigner.Application;
using CabinetDesigner.Persistence;
using CabinetDesigner.Persistence.Migrations;
using CabinetDesigner.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetDesigner.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _appScope;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider(validateScopes: true);
            _appScope = _serviceProvider.CreateScope();

            var orchestrator = _appScope.ServiceProvider.GetRequiredService<StartupOrchestrator>();
            await Task.Run(orchestrator.RunAsync).ConfigureAwait(true);

            var window = _appScope.ServiceProvider.GetRequiredService<CabinetDesigner.Presentation.MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.ToString(),
                "Carpenter Studio Startup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _appScope?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddApplicationServices();
        services.AddPersistence(GetDatabasePath());
        services.AddPresentationServices();
        services.AddScoped<IDialogService, WpfDialogService>();
        services.AddScoped<CabinetDesigner.Presentation.MainWindow>();
    }

    private static string GetDatabasePath()
    {
        var appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CarpenterStudio");
        Directory.CreateDirectory(appDirectory);
        return Path.Combine(appDirectory, "carpenter-studio.db");
    }
}
