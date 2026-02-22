using BlazorClient.Domain.Models;

namespace BlazorClient.Application.Services;

/// <summary>
/// Interface for managing the simulation loop.
/// </summary>
/// <remarks>
/// <para>
/// Controls the main game/simulation loop including start, stop, pause,
/// and single-step functionality.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Threading:</strong> In Blazor WebAssembly, the loop runs on the
/// main thread using <c>requestAnimationFrame</c> via JS interop.
/// </para>
/// </remarks>
public interface ISimulationLoopService : IAsyncDisposable
{
    /// <summary>
    /// Gets or sets whether the simulation is paused.
    /// </summary>
    /// <value>
    /// <c>true</c> if the simulation is paused; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// When paused, the loop continues running but physics steps are skipped.
    /// Use <see cref="StepOnceAsync"/> to advance a single frame while paused.
    /// </remarks>
    bool IsPaused { get; set; }

    /// <summary>
    /// Current frames per second.
    /// </summary>
    float Fps { get; }

    /// <summary>
    /// Time spent in physics calculations (ms).
    /// </summary>
    float PhysicsTimeMs { get; }

    /// <summary>
    /// Whether the simulation loop is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Event raised after each simulation tick.
    /// </summary>
    /// <remarks>
    /// The float parameter is the delta time (time since last tick) in seconds.
    /// Subscribers should avoid heavy operations to maintain frame rate.
    /// </remarks>
    event Action<float>? OnTick;

    /// <summary>
    /// Event raised when simulation state changes (for UI updates).
    /// </summary>
    event Action? OnSimulationStateChanged;

    /// <summary>
    /// Starts the simulation loop.
    /// </summary>
    /// <returns>A task representing the asynchronous start operation.</returns>
    /// <remarks>
    /// The loop will continue running until <see cref="StopAsync"/> is called
    /// or the service is disposed.
    /// </remarks>
    Task StartAsync();

    /// <summary>
    /// Stops the simulation loop.
    /// </summary>
    /// <returns>A task representing the asynchronous stop operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Performs a single simulation step (useful when paused).
    /// </summary>
    /// <returns>A task representing the asynchronous step operation.</returns>
    /// <remarks>
    /// This method advances the simulation by one frame regardless of
    /// the <see cref="IsPaused"/> state.
    /// </remarks>
    Task StepOnceAsync();
}

/// <summary>
/// Interface for managing scene state and objects.
/// </summary>
/// <remarks>
/// <para>
/// Central state management for all scene objects (rigid bodies, soft bodies),
/// simulation settings, and render settings.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Pattern:</strong> State Management. This acts as a single source
/// of truth for scene state, similar to a Redux store.
/// </para>
/// </remarks>
public interface ISceneStateService
{
    /// <summary>
    /// Gets all rigid bodies in the scene.
    /// </summary>
    /// <value>Read-only list of rigid bodies.</value>
    IReadOnlyList<RigidBody> RigidBodies { get; }

    /// <summary>
    /// Gets all soft bodies in the scene.
    /// </summary>
    /// <value>Read-only list of soft bodies.</value>
    IReadOnlyList<SoftBody> SoftBodies { get; }

    /// <summary>
    /// Gets the current simulation settings.
    /// </summary>
    /// <value>Current simulation settings (gravity, time step, etc.).</value>
    SimulationSettings Settings { get; }

    /// <summary>
    /// Gets the current render settings.
    /// </summary>
    /// <value>Current render settings (shadows, post-processing, etc.).</value>
    RenderSettings RenderSettings { get; }

    /// <summary>
    /// Gets the ID of the currently selected object.
    /// </summary>
    /// <value>
    /// The selected object's ID, or <c>null</c> if nothing is selected.
    /// </value>
    string? SelectedObjectId { get; }

    /// <summary>
    /// Gets the selected rigid body if one is selected.
    /// </summary>
    RigidBody? SelectedRigidBody { get; }

    /// <summary>
    /// Gets the selected soft body if one is selected.
    /// </summary>
    SoftBody? SelectedSoftBody { get; }

    /// <summary>
    /// Current performance statistics.
    /// </summary>
    PerformanceStats Stats { get; }

    /// <summary>
    /// Event raised when the scene state changes.
    /// </summary>
    /// <remarks>
    /// Subscribe to this event to update UI components when state changes.
    /// Consider debouncing frequent updates to avoid excessive re-renders.
    /// </remarks>
    event Action? OnStateChanged;

    /// <summary>
    /// Adds a rigid body to the scene.
    /// </summary>
    /// <param name="body">The rigid body to add.</param>
    /// <remarks>
    /// This only adds to state. Call the physics and rendering services
    /// separately to create the actual physics and visual representations.
    /// </remarks>
    void AddRigidBody(RigidBody body);

    /// <summary>
    /// Adds a soft body to the scene.
    /// </summary>
    /// <param name="body">The soft body to add.</param>
    /// <remarks>
    /// This only adds to state. Call the physics and rendering services
    /// separately to create the actual physics and visual representations.
    /// </remarks>
    void AddSoftBody(SoftBody body);

    /// <summary>
    /// Removes an object from the scene.
    /// </summary>
    /// <param name="id">The unique identifier of the object to remove.</param>
    /// <remarks>
    /// This removes from both rigid and soft body collections.
    /// Also clears selection if the removed object was selected.
    /// </remarks>
    void RemoveObject(string id);

    /// <summary>
    /// Selects an object by ID.
    /// </summary>
    /// <param name="id">
    /// The object ID to select, or <c>null</c> to clear selection.
    /// </param>
    void SelectObject(string? id);

    /// <summary>
    /// Clears all objects from the scene.
    /// </summary>
    /// <remarks>
    /// Removes all rigid and soft bodies and clears selection.
    /// Does not reset settings.
    /// </remarks>
    void ClearScene();

    /// <summary>
    /// Updates simulation settings.
    /// </summary>
    /// <param name="settings">The new simulation settings.</param>
    void UpdateSettings(SimulationSettings settings);

    /// <summary>
    /// Updates render settings.
    /// </summary>
    /// <param name="settings">The new render settings.</param>
    void UpdateRenderSettings(RenderSettings settings);

    /// <summary>
    /// Updates performance statistics.
    /// </summary>
    void UpdateStats(PerformanceStats stats);

    /// <summary>
    /// Gets a scene object by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the object.</param>
    /// <returns>
    /// The scene object (either RigidBody or SoftBody), or <c>null</c> if not found.
    /// </returns>
    SceneObject? GetObject(string id);

    /// <summary>
    /// Notifies subscribers that state has changed.
    /// </summary>
    /// <remarks>
    /// Call this after making batch changes to trigger a single state update.
    /// Individual add/remove methods call this automatically.
    /// </remarks>
    void NotifyStateChanged();

    /// <summary>
    /// Loads a scene preset, replacing current scene contents.
    /// </summary>
    /// <param name="preset">The preset to load.</param>
    void LoadPreset(ScenePreset preset);

    /// <summary>
    /// Exports the current scene to a preset.
    /// </summary>
    /// <returns>A scene preset containing all scene data.</returns>
    ScenePreset ExportToPreset();

    /// <summary>
    /// Exports the current scene as a preset with a custom name.
    /// </summary>
    ScenePreset ExportPreset(string name);
}
