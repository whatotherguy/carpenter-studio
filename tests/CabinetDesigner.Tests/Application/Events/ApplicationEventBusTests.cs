using CabinetDesigner.Application.Events;
using CabinetDesigner.Application.Diagnostics;
using Xunit;

namespace CabinetDesigner.Tests.Application.Events;

public sealed class ApplicationEventBusTests
{
    [Fact]
    public void Publish_DeliversToAllSubscribers()
    {
        var eventBus = new ApplicationEventBus();
        var deliveries = new List<string>();

        eventBus.Subscribe<TestEvent>(e => deliveries.Add($"first:{e.Value}"));
        eventBus.Subscribe<TestEvent>(e => deliveries.Add($"second:{e.Value}"));

        eventBus.Publish(new TestEvent("payload"));

        Assert.Equal(["first:payload", "second:payload"], deliveries);
    }

    [Fact]
    public void Publish_ContinuesDeliveryWhenOneHandlerThrows()
    {
        var logger = new RecordingAppLogger();
        var eventBus = new ApplicationEventBus(logger);
        var deliveries = new List<string>();

        eventBus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
        eventBus.Subscribe<TestEvent>(e => deliveries.Add(e.Value));

        eventBus.Publish(new TestEvent("payload"));

        Assert.Equal(["payload"], deliveries);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Application", entry.Category);
        Assert.Equal("Application event handler threw while processing an event.", entry.Message);
    }

    [Fact]
    public void Unsubscribe_StopsDeliveryToRemovedHandler()
    {
        var eventBus = new ApplicationEventBus();
        var deliveries = new List<string>();
        Action<TestEvent> handler = e => deliveries.Add(e.Value);

        eventBus.Subscribe(handler);
        eventBus.Unsubscribe(handler);

        eventBus.Publish(new TestEvent("payload"));

        Assert.Empty(deliveries);
    }

    [Fact]
    public void Subscribe_AfterPublish_DoesNotReceivePastEvent()
    {
        var eventBus = new ApplicationEventBus();
        var deliveries = new List<string>();

        eventBus.Publish(new TestEvent("before"));
        eventBus.Subscribe<TestEvent>(e => deliveries.Add(e.Value));
        eventBus.Publish(new TestEvent("after"));

        Assert.Equal(["after"], deliveries);
    }

    private sealed record TestEvent(string Value) : IApplicationEvent;

    private sealed class RecordingAppLogger : IAppLogger
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log(LogEntry entry) => Entries.Add(entry);
    }
}
