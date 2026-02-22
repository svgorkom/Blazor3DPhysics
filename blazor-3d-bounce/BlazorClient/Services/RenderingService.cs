using BlazorClient.Models;
using Microsoft.JSInterop;
using BlazorClient.Domain.Models;

namespace BlazorClient.Services;

/// <summary>
/// Interface for the Babylon.js rendering service.
/// </summary>
public interface IRenderingService
{
    /// <summary>
    /// Initializes the rendering engine with the specified canvas.
    /// </summary>
    Task InitializeAsync(string canvasId, RenderSettings settings);

    /// <summary>
    /// Creates a visual mesh for a rigid body.
    /// </summary>
    Task CreateRigidMeshAsync(RigidBody body);

    /// <summary>
    /// Creates a visual mesh for a soft body.
    /// </summary>
    Task CreateSoftMeshAsync(SoftBody body);

    /// <summary>
    /// Updates the transform of a mesh.
    /// </summary>
    Task UpdateMeshTransformAsync(string id, TransformData transform);

    /// <summary>
    /// Updates the vertices of a soft body mesh.
    /// </summary>
    Task UpdateSoftMeshVerticesAsync(string id, float[] vertices, float[]? normals = null);

    /// <summary>
    /// Removes a mesh from the scene.
    /// </summary>
    Task RemoveMeshAsync(string id);

    /// <summary>
    /// Loads a GLTF/GLB model.
    /// </summary>
    Task<string> LoadModelAsync(string path);

    /// <summary>
    /// Sets the selected object highlight.
    /// </summary>
    Task SetSelectionAsync(string? id);

    /// <summary>
    /// Updates render settings.
    /// </summary>
    Task UpdateRenderSettingsAsync(RenderSettings settings);

    /// <summary>
    /// Resizes the rendering viewport.
    /// </summary>
    Task ResizeAsync();
    
    /// <summary>
    /// Gets information about the active rendering backend.
    /// </summary>
    Task<RendererInfo> GetRendererInfoAsync();
    
    /// <summary>
    /// Gets the currently active rendering backend name.
    /// </summary>
    Task<string> GetActiveBackendAsync();
    
    /// <summary>
    /// Detects available rendering backends.
    /// </summary>
    Task<RendererCapabilities> DetectBackendsAsync();
    
    /// <summary>
    /// Gets performance metrics from the renderer.
    /// </summary>
    Task<RendererPerformanceMetrics> GetPerformanceMetricsAsync();
    
    /// <summary>
    /// Runs a performance benchmark comparing available backends.
    /// </summary>
    Task<BenchmarkResults> RunBenchmarkAsync(string canvasId);

    /// <summary>
    /// Disposes the rendering resources.
    /// </summary>
    ValueTask DisposeAsync();
}

/// <summary>
/// Capabilities of available rendering backends.
/// </summary>
public class RendererCapabilities
{
    public BackendCapability WebGPU { get; set; } = new();
    public BackendCapability WebGL2 { get; set; } = new();
    public BackendCapability WebGL { get; set; } = new();
}

