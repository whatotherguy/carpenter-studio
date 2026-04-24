using System.ComponentModel;
using System.Threading;
using System.Windows.Input;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.Events;

namespace CabinetDesigner.Presentation.Commands;

public sealed class AsyncRelayCommand : ICommand, INotifyPropertyChanged
{
    private readonly Func<Task> _executeAsync;
    private readonly string _commandName;
    private readonly Func<bool>? _canExecute;
    private readonly IAppLogger _logger;
    private readonly IApplicationEventBus _eventBus;
    private readonly SynchronizationContext? _synchronizationContext;
    private bool _isExecuting;

    public AsyncRelayCommand(
        Func<Task> executeAsync,
        string commandName,
        IAppLogger logger,
        IApplicationEventBus eventBus,
        Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        ArgumentException.ThrowIfNullOrWhiteSpace(commandName);
        _commandName = commandName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _canExecute = canExecute;
        _synchronizationContext = SynchronizationContext.Current;
    }

    public event EventHandler? CanExecuteChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsExecuting => _isExecuting;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter) => await ExecuteAsync().ConfigureAwait(false);

    public async Task ExecuteAsync()
    {
        if (!CanExecute(null))
        {
            return;
        }

        _isExecuting = true;
        PostToUiThread(() =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
            NotifyCanExecuteChanged();
        });

        try
        {
            await _executeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            UserActionErrorReporter.Report(
                _logger,
                _eventBus,
                "Presentation",
                _commandName,
                "Async command execution failed.",
                ex);
        }
        finally
        {
            _isExecuting = false;
            PostToUiThread(() =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExecuting)));
                NotifyCanExecuteChanged();
            });
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);

    private void PostToUiThread(Action action)
    {
        if (_synchronizationContext is not null && SynchronizationContext.Current != _synchronizationContext)
        {
            _synchronizationContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }
}
