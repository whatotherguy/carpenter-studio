using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Presentation.ViewModels;

namespace CabinetDesigner.App;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private readonly IAppLogger? _logger;
    private bool _closeAfterSavePending;

    public MainWindow(ShellViewModel viewModel, IAppLogger? logger = null)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _logger = logger;
        InitializeComponent();
        DataContext = _viewModel;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
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

    private async Task CloseAfterSaveAsync()
    {
        try
        {
            await _viewModel.SaveCommand.ExecuteAsync().ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Error,
                Category = "MainWindow",
                Message = "Save failed during close-after-save flow.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = exception
            });
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
