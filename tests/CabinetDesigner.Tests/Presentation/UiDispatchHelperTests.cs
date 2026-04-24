using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using CabinetDesigner.Presentation;
using Xunit;

namespace CabinetDesigner.Tests.Presentation;

// Covers the root cause of the presentation-test-suite flake documented in
// docs/ai/finish_round/final_finish_audit.md. The previous DispatchIfNeeded
// helpers called Dispatcher.Invoke against a dispatcher whose thread was not
// pumping, which blocks forever. UiDispatchHelper replaces that with
// BeginInvoke plus a liveness guard, which cannot deadlock.
public sealed class UiDispatchHelperTests
{
    [Fact]
    public void Run_ExecutesInline_WhenDispatcherIsNull()
    {
        var executed = false;

        UiDispatchHelper.Run(dispatcher: null, () => executed = true);

        Assert.True(executed);
    }

    [Fact]
    public void Run_ExecutesInline_WhenDispatcherHasShutdown()
    {
        var dispatcher = CreateAndShutDownDispatcher();
        var executed = false;

        UiDispatchHelper.Run(dispatcher, () => executed = true);

        Assert.True(executed);
        Assert.True(dispatcher.HasShutdownFinished);
    }

    [Fact]
    public void Run_ExecutesInline_WhenOwningThreadExitedWithoutShutdown()
    {
        var dispatcher = CreateZombieDispatcher();
        Assert.False(dispatcher.HasShutdownStarted);
        Assert.False(dispatcher.HasShutdownFinished);
        Assert.False(dispatcher.Thread.IsAlive);
        var executed = false;

        UiDispatchHelper.Run(dispatcher, () => executed = true);

        Assert.True(executed);
    }

    [Fact]
    public async Task Run_DoesNotBlock_WhenOwnerThreadIsAliveButNotPumping()
    {
        // This mirrors MainWindowSmokeThreadFixture: the dispatcher-owning
        // thread is alive but blocked on something other than Dispatcher.Run,
        // so its queue is never pumped. A plain Dispatcher.Invoke from another
        // thread would hang forever. UiDispatchHelper.Run must still return.
        using var release = new ManualResetEventSlim(false);
        var (dispatcher, ownerThread) = CreateParkedDispatcher(release);
        try
        {
            Assert.True(dispatcher.Thread.IsAlive);

            var runTask = Task.Run(() =>
            {
                UiDispatchHelper.Run(dispatcher, () => { /* queued; we only care the caller returns */ });
                return true;
            });
            var winner = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(3)));

            Assert.True(ReferenceEquals(runTask, winner),
                "UiDispatchHelper.Run blocked on a parked dispatcher — BeginInvoke semantics are broken.");
            Assert.True(await runTask);
        }
        finally
        {
            release.Set();
            ownerThread.Join(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public void Run_InvokesOnDispatcherThread_WhenCalledFromAnotherThread()
    {
        using var ready = new ManualResetEventSlim(false);
        Dispatcher? dispatcher = null;
        DispatcherFrame? frame = null;

        var ownerThread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            frame = new DispatcherFrame();
            ready.Set();
            Dispatcher.PushFrame(frame);
        })
        {
            IsBackground = true,
            Name = "UiDispatchHelperTests Pump"
        };
        ownerThread.SetApartmentState(ApartmentState.STA);
        ownerThread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(2)));
        Assert.NotNull(dispatcher);
        Assert.NotNull(frame);

        int dispatchedThreadId = 0;
        using var dispatched = new ManualResetEventSlim(false);

        UiDispatchHelper.Run(dispatcher, () =>
        {
            dispatchedThreadId = Thread.CurrentThread.ManagedThreadId;
            dispatched.Set();
        });

        Assert.True(dispatched.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(ownerThread.ManagedThreadId, dispatchedThreadId);

        dispatcher!.BeginInvoke(() => frame!.Continue = false);
        Assert.True(ownerThread.Join(TimeSpan.FromSeconds(2)));
    }

    private static Dispatcher CreateZombieDispatcher()
    {
        Dispatcher? captured = null;
        using var ready = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            captured = Dispatcher.CurrentDispatcher;
            ready.Set();
        })
        {
            IsBackground = true,
            Name = "UiDispatchHelperTests Zombie"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        Assert.NotNull(captured);
        return captured!;
    }

    private static Dispatcher CreateAndShutDownDispatcher()
    {
        Dispatcher? captured = null;
        using var ready = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            captured = Dispatcher.CurrentDispatcher;
            ready.Set();
            captured.InvokeShutdown();
        })
        {
            IsBackground = true,
            Name = "UiDispatchHelperTests ShutDown"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        Assert.NotNull(captured);
        return captured!;
    }

    private static (Dispatcher Dispatcher, Thread OwnerThread) CreateParkedDispatcher(ManualResetEventSlim release)
    {
        Dispatcher? captured = null;
        using var ready = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            captured = Dispatcher.CurrentDispatcher;
            ready.Set();
            release.Wait();
        })
        {
            IsBackground = true,
            Name = "UiDispatchHelperTests Parked"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(ready.Wait(TimeSpan.FromSeconds(2)));
        Assert.NotNull(captured);
        return (captured!, thread);
    }
}
