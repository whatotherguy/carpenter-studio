using System.Windows;
using System.Windows.Input;
using CabinetDesigner.Presentation.ViewModels;

namespace CabinetDesigner.Presentation.Views;

public partial class CatalogPanelView
{
    public CatalogPanelView()
    {
        InitializeComponent();
    }

    private void OnItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: CatalogItemViewModel item } &&
            DataContext is CatalogPanelViewModel vm)
        {
            vm.ActivateItem(item);
            e.Handled = true;
        }
    }
}
