using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class StatusBarViewModel : ObservableObject, IDisposable
{
    private readonly IApplicationEventBus _eventBus;
    private readonly IValidationSummaryService _validationSummaryService;
    private readonly IAppLogger? _logger;
    private string _revisionLabel = "No revision";
    private bool _hasUnsavedChanges;
    private string _statusMessage = "Ready";
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;
    private bool _hasManufactureBlockers;

    public StatusBarViewModel(
        IApplicationEventBus eventBus,
        IValidationSummaryService validationSummaryService,
        IAppLogger? logger = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _validationSummaryService = validationSummaryService ?? throw new ArgumentNullException(nameof(validationSummaryService));
        _logger = logger;

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Subscribe<RedoAppliedEvent>(OnDesignChanged);

        RefreshValidationCounts();
    }

    public int ErrorCount
    {
        get => _errorCount;
        private set => SetProperty(ref _errorCount, value);
    }

    public int WarningCount
    {
        get => _warningCount;
        private set => SetProperty(ref _warningCount, value);
    }

    public int InfoCount
    {
        get => _infoCount;
        private set => SetProperty(ref _infoCount, value);
    }

    public bool HasManufactureBlockers
    {
        get => _hasManufactureBlockers;
        private set => SetProperty(ref _hasManufactureBlockers, value);
    }

    public string RevisionLabel
    {
        get => _revisionLabel;
        private set => SetProperty(ref _revisionLabel, value);
    }

    public bool HasUnsavedChanges
    {
        get => _hasUnsavedChanges;
        private set
        {
            if (SetProperty(ref _hasUnsavedChanges, value))
            {
                OnPropertyChanged(nameof(SaveStateDisplay));
            }
        }
    }

    public string SaveStateDisplay => HasUnsavedChanges ? "Unsaved changes" : "Saved";

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string IssueSummaryDisplay => $"{ErrorCount}E {WarningCount}W {InfoCount}I";

    public string RevisionSummaryDisplay => $"Revision: {RevisionLabel}";

    public string SaveSummaryDisplay => SaveStateDisplay;

    public string ProjectSummaryDisplay => HasUnsavedChanges ? $"{RevisionLabel} - Unsaved" : RevisionLabel;

    public void SetProjectSummary(ProjectSummaryDto? project)
    {
        if (project is null)
        {
            RevisionLabel = "No revision";
            HasUnsavedChanges = false;
            OnPropertyChanged(nameof(RevisionSummaryDisplay));
            OnPropertyChanged(nameof(SaveSummaryDisplay));
            OnPropertyChanged(nameof(ProjectSummaryDisplay));
            return;
        }

        RevisionLabel = project.CurrentRevisionLabel;
        HasUnsavedChanges = project.HasUnsavedChanges;
        OnPropertyChanged(nameof(RevisionSummaryDisplay));
        OnPropertyChanged(nameof(SaveSummaryDisplay));
        OnPropertyChanged(nameof(ProjectSummaryDisplay));
    }

    public void SetStatusMessage(string statusMessage)
    {
        StatusMessage = string.IsNullOrWhiteSpace(statusMessage) ? "Ready" : statusMessage;
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnDesignChanged);
    }

    private void OnProjectOpened(ProjectOpenedEvent @event)
    {
        SetProjectSummary(@event.Project);
        RefreshValidationCounts();
    }

    private void OnProjectClosed(ProjectClosedEvent _)
    {
        SetProjectSummary(null);
        ErrorCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        HasManufactureBlockers = false;
        OnPropertyChanged(nameof(IssueSummaryDisplay));
    }

    private void OnDesignChanged<TEvent>(TEvent _) where TEvent : IApplicationEvent
    {
        RefreshValidationCounts();
    }

    private void RefreshValidationCounts()
    {
        try
        {
            var issues = _validationSummaryService.GetAllIssues();
            ErrorCount = issues.Count(issue => string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));
            WarningCount = issues.Count(issue => string.Equals(issue.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
            InfoCount = issues.Count(issue => string.Equals(issue.Severity, "Info", StringComparison.OrdinalIgnoreCase));
            HasManufactureBlockers = _validationSummaryService.HasManufactureBlockers;
        }
        catch (NotImplementedException notImplemented)
        {
            _logger?.Log(new LogEntry
            {
                Level = LogLevel.Warning,
                Category = "StatusBarViewModel",
                Message = "Validation summary service is not yet implemented; issue counts will be suppressed.",
                Timestamp = DateTimeOffset.UtcNow,
                Exception = notImplemented
            });
            ErrorCount = 0;
            WarningCount = 0;
            InfoCount = 0;
            HasManufactureBlockers = false;
        }

        OnPropertyChanged(nameof(IssueSummaryDisplay));
    }
}
