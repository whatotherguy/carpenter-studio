#if WINDOWS
using System.ComponentModel;
using System.Windows;
using CabinetDesigner.Presentation.ViewModels;

namespace CabinetDesigner.Presentation;

public partial class MainWindow : Window
{
    private readonly ShellViewModel _viewModel;
    private bool _closeAfterSavePending;

    public MainWindow(ShellViewModel viewModel)
    {
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
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
            MessageBox.Show(
                exception.ToString(),
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
