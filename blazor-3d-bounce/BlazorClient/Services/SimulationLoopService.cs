using BlazorClient.Models;

namespace BlazorClient.Services;

/// <summary>
/// Interface for the physics simulation loop.
/// Follows Single Responsibility Principle - only handles simulation timing and coordination.
/// </summary>
public interface ISimulationLoopService : IAsyncDisposable
{
    /// <summary>
    /// Event raised when simulation state changes (for UI updates).
    /// </summary>
    event Action? OnSimulationStateChanged;

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
    /// Starts the simulation loop.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops the simulation loop.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Executes a single simulation step (when paused).
    /// </summary>
    Task StepOnceAsync();
}

/// <summary>
/// Implementation of the simulation loop service.
/// Coordinates physics stepping and transform synchronization.
/// </summary>
public class SimulationLoopService : ISimulationLoopService
{
    private readonly IRigidPhysicsService _rigidPhysics;
    private readonly ISoftPhysicsService _softPhysics;
    private readonly IInteropService _interop;
    private readonly ISceneStateService _sceneState;

    private PeriodicTimer? _simulationTimer;
    private CancellationTokenSource? _timerCts;
    private DateTime _lastFrameTime = DateTime.UtcNow;
    private float _accumulator;
    private bool _isRunning;

    public event Action? OnSimulationStateChanged;
    public float Fps { get; private set; }
    public float PhysicsTimeMs { get; private set; }
    public bool IsRunning => _isRunning;

    public SimulationLoopService(
        IRigidPhysicsService rigidPhysics,
        ISoftPhysicsService softPhysics,
        IInteropService interop,
        ISceneStateService sceneState)
    {
        _rigidPhysics = rigidPhysics;
        _softPhysics = softPhysics;
        _interop = interop;
        _sceneState = sceneState;
    }

    /// <inheritdoc />
    public async Task StartAsync()
    {
        if (_isRunning) return;

        _isRunning = true;
        _timerCts = new CancellationTokenSource();
        _lastFrameTime = DateTime.UtcNow;
        _accumulator = 0;

        _ = RunSimulationLoopAsync(_timerCts.Token);
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        if (!_isRunning) return;

        _timerCts?.Cancel();
        _simulationTimer?.Dispose();
        _simulationTimer = null;
        _isRunning = false;
    }

    /// <inheritdoc />
    public async Task StepOnceAsync()
    {
        await ExecutePhysicsStepAsync(_sceneState.Settings.TimeStep);
        await SynchronizeTransformsAsync();
        OnSimulationStateChanged?.Invoke();
    }

    private async Task RunSimulationLoopAsync(CancellationToken cancellationToken)
    {
        _simulationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000.0 / 60.0));

        try
        {
            while (await _simulationTimer.WaitForNextTickAsync(cancellationToken))
            {
                await SimulationTickAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when disposed
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Simulation loop error: {ex.Message}");
        }
    }

    private async Task SimulationTickAsync()
    {
        if (_sceneState.Settings.IsPaused) return;

        try
        {
            var now = DateTime.UtcNow;
            var deltaTime = (float)(now - _lastFrameTime).TotalSeconds;
            _lastFrameTime = now;

            // Clamp delta time to prevent spiral of death
            deltaTime = Math.Min(deltaTime, 0.1f);

            var physicsStart = DateTime.UtcNow;

            // Fixed timestep accumulator
            _accumulator += deltaTime * _sceneState.Settings.TimeScale;
            var fixedDt = _sceneState.Settings.TimeStep;

            while (_accumulator >= fixedDt)
            {
                await ExecutePhysicsStepAsync(fixedDt);
                _accumulator -= fixedDt;
            }

            PhysicsTimeMs = (float)(DateTime.UtcNow - physicsStart).TotalMilliseconds;

            // Synchronize transforms with rendering
            await SynchronizeTransformsAsync();

            // Calculate FPS
            Fps = deltaTime > 0 ? 1f / deltaTime : 0;

            OnSimulationStateChanged?.Invoke();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Simulation tick error: {ex.Message}");
        }
    }

    private async Task ExecutePhysicsStepAsync(float deltaTime)
    {
        await _rigidPhysics.StepAsync(deltaTime);

        if (await _softPhysics.IsAvailableAsync())
        {
            await _softPhysics.StepAsync(deltaTime);
        }
    }

    private async Task SynchronizeTransformsAsync()
    {
        // Get rigid body transforms
        var rigidBatch = await _rigidPhysics.GetTransformBatchAsync();
        if (rigidBatch.Ids.Length > 0)
        {
            await _interop.CommitRigidTransformsAsync(rigidBatch.Transforms, rigidBatch.Ids);
        }

        // Get soft body vertices
        if (await _softPhysics.IsAvailableAsync() && _sceneState.SoftBodies.Count > 0)
        {
            var softVertices = await _softPhysics.GetDeformedVerticesAsync();
            foreach (var (id, data) in softVertices)
            {
                await _interop.CommitSoftVerticesAsync(id, data.Vertices, data.Normals);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _timerCts?.Dispose();
    }
}
