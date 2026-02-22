using BlazorClient.Application.Events;

namespace BlazorClient.Infrastructure.Events;

/// <summary>
/// Implementation of the event aggregator.
/// </summary>
public class EventAggregator : IEventAggregator
{
    private readonly Dictionary<Type, List<object>> _subscriptions = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Subscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : Event
    {
        var eventType = typeof(TEvent);

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<object>();
                _subscriptions[eventType] = handlers;
            }

            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }
    }

    /// <inheritdoc />
    public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : Event
    {
        var eventType = typeof(TEvent);

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<object>();
                _subscriptions[eventType] = handlers;
            }

            if (!handlers.Contains(handler))
            {
                handlers.Add(handler);
            }
        }
    }

    /// <inheritdoc />
    public void Unsubscribe<TEvent>(IEventHandler<TEvent> handler) where TEvent : Event
    {
        var eventType = typeof(TEvent);

        lock (_lock)
        {
            if (_subscriptions.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event) where TEvent : Event
    {
        var eventType = typeof(TEvent);
        List<object>? handlersCopy;

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out var handlers))
                return;

            // Copy to avoid issues if handlers modify subscriptions
            handlersCopy = handlers.ToList();
        }

        foreach (var handler in handlersCopy)
        {
            try
            {
                if (handler is IEventHandler<TEvent> typedHandler)
                {
                    await typedHandler.HandleAsync(@event);
                }
                else if (handler is Func<TEvent, Task> funcHandler)
                {
                    await funcHandler(@event);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Event handler error for {eventType.Name}: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent @event) where TEvent : Event
    {
        // Fire-and-forget async execution
        _ = PublishAsync(@event);
    }
}
