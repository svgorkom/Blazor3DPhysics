using BlazorClient.Models;

namespace BlazorClient.Services.Events;

/// <summary>
/// Event aggregator for decoupled component communication.
/// Implements the publish-subscribe pattern.
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// Subscribes to events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="handler">The handler to invoke when the event is published.</param>
    /// <returns>A subscription that can be disposed to unsubscribe.</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;

    /// <summary>
    /// Publishes an event to all subscribers.
    /// </summary>
    /// <typeparam name="TEvent">The event type.</typeparam>
    /// <param name="eventData">The event data.</param>
    void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;

    /// <summary>
    /// Publishes an event asynchronously to all subscribers.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IEvent;
}

/// <summary>
/// Marker interface for events.
/// </summary>
public interface IEvent { }

/// <summary>
/// Implementation of the event aggregator.
/// </summary>
public class EventAggregator : IEventAggregator
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
    private readonly object _lock = new();

    /// <inheritdoc />
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);

        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(eventType, out var handlers))
            {
                handlers = new List<Delegate>();
                _subscriptions[eventType] = handlers;
            }

            handlers.Add(handler);
        }

        return new Subscription(() => Unsubscribe(eventType, handler));
    }

    private void Unsubscribe(Type eventType, Delegate handler)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
            }
        }
    }

    /// <inheritdoc />
    public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
    {
        var eventType = typeof(TEvent);
        List<Delegate>? handlersCopy;

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
                ((Action<TEvent>)handler)(eventData);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Event handler error for {eventType.Name}: {ex.Message}");
            }
        }
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IEvent
    {
        // In Blazor WASM, we're single-threaded, so this just yields
        await Task.Yield();
        Publish(eventData);
    }

    private class Subscription : IDisposable
    {
        private readonly Action _unsubscribe;
        private bool _disposed;

        public Subscription(Action unsubscribe)
        {
            _unsubscribe = unsubscribe;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _unsubscribe();
        }
    }
}

#region Domain Events

/// <summary>
/// Event raised when an object is spawned in the scene.
/// </summary>
public record ObjectSpawnedEvent(string Id, string Name, string ObjectType) : IEvent;

/// <summary>
/// Event raised when an object is deleted from the scene.
/// </summary>
public record ObjectDeletedEvent(string Id) : IEvent;

/// <summary>
/// Event raised when an object is selected.
/// </summary>
public record ObjectSelectedEvent(string? Id, string? PreviousId) : IEvent;

/// <summary>
/// Event raised when the simulation is paused or resumed.
/// </summary>
public record SimulationPausedEvent(bool IsPaused) : IEvent;

/// <summary>
/// Event raised when simulation settings change.
/// </summary>
public record SimulationSettingsChangedEvent(SimulationSettings Settings) : IEvent;

/// <summary>
/// Event raised when render settings change.
/// </summary>
public record RenderSettingsChangedEvent(RenderSettings Settings) : IEvent;

/// <summary>
/// Event raised after each physics step.
/// </summary>
public record PhysicsSteppedEvent(float DeltaTime, float PhysicsTimeMs, int RigidBodyCount, int SoftBodyCount) : IEvent;

/// <summary>
/// Event raised when the scene is reset.
/// </summary>
public record SceneResetEvent : IEvent;

/// <summary>
/// Event raised when a scene is loaded.
/// </summary>
public record SceneLoadedEvent(string PresetName) : IEvent;

/// <summary>
/// Event raised when initialization completes.
/// </summary>
public record InitializationCompleteEvent(bool SoftBodyAvailable) : IEvent;

/// <summary>
/// Event raised when an error occurs.
/// </summary>
public record ErrorOccurredEvent(string Message, string? Details, ErrorSeverity Severity) : IEvent;

/// <summary>
/// Error severity levels.
/// </summary>
public enum ErrorSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

#endregion
