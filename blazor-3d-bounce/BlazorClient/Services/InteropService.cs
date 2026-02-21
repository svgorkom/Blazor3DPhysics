using Microsoft.JSInterop;

namespace BlazorClient.Services;

/// <summary>
/// Interface for JavaScript interop batching and communication.
/// </summary>
public interface IInteropService
{
    /// <summary>
    /// Initializes the JavaScript modules (rendering and physics).
    /// </summary>
    Task InitializeAsync(string canvasId);

    /// <summary>
    /// Commits a batch of transform updates for rigid bodies.
    /// </summary>
    Task CommitRigidTransformsAsync(float[] transforms, string[] ids);

    /// <summary>
    /// Commits deformed vertex data for a single soft body.
    /// </summary>
    Task CommitSoftVerticesAsync(string id, float[] vertices, float[]? normals = null);

    /// <summary>
    /// Commits deformed vertex data for all soft bodies in a single call.
    /// </summary>
    Task CommitAllSoftVerticesAsync(Dictionary<string, SoftBodyVertexData> vertexData);

    /// <summary>
    /// Applies a single frame update (rigid transforms + soft vertices).
    /// </summary>
    Task ApplyFrameAsync(FrameData frameData);

    /// <summary>
    /// Gets the current performance statistics.
    /// </summary>
    Task<PerformanceStatsDto> GetPerformanceStatsAsync();

    /// <summary>
    /// Disposes resources.
    /// </summary>
    ValueTask DisposeAsync();
}

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
/// Implementation of JavaScript interop service with batching support.
/// Optimized to minimize number of JS interop calls.
/// </summary>
public class InteropService : IInteropService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public InteropService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
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
    public async Task CommitRigidTransformsAsync(float[] transforms, string[] ids)
    {
        if (!_initialized || ids.Length == 0) return;
        
        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.updateRigidTransforms", transforms, ids);
    }

    /// <inheritdoc />
    public async Task CommitSoftVerticesAsync(string id, float[] vertices, float[]? normals = null)
    {
        if (!_initialized || vertices.Length == 0) return;

        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.updateSoftBodyVertices", id, vertices, normals);
    }

    /// <inheritdoc />
    public async Task CommitAllSoftVerticesAsync(Dictionary<string, SoftBodyVertexData> vertexData)
    {
        if (!_initialized || vertexData.Count == 0) return;

        // Single batched call for all soft bodies
        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.updateAllSoftBodies", vertexData);
    }

    /// <inheritdoc />
    public async Task ApplyFrameAsync(FrameData frameData)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("PhysicsInterop.applyFrame", frameData);
    }

    /// <inheritdoc />
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
