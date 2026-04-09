using System.Globalization;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Presentation.Commands;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class PropertyInspectorViewModel : ObservableObject, IDisposable
{
    private readonly IRunService _runService;
    private readonly IApplicationEventBus _eventBus;
    private IReadOnlyList<Guid> _selectedCabinetIds = [];
    private IReadOnlyList<PropertyRowViewModel> _properties = [];
    private RenderSceneDto? _scene;
    private string _selectedEntityLabel = "No cabinet selected";
    private string _selectionSummaryDisplay = "Nothing selected";
    private string _statusMessage = "Open a project to inspect properties.";
    private string _sourceLabel = "Selection idle";
    private string _editabilityStatusDisplay = "No editable properties";
    private string _nominalWidthDisplay = "-";
    private string _nominalWidthEditValue = string.Empty;
    private string? _lastErrorMessage;
    private decimal? _currentNominalWidthInches;
    private bool _hasSelection;
    private bool _hasSingleSelection;
    private bool _isProjectOpen;
    private bool _isEditingNominalWidth;
    private bool _canEditNominalWidth;

    public PropertyInspectorViewModel(
        IRunService runService,
        IApplicationEventBus eventBus)
    {
        _runService = runService ?? throw new ArgumentNullException(nameof(runService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);

        BeginNominalWidthEditCommand = new RelayCommand(BeginNominalWidthEdit, () => CanEditNominalWidth && !IsEditingNominalWidth);
        CommitNominalWidthEditCommand = new AsyncRelayCommand(CommitNominalWidthEditAsync, () => CanEditNominalWidth && IsEditingNominalWidth);
        CancelNominalWidthEditCommand = new RelayCommand(CancelNominalWidthEdit, () => IsEditingNominalWidth);

        RefreshSelectionState();
    }

    public bool IsProjectOpen
    {
        get => _isProjectOpen;
        private set => SetProperty(ref _isProjectOpen, value);
    }

    public bool HasSelection
    {
        get => _hasSelection;
        private set => SetProperty(ref _hasSelection, value);
    }

    public bool HasSingleSelection
    {
        get => _hasSingleSelection;
        private set => SetProperty(ref _hasSingleSelection, value);
    }

    public bool CanEditNominalWidth
    {
        get => _canEditNominalWidth;
        private set
        {
            if (SetProperty(ref _canEditNominalWidth, value))
            {
                BeginNominalWidthEditCommand.NotifyCanExecuteChanged();
                CommitNominalWidthEditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool IsEditingNominalWidth
    {
        get => _isEditingNominalWidth;
        private set
        {
            if (SetProperty(ref _isEditingNominalWidth, value))
            {
                BeginNominalWidthEditCommand.NotifyCanExecuteChanged();
                CommitNominalWidthEditCommand.NotifyCanExecuteChanged();
                CancelNominalWidthEditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(LastErrorMessage);

    public string? LastErrorMessage
    {
        get => _lastErrorMessage;
        private set
        {
            if (SetProperty(ref _lastErrorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public string SelectedEntityLabel
    {
        get => _selectedEntityLabel;
        private set => SetProperty(ref _selectedEntityLabel, value);
    }

    public string SelectionSummaryDisplay
    {
        get => _selectionSummaryDisplay;
        private set => SetProperty(ref _selectionSummaryDisplay, value);
    }

    public IReadOnlyList<PropertyRowViewModel> Properties
    {
        get => _properties;
        private set => SetProperty(ref _properties, value);
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

    public string EditabilityStatusDisplay
    {
        get => _editabilityStatusDisplay;
        private set => SetProperty(ref _editabilityStatusDisplay, value);
    }

    public string EmptyStateText => !IsProjectOpen
        ? "Open a project to inspect properties."
        : HasSelection
            ? HasSingleSelection
                ? "Nominal width can be resized here. Other properties remain read-only."
                : "Multiple selection is not yet expanded in the property inspector."
            : "No cabinet selected. Click a cabinet on the canvas to inspect it.";

    public string PropertySummaryDisplay => HasSingleSelection
        ? $"{Properties.Count} details"
        : "0 details";

    public string NominalWidthDisplay
    {
        get => _nominalWidthDisplay;
        private set => SetProperty(ref _nominalWidthDisplay, value);
    }

    public string NominalWidthEditValue
    {
        get => _nominalWidthEditValue;
        set
        {
            if (SetProperty(ref _nominalWidthEditValue, value))
            {
                CommitNominalWidthEditCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public RelayCommand BeginNominalWidthEditCommand { get; }

    public AsyncRelayCommand CommitNominalWidthEditCommand { get; }

    public RelayCommand CancelNominalWidthEditCommand { get; }

    public void OnSelectionChanged(IReadOnlyList<Guid> selectedCabinetIds, RenderSceneDto? scene = null)
    {
        _selectedCabinetIds = selectedCabinetIds?.ToArray() ?? [];
        _scene = scene;
        RefreshSelectionState();
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
    }

    private void OnProjectOpened(ProjectOpenedEvent _) => SetProjectOpen(true);

    private void OnProjectClosed(ProjectClosedEvent _) => SetProjectOpen(false);

    private void SetProjectOpen(bool isOpen)
    {
        IsProjectOpen = isOpen;

        if (!isOpen)
        {
            _selectedCabinetIds = [];
            _scene = null;
        }

        RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        HasSelection = _selectedCabinetIds.Count > 0;
        HasSingleSelection = _selectedCabinetIds.Count == 1;
        LastErrorMessage = null;

        if (!IsProjectOpen)
        {
            ClearReadOnlySelectionState();
            StatusMessage = "Open a project to inspect properties.";
            SourceLabel = "No project open";
            EditabilityStatusDisplay = "No editable properties";
            Properties = [];
            CanEditNominalWidth = false;
            ResetEditableWidth();
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(PropertySummaryDisplay));
            return;
        }

        if (!HasSelection)
        {
            ClearReadOnlySelectionState();
            StatusMessage = "Click a cabinet to inspect it.";
            SourceLabel = "No cabinet selected";
            EditabilityStatusDisplay = "No editable properties";
            Properties = [];
            CanEditNominalWidth = false;
            ResetEditableWidth();
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(PropertySummaryDisplay));
            return;
        }

        if (!HasSingleSelection)
        {
            ClearReadOnlySelectionState();
            SelectedEntityLabel = $"{_selectedCabinetIds.Count} cabinets selected";
            SelectionSummaryDisplay = $"{_selectedCabinetIds.Count} selected";
            SourceLabel = "Selection summary";
            EditabilityStatusDisplay = "Read-only shell";
            StatusMessage = "Multiple cabinets are selected. Width editing requires a single selection.";
            Properties =
            [
                new PropertyRowViewModel("selection-count", "Selection Count", _selectedCabinetIds.Count.ToString(), false),
                new PropertyRowViewModel("editability", "Editability", "Read-only shell", false),
                new PropertyRowViewModel("data-source", "Data Source", "Canvas selection only", false)
            ];
            CanEditNominalWidth = false;
            ResetEditableWidth();
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(PropertySummaryDisplay));
            return;
        }

        var selectedCabinetId = _selectedCabinetIds[0];
        var selectedCabinet = _scene?.Cabinets.FirstOrDefault(cabinet => cabinet.CabinetId == selectedCabinetId);

        if (selectedCabinet is null)
        {
            SelectedEntityLabel = $"Cabinet {selectedCabinetId.ToString()[..8]}";
            SelectionSummaryDisplay = "1 selected";
            SourceLabel = "Canvas selection";
            EditabilityStatusDisplay = "Read-only shell";
            StatusMessage = "Selected cabinet details will refresh when the canvas scene is available.";
            Properties =
            [
                new PropertyRowViewModel("cabinet-id", "Cabinet ID", selectedCabinetId.ToString(), false),
                new PropertyRowViewModel("editability", "Editability", "Read-only shell", false),
                new PropertyRowViewModel("data-source", "Data Source", "Selection only", false)
            ];
            CanEditNominalWidth = false;
            ResetEditableWidth();
            OnPropertyChanged(nameof(EmptyStateText));
            OnPropertyChanged(nameof(PropertySummaryDisplay));
            return;
        }

        SelectedEntityLabel = $"{selectedCabinet.TypeDisplayName} ({selectedCabinet.CabinetId.ToString()[..8]})";
        SelectionSummaryDisplay = "1 selected";
        SourceLabel = "Projected scene data";
        EditabilityStatusDisplay = "Nominal width editable";
        StatusMessage = "Showing projected cabinet details. Nominal width can be resized.";
        Properties = CreatePropertyRows(selectedCabinet, canEditWidth: true);
        CanEditNominalWidth = true;
        SetEditableWidth(selectedCabinet.WorldBounds.Width.Inches);
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(PropertySummaryDisplay));
    }

    private void BeginNominalWidthEdit()
    {
        if (!CanEditNominalWidth || _currentNominalWidthInches is null)
        {
            return;
        }

        LastErrorMessage = null;
        NominalWidthEditValue = FormatEditableWidth(_currentNominalWidthInches.Value);
        IsEditingNominalWidth = true;
        StatusMessage = "Editing cabinet width.";
    }

    private async Task CommitNominalWidthEditAsync()
    {
        if (!CanEditNominalWidth || !IsEditingNominalWidth || _currentNominalWidthInches is null || _selectedCabinetIds.Count != 1)
        {
            return;
        }

        if (!TryParseWidth(NominalWidthEditValue, out var nextWidth))
        {
            SetError("Enter a valid width in inches.");
            return;
        }

        if (nextWidth <= 0m)
        {
            SetError("Width must be positive.");
            return;
        }

        var currentWidth = _currentNominalWidthInches.Value;
        if (nextWidth == currentWidth)
        {
            IsEditingNominalWidth = false;
            StatusMessage = "Cabinet width is unchanged.";
            LastErrorMessage = null;
            return;
        }

        var result = await _runService.ResizeCabinetAsync(new ResizeCabinetRequestDto(
            _selectedCabinetIds[0],
            currentWidth,
            nextWidth)).ConfigureAwait(true);

        if (!result.Success)
        {
            SetError(result.Issues.FirstOrDefault()?.Message ?? "Cabinet width update rejected.");
            return;
        }

        SetEditableWidth(nextWidth);
        IsEditingNominalWidth = false;
        StatusMessage = "Cabinet width updated.";
        LastErrorMessage = null;
        OnPropertyChanged(nameof(EmptyStateText));
    }

    private void CancelNominalWidthEdit()
    {
        if (!IsEditingNominalWidth)
        {
            return;
        }

        IsEditingNominalWidth = false;
        LastErrorMessage = null;
        if (_currentNominalWidthInches is not null)
        {
            NominalWidthEditValue = FormatEditableWidth(_currentNominalWidthInches.Value);
        }

        StatusMessage = "Width edit canceled.";
    }

    private static bool TryParseWidth(string? value, out decimal width)
    {
        var normalized = (value ?? string.Empty).Trim().TrimEnd('"');
        return decimal.TryParse(
            normalized,
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out width);
    }

    private static string FormatEditableWidth(decimal widthInches) =>
        widthInches.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatWidthDisplay(decimal widthInches) => $"{widthInches:0.##}\"";

    private void SetEditableWidth(decimal widthInches)
    {
        _currentNominalWidthInches = widthInches;
        NominalWidthDisplay = FormatWidthDisplay(widthInches);
        NominalWidthEditValue = FormatEditableWidth(widthInches);
    }

    private void ResetEditableWidth()
    {
        _currentNominalWidthInches = null;
        NominalWidthDisplay = "-";
        NominalWidthEditValue = string.Empty;
        IsEditingNominalWidth = false;
    }

    private void ClearReadOnlySelectionState()
    {
        SelectedEntityLabel = "No cabinet selected";
        SelectionSummaryDisplay = "Nothing selected";
    }

    private void SetError(string message)
    {
        LastErrorMessage = message;
        StatusMessage = message;
    }

    private static IReadOnlyList<PropertyRowViewModel> CreatePropertyRows(CabinetRenderDto cabinet, bool canEditWidth) =>
    [
        new PropertyRowViewModel("cabinet-id", "Cabinet ID", cabinet.CabinetId.ToString(), false),
        new PropertyRowViewModel("cabinet-label", "Cabinet Label", cabinet.Label, false),
        new PropertyRowViewModel("cabinet-type", "Cabinet Type", cabinet.TypeDisplayName, false),
        new PropertyRowViewModel("cabinet-state", "Render State", cabinet.State.ToString(), false),
        new PropertyRowViewModel("editability", "Editability", canEditWidth ? "Nominal width editable" : "Read-only shell", false),
        new PropertyRowViewModel("data-source", "Data Source", "Projected scene data", false)
    ];
}
