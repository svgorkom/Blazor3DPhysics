namespace BlazorClient.Application.Events;

/// <summary>
/// Base class for events.
/// </summary>
public abstract record Event;

/// <summary>
/// Event handler interface.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IEventHandler<in TEvent> where TEvent : Event
{
    Task HandleAsync(TEvent @event);
}

/// <summary>
/// Event aggregator for decoupled component communication.
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// Subscribes a handler to an event type.
    /// </summary>
    void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : Event;

    /// <summary>
    /// Subscribes a callback to an event type.
    /// </summary>
    void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : Event;

    /// <summary>
    /// Unsubscribes a handler from an event type.
    /// </summary>
    void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : Event;

    /// <summary>
    /// Publishes an event to all subscribers asynchronously.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event) where TEvent : Event;

    /// <summary>
    /// Publishes an event to all subscribers synchronously (fire-and-forget).
    /// </summary>
    void Publish<TEvent>(TEvent @event) where TEvent : Event;
}
