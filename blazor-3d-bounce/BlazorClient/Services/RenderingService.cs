using BlazorClient.Domain.Models;
using Microsoft.JSInterop;

namespace BlazorClient.Services;

/// <summary>
/// Implementation of the Babylon.js rendering service.
/// </summary>
public class RenderingService : IRenderingService
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

    /// <summary>
    /// Loads a GLTF/GLB model.
    /// </summary>
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

    /// <summary>
    /// Resizes the rendering viewport.
    /// </summary>
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
    public async Task<float> GetFpsAsync()
    {
        if (!_initialized) return 0f;

        try
        {
            return await _jsRuntime.InvokeAsync<float>("RenderingModule.getFps");
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// Gets the currently active rendering backend name.
    /// </summary>
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

    /// <summary>
    /// Detects available rendering backends.
    /// </summary>
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

    /// <summary>
    /// Gets performance metrics from the renderer.
    /// </summary>
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

    /// <summary>
    /// Runs a performance benchmark comparing available backends.
    /// </summary>
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
