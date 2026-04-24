using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CabinetDesigner.Application;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Persistence;
using CabinetDesigner.Persistence.Migrations;
using CabinetDesigner.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace CabinetDesigner.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;
    private IServiceScope? _appScope;
    private bool _unhandledExceptionHandlersRegistered;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider(validateScopes: true);
            _appScope = _serviceProvider.CreateScope();
            RegisterUnhandledExceptionHandlers();

            var orchestrator = _appScope.ServiceProvider.GetRequiredService<StartupOrchestrator>();
            await Task.Run(() => orchestrator.RunAsync()).ConfigureAwait(true);

            var window = _appScope.ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            // Log through a bare logger since the DI container may not be available.
            try
            {
                new TextFileAppLogger().Log(new LogEntry
                {
                    Level = LogLevel.Fatal,
                    Category = "App",
                    Message = "Unhandled exception during startup.",
                    Timestamp = DateTimeOffset.UtcNow,
                    Exception = exception
                });
            }
            catch
            {
                // Best-effort; logging must not mask the real failure path.
            }

            MessageBox.Show(
                "Carpenter Studio could not start due to an unexpected error.\n\nPlease check the application log for details.",
                "Carpenter Studio Startup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        UnregisterUnhandledExceptionHandlers();
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
        services.AddScoped<MainWindow>();
    }

    private static string GetDatabasePath()
    {
        var appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CarpenterStudio");
        Directory.CreateDirectory(appDirectory);
        return Path.Combine(appDirectory, "carpenter-studio.db");
    }

    private void RegisterUnhandledExceptionHandlers()
    {
        if (_unhandledExceptionHandlersRegistered)
        {
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        _unhandledExceptionHandlersRegistered = true;
    }

    private void UnregisterUnhandledExceptionHandlers()
    {
        if (!_unhandledExceptionHandlersRegistered)
        {
            return;
        }

        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        _unhandledExceptionHandlersRegistered = false;
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ReportUnhandledException(e.Exception);

        if (UserActionErrorReporter.IsFatal(e.Exception))
        {
            return;
        }

        e.Handled = true;
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ReportUnhandledException(e.Exception);

        if (!UserActionErrorReporter.IsFatal(e.Exception))
        {
            e.SetObserved();
        }
    }

    private void ReportUnhandledException(Exception exception)
    {
        var logger = _appScope?.ServiceProvider.GetService<IAppLogger>() ?? new TextFileAppLogger();
        var eventBus = _appScope?.ServiceProvider.GetService<IApplicationEventBus>();

        if (eventBus is null)
        {
            logger.Log(new LogEntry
            {
                Level = LogLevel.Error,
                Category = "App",
                Message = "Unhandled exception in application.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = exception
            });
            return;
        }

        UserActionErrorReporter.Report(
            logger,
            eventBus,
            "App",
            "app.unhandled",
            "Unhandled exception in application.",
            exception);
    }
}
