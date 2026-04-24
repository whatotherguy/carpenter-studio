using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CabinetDesigner.Presentation.Behaviors;

public static class FocusWhenTrue
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(FocusWhenTrue),
        new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element || args.NewValue is not true)
        {
            return;
        }

        void FocusElement()
        {
            if (!element.IsVisible || !element.Focusable)
            {
                return;
            }

            element.Focus();
            Keyboard.Focus(element);
        }

        if (!element.IsLoaded)
        {
            RoutedEventHandler? loadedHandler = null;
            loadedHandler = (_, _) =>
            {
                element.Loaded -= loadedHandler;
                element.Dispatcher.BeginInvoke((Action)FocusElement, DispatcherPriority.Input);
            };
            element.Loaded += loadedHandler;
            return;
        }

        element.Dispatcher.BeginInvoke((Action)FocusElement, DispatcherPriority.Input);
    }
}
