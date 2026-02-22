using BlazorClient.Domain.Models;

namespace BlazorClient.Application.Events;

/// <summary>
/// Marker interface for events.
/// </summary>
public interface IEvent { }

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

#region Domain Events

/// <summary>
/// Event raised when an object is spawned in the scene.
/// </summary>
public record ObjectSpawnedEvent(string Id, string Name, string ObjectType) : Event;

/// <summary>
/// Event raised when an object is deleted from the scene.
/// </summary>
public record ObjectDeletedEvent(string Id) : Event;

/// <summary>
/// Event raised when an object is selected.
/// </summary>
public record ObjectSelectedEvent(string? Id, string? PreviousId) : Event;

/// <summary>
/// Event raised when the simulation is paused or resumed.
/// </summary>
public record SimulationPausedEvent(bool IsPaused) : Event;

/// <summary>
/// Event raised when simulation settings change.
/// </summary>
public record SimulationSettingsChangedEvent(SimulationSettings Settings) : Event;

/// <summary>
/// Event raised when render settings change.
/// </summary>
public record RenderSettingsChangedEvent(RenderSettings Settings) : Event;

/// <summary>
/// Event raised after each physics step.
/// </summary>
public record PhysicsSteppedEvent(float DeltaTime, float PhysicsTimeMs, int RigidBodyCount, int SoftBodyCount) : Event;

/// <summary>
/// Event raised when the scene is reset.
/// </summary>
public record SceneResetEvent : Event;

/// <summary>
/// Event raised when a scene is loaded.
/// </summary>
public record SceneLoadedEvent(string PresetName) : Event;

/// <summary>
/// Event raised when initialization completes.
/// </summary>
public record InitializationCompleteEvent(bool SoftBodyAvailable) : Event;

/// <summary>
/// Event raised when an error occurs.
/// </summary>
public record ErrorOccurredEvent(string Message, string? Details, ErrorSeverity Severity) : Event;

#endregion
