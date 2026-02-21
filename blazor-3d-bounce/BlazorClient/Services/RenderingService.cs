using BlazorClient.Models;
using Microsoft.JSInterop;

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
    /// Disposes the rendering resources.
    /// </summary>
    ValueTask DisposeAsync();
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

        await _jsRuntime.InvokeVoidAsync("RenderingModule.initialize", canvasId, settings);
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
    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            await _jsRuntime.InvokeVoidAsync("RenderingModule.dispose");
        }
    }
}
