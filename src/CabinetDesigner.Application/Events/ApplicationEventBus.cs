using CabinetDesigner.Application.Diagnostics;

namespace CabinetDesigner.Application.Events;

public sealed class ApplicationEventBus : IApplicationEventBus
{
    private readonly object _sync = new();
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];
    private readonly IAppLogger? _logger;

    public ApplicationEventBus(IAppLogger? logger = null)
    {
        _logger = logger;
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        Delegate[] snapshot;
        lock (_sync)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                return;
            }

            snapshot = handlers.ToArray();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                ((Action<TEvent>)handler)(@event);
            }
            catch (Exception exception)
            {
                _logger?.Log(new LogEntry
                {
                    Level = LogLevel.Error,
                    Category = "Application",
                    Message = "Application event handler threw while processing an event.",
                    Timestamp = DateTimeOffset.UtcNow,
                    Properties = new Dictionary<string, string>
                    {
                        ["eventType"] = typeof(TEvent).FullName ?? typeof(TEvent).Name,
                        ["handlerType"] = handler.GetType().FullName ?? handler.GetType().Name
                    },
                    Exception = exception
                });
            }
        }
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_sync)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = [];
                _handlers[typeof(TEvent)] = handlers;
            }

            handlers.Add(handler);
        }
    }

    public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        lock (_sync)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }
}
