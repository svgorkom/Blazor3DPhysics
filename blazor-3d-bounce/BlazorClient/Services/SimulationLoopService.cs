using BlazorClient.Domain.Models;
using BlazorClient.Application.Events;
using BlazorClient.Application.Services;

namespace BlazorClient.Services;

/// <summary>
/// Implementation of the simulation loop service.
/// Coordinates physics stepping and transform synchronization.
/// Optimized to minimize interop overhead.
/// </summary>
public class SimulationLoopService : BlazorClient.Application.Services.ISimulationLoopService
{
    private readonly IRigidPhysicsService _rigidPhysics;
    private readonly ISoftPhysicsService _softPhysics;
    private readonly ISceneStateService _sceneState;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly IEventAggregator _events;

    private PeriodicTimer? _simulationTimer;
    private CancellationTokenSource? _timerCts;
    private DateTime _lastFrameTime = DateTime.UtcNow;
    private float _accumulator;
    private bool _isRunning;
    private bool _isPaused;
    
    // Frame skip for soft body sync (reduce interop frequency)
    private int _frameCounter;
    private const int SoftBodySyncInterval = 2; // Sync every 2 frames

    /// <inheritdoc />
    public bool IsPaused 
    { 
        get => _isPaused; 
        set => _isPaused = value; 
    }

    /// <inheritdoc />
    public float Fps => _performanceMonitor.GetSnapshot().Fps;

    /// <inheritdoc />
    public float PhysicsTimeMs => _performanceMonitor.GetAverageTiming("Physics");

    /// <inheritdoc />
    public bool IsRunning => _isRunning;

    /// <inheritdoc />
    public event Action<float>? OnTick;

    /// <inheritdoc />
    public event Action? OnSimulationStateChanged;

    public SimulationLoopService(
        IRigidPhysicsService rigidPhysics,
        ISoftPhysicsService softPhysics,
        ISceneStateService sceneState,
        IPerformanceMonitor performanceMonitor,
        IEventAggregator events)
    {
        _rigidPhysics = rigidPhysics;
        _softPhysics = softPhysics;
        _sceneState = sceneState;
        _performanceMonitor = performanceMonitor;
        _events = events;
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
        var deltaTime = _sceneState.Settings.TimeStep;
        
        using (_performanceMonitor.MeasureTiming("Physics"))
        {
            await ExecutePhysicsStepAsync(deltaTime);
        }

        PublishPhysicsEvent(deltaTime);
        OnTick?.Invoke(deltaTime);
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
        if (_isPaused) return;

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

                // Update performance stats
                _performanceMonitor.RecordFrame();
                _performanceMonitor.UpdateBodyCounts(
                    _sceneState.RigidBodies.Count,
                    _sceneState.SoftBodies.Count);

                // Publish physics event for interested subscribers
                if (stepsThisFrame > 0)
                {
                    var totalDelta = fixedDt * stepsThisFrame;
                    PublishPhysicsEvent(totalDelta);
                    OnTick?.Invoke(totalDelta);
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
        await _rigidPhysics.StepAsync(deltaTime);
        await _softPhysics.StepAsync(deltaTime);
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
