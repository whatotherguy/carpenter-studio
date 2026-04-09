using CabinetDesigner.Presentation.ViewModels;
using CabinetDesigner.Application.Services;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

public sealed class CatalogPanelViewModelTests
{
    [Fact]
    public void InitialState_ExposesBuiltInCatalogItems()
    {
        var viewModel = new CatalogPanelViewModel(new CatalogService());

        Assert.False(viewModel.IsPlaceholderData);
        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(7, viewModel.AllItems.Count);
        Assert.Equal(viewModel.AllItems, viewModel.FilteredItems);
        Assert.True(viewModel.HasFilteredItems);
        Assert.Equal("Built-in catalog", viewModel.SourceLabel);
        Assert.Equal("7 items", viewModel.ItemCountText);
    }

    [Fact]
    public void SearchText_FiltersByCategoryNameTypeIdDescriptionAndWidth()
    {
        var viewModel = new CatalogPanelViewModel(new CatalogService());

        viewModel.SearchText = "wall";

        Assert.Equal(2, viewModel.FilteredItems.Count);
        Assert.All(viewModel.FilteredItems, item => Assert.Equal("Wall", item.Category));

        viewModel.SearchText = "36";

        Assert.Equal(3, viewModel.FilteredItems.Count);
        Assert.All(viewModel.FilteredItems, item => Assert.Contains("36", item.DefaultWidthDisplay));

        viewModel.SearchText = "template";

        Assert.Equal(7, viewModel.FilteredItems.Count);
    }

    [Fact]
    public void SearchText_ReturnsEmptyWhenNoItemMatches()
    {
        var viewModel = new CatalogPanelViewModel(new CatalogService());

        viewModel.SearchText = "sink";

        Assert.Empty(viewModel.FilteredItems);
        Assert.False(viewModel.HasFilteredItems);
        Assert.Equal("0 of 7 items", viewModel.ItemCountText);
        Assert.Equal("Try another cabinet name, category, type id, or width.", viewModel.EmptyStateText);
    }

    [Fact]
    public void SearchText_RaisesFilteredStatePropertyChanges()
    {
        var viewModel = new CatalogPanelViewModel(new CatalogService());
        var changedProperties = new List<string>();

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is not null)
            {
                changedProperties.Add(args.PropertyName);
            }
        };

        viewModel.SearchText = "wall";

        Assert.Contains(nameof(CatalogPanelViewModel.FilteredItems), changedProperties);
        Assert.Contains(nameof(CatalogPanelViewModel.HasFilteredItems), changedProperties);
        Assert.Contains(nameof(CatalogPanelViewModel.ItemCountText), changedProperties);
    }

    [Fact]
    public void ActivateItem_RaisesItemActivated()
    {
        var viewModel = new CatalogPanelViewModel(new CatalogService());
        CatalogItemViewModel? activatedItem = null;
        viewModel.ItemActivated += (_, item) => activatedItem = item;

        viewModel.ActivateItem(viewModel.AllItems[0]);

        Assert.Same(viewModel.AllItems[0], activatedItem);
    }
}
