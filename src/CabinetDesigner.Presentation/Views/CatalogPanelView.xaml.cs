using System.Windows;
using System.Windows.Input;
using CabinetDesigner.Presentation.ViewModels;

namespace CabinetDesigner.Presentation.Views;

public partial class CatalogPanelView
{
    private Point? _dragStartPoint;
    private CatalogItemViewModel? _dragSourceItem;

    public CatalogPanelView()
    {
        InitializeComponent();
    }

    private void OnItemPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CatalogItemViewModel item })
        {
            _dragSourceItem = item;
            _dragStartPoint = e.GetPosition(this);
        }
    }

    private void OnItemPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragSourceItem is null || _dragStartPoint is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var position = e.GetPosition(this);
        var deltaX = Math.Abs(position.X - _dragStartPoint.Value.X);
        var deltaY = Math.Abs(position.Y - _dragStartPoint.Value.Y);
        if (deltaX < SystemParameters.MinimumHorizontalDragDistance &&
            deltaY < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var payload = new CatalogTemplateDragPayload(_dragSourceItem.TypeId);
        var data = new DataObject(typeof(CatalogTemplateDragPayload), payload);
        DragDrop.DoDragDrop(this, data, DragDropEffects.Copy);
        _dragSourceItem = null;
        _dragStartPoint = null;
        e.Handled = true;
    }

    private void OnItemMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount != 2)
        {
            _dragSourceItem = null;
            _dragStartPoint = null;
            return;
        }

        if (sender is FrameworkElement { DataContext: CatalogItemViewModel item } &&
            DataContext is CatalogPanelViewModel vm)
        {
            vm.ActivateItem(item);
            e.Handled = true;
        }

        _dragSourceItem = null;
        _dragStartPoint = null;
    }
}
