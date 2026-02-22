using Microsoft.JSInterop;
using System.Text.Json.Serialization;

namespace BlazorClient.Services;

/// <summary>
/// Configuration for GPU physics engine.
/// </summary>
public class GpuPhysicsConfig
{
    /// <summary>
    /// Maximum number of rigid bodies supported.
    /// </summary>
    public int MaxBodies { get; set; } = 16384;

    /// <summary>
    /// Maximum number of contacts per frame.
    /// </summary>
    public int MaxContacts { get; set; } = 65536;

    /// <summary>
    /// Number of solver iterations per physics step.
    /// Higher values improve stability but reduce performance.
    /// </summary>
    public int SolverIterations { get; set; } = 8;

    /// <summary>
    /// Spatial hash grid cell size.
    /// Should be approximately 2x the largest object radius.
    /// </summary>
    public float GridCellSize { get; set; } = 2.0f;

    /// <summary>
    /// Enable continuous collision detection for fast-moving objects.
    /// </summary>
    public bool EnableCCD { get; set; } = false;

    /// <summary>
    /// Enable warm-starting for faster solver convergence.
    /// </summary>
    public bool EnableWarmStarting { get; set; } = true;

    /// <summary>
    /// Fall back to CPU physics if GPU is unavailable.
    /// </summary>
    public bool EnableCpuFallback { get; set; } = true;
}

/// <summary>
/// Performance metrics from GPU physics.
/// </summary>
public class GpuPhysicsMetrics
{
    /// <summary>
    /// Total time for last physics step (ms).
    /// </summary>
    [JsonPropertyName("totalStepTimeMs")]
    public float TotalStepTimeMs { get; set; }

    /// <summary>
    /// Broad phase collision detection time (ms).
    /// </summary>
    [JsonPropertyName("broadPhaseTimeMs")]
    public float BroadPhaseTimeMs { get; set; }

    /// <summary>
    /// Narrow phase collision detection time (ms).
    /// </summary>
    [JsonPropertyName("narrowPhaseTimeMs")]
    public float NarrowPhaseTimeMs { get; set; }

    /// <summary>
    /// Constraint solver time (ms).
    /// </summary>
    [JsonPropertyName("solverTimeMs")]
    public float SolverTimeMs { get; set; }

    /// <summary>
    /// Number of collision pairs detected in broad phase.
    /// </summary>
    [JsonPropertyName("pairCount")]
    public int PairCount { get; set; }

    /// <summary>
    /// Number of actual contacts after narrow phase.
    /// </summary>
    [JsonPropertyName("contactCount")]
    public int ContactCount { get; set; }

    /// <summary>
    /// Current body count.
    /// </summary>
    [JsonPropertyName("bodyCount")]
    public int BodyCount { get; set; }

    /// <summary>
    /// Whether GPU physics is active (vs CPU fallback).
    /// </summary>
    [JsonPropertyName("isGpuActive")]
    public bool IsGpuActive { get; set; }
}

/// <summary>
/// Interface for GPU-accelerated rigid body physics.
/// Extends IRigidPhysicsService with GPU-specific capabilities.
/// </summary>
public interface IGpuPhysicsService : IRigidPhysicsService
{
    /// <summary>
    /// Checks if WebGPU compute is available.
    /// </summary>
    Task<bool> IsGpuAvailableAsync();

    /// <summary>
    /// Gets current GPU physics metrics.
    /// </summary>
    Task<GpuPhysicsMetrics> GetMetricsAsync();

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    GpuPhysicsConfig Config { get; }

    /// <summary>
    /// Whether GPU physics is currently active.
    /// </summary>
    bool IsGpuActive { get; }
}

