using BlazorClient.Domain.Models;
using BlazorClient.Application.Events;

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
/// Optimized to minimize interop overhead.
/// </summary>
public class SimulationLoopService : ISimulationLoopService
{
    private readonly IRigidPhysicsService _rigidPhysics;
    private readonly ISoftPhysicsService _softPhysics;
    private readonly IInteropService _interop;
    private readonly ISceneStateService _sceneState;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IEventAggregator _events;
    private readonly ArrayPool<float> _transformPool;

    private PeriodicTimer? _simulationTimer;
    private CancellationTokenSource? _timerCts;
    private DateTime _lastFrameTime = DateTime.UtcNow;
    private float _accumulator;
    private bool _isRunning;
    
    // Frame skip for soft body sync (reduce interop frequency)
    private int _frameCounter;
    private const int SoftBodySyncInterval = 2; // Sync every 2 frames

    public event Action? OnSimulationStateChanged;
    public float Fps => _performanceMonitor.GetSnapshot().Fps;
    public float PhysicsTimeMs => _performanceMonitor.GetAverageTiming("Physics");
    public bool IsRunning => _isRunning;

    public SimulationLoopService(
        IRigidPhysicsService rigidPhysics,
        ISoftPhysicsService softPhysics,
        IInteropService interop,
        ISceneStateService sceneState,
        IPerformanceMonitor performanceMonitor,
        IEventAggregator events,
        ArrayPool<float> transformPool)
    {
        _rigidPhysics = rigidPhysics;
        _softPhysics = softPhysics;
        _interop = interop;
        _sceneState = sceneState;
        _performanceMonitor = performanceMonitor;
        _events = events;
        _transformPool = transformPool;
    }

    /// <inheritdoc />
    public Task StartAsync()
    {
        if (_isRunning) return Task.CompletedTask;

        _isRunning = true;
        _timerCts = new CancellationTokenSource();
        _lastFrameTime = DateTime.UtcNow;
        _accumulator = 0;
        _frameCounter = 0;
        _performanceMonitor.Reset();

        _ = RunSimulationLoopAsync(_timerCts.Token);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync()
    {
        if (!_isRunning) return Task.CompletedTask;

        _timerCts?.Cancel();
        _simulationTimer?.Dispose();
        _simulationTimer = null;
        _isRunning = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StepOnceAsync()
    {
        using (_performanceMonitor.MeasureTiming("Physics"))
        {
            await ExecutePhysicsStepAsync(_sceneState.Settings.TimeStep);
        }

        using (_performanceMonitor.MeasureTiming("Interop"))
        {
            await SynchronizeTransformsAsync(true);
        }

        PublishPhysicsEvent(_sceneState.Settings.TimeStep);
        OnSimulationStateChanged?.Invoke();
    }

    private async Task RunSimulationLoopAsync(CancellationToken cancellationToken)
    {
        // Target 60 FPS
        _simulationTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(16.667));

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
            _events.Publish(new ErrorOccurredEvent(
                "Simulation loop error", 
                ex.Message, 
                ErrorSeverity.Error));
        }
    }

    private async Task SimulationTickAsync()
    {
        if (_sceneState.Settings.IsPaused) return;

        try
        {
            _frameCounter++;
            
            using (_performanceMonitor.MeasureTiming("Frame"))
            {
                var now = DateTime.UtcNow;
                var deltaTime = (float)(now - _lastFrameTime).TotalSeconds;
                _lastFrameTime = now;

                // Clamp delta time to prevent spiral of death
                deltaTime = Math.Min(deltaTime, 0.05f); // Max 50ms

                // Fixed timestep accumulator
                _accumulator += deltaTime * _sceneState.Settings.TimeScale;
                var fixedDt = _sceneState.Settings.TimeStep;
                var stepsThisFrame = 0;

                // Cap physics steps per frame
                while (_accumulator >= fixedDt && stepsThisFrame < 4)
                {
                    using (_performanceMonitor.MeasureTiming("Physics"))
                    {
                        await ExecutePhysicsStepAsync(fixedDt);
                    }

                    _accumulator -= fixedDt;
                    stepsThisFrame++;
                }

                // Synchronize transforms with rendering
                // Soft bodies sync less frequently to reduce interop overhead
                var syncSoftBodies = (_frameCounter % SoftBodySyncInterval) == 0;
                
                using (_performanceMonitor.MeasureTiming("Interop"))
                {
                    await SynchronizeTransformsAsync(syncSoftBodies);
                }

                // Update performance stats
                _performanceMonitor.RecordFrame();
                _performanceMonitor.UpdateBodyCounts(
                    _sceneState.RigidBodies.Count,
                    _sceneState.SoftBodies.Count);

                // Publish physics event for interested subscribers
                if (stepsThisFrame > 0)
                {
                    PublishPhysicsEvent(fixedDt * stepsThisFrame);
                }

                OnSimulationStateChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Simulation tick error: {ex.Message}");
        }
    }

    private async Task ExecutePhysicsStepAsync(float deltaTime)
    {
        // Run rigid and soft physics
        // Use cached IsAvailable to avoid async call
        await _rigidPhysics.StepAsync(deltaTime);

        if (_softPhysics.IsAvailable)
        {
            await _softPhysics.StepAsync(deltaTime);
        }
    }

    private async Task SynchronizeTransformsAsync(bool includeSoftBodies)
    {
        // Get rigid body transforms
        var rigidBatch = await _rigidPhysics.GetTransformBatchAsync();
        if (rigidBatch.Ids.Length > 0)
        {
            await _interop.CommitRigidTransformsAsync(rigidBatch.Transforms, rigidBatch.Ids);
        }

        // Get soft body vertices - only on scheduled frames
        if (includeSoftBodies && _softPhysics.IsAvailable && _sceneState.SoftBodies.Count > 0)
        {
            // Single batched call to get all vertices
            var softVertices = await _softPhysics.GetDeformedVerticesAsync();
            
            // Batch commit all soft body data
            if (softVertices.Count > 0)
            {
                await _interop.CommitAllSoftVerticesAsync(softVertices);
            }
        }
    }

    private void PublishPhysicsEvent(float deltaTime)
    {
        _events.Publish(new PhysicsSteppedEvent(
            deltaTime,
            _performanceMonitor.GetAverageTiming("Physics"),
            _sceneState.RigidBodies.Count,
            _sceneState.SoftBodies.Count));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _timerCts?.Dispose();
    }
}
