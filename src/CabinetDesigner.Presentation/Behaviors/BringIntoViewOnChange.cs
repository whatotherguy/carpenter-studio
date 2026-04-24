using System.Windows;
using System.Windows.Threading;

namespace CabinetDesigner.Presentation.Behaviors;

public static class BringIntoViewOnChange
{
    public static readonly DependencyProperty TokenProperty = DependencyProperty.RegisterAttached(
        "Token",
        typeof(int),
        typeof(BringIntoViewOnChange),
        new PropertyMetadata(0, OnTokenChanged));

    public static void SetToken(DependencyObject element, int value) => element.SetValue(TokenProperty, value);

    public static int GetToken(DependencyObject element) => (int)element.GetValue(TokenProperty);

    private static void OnTokenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not FrameworkElement element)
        {
            return;
        }

        if (Equals(args.OldValue, args.NewValue) || (int)args.NewValue <= 0)
        {
            return;
        }

        void BringSelectedSectionIntoView()
        {
            if (!element.IsVisible)
            {
                return;
            }

            element.BringIntoView();
        }

        if (!element.IsLoaded)
        {
            RoutedEventHandler? loadedHandler = null;
            loadedHandler = (_, _) =>
            {
                element.Loaded -= loadedHandler;
                element.Dispatcher.BeginInvoke((Action)BringSelectedSectionIntoView, DispatcherPriority.Input);
            };
            element.Loaded += loadedHandler;
            return;
        }

        element.Dispatcher.BeginInvoke((Action)BringSelectedSectionIntoView, DispatcherPriority.Input);
    }
}
