using System.Windows.Threading;

namespace CabinetDesigner.Presentation;

/// <summary>
/// Routes callbacks to the WPF UI thread when one is available and healthy,
/// and runs them inline otherwise. Every viewmodel that reacts to event-bus
/// messages across threads funnels through here so the dispatcher policy is
/// defined in exactly one place.
/// </summary>
/// <remarks>
/// Cross-thread dispatch uses <see cref="Dispatcher.BeginInvoke(Delegate, object[])"/>
/// (fire-and-forget) rather than <see cref="Dispatcher.Invoke(Action)"/>.
/// A blocking Invoke deadlocks whenever the target dispatcher is not pumping —
/// for example when a test harness creates an
/// <see cref="System.Windows.Application"/> on an STA helper thread that
/// blocks on a queue instead of running <c>Dispatcher.Run</c>. This was the
/// root cause of the presentation-test-suite flake tracked in the
/// finish-round audit.
/// </remarks>
public static class UiDispatchHelper
{
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Run(System.Windows.Application.Current?.Dispatcher, action);
    }

    public static void Run(Dispatcher? dispatcher, Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!IsDispatcherUsable(dispatcher) || dispatcher!.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private static bool IsDispatcherUsable(Dispatcher? dispatcher)
    {
        if (dispatcher is null)
        {
            return false;
        }

        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return false;
        }

        var thread = dispatcher.Thread;
        return thread is not null && thread.IsAlive;
    }
}
