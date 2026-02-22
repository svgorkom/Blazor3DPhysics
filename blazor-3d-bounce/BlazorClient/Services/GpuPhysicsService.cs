using Microsoft.JSInterop;
using System.Text.Json.Serialization;
using BlazorClient.Domain.Models;
using BlazorClient.Application.Services;
using AppGpuPhysicsConfig = BlazorClient.Application.Services.GpuPhysicsConfig;
using AppGpuPhysicsMetrics = BlazorClient.Application.Services.GpuPhysicsMetrics;

namespace BlazorClient.Services;

/// <summary>
/// Performance metrics from GPU physics (JS interop compatible).
/// </summary>
/// <remarks>
/// <para>
/// This internal type is used for JSON deserialization from JavaScript.
/// It maps to the Application layer's <see cref="AppGpuPhysicsMetrics"/>.
/// </para>
/// </remarks>
internal class JsGpuPhysicsMetrics
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

    /// <summary>
    /// Converts to Application layer metrics type.
    /// </summary>
    public AppGpuPhysicsMetrics ToAppMetrics() => new()
    {
        TotalStepTimeMs = TotalStepTimeMs,
        BroadphaseTimeMs = BroadPhaseTimeMs,
        NarrowphaseTimeMs = NarrowPhaseTimeMs,
        SolverTimeMs = SolverTimeMs,
        ContactCount = ContactCount,
        BodyCount = BodyCount,
        IsGpuActive = IsGpuActive
    };
}

/// <summary>
/// Extended GPU physics service interface with UI-specific properties.
/// </summary>
/// <remarks>
/// <para>
/// Extends the Application layer's <see cref="Application.Services.IGpuPhysicsService"/>
/// with additional properties needed by the UI layer.
/// </para>
/// </remarks>
public interface IGpuPhysicsService : IRigidPhysicsService
{
    /// <summary>
    /// Checks if WebGPU compute is available.
    /// </summary>
    /// <returns>True if GPU physics is available and active.</returns>
    Task<bool> IsGpuAvailableAsync();

    /// <summary>
    /// Gets current GPU physics metrics.
    /// </summary>
    /// <returns>Performance metrics from the GPU physics engine.</returns>
    Task<AppGpuPhysicsMetrics> GetMetricsAsync();

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    AppGpuPhysicsConfig Config { get; }

    /// <summary>
    /// Whether GPU physics is currently active.
    /// </summary>
    bool IsGpuActive { get; }
}

/// <summary>
/// GPU-accelerated physics service using WebGPU compute shaders.
/// </summary>
/// <remarks>
/// <para>
/// Implements GPU-accelerated rigid body physics with automatic CPU fallback
/// when WebGPU is unavailable.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Presentation/Services Layer (UI-specific implementation).
/// </para>
/// <para>
/// <strong>Fallback Strategy:</strong> When GPU initialization fails and
/// <see cref="AppGpuPhysicsConfig.EnableCpuFallback"/> is true, automatically
/// delegates to <see cref="CpuPhysicsService"/>.
/// </para>
/// </remarks>
public class GpuPhysicsService : BlazorClient.Application.Services.IGpuPhysicsService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly CpuPhysicsService _cpuFallback;
    private readonly AppGpuPhysicsConfig _config;

    private bool _initialized;
    private bool _gpuAvailable;
    private bool _useGpu;

    /// <inheritdoc />
    public AppGpuPhysicsConfig Config => _config;

    /// <inheritdoc />
    public bool IsGpuActive => _useGpu;

    /// <summary>
    /// Initializes a new GPU physics service.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop.</param>
    /// <param name="cpuFallback">The CPU physics service for fallback.</param>
    /// <param name="config">Optional GPU physics configuration.</param>
    public GpuPhysicsService(
        IJSRuntime jsRuntime,
        CpuPhysicsService cpuFallback,
        AppGpuPhysicsConfig? config = null)
    {
        _jsRuntime = jsRuntime;
        _cpuFallback = cpuFallback;
        _config = config ?? new AppGpuPhysicsConfig();
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
    public async Task CreateGroundAsync(float restitution = 0.3f, float friction = 0.5f)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.createGround", restitution, friction);
        }
        else
        {
            await _cpuFallback.CreateGroundAsync(restitution, friction);
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
    public async Task<Dictionary<string, TransformData>> GetTransformBatchAsync()
    {
        if (!_initialized) return new Dictionary<string, TransformData>();

        if (_useGpu)
        {
            // Get from GPU and convert to Dictionary
            var batch = await _jsRuntime.InvokeAsync<RigidTransformBatch>("GPUPhysicsModule.getTransformBatch");
            return ConvertBatchToDictionary(batch);
        }
        else
        {
            return await _cpuFallback.GetTransformBatchAsync();
        }
    }

    /// <inheritdoc />
    public async Task UpdateTransformAsync(string id, TransformData transform)
    {
        if (!_initialized) return;

        if (_useGpu)
        {
            await _jsRuntime.InvokeVoidAsync("GPUPhysicsModule.updateTransform", id, 
                transform.Position.ToArray(), transform.Rotation.ToArray());
        }
        else
        {
            await _cpuFallback.UpdateTransformAsync(id, transform);
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
    public async Task<AppGpuPhysicsMetrics> GetMetricsAsync()
    {
        if (!_initialized || !_useGpu)
        {
            return new AppGpuPhysicsMetrics { IsGpuActive = false };
        }

        try
        {
            var jsMetrics = await _jsRuntime.InvokeAsync<JsGpuPhysicsMetrics>("GPUPhysicsModule.getMetrics");
            var metrics = jsMetrics.ToAppMetrics();
            metrics.IsGpuActive = true;
            return metrics;
        }
        catch
        {
            return new AppGpuPhysicsMetrics { IsGpuActive = false };
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

    private static Dictionary<string, TransformData> ConvertBatchToDictionary(RigidTransformBatch batch)
    {
        var result = new Dictionary<string, TransformData>();
        for (int i = 0; i < batch.Ids.Length; i++)
        {
            var offset = i * 7;
            result[batch.Ids[i]] = new TransformData
            {
                Position = new Vector3(batch.Transforms[offset], batch.Transforms[offset + 1], batch.Transforms[offset + 2]),
                Rotation = new Quaternion(batch.Transforms[offset + 3], batch.Transforms[offset + 4], batch.Transforms[offset + 5], batch.Transforms[offset + 6]),
                Scale = Vector3.One
            };
        }
        return result;
    }
}