/// <summary>
/// GPU-accelerated physics service using WebGPU compute shaders.
/// Falls back to CPU physics when GPU is unavailable.
/// </summary>
public class GpuPhysicsService : IGpuPhysicsService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly IRigidPhysicsService _cpuFallback;
    private readonly GpuPhysicsConfig _config;

    private bool _initialized;
    private bool _gpuAvailable;
    private bool _useGpu;

    public GpuPhysicsConfig Config => _config;
    public bool IsGpuActive => _useGpu;

    public GpuPhysicsService(
        IJSRuntime jsRuntime,
        IRigidPhysicsService cpuFallback,
        GpuPhysicsConfig? config = null)
    {
        _jsRuntime = jsRuntime;
        _cpuFallback = cpuFallback;
        _config = config ?? new GpuPhysicsConfig();
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync()
    {
        return _initialized;
    }

    /// <inheritdoc />
    public async Task<bool> IsGpuAvailableAsync()
    {
        if (!_initialized)
        {
            return false;
        }

        try
        {
            return await _jsRuntime.InvokeAsync<bool>("GPUPhysicsModule.isAvailable");
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task InitializeAsync(SimulationSettings settings)
    {
        if (_initialized) return;

        try
        {
            // Try to initialize GPU physics
            var gpuConfig = new
            {
                gravity = settings.Gravity.ToArray(),
                timeStep = settings.TimeStep,
                subSteps = settings.SubSteps,
                solverIterations = _config.SolverIterations,
                gridCellSize = _config.GridCellSize,
                enableCCD = _config.EnableCCD
            };

            _gpuAvailable = await _jsRuntime.InvokeAsync<bool>(
                "GPUPhysicsModule.initialize",
                gpuConfig);

            if (_gpuAvailable)
            {
                _useGpu = true;
                Console.WriteLine("GPU physics initialized successfully");
            }
            else if (_config.EnableCpuFallback)
            {
                Console.WriteLine("GPU physics unavailable, using CPU fallback");
                await _cpuFallback.InitializeAsync(settings);
                _useGpu = false;
            }
            else
            {
                throw new InvalidOperationException("GPU physics not available and fallback disabled");
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"GPU physics initialization failed: {ex.Message}");

            if (_config.EnableCpuFallback)
            {
                await _cpuFallback.InitializeAsync(settings);
                _useGpu = false;
                _initialized = true;
            }
            else
            {
                throw;
            }
        }
    }

    /// <inheritdoc />
    public async Task CreateRigidBodyAsync(RigidBody body)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            var bodyData = new
            {
                id = body.Id,
                primitiveType = body.PrimitiveType.ToString().ToLower(),
                position = body.Transform.Position.ToArray(),
                rotation = body.Transform.Rotation.ToArray(),
                scale = body.Transform.Scale.ToArray(),
                mass = body.Mass,
                isStatic = body.IsStatic,
                restitution = body.Material.Restitution,
                staticFriction = body.Material.StaticFriction,
                dynamicFriction = body.Material.DynamicFriction,
                linearDamping = body.LinearDamping,
                angularDamping = body.AngularDamping,
                enableCCD = body.EnableCCD,
                linearVelocity = body.LinearVelocity.ToArray(),
                angularVelocity = body.AngularVelocity.ToArray()
            };

            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.createRigidBody", bodyData);
        }
        else
        {
            await _cpuFallback.CreateRigidBodyAsync(body);
        }
    }

    /// <inheritdoc />
    public async Task RemoveRigidBodyAsync(string id)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.removeRigidBody", id);
        }
        else
        {
            await _cpuFallback.RemoveRigidBodyAsync(id);
        }
    }

    /// <inheritdoc />
    public async Task UpdateRigidBodyAsync(RigidBody body)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            var updates = new
            {
                id = body.Id,
                mass = body.Mass,
                restitution = body.Material.Restitution,
                staticFriction = body.Material.StaticFriction,
                dynamicFriction = body.Material.DynamicFriction,
                linearDamping = body.LinearDamping,
                angularDamping = body.AngularDamping,
                enableCCD = body.EnableCCD
            };

            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.updateRigidBody", updates);
        }
        else
        {
            await _cpuFallback.UpdateRigidBodyAsync(body);
        }
    }

    /// <inheritdoc />
    public async Task ApplyImpulseAsync(string id, Vector3 impulse)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.applyImpulse", id, impulse.ToArray());
        }
        else
        {
            await _cpuFallback.ApplyImpulseAsync(id, impulse);
        }
    }

    /// <inheritdoc />
    public async Task ApplyForceAsync(string id, Vector3 force)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            // Convert force to impulse (F * dt)
            var impulse = new Vector3(
                force.X * 0.016f,
                force.Y * 0.016f,
                force.Z * 0.016f);
            await ApplyImpulseAsync(id, impulse);
        }
        else
        {
            await _cpuFallback.ApplyForceAsync(id, force);
        }
    }

    /// <inheritdoc />
    public async Task SetLinearVelocityAsync(string id, Vector3 velocity)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.setLinearVelocity", id, velocity.ToArray());
        }
        else
        {
            await _cpuFallback.SetLinearVelocityAsync(id, velocity);
        }
    }

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(SimulationSettings settings)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            var config = new
            {
                gravity = settings.Gravity.ToArray(),
                timeStep = settings.TimeStep,
                subSteps = settings.SubSteps
            };

            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.updateSettings", config);
        }
        else
        {
            await _cpuFallback.UpdateSettingsAsync(settings);
        }
    }

    /// <inheritdoc />
    public async Task StepAsync(float deltaTime)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.step", deltaTime);
        }
        else
        {
            await _cpuFallback.StepAsync(deltaTime);
        }
    }

    /// <inheritdoc />
    public async Task<RigidTransformBatch> GetTransformBatchAsync()
    {
        if (!_initialized) return new RigidTransformBatch();

        if (_useGpu)
        {
            return await _jsRuntime.InvokeAsync<RigidTransformBatch>("GPUPhysicsModule.getTransformBatch");
        }
        else
        {
            return await _cpuFallback.GetTransformBatchAsync();
        }
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.reset");
        }
        else
        {
            await _cpuFallback.ResetAsync();
        }
    }

    /// <inheritdoc />
    public async Task CreateGroundAsync(float restitution = 0.3f, float friction = 0.5f)
    {
        if (!_initialized) return;

        // GPU physics handles ground collision in the shader
        // CPU fallback needs explicit ground creation
        if (!_useGpu)
        {
            await _cpuFallback.CreateGroundAsync(restitution, friction);
        }
    }

    /// <inheritdoc />
    public async Task<GpuPhysicsMetrics> GetMetricsAsync()
    {
        if (!_initialized || !_useGpu)
        {
            return new GpuPhysicsMetrics
            {
                IsGpuActive = false
            };
        }

        try
        {
            var metrics = await _jsRuntime.InvokeAsync<GpuPhysicsMetrics>("GPUPhysicsModule.getMetrics");
            metrics.IsGpuActive = true;
            return metrics;
        }
        catch
        {
            return new GpuPhysicsMetrics
            {
                IsGpuActive = false
            };
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            if (_useGpu)
            {
                await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.dispose");
            }
            else if (_cpuFallback is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
        }
    }
}
