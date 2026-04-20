using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Persistence;
using CabinetDesigner.Application.Services;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class RunSummaryPanelViewModel : ObservableObject, IDisposable
{
    private const string SelectionPromptText = "Select a cabinet to see selection context.";
    private const string NoProjectText = "Open a project to see the run summary.";
    private const string NoRunsText = "No runs in design.";
    private const string LiveSourceLabel = "Live run summary";
    private const string NoProjectSourceLabel = "No project open";
    private const string NoRunsSourceLabel = "No runs in design";

    private readonly IRunSummaryService _runSummaryService;
    private readonly ICurrentPersistedProjectState _currentProjectState;
    private readonly IApplicationEventBus _eventBus;
    private IReadOnlyList<Guid> _selectedCabinetIds = [];
    private IReadOnlyList<RunSlotViewModel> _slots = [];
    private string _activeRunDisplay = "No active run selected";
    private string _totalWidthDisplay = "-";
    private string _cabinetCountDisplay = "-";
    private string _capacityStatusDisplay = "-";
    private string _slotCountDisplay = "0 slots";
    private string _selectionSummaryDisplay = "0 selected";
    private string _statusMessage = NoProjectText;
    private string _sourceLabel = NoProjectSourceLabel;
    private string _emptyStateText = NoProjectText;
    private bool _hasActiveRun;
    private bool _hasSelection;
    private bool _isProjectOpen;
    private RunSummaryDto? _activeRunSummary;

    public RunSummaryPanelViewModel(
        IRunSummaryService runSummaryService,
        ICurrentPersistedProjectState currentProjectState,
        IApplicationEventBus eventBus)
    {
        _runSummaryService = runSummaryService ?? throw new ArgumentNullException(nameof(runSummaryService));
        _currentProjectState = currentProjectState ?? throw new ArgumentNullException(nameof(currentProjectState));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Subscribe<RedoAppliedEvent>(OnDesignChanged);

        RefreshState(resetSelection: true);
    }

    public bool IsProjectOpen
    {
        get => _isProjectOpen;
        private set => SetProperty(ref _isProjectOpen, value);
    }

    public bool HasActiveRun
    {
        get => _hasActiveRun;
        private set => SetProperty(ref _hasActiveRun, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    public string ActiveRunDisplay
    {
        get => _activeRunDisplay;
        private set => SetProperty(ref _activeRunDisplay, value);
    }

    public string TotalWidthDisplay
    {
        get => _totalWidthDisplay;
        private set => SetProperty(ref _totalWidthDisplay, value);
    }

    public string CabinetCountDisplay
    {
        get => _cabinetCountDisplay;
        private set => SetProperty(ref _cabinetCountDisplay, value);
    }

    public string CapacityStatusDisplay
    {
        get => _capacityStatusDisplay;
        private set => SetProperty(ref _capacityStatusDisplay, value);
    }

    public string SlotCountDisplay
    {
        get => _slotCountDisplay;
        private set => SetProperty(ref _slotCountDisplay, value);
    }

    public string SelectionSummaryDisplay
    {
        get => _selectionSummaryDisplay;
        private set => SetProperty(ref _selectionSummaryDisplay, value);
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

    public string EmptyStateText
    {
        get => _emptyStateText;
        private set => SetProperty(ref _emptyStateText, value);
    }

    public IReadOnlyList<RunSlotViewModel> Slots
    {
        get => _slots;
        private set => SetProperty(ref _slots, value);
    }

    public string PanelSummary => HasActiveRun
        ? $"{ActiveRunDisplay} - {SlotCountDisplay}"
        : (IsProjectOpen ? NoRunsText : NoProjectText);

    public void OnSelectionChanged(IReadOnlyList<Guid> selectedCabinetIds)
    {
        _selectedCabinetIds = selectedCabinetIds?.ToArray() ?? [];
        RefreshState(resetSelection: false);
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnDesignChanged);
    }

    private void OnProjectOpened(ProjectOpenedEvent _) =>
        DispatchIfNeeded(() => RefreshState(resetSelection: true));

    private void OnProjectClosed(ProjectClosedEvent _) =>
        DispatchIfNeeded(() => RefreshState(resetSelection: true));

    private void OnDesignChanged<TEvent>(TEvent _) where TEvent : IApplicationEvent =>
        DispatchIfNeeded(() => RefreshState(resetSelection: false));

    private void RefreshState(bool resetSelection = false)
    {
        if (resetSelection)
        {
            _selectedCabinetIds = [];
            HasSelection = false;
            SelectionSummaryDisplay = "0 selected";
        }
        else
        {
            HasSelection = _selectedCabinetIds.Count > 0;
            SelectionSummaryDisplay = HasSelection
                ? $"{_selectedCabinetIds.Count} selected"
                : "0 selected";
        }

        var projection = _runSummaryService.GetCurrentSummary(_selectedCabinetIds);
        IsProjectOpen = projection.IsProjectOpen;
        if (!IsProjectOpen)
        {
            HasSelection = false;
            SelectionSummaryDisplay = "0 selected";
        }
        _activeRunSummary = projection.ActiveRunSummary;

        HasActiveRun = _activeRunSummary is not null;
        ActiveRunDisplay = HasActiveRun ? "Active run" : "No active run selected";
        TotalWidthDisplay = HasActiveRun ? FormatInches(_activeRunSummary!.TotalNominalWidthInches) : "-";
        CabinetCountDisplay = HasActiveRun ? FormatCabinetCount(_activeRunSummary!.CabinetCount) : "-";
        CapacityStatusDisplay = HasActiveRun
            ? (_activeRunSummary!.IsOverCapacity
                ? $"Over capacity by {FormatInches(_activeRunSummary!.OverCapacityAmountInches)}"
                : $"{FormatInches(_activeRunSummary!.RemainingLengthInches)} remaining")
            : "-";
        SlotCountDisplay = HasActiveRun ? FormatSlotCount(_activeRunSummary!.Slots.Count) : "0 slots";
        Slots = HasActiveRun
            ? _activeRunSummary!.Slots
                .Select(slot => new RunSlotViewModel(
                    slot.CabinetId,
                    slot.CabinetTypeId,
                    FormatInches(slot.NominalWidthInches),
                    slot.Index,
                    _selectedCabinetIds.Contains(slot.CabinetId)))
                .ToArray()
            : [];

        SourceLabel = HasActiveRun
            ? LiveSourceLabel
            : (IsProjectOpen ? NoRunsSourceLabel : NoProjectSourceLabel);
        EmptyStateText = HasActiveRun
            ? SelectionPromptText
            : (IsProjectOpen ? NoRunsText : NoProjectText);
        StatusMessage = HasActiveRun
            ? (HasSelection ? "Showing the run for the selected cabinet." : "Showing the active run.")
            : (IsProjectOpen ? NoRunsText : NoProjectText);

        OnPropertyChanged(nameof(PanelSummary));
    }

    private static string FormatInches(decimal inches) => $"{inches:0.##}\"";

    private static string FormatCabinetCount(int count) => count == 1 ? "1 cabinet" : $"{count} cabinets";

    private static string FormatSlotCount(int count) => count == 1 ? "1 slot" : $"{count} slots";

    private static void DispatchIfNeeded(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.Invoke(action);
    }
}
