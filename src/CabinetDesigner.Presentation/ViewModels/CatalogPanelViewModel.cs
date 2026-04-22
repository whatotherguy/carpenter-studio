using CabinetDesigner.Application.Services;

namespace CabinetDesigner.Presentation.ViewModels;

public sealed class CatalogPanelViewModel : ObservableObject
{
    private readonly IReadOnlyList<CatalogItemViewModel> _allItems;
    private IReadOnlyList<CatalogItemViewModel> _filteredItems = [];
    private string _searchText = string.Empty;

    public CatalogPanelViewModel(ICatalogService catalogService)
    {
        ArgumentNullException.ThrowIfNull(catalogService);

        _allItems = catalogService.GetAllItems()
            .Select(item => new CatalogItemViewModel(
                item.TypeId,
                item.DisplayName,
                item.Category,
                item.ConstructionMethod.ToString(),
                item.Description,
                $"{item.NominalWidth.Inches:0.##}\" W x {item.Depth.Inches:0.##}\" D x {item.Height.Inches:0.##}\" H",
                $"{item.DefaultOpenings} opening{(item.DefaultOpenings == 1 ? string.Empty : "s")}",
                FormatWidth(item.DefaultNominalWidthInches),
                item.DefaultNominalWidthInches))
            .ToArray();
        _filteredItems = _allItems;
    }

    public IReadOnlyList<CatalogItemViewModel> AllItems => _allItems;

    public IReadOnlyList<CatalogItemViewModel> FilteredItems
    {
        get => _filteredItems;
        private set => SetProperty(ref _filteredItems, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                RefreshFilteredItems();
            }
        }
    }

    public bool HasFilteredItems => FilteredItems.Count > 0;

    public bool IsPlaceholderData => false;

    public string SourceLabel => "Built-in catalog";

    public string EmptyStateText => "Try another cabinet name, category, type id, or width.";

    public string ItemCountText => FilteredItems.Count == AllItems.Count
        ? $"{FilteredItems.Count} items"
        : $"{FilteredItems.Count} of {AllItems.Count} items";

    // Future drag/drop or add actions can subscribe to this without changing the item model.
    public event EventHandler<CatalogItemViewModel>? ItemActivated;

    public void ActivateItem(CatalogItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ItemActivated?.Invoke(this, item);
    }

    private void RefreshFilteredItems()
    {
        var search = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(search)
            ? _allItems
            : _allItems.Where(item => Matches(item, search)).ToArray();

        FilteredItems = filtered;
        OnPropertyChanged(nameof(HasFilteredItems));
        OnPropertyChanged(nameof(ItemCountText));
    }

    private static bool Matches(CatalogItemViewModel item, string search) =>
        item.TypeId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.ConstructionMethod.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.DimensionsDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.DefaultOpeningsDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
        item.DefaultWidthDisplay.Contains(search, StringComparison.OrdinalIgnoreCase);

    private static string FormatWidth(decimal widthInches) => $"{widthInches:0.##}\"";
}