/// <summary>
/// Capability information for a single backend.
/// </summary>
public class BackendCapability
{
    public bool IsSupported { get; set; }
    public string? Vendor { get; set; }
    public string? Renderer { get; set; }
    public string? Version { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Performance metrics from the renderer.
/// </summary>
public class RendererPerformanceMetrics
{
    public string Backend { get; set; } = "Unknown";
    public float Fps { get; set; }
    public float FrameTimeMs { get; set; }
    public float Percentile95 { get; set; }
    public float Percentile99 { get; set; }
    public float MinFrameTime { get; set; }
    public float MaxFrameTime { get; set; }
    public int DrawCalls { get; set; }
    public int TriangleCount { get; set; }
}

/// <summary>
/// Results from a rendering benchmark.
/// </summary>
public class BenchmarkResults
{
    public BenchmarkResult? WebGPU { get; set; }
    public BenchmarkResult? WebGL2 { get; set; }
    public string? Recommendation { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Benchmark result for a single backend.
/// </summary>
public class BenchmarkResult
{
    public float AvgFrameTime { get; set; }
    public float MinFrameTime { get; set; }
    public float MaxFrameTime { get; set; }
    public int Iterations { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Implementation of the Babylon.js rendering service.
/// </summary>
public class RenderingService : IRenderingService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public RenderingService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(string canvasId, RenderSettings settings)
    {
        if (_initialized) return;

        // Convert settings to JS-compatible object
        var jsSettings = new
        {
            enableShadows = settings.EnableShadows,
            shadowMapSize = settings.ShadowMapSize,
            enableSSAO = settings.EnableSSAO,
            enableFXAA = settings.EnableFXAA,
            showGrid = settings.ShowGrid,
            showAxes = settings.ShowAxes,
            showWireframe = settings.ShowWireframe,
            showBoundingBoxes = settings.ShowBoundingBoxes,
            showDebugOverlay = settings.ShowDebugOverlay,
            hdriPath = settings.HdriPath,
            preferredBackend = settings.PreferredBackend.ToString()
        };

        await _jsRuntime.InvokeVoidAsync("RenderingModule.initialize", canvasId, jsSettings);
        _initialized = true;
    }

    /// <inheritdoc />
    public async Task CreateRigidMeshAsync(RigidBody body)
    {
        if (!_initialized) return;

        var meshData = new
        {
            id = body.Id,
            name = body.Name,
            primitiveType = body.PrimitiveType.ToString().ToLower(),
            position = body.Transform.Position.ToArray(),
            rotation = body.Transform.Rotation.ToArray(),
            scale = body.Transform.Scale.ToArray(),
            materialPreset = body.Material.Name.ToLower(),
            isStatic = body.IsStatic,
            meshPath = body.MeshPath
        };

        await _jsRuntime.InvokeVoidAsync("RenderingModule.createRigidMesh", meshData);
    }

    /// <inheritdoc />
    public async Task CreateSoftMeshAsync(SoftBody body)
    {
        if (!_initialized) return;

        var meshData = new
        {
            id = body.Id,
            name = body.Name,
            type = body.Type.ToString().ToLower(),
            position = body.Transform.Position.ToArray(),
            resolutionX = body.ResolutionX,
            resolutionY = body.ResolutionY,
            segments = body.Segments,
            width = body.Width,
            height = body.Height,
            depth = body.Depth,
            length = body.Length,
            radius = body.Radius,
            pinnedVertices = body.PinnedVertices,
            meshPath = body.MeshPath
        };

        await _jsRuntime.InvokeVoidAsync("RenderingModule.createSoftMesh", meshData);
    }

    /// <inheritdoc />
    public async Task UpdateMeshTransformAsync(string id, TransformData transform)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RenderingModule.updateMeshTransform", 
            id, 
            transform.Position.ToArray(), 
            transform.Rotation.ToArray(),
            transform.Scale.ToArray());
    }

    /// <inheritdoc />
    public async Task UpdateSoftMeshVerticesAsync(string id, float[] vertices, float[]? normals = null)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RenderingModule.updateSoftMeshVertices", id, vertices, normals);
    }

    /// <inheritdoc />
    public async Task RemoveMeshAsync(string id)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RenderingModule.removeMesh", id);
    }

    /// <inheritdoc />
    public async Task<string> LoadModelAsync(string path)
    {
        if (!_initialized) return string.Empty;

        return await _jsRuntime.InvokeAsync<string>("RenderingModule.loadModel", path);
    }

    /// <inheritdoc />
    public async Task SetSelectionAsync(string? id)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RenderingModule.setSelection", id);
    }

    /// <inheritdoc />
    public async Task UpdateRenderSettingsAsync(RenderSettings settings)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RenderingModule.updateSettings", settings);
    }

    /// <inheritdoc />
    public async Task ResizeAsync()
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RenderingModule.resize");
    }

    /// <inheritdoc />
    public async Task<RendererInfo> GetRendererInfoAsync()
    {
        if (!_initialized)
        {
            return new RendererInfo { Backend = "Not Initialized" };
        }

        try
        {
            return await _jsRuntime.InvokeAsync<RendererInfo>("RenderingModule.getRendererInfo");
        }
        catch
        {
            return new RendererInfo { Backend = "Unknown" };
        }
    }

    /// <inheritdoc />
    public async Task<string> GetActiveBackendAsync()
    {
        if (!_initialized) return "Not Initialized";

        try
        {
            return await _jsRuntime.InvokeAsync<string>("RenderingModule.getActiveBackend");
        }
        catch
        {
            return "Unknown";
        }
    }

    /// <inheritdoc />
    public async Task<RendererCapabilities> DetectBackendsAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<RendererCapabilities>("RenderingModule.detectBackends");
        }
        catch
        {
            return new RendererCapabilities();
        }
    }

    /// <inheritdoc />
    public async Task<RendererPerformanceMetrics> GetPerformanceMetricsAsync()
    {
        if (!_initialized)
        {
            return new RendererPerformanceMetrics();
        }

        try
        {
            return await _jsRuntime.InvokeAsync<RendererPerformanceMetrics>("RenderingModule.getPerformanceMetrics");
        }
        catch
        {
            return new RendererPerformanceMetrics();
        }
    }

    /// <inheritdoc />
    public async Task<BenchmarkResults> RunBenchmarkAsync(string canvasId)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<BenchmarkResults>("RenderingModule.runBenchmark", canvasId);
        }
        catch (Exception ex)
        {
            return new BenchmarkResults { Error = ex.Message };
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            await _jsRuntime.InvokeVoidAsync("RenderingModule.dispose");
        }
    }
}
