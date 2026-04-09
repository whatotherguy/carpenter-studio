using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.Commands;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class IssuePanelViewModel : ObservableObject, IDisposable
{
    private readonly IValidationSummaryService _validationSummaryService;
    private readonly IApplicationEventBus _eventBus;
    private Action<IReadOnlyList<Guid>> _selectEntities = _ => { };
    private IReadOnlyList<IssueRowViewModel> _allIssues = [];
    private IReadOnlyList<IssueRowViewModel> _filteredIssues = [];
    private string? _severityFilter;
    private string _statusMessage = "Validation issues are not available yet.";
    private string _sourceLabel = "Placeholder validation data";
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;
    private bool _hasManufactureBlockers;
    private bool _hasValidationData;

    public IssuePanelViewModel(
        IValidationSummaryService validationSummaryService,
        IApplicationEventBus eventBus)
    {
        _validationSummaryService = validationSummaryService ?? throw new ArgumentNullException(nameof(validationSummaryService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        GoToEntityCommand = new RelayCommand<IssueRowViewModel>(GoToEntity, CanGoToEntity);

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Subscribe<RedoAppliedEvent>(OnDesignChanged);

        RefreshIssues();
    }

    public IReadOnlyList<IssueRowViewModel> AllIssues
    {
        get => _allIssues;
        private set => SetProperty(ref _allIssues, value);
    }

    public IReadOnlyList<IssueRowViewModel> FilteredIssues
    {
        get => _filteredIssues;
        private set => SetProperty(ref _filteredIssues, value);
    }

    public string? SeverityFilter
    {
        get => _severityFilter;
        set
        {
            if (SetProperty(ref _severityFilter, NormalizeSeverityFilter(value)))
            {
                ApplyFilter();
            }
        }
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

    public bool HasValidationData
    {
        get => _hasValidationData;
        private set => SetProperty(ref _hasValidationData, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string SourceLabel
    {
        get => _sourceLabel;
        private set => SetProperty(ref _sourceLabel, value);
    }

    public string EmptyStateText => string.IsNullOrWhiteSpace(SeverityFilter)
        ? (HasValidationData ? "No validation issues." : "No validation issues are available yet.")
        : $"No issues match '{SeverityFilter}'.";

    public string CountSummaryDisplay => $"{ErrorCount}E {WarningCount}W {InfoCount}I";

    public string FilterSummaryDisplay => string.IsNullOrWhiteSpace(SeverityFilter)
        ? "All severities"
        : SeverityFilter;

    public bool HasFilteredIssues => FilteredIssues.Count > 0;

    public bool IsPlaceholderData => !HasValidationData;

    public string BlockerStateDisplay => HasManufactureBlockers ? "Yes" : "No";

    public RelayCommand<IssueRowViewModel> GoToEntityCommand { get; }

    public void SetSelectionCallback(Action<IReadOnlyList<Guid>> selectEntities)
    {
        _selectEntities = selectEntities ?? throw new ArgumentNullException(nameof(selectEntities));
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnDesignChanged);
    }

    private void OnProjectOpened(ProjectOpenedEvent _) => RefreshIssues();

    private void OnProjectClosed(ProjectClosedEvent _)
    {
        ResetToPlaceholderState("Validation issues are not available while no project is open.");
    }

    private void OnDesignChanged<TEvent>(TEvent _) where TEvent : IApplicationEvent => RefreshIssues();

    private void RefreshIssues()
    {
        try
        {
            var serviceIssues = _validationSummaryService.GetAllIssues();
            SourceLabel = "Validation service data";
            HasValidationData = true;
            AllIssues = serviceIssues.Select(MapIssue).ToArray();
            HasManufactureBlockers = _validationSummaryService.HasManufactureBlockers;
            StatusMessage = AllIssues.Count == 0
                ? "No validation issues."
                : "Validation issues loaded from the service.";
        }
        catch (NotImplementedException)
        {
            ResetToPlaceholderState("Validation issues are not available yet.");
            return;
        }

        ErrorCount = AllIssues.Count(issue => string.Equals(issue.Severity, "Error", StringComparison.OrdinalIgnoreCase));
        WarningCount = AllIssues.Count(issue => string.Equals(issue.Severity, "Warning", StringComparison.OrdinalIgnoreCase));
        InfoCount = AllIssues.Count(issue => string.Equals(issue.Severity, "Info", StringComparison.OrdinalIgnoreCase));

        ApplyFilter();
        GoToEntityCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BlockerStateDisplay));
        OnPropertyChanged(nameof(CountSummaryDisplay));
        OnPropertyChanged(nameof(FilterSummaryDisplay));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(HasFilteredIssues));
        OnPropertyChanged(nameof(IsPlaceholderData));
    }

    private void ResetToPlaceholderState(string statusMessage)
    {
        _severityFilter = null;
        SourceLabel = "Placeholder validation data";
        HasValidationData = false;
        AllIssues = [];
        FilteredIssues = [];
        ErrorCount = 0;
        WarningCount = 0;
        InfoCount = 0;
        HasManufactureBlockers = false;
        StatusMessage = statusMessage;
        GoToEntityCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(BlockerStateDisplay));
        OnPropertyChanged(nameof(CountSummaryDisplay));
        OnPropertyChanged(nameof(FilterSummaryDisplay));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(HasFilteredIssues));
        OnPropertyChanged(nameof(IsPlaceholderData));
        OnPropertyChanged(nameof(SeverityFilter));
    }

    private void ApplyFilter()
    {
        var filter = NormalizeSeverityFilter(SeverityFilter);

        FilteredIssues = string.IsNullOrWhiteSpace(filter)
            ? AllIssues
            : AllIssues.Where(issue => string.Equals(issue.Severity, filter, StringComparison.OrdinalIgnoreCase)).ToArray();

        GoToEntityCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasFilteredIssues));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(FilterSummaryDisplay));
    }

    private static string? NormalizeSeverityFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "All", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim();
    }

    private static IssueRowViewModel MapIssue(ValidationIssueSummaryDto issue) =>
        new(issue.Severity, issue.Code, issue.Message, issue.AffectedEntityIds?.ToArray() ?? []);

    private void GoToEntity(IssueRowViewModel? issue)
    {
        if (issue is null)
        {
            return;
        }

        var entityIds = issue.AffectedEntityIds
            .Select(id => Guid.TryParse(id, out var guid) ? guid : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToArray();

        if (entityIds.Length == 0)
        {
            StatusMessage = $"Issue {issue.Code} has no selectable entities.";
            return;
        }

        _selectEntities(entityIds);
        StatusMessage = $"Selected {entityIds.Length} affected entit{(entityIds.Length == 1 ? "y" : "ies")}.";
    }

    private bool CanGoToEntity(IssueRowViewModel? issue) =>
        issue is not null && issue.AffectedEntityIds.Any(id => Guid.TryParse(id, out _));
}
