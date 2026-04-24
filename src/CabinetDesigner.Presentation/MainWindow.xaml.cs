#if WINDOWS
using System.ComponentModel;
using System.Windows;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Presentation.ViewModels;

namespace CabinetDesigner.Presentation;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly IAppLogger? _logger;
    private readonly IApplicationEventBus? _eventBus;
    private bool _closeAfterSavePending;

    public MainWindow(ShellViewModel viewModel, IApplicationEventBus? eventBus = null, IAppLogger? logger = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _eventBus = eventBus;
        _logger = logger;
        InitializeComponent();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        DisposeViewModel();
        base.OnClosed(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closeAfterSavePending)
        {
            base.OnClosing(e);
            return;
        }

        if (_viewModel.ActiveProject?.HasUnsavedChanges == true)
        {
            var result = MessageBox.Show(
                $"'{_viewModel.ActiveProject.Name}' has unsaved changes. Save before exiting?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                e.Cancel = true;
                _ = CloseAfterSaveAsync();
                return;
            }
        }

        base.OnClosing(e);
    }

    private void DisposeViewModel()
    {
        _viewModel.Dispose();
    }

    private async Task CloseAfterSaveAsync()
    {
        try
        {
            await _viewModel.SaveCommand.ExecuteAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            if (_logger is not null && _eventBus is not null)
            {
                UserActionErrorReporter.Report(
                    _logger,
                    _eventBus,
                    "Presentation",
                    "project.close-after-save",
                    "Save failed during close-after-save flow.",
                    exception);
            }
            else
            {
                _logger?.Log(new LogEntry
                {
                    Level = LogLevel.Error,
                    Category = "MainWindow",
                    Message = "Save failed during close-after-save flow.",
                    Timestamp = DateTimeOffset.UtcNow,
                    Exception = exception
                });
            }
            MessageBox.Show(
                "The project could not be saved. Your changes may not be persisted.\n\nPlease check the application log for details.",
                "Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return;
        }

        _closeAfterSavePending = true;
        Close();
    }
}
#endif
