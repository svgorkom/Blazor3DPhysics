using Microsoft.JSInterop;
using BlazorClient.Application.Services;

namespace BlazorClient.Services;

/// <summary>
/// Frame data for batched updates.
/// </summary>
public class FrameData
{
    public float DeltaTime { get; set; }
    public float[] RigidTransforms { get; set; } = Array.Empty<float>();
    public string[] RigidIds { get; set; } = Array.Empty<string>();
    public Dictionary<string, SoftBodyFrameData> SoftBodies { get; set; } = new();
}

/// <summary>
/// Per-soft-body frame data.
/// </summary>
public class SoftBodyFrameData
{
    public float[] Vertices { get; set; } = Array.Empty<float>();
    public float[]? Normals { get; set; }
}

/// <summary>
/// Performance statistics DTO from JavaScript.
/// </summary>
public class PerformanceStatsDto
{
    public float Fps { get; set; }
    public float FrameTimeMs { get; set; }
    public float PhysicsTimeMs { get; set; }
    public float RenderTimeMs { get; set; }
}

/// <summary>
/// Implementation of JavaScript interop service.
/// Implements the Application layer IInteropService interface.
/// </summary>
public class InteropService : BlazorClient.Application.Services.IInteropService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public InteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            // Initialize general interop bridge
            await _jsRuntime.InvokeVoidAsync("PhysicsInterop.initialize");
            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize interop: {ex.Message}");
            // Don't throw - interop may not be required for all operations
            _initialized = true;
        }
    }

    /// <summary>
    /// Initializes with a canvas ID for rendering.
    /// </summary>
    public async Task InitializeAsync(string canvasId)
    {
        if (_initialized) return;

        try
        {
            await _jsRuntime.InvokeVoidAsync("PhysicsInterop.initialize", canvasId);
            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to initialize interop: {ex.Message}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task InvokeVoidAsync(string identifier, params object?[] args)
    {
        await _jsRuntime.InvokeVoidAsync(identifier, args);
    }

    /// <inheritdoc />
    public async Task<T> InvokeAsync<T>(string identifier, params object?[] args)
    {
        return await _jsRuntime.InvokeAsync<T>(identifier, args);
    }

    /// <summary>
    /// Commits a batch of transform updates for rigid bodies.
    /// </summary>
    public async Task CommitRigidTransformsAsync(float[] transforms, string[] ids)
    {
        if (!_initialized || ids.Length == 0) return;
        
        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.updateRigidTransforms", transforms, ids);
    }

    /// <summary>
    /// Commits deformed vertex data for a single soft body.
    /// </summary>
    public async Task CommitSoftVerticesAsync(string id, float[] vertices, float[]? normals = null)
    {
        if (!_initialized || vertices.Length == 0) return;

        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.updateSoftBodyVertices", id, vertices, normals);
    }

    /// <summary>
    /// Commits deformed vertex data for all soft bodies in a single call.
    /// </summary>
    public async Task CommitAllSoftVerticesAsync(Dictionary<string, SoftBodyVertexData> vertexData)
    {
        if (!_initialized || vertexData.Count == 0) return;

        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.updateAllSoftBodies", vertexData);
    }

    /// <summary>
    /// Applies a single frame update (rigid transforms + soft vertices).
    /// </summary>
    public async Task ApplyFrameAsync(FrameData frameData)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.applyFrame", frameData);
    }

    /// <summary>
    /// Gets the current performance statistics.
    /// </summary>
    public async Task<PerformanceStatsDto> GetPerformanceStatsAsync()
    {
        if (!_initialized)
        {
            return new PerformanceStatsDto();
        }

        return await _jsRuntime.InvokeAsync<PerformanceStatsDto>("PhysicsInterop.getPerformanceStats");
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
