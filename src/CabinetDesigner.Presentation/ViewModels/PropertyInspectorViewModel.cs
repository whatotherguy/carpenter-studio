using System.Globalization;
using System.Linq;
using CabinetDesigner.Application.DTOs;
using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Services;
using CabinetDesigner.Application.Diagnostics;
using CabinetDesigner.Application.State;
using CabinetDesigner.Domain;
using CabinetDesigner.Domain.CabinetContext;
using CabinetDesigner.Domain.Geometry;
using CabinetDesigner.Presentation.Commands;
using CabinetDesigner.Rendering.DTOs;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class PropertyInspectorViewModel : ObservableObject, IDisposable
{
    private static readonly string[] MaterialOverrideKeys =
    [
        "material.Side",
        "material.Top",
        "material.Bottom",
        "material.Back",
        "material.Shelf",
        "material.FrameStile",
        "material.FrameRail",
        "material.FrameMullion"
    ];

    private static readonly string[] ThicknessOverrideKeys =
    [
        "thickness.Side",
        "thickness.Top",
        "thickness.Bottom",
        "thickness.Back",
        "thickness.Shelf",
        "thickness.FrameStile",
        "thickness.FrameRail",
        "thickness.FrameMullion"
    ];

    private readonly ICabinetPropertyService _cabinetService;
    private readonly IApplicationEventBus _eventBus;
    private readonly ICatalogService _catalogService;
    private readonly Action<string>? _statusSink;
    private IReadOnlyList<Guid> _selectedCabinetIds = [];
    private IReadOnlyList<CabinetStateRecord> _selectedCabinets = [];
    private IReadOnlyList<PropertyRowViewModel> _properties = [];
    private IReadOnlyList<CabinetOpeningRowViewModel> _openings = [];
    private IReadOnlyList<CabinetOverrideRowViewModel> _materialOverrides = [];
    private IReadOnlyList<CabinetOverrideRowViewModel> _thicknessOverrides = [];
    private object? _lastSelectionContext;
    private string _selectedEntityLabel = "No cabinet selected";
    private string _selectionSummaryDisplay = "Nothing selected";
    private string _sourceLabel = "Selection idle";
    private string _editabilityStatusDisplay = "No editable properties";
    private string _statusMessage = "No project is open, so there are no cabinet properties to inspect yet. Open or create a project to populate this panel.";
    private string _lastErrorMessage = string.Empty;
    private string _displayName = "—";
    private string _nominalWidth = "—";
    private string _depth = "—";
    private string _height = "—";
    private string _category = "—";
    private string _construction = "—";
    private string _shelfCount = "—";
    private string _toeKickHeight = "—";
    private string _notes = "—";
    private string _nominalWidthEditValue = string.Empty;
    private int _selectionBringIntoViewToken;
    private bool _isProjectOpen;
    private bool _hasSelection;
    private bool _hasSingleSelection;
    private bool _isEditingNominalWidth;
    private decimal? _currentNominalWidthInches;
    private CabinetCategory? _selectedCategory;
    private ConstructionMethod? _selectedConstruction;
    private bool _hasError;

    public PropertyInspectorViewModel(
        ICabinetPropertyService cabinetService,
        IApplicationEventBus eventBus,
        IAppLogger logger,
        ICatalogService? catalogService = null,
        Action<string>? statusSink = null)
    {
        _cabinetService = cabinetService ?? throw new ArgumentNullException(nameof(cabinetService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        ArgumentNullException.ThrowIfNull(logger);
        _catalogService = catalogService ?? new CatalogService();
        _statusSink = statusSink;

        BeginNominalWidthEditCommand = new RelayCommand(BeginNominalWidthEdit, () => CanEditNominalWidth && !IsEditingNominalWidth);
        CommitNominalWidthEditCommand = new AsyncRelayCommand(CommitNominalWidthEditAsync, "property.nominal-width.commit", logger, eventBus, () => CanEditNominalWidth && IsEditingNominalWidth);
        CancelNominalWidthEditCommand = new RelayCommand(CancelNominalWidthEdit, () => IsEditingNominalWidth);

        _eventBus.Subscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Subscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Subscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Subscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Subscribe<RedoAppliedEvent>(OnDesignChanged);

        RefreshSelectionState();
    }

    public PropertyInspectorViewModel(
        IRunService runService,
        IApplicationEventBus eventBus,
        IAppLogger logger,
        ICatalogService? catalogService = null,
        Action<string>? statusSink = null)
        : this(
            runService as ICabinetPropertyService
                ?? new NoOpCabinetPropertyService(eventBus),
            eventBus,
            logger,
            catalogService,
            statusSink)
    {
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

    public bool CanEditNominalWidth => IsProjectOpen && HasSelection;

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

    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    public string LastErrorMessage
    {
        get => _lastErrorMessage;
        private set
        {
            if (SetProperty(ref _lastErrorMessage, value))
            {
                HasError = !string.IsNullOrWhiteSpace(value);
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

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public string NominalWidth
    {
        get => _nominalWidth;
        private set => SetProperty(ref _nominalWidth, value);
    }

    public string NominalWidthDisplay => HasSelection ? NominalWidth : "-";

    public string Depth
    {
        get => _depth;
        private set => SetProperty(ref _depth, value);
    }

    public string Height
    {
        get => _height;
        private set => SetProperty(ref _height, value);
    }

    public string Category
    {
        get => _category;
        private set => SetProperty(ref _category, value);
    }

    public string Construction
    {
        get => _construction;
        private set => SetProperty(ref _construction, value);
    }

    public string ShelfCount
    {
        get => _shelfCount;
        private set => SetProperty(ref _shelfCount, value);
    }

    public string ToeKickHeight
    {
        get => _toeKickHeight;
        private set => SetProperty(ref _toeKickHeight, value);
    }

    public string Notes
    {
        get => _notes;
        private set => SetProperty(ref _notes, value);
    }

    public IReadOnlyList<CabinetOpeningRowViewModel> Openings
    {
        get => _openings;
        private set => SetProperty(ref _openings, value);
    }

    public IReadOnlyList<CabinetOverrideRowViewModel> MaterialOverrides
    {
        get => _materialOverrides;
        private set => SetProperty(ref _materialOverrides, value);
    }

    public IReadOnlyList<CabinetOverrideRowViewModel> ThicknessOverrides
    {
        get => _thicknessOverrides;
        private set => SetProperty(ref _thicknessOverrides, value);
    }

    public IReadOnlyList<PropertyRowViewModel> Properties
    {
        get => _properties;
        private set => SetProperty(ref _properties, value);
    }

    public string EmptyStateText => !IsProjectOpen
        ? "No project is open, so there are no cabinet properties to inspect yet. Open or create a project to populate this panel."
        : !HasSelection
            ? "No cabinet is selected on the canvas right now. Click a cabinet to inspect its properties here."
            : HasSingleSelection
                ? $"A cabinet is selected, so its current properties are shown below. Edit only the supported fields for now while {AlphaLimitations.AllByCode["ALPHA-PROPERTIES-NOOP-FALLBACK"].Title.ToLowerInvariant()} remains in effect (press F1 for alpha notes)."
                : $"Multiple cabinets are selected, so shared properties are summarized below. Edit supported shared fields here while {AlphaLimitations.AllByCode["ALPHA-PROPERTIES-NOOP-FALLBACK"].Title.ToLowerInvariant()} remains in effect (press F1 for alpha notes).";

    public string PropertySummaryDisplay => HasSelection
        ? $"{Properties.Count} details"
        : "0 details";

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

    public CabinetCategory? SelectedCategory
    {
        get => _selectedCategory;
        private set => SetProperty(ref _selectedCategory, value);
    }

    public ConstructionMethod? SelectedConstruction
    {
        get => _selectedConstruction;
        private set => SetProperty(ref _selectedConstruction, value);
    }

    public RelayCommand BeginNominalWidthEditCommand { get; }

    public AsyncRelayCommand CommitNominalWidthEditCommand { get; }

    public RelayCommand CancelNominalWidthEditCommand { get; }

    public int SelectionBringIntoViewToken
    {
        get => _selectionBringIntoViewToken;
        private set => SetProperty(ref _selectionBringIntoViewToken, value);
    }

    public void OnSelectionChanged(IReadOnlyList<Guid> selectedCabinetIds, object? selectionContext = null)
    {
        _selectedCabinetIds = selectedCabinetIds?.ToArray() ?? [];
        _lastSelectionContext = selectionContext;
        RefreshSelectionState();
    }

    public void Dispose()
    {
        _eventBus.Unsubscribe<ProjectOpenedEvent>(OnProjectOpened);
        _eventBus.Unsubscribe<ProjectClosedEvent>(OnProjectClosed);
        _eventBus.Unsubscribe<DesignChangedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<UndoAppliedEvent>(OnDesignChanged);
        _eventBus.Unsubscribe<RedoAppliedEvent>(OnDesignChanged);
    }

    public async Task SetCategoryAsync(CabinetCategory category)
    {
        if (!CanEditSelection)
        {
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.SetCabinetCategoryAsync(cabinet.CabinetId.Value, category)).ConfigureAwait(true);
    }

    public async Task SetConstructionAsync(ConstructionMethod construction)
    {
        if (!CanEditSelection)
        {
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.SetCabinetConstructionAsync(cabinet.CabinetId.Value, construction)).ConfigureAwait(true);
    }

    public async Task AddOpeningAsync(OpeningType openingType, decimal widthInches, decimal heightInches, int? insertIndex)
    {
        if (!CanEditSelection)
        {
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.AddOpeningAsync(cabinet.CabinetId.Value, openingType, widthInches, heightInches, insertIndex)).ConfigureAwait(true);
    }

    public async Task RemoveOpeningAsync(Guid openingId)
    {
        if (!CanEditSelection)
        {
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.RemoveOpeningAsync(cabinet.CabinetId.Value, openingId)).ConfigureAwait(true);
    }

    public async Task ReorderOpeningAsync(Guid openingId, int newIndex)
    {
        if (!CanEditSelection)
        {
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.ReorderOpeningAsync(cabinet.CabinetId.Value, openingId, newIndex)).ConfigureAwait(true);
    }

    public async Task SetMaterialOverrideAsync(string partKey, Guid? materialId)
    {
        if (!CanEditSelection)
        {
            return;
        }

        if (materialId is null)
        {
            await ClearOverrideAsync($"material.{partKey}").ConfigureAwait(true);
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.SetCabinetOverrideAsync(
            cabinet.CabinetId.Value,
            $"material.{partKey}",
            new OverrideValueDto.OfMaterialId(materialId.Value))).ConfigureAwait(true);
    }

    public async Task SetThicknessOverrideAsync(string partKey, decimal? thicknessInches)
    {
        if (!CanEditSelection)
        {
            return;
        }

        if (thicknessInches is null)
        {
            await ClearOverrideAsync($"thickness.{partKey}").ConfigureAwait(true);
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.SetCabinetOverrideAsync(
            cabinet.CabinetId.Value,
            $"thickness.{partKey}",
            new OverrideValueDto.OfDecimalInches(thicknessInches.Value))).ConfigureAwait(true);
    }

    public async Task SetNotesAsync(string notes)
    {
        if (!CanEditSelection)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(notes))
        {
            await ClearOverrideAsync("notes").ConfigureAwait(true);
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.SetCabinetOverrideAsync(
            cabinet.CabinetId.Value,
            "notes",
            new OverrideValueDto.OfString(notes))).ConfigureAwait(true);
    }

    public async Task SetShelfCountAsync(int? shelfCount)
    {
        if (!CanEditSelection)
        {
            return;
        }

        if (shelfCount is null)
        {
            await ClearOverrideAsync("shelfCount").ConfigureAwait(true);
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.SetCabinetOverrideAsync(
            cabinet.CabinetId.Value,
            "shelfCount",
            new OverrideValueDto.OfInt(Math.Clamp(shelfCount.Value, 0, 6)))).ConfigureAwait(true);
    }

    public async Task SetToeKickHeightAsync(decimal? inches)
    {
        if (!CanEditSelection)
        {
            return;
        }

        if (inches is null)
        {
            await ClearOverrideAsync("toeKickHeight").ConfigureAwait(true);
            return;
        }

        await ApplyToSelectionAsync(cabinet => _cabinetService.SetCabinetOverrideAsync(
            cabinet.CabinetId.Value,
            "toeKickHeight",
            new OverrideValueDto.OfDecimalInches(Math.Max(0m, inches.Value)))).ConfigureAwait(true);
    }

    public async Task SetNominalWidthAsync(decimal widthInches)
    {
        if (!CanEditSelection)
        {
            return;
        }

        var targets = _selectedCabinets.ToArray();
        if (targets.Length == 0)
        {
            return;
        }

        var commands = targets.Select(cabinet =>
        {
            var bounds = ResolveWidthBounds(cabinet);
            var clampedWidth = Math.Clamp(widthInches, bounds.Min, bounds.Max);
            if (clampedWidth != widthInches)
            {
                SetStatus($"Width clamped to {clampedWidth:0.##} inches for {cabinet.CabinetTypeId}.");
            }

            return _cabinetService.ResizeCabinetAsync(cabinet.CabinetId.Value, clampedWidth, cabinet.NominalDepth.Inches, cabinet.EffectiveNominalHeight.Inches);
        });

        await Task.WhenAll(commands).ConfigureAwait(true);
        RefreshSelectionState();
    }

    private bool CanEditSelection => IsProjectOpen && HasSelection;

    private void OnProjectOpened(ProjectOpenedEvent _) => UiDispatchHelper.Run(() => SetProjectOpen(true));

    private void OnProjectClosed(ProjectClosedEvent _) => UiDispatchHelper.Run(() => SetProjectOpen(false));

    private void OnDesignChanged<TEvent>(TEvent _) where TEvent : IApplicationEvent =>
        UiDispatchHelper.Run(RefreshSelectionState);

    private void SetProjectOpen(bool isOpen)
    {
        IsProjectOpen = isOpen;
        if (!isOpen)
        {
            _selectedCabinetIds = [];
            _selectedCabinets = [];
        }

        RefreshSelectionState();
    }

    private void RefreshSelectionState()
    {
        HasSelection = _selectedCabinetIds.Count > 0;
        HasSingleSelection = _selectedCabinetIds.Count == 1;
        OnPropertyChanged(nameof(CanEditNominalWidth));

        if (!IsProjectOpen)
        {
            ClearSelectionProperties("No project open", "No project open", "No project is open, so there are no cabinet properties to inspect yet. Open or create a project to populate this panel.", "No editable properties");
            return;
        }

        _selectedCabinets = _cabinetService.GetCabinets(_selectedCabinetIds);
        if (_selectedCabinets.Count == 0)
        {
            if (TryPopulateFromSceneFallback())
            {
                return;
            }

            ClearSelectionProperties("No cabinet selected", "Nothing selected", "No cabinet is selected on the canvas right now. Click a cabinet to inspect its properties here.", "No editable properties");
            return;
        }

        if (_selectedCabinets.Count == 1)
        {
            PopulateSingleSelection(_selectedCabinets[0]);
            return;
        }

        PopulateMultiSelection(_selectedCabinets);
    }

    private void PopulateSingleSelection(CabinetStateRecord cabinet)
    {
        var ordinal = ResolveCabinetOrdinal(cabinet);
        DisplayName = $"{cabinet.CabinetTypeId} #{ordinal}";
        SelectedCategory = cabinet.Category;
        SelectedConstruction = cabinet.Construction;
        SelectedEntityLabel = $"{DisplayName} ({cabinet.CabinetId.Value.ToString()[..8]})";
        SelectionSummaryDisplay = "1 selected";
        SourceLabel = "Cabinet state store";
        EditabilityStatusDisplay = "Editable";
        StatusMessage = "Editing the selected cabinet.";
        LastErrorMessage = string.Empty;
        RequestSelectionBringIntoView();
        OnPropertyChanged(nameof(CanEditNominalWidth));
        PopulateFieldProperties([cabinet], isMultiSelect: false);
    }

    private void PopulateMultiSelection(IReadOnlyList<CabinetStateRecord> cabinets)
    {
        DisplayName = cabinets.All(cabinet => cabinet.CabinetTypeId == cabinets[0].CabinetTypeId)
            ? $"{cabinets[0].CabinetTypeId} selection"
            : "—";
        SelectedCategory = cabinets.All(cabinet => cabinet.Category == cabinets[0].Category) ? cabinets[0].Category : null;
        SelectedConstruction = cabinets.All(cabinet => cabinet.Construction == cabinets[0].Construction) ? cabinets[0].Construction : null;
        SelectedEntityLabel = $"{cabinets.Count} cabinets selected";
        SelectionSummaryDisplay = $"{cabinets.Count} selected";
        SourceLabel = "Cabinet state store";
        EditabilityStatusDisplay = "Multi-edit";
        StatusMessage = "Multiple cabinets are selected.";
        LastErrorMessage = string.Empty;
        RequestSelectionBringIntoView();
        OnPropertyChanged(nameof(CanEditNominalWidth));
        PopulateFieldProperties(cabinets, isMultiSelect: true);
    }

    private void PopulateFieldProperties(IReadOnlyList<CabinetStateRecord> cabinets, bool isMultiSelect)
    {
        NominalWidth = GetCommonValue(cabinets, cabinet => FormatLength(cabinet.NominalWidth));
        Depth = GetCommonValue(cabinets, cabinet => FormatLength(cabinet.NominalDepth));
        Height = GetCommonValue(cabinets, cabinet => FormatLength(cabinet.EffectiveNominalHeight));
        Category = GetCommonValue(cabinets, cabinet => cabinet.Category.ToString());
        Construction = GetCommonValue(cabinets, cabinet => cabinet.Construction.ToString());
        ShelfCount = GetCommonOverrideValue(cabinets, "shelfCount", value => value switch
        {
            OverrideValue.OfInt intValue => intValue.Value.ToString(CultureInfo.InvariantCulture),
            _ => "—"
        }, "—");
        ToeKickHeight = GetToeKickDisplay(cabinets);
        Notes = GetCommonOverrideValue(cabinets, "notes", value => value switch
        {
            OverrideValue.OfString text => text.Value,
            _ => "—"
        }, "—");

        Openings = isMultiSelect && !AllOpeningsMatch(cabinets)
            ? []
            : BuildOpeningRows(cabinets[0].EffectiveOpenings);
        MaterialOverrides = BuildOverrideRows(cabinets, "material", MaterialOverrideKeys);
        ThicknessOverrides = BuildOverrideRows(cabinets, "thickness", ThicknessOverrideKeys);
        Properties = BuildPropertyRows();

        if (HasSingleSelection)
        {
            var selected = cabinets[0];
            SetEditableWidth(selected.NominalWidth.Inches);
        }
        else
        {
            ResetEditableWidth();
        }

        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(PropertySummaryDisplay));
    }

    private bool TryPopulateFromSceneFallback()
    {
        if (_selectedCabinetIds.Count != 1 || _lastSelectionContext is not RenderSceneDto scene)
        {
            return false;
        }

        var render = scene.Cabinets.FirstOrDefault(cabinet => cabinet.CabinetId == _selectedCabinetIds[0]);
        if (render is null)
        {
            return false;
        }

        _isProjectOpen = true;
        _hasSelection = true;
        _hasSingleSelection = true;
        _selectedEntityLabel = $"{render.TypeDisplayName} ({render.CabinetId.ToString()[..8]})";
        _selectionSummaryDisplay = "1 selected";
        _sourceLabel = "Projected scene data";
        _editabilityStatusDisplay = "Nominal width editable";
        _statusMessage = "Showing projected cabinet details. Nominal width can be resized.";
        _displayName = render.TypeDisplayName;
        _nominalWidth = FormatLength(render.WorldBounds.Width);
        _depth = "—";
        _height = "—";
        _category = "—";
        _construction = "—";
        _shelfCount = "—";
        _toeKickHeight = "—";
        _notes = "—";
        Openings = [];
        MaterialOverrides = [];
        ThicknessOverrides = [];
        Properties = BuildPropertyRows();
        SetEditableWidth(render.WorldBounds.Width.Inches);
        RequestSelectionBringIntoView();
        OnPropertyChanged(nameof(IsProjectOpen));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasSingleSelection));
        OnPropertyChanged(nameof(CanEditNominalWidth));
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(PropertySummaryDisplay));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(NominalWidth));
        OnPropertyChanged(nameof(Depth));
        OnPropertyChanged(nameof(Height));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(Construction));
        OnPropertyChanged(nameof(ShelfCount));
        OnPropertyChanged(nameof(ToeKickHeight));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(SelectedEntityLabel));
        OnPropertyChanged(nameof(SelectionSummaryDisplay));
        OnPropertyChanged(nameof(SourceLabel));
        OnPropertyChanged(nameof(EditabilityStatusDisplay));
        OnPropertyChanged(nameof(StatusMessage));
        return true;
    }

    private IReadOnlyList<PropertyRowViewModel> BuildPropertyRows() =>
        _lastSelectionContext is RenderSceneDto && _selectedCabinets.Count == 0 && _selectedCabinetIds.Count == 1
            ? [
                new PropertyRowViewModel("display-name", "Display Name", DisplayName, false),
                new PropertyRowViewModel("nominal-width", "Nominal Width", NominalWidth, true),
                new PropertyRowViewModel("category", "Category", Category, true),
                new PropertyRowViewModel("construction", "Construction", Construction, true),
                new PropertyRowViewModel("editability", "Editability", EditabilityStatusDisplay, false),
                new PropertyRowViewModel("selection", "Selection", SelectionSummaryDisplay, false)
            ]
            : [
                new PropertyRowViewModel("display-name", "Display Name", DisplayName, false),
                new PropertyRowViewModel("nominal-width", "Nominal Width", NominalWidth, true),
                new PropertyRowViewModel("depth", "Depth", Depth, true),
                new PropertyRowViewModel("height", "Height", Height, true),
                new PropertyRowViewModel("category", "Category", Category, true),
                new PropertyRowViewModel("construction", "Construction", Construction, true),
                new PropertyRowViewModel("openings", "Openings", Openings.Count == 0 ? "—" : $"{Openings.Count} openings", true),
                new PropertyRowViewModel("shelf-count", "Shelf Count", ShelfCount, true),
                new PropertyRowViewModel("toe-kick-height", "Toe Kick Height", ToeKickHeight, true),
                new PropertyRowViewModel("material-overrides", "Material Overrides", MaterialOverrides.Count == 0 ? "—" : $"{MaterialOverrides.Count} overrides", true),
                new PropertyRowViewModel("thickness-overrides", "Thickness Overrides", ThicknessOverrides.Count == 0 ? "—" : $"{ThicknessOverrides.Count} overrides", true),
                new PropertyRowViewModel("notes", "Notes", Notes, true)
            ];

    private IReadOnlyList<CabinetOpeningRowViewModel> BuildOpeningRows(IReadOnlyList<CabinetOpeningStateRecord> openings) =>
        openings
            .OrderBy(opening => opening.Index)
            .Select(opening => new CabinetOpeningRowViewModel(
                opening.OpeningId,
                opening.Index,
                opening.Type.ToString(),
                FormatLength(opening.Width),
                FormatLength(opening.Height)))
            .ToArray();

    private IReadOnlyList<CabinetOverrideRowViewModel> BuildOverrideRows(
        IReadOnlyList<CabinetStateRecord> cabinets,
        string prefix,
        IReadOnlyList<string> keys) =>
        keys.Select(key =>
        {
            var displayName = key[(key.IndexOf('.') + 1)..];
            var value = GetCommonOverrideValue(cabinets, $"{prefix}.{displayName}", FormatOverrideValue, "—");
            return new CabinetOverrideRowViewModel(key, displayName, value, true, value == "—");
        }).ToArray();

    private string GetToeKickDisplay(IReadOnlyList<CabinetStateRecord> cabinets)
    {
        var first = cabinets[0];
        if (first.Category is not CabinetCategory.Base and not CabinetCategory.Tall)
        {
            return "—";
        }

        return GetCommonOverrideValue(
            cabinets,
            "toeKickHeight",
            value => value switch
            {
                OverrideValue.OfDecimal decimalValue => $"{decimalValue.Value.ToString("0.##", CultureInfo.InvariantCulture)}\"",
                OverrideValue.OfLength lengthValue => FormatLength(lengthValue.Value),
                _ => "4.5\""
            },
            "4.5\"");
    }

    private static string FormatOverrideValue(OverrideValue value) =>
        value switch
        {
            OverrideValue.OfMaterialId material => material.Value == default ? "(catalog default)" : material.Value.ToString(),
            OverrideValue.OfThickness thickness => FormatThickness(thickness.Value),
            OverrideValue.OfLength length => FormatLength(length.Value),
            OverrideValue.OfInt intValue => intValue.Value.ToString(CultureInfo.InvariantCulture),
            OverrideValue.OfDecimal decimalValue => decimalValue.Value.ToString("0.##", CultureInfo.InvariantCulture),
            OverrideValue.OfString text => string.IsNullOrWhiteSpace(text.Value) ? "(catalog default)" : text.Value,
            _ => "(catalog default)"
        };

    private static string FormatLength(Length length) => $"{length.Inches:0.##}\"";

    private static string FormatThickness(Thickness thickness) => $"{thickness.Actual.Inches:0.###}\"";

    private static string GetCommonValue(IReadOnlyList<CabinetStateRecord> cabinets, Func<CabinetStateRecord, string> selector)
    {
        var first = selector(cabinets[0]);
        return cabinets.All(cabinet => selector(cabinet) == first) ? first : "—";
    }

    private static string GetCommonOverrideValue(
        IReadOnlyList<CabinetStateRecord> cabinets,
        string key,
        Func<OverrideValue, string> formatter,
        string defaultValue)
    {
        var firstValue = GetOverrideValue(cabinets[0], key);
        if (firstValue is null)
        {
            return cabinets.All(cabinet => GetOverrideValue(cabinet, key) is null) ? defaultValue : "—";
        }

        var formatted = formatter(firstValue);
        return cabinets.All(cabinet => GetOverrideValue(cabinet, key) is not null && formatter(GetOverrideValue(cabinet, key)!) == formatted)
            ? formatted
            : "—";
    }

    private static OverrideValue? GetOverrideValue(CabinetStateRecord cabinet, string key) =>
        cabinet.EffectiveOverrides.TryGetValue(key, out var value) ? value : null;

    private bool AllOpeningsMatch(IReadOnlyList<CabinetStateRecord> cabinets)
    {
        var first = cabinets[0].EffectiveOpenings;
        return cabinets.All(cabinet =>
            cabinet.EffectiveOpenings.Count == first.Count &&
            cabinet.EffectiveOpenings.Zip(first, (left, right) =>
                left.OpeningId == right.OpeningId &&
                left.Index == right.Index &&
                left.Type == right.Type &&
                left.Width == right.Width &&
                left.Height == right.Height).All(match => match));
    }

    private int ResolveCabinetOrdinal(CabinetStateRecord cabinet)
    {
        var allCabinets = _cabinetService.GetAllCabinets()
            .Where(candidate => candidate.CabinetTypeId == cabinet.CabinetTypeId)
            .OrderBy(candidate => candidate.CabinetTypeId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.CabinetId.Value)
            .ToArray();

        var index = Array.FindIndex(allCabinets, candidate => candidate.CabinetId == cabinet.CabinetId);
        return index >= 0 ? index + 1 : 1;
    }

    private (decimal Min, decimal Max) ResolveWidthBounds(CabinetStateRecord cabinet)
    {
        var matchingTemplates = _catalogService.GetAllItems()
            .Where(item => string.Equals(item.Category, cabinet.Category.ToString(), StringComparison.Ordinal) &&
                           item.ConstructionMethod == cabinet.Construction)
            .ToArray();

        if (matchingTemplates.Length == 0)
        {
            return (9m, 144m);
        }

        return (matchingTemplates.Min(item => item.NominalWidth.Inches), matchingTemplates.Max(item => item.NominalWidth.Inches));
    }

    private async Task CommitNominalWidthEditAsync()
    {
        if (!CanEditNominalWidth || _selectedCabinets.Count == 0)
        {
            return;
        }

        if (!decimal.TryParse(NominalWidthEditValue.Trim().TrimEnd('"'), NumberStyles.Number, CultureInfo.InvariantCulture, out var widthInches))
        {
            Fail("Enter a valid width in inches.");
            return;
        }

        var alphaLimitationEncountered = false;
        var tasks = _selectedCabinets.Select(async cabinet =>
        {
            var bounds = ResolveWidthBounds(cabinet);
            var clampedWidth = Math.Clamp(widthInches, bounds.Min, bounds.Max);
            if (clampedWidth != widthInches)
            {
                Fail($"Width clamped to {clampedWidth:0.##} inches.");
            }

            var result = await _cabinetService.ResizeCabinetAsync(
                cabinet.CabinetId.Value,
                clampedWidth,
                cabinet.NominalDepth.Inches,
                cabinet.EffectiveNominalHeight.Inches).ConfigureAwait(true);

            if (IsAlphaNoOpResult(result))
            {
                LastErrorMessage = string.Empty;
                alphaLimitationEncountered = true;
                return;
            }

            if (!result.Success)
            {
                Fail(result.Issues.FirstOrDefault()?.Message ?? "Cabinet width update rejected.");
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(true);
        if (alphaLimitationEncountered)
        {
            IsEditingNominalWidth = false;
            RefreshSelectionState();
            return;
        }

        IsEditingNominalWidth = false;
        RefreshSelectionState();
        SetStatus("Cabinet width updated.");
    }

    private void BeginNominalWidthEdit()
    {
        if (!CanEditNominalWidth || _currentNominalWidthInches is null)
        {
            return;
        }

        LastErrorMessage = string.Empty;
        NominalWidthEditValue = FormatLength(Length.FromInches(_currentNominalWidthInches.Value)).TrimEnd('"');
        IsEditingNominalWidth = true;
        SetStatus("Editing cabinet width.");
    }

    private void CancelNominalWidthEdit()
    {
        if (!IsEditingNominalWidth)
        {
            return;
        }

        IsEditingNominalWidth = false;
        LastErrorMessage = string.Empty;
        if (_currentNominalWidthInches is not null)
        {
            NominalWidthEditValue = _currentNominalWidthInches.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        SetStatus("Width edit canceled.");
    }

    private void SetEditableWidth(decimal widthInches)
    {
        _currentNominalWidthInches = widthInches;
        NominalWidth = FormatLength(Length.FromInches(widthInches));
        NominalWidthEditValue = widthInches.ToString("0.##", CultureInfo.InvariantCulture);
        IsEditingNominalWidth = false;
    }

    private void ResetEditableWidth()
    {
        _currentNominalWidthInches = null;
        NominalWidth = "—";
        NominalWidthEditValue = string.Empty;
        IsEditingNominalWidth = false;
    }

    private void ClearSelectionProperties(string selectedEntityLabel, string summary, string statusText, string editabilityText)
    {
        SelectedEntityLabel = selectedEntityLabel;
        SelectionSummaryDisplay = summary;
        SourceLabel = "No project open" == selectedEntityLabel ? "No project open" : "Selection idle";
        EditabilityStatusDisplay = editabilityText;
        StatusMessage = statusText;
        DisplayName = "—";
        NominalWidth = "—";
        Depth = "—";
        Height = "—";
        Category = "—";
        Construction = "—";
        ShelfCount = "—";
        ToeKickHeight = "—";
        Notes = "—";
        Openings = [];
        MaterialOverrides = [];
        ThicknessOverrides = [];
        Properties = BuildPropertyRows();
        ResetEditableWidth();
        OnPropertyChanged(nameof(EmptyStateText));
        OnPropertyChanged(nameof(PropertySummaryDisplay));
        OnPropertyChanged(nameof(CanEditNominalWidth));
    }

    private void RequestSelectionBringIntoView() => SelectionBringIntoViewToken++;

    private async Task ApplyToSelectionAsync(Func<CabinetStateRecord, Task<CommandResultDto>> action)
    {
        var cabinets = _selectedCabinets.ToArray();
        if (cabinets.Length == 0)
        {
            return;
        }

        foreach (var cabinet in cabinets)
        {
            var result = await action(cabinet).ConfigureAwait(true);
            if (IsAlphaNoOpResult(result))
            {
                LastErrorMessage = string.Empty;
                return;
            }

            if (!result.Success)
            {
                Fail(result.Issues.FirstOrDefault()?.Message ?? "Edit rejected.");
                return;
            }
        }

        RefreshSelectionState();
        SetStatus("Cabinet updated.");
    }

    private async Task ClearOverrideAsync(string overrideKey)
    {
        foreach (var cabinet in _selectedCabinets)
        {
            var result = await _cabinetService.RemoveCabinetOverrideAsync(cabinet.CabinetId.Value, overrideKey).ConfigureAwait(true);
            if (IsAlphaNoOpResult(result))
            {
                LastErrorMessage = string.Empty;
                return;
            }

            if (!result.Success)
            {
                Fail(result.Issues.FirstOrDefault()?.Message ?? "Edit rejected.");
                return;
            }
        }

        RefreshSelectionState();
        SetStatus("Cabinet updated.");
    }

    private void SetStatus(string message)
    {
        StatusMessage = string.IsNullOrWhiteSpace(message) ? "Ready" : message;
        _statusSink?.Invoke(StatusMessage);
    }

    private void Fail(string message)
    {
        LastErrorMessage = message;
        SetStatus(message);
    }

    private static bool IsAlphaNoOpResult(CommandResultDto result) =>
        !result.Success &&
        result.CommandId == Guid.Empty &&
        result.Issues.Count == 0;

    private sealed class NoOpCabinetPropertyService : ICabinetPropertyService
    {
        private readonly IApplicationEventBus _eventBus;

        public NoOpCabinetPropertyService(IApplicationEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public IReadOnlyList<CabinetStateRecord> GetAllCabinets() => [];

        public CabinetStateRecord? GetCabinet(Guid cabinetId) => null;

        public IReadOnlyList<CabinetStateRecord> GetCabinets(IReadOnlyList<Guid> cabinetIds) => [];

        public Task<CommandResultDto> ResizeCabinetAsync(Guid cabinetId, decimal widthInches, decimal depthInches, decimal heightInches)
        {
            PublishAlphaLimitation($"resize cabinet {cabinetId}");
            return Task.FromResult(CommandResultDto.NoOp("resize_cabinet"));
        }

        public Task<CommandResultDto> SetCabinetCategoryAsync(Guid cabinetId, CabinetCategory category)
        {
            PublishAlphaLimitation($"set category {category} for cabinet {cabinetId}");
            return Task.FromResult(CommandResultDto.NoOp("set_cabinet_category"));
        }

        public Task<CommandResultDto> SetCabinetConstructionAsync(Guid cabinetId, ConstructionMethod construction)
        {
            PublishAlphaLimitation($"set construction {construction} for cabinet {cabinetId}");
            return Task.FromResult(CommandResultDto.NoOp("set_cabinet_construction"));
        }

        public Task<CommandResultDto> AddOpeningAsync(Guid cabinetId, OpeningType openingType, decimal widthInches, decimal heightInches, int? insertIndex)
        {
            PublishAlphaLimitation($"add {openingType} opening to cabinet {cabinetId}");
            return Task.FromResult(CommandResultDto.NoOp("add_opening"));
        }

        public Task<CommandResultDto> RemoveOpeningAsync(Guid cabinetId, Guid openingId)
        {
            PublishAlphaLimitation($"remove opening {openingId} from cabinet {cabinetId}");
            return Task.FromResult(CommandResultDto.NoOp("remove_opening"));
        }

        public Task<CommandResultDto> ReorderOpeningAsync(Guid cabinetId, Guid openingId, int newIndex)
        {
            PublishAlphaLimitation($"reorder opening {openingId} on cabinet {cabinetId} to index {newIndex}");
            return Task.FromResult(CommandResultDto.NoOp("reorder_opening"));
        }

        public Task<CommandResultDto> SetCabinetOverrideAsync(Guid cabinetId, string overrideKey, OverrideValueDto value)
        {
            PublishAlphaLimitation($"set override {overrideKey} on cabinet {cabinetId}");
            return Task.FromResult(CommandResultDto.NoOp("set_cabinet_override"));
        }

        public Task<CommandResultDto> RemoveCabinetOverrideAsync(Guid cabinetId, string overrideKey)
        {
            PublishAlphaLimitation($"remove override {overrideKey} from cabinet {cabinetId}");
            return Task.FromResult(CommandResultDto.NoOp("remove_cabinet_override"));
        }

        private void PublishAlphaLimitation(string contextHint)
        {
            _eventBus.Publish(new AlphaLimitationEncounteredEvent(
                AlphaLimitations.AllByCode["ALPHA-PROPERTIES-NOOP-FALLBACK"],
                contextHint));
        }
    }
}

public sealed record CabinetOpeningRowViewModel(
    Guid OpeningId,
    int Index,
    string Type,
    string Width,
    string Height);

public sealed record CabinetOverrideRowViewModel(
    string Key,
    string DisplayName,
    string DisplayValue,
    bool IsEditable,
    bool IsPlaceholder = false);
