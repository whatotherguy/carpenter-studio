namespace CabinetDesigner.Application.Events;

public interface IApplicationEventBus
{
    void Publish<TEvent>(TEvent @event) where TEvent : IApplicationEvent;

    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent;

    void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IApplicationEvent;
}
