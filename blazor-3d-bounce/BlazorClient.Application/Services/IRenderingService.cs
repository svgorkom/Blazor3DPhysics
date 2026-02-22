using BlazorClient.Domain.Models;

namespace BlazorClient.Application.Services;

/// <summary>
/// Interface for 3D rendering operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface abstracts the rendering engine (e.g., Babylon.js, Three.js)
/// from the application logic. Implementations handle JavaScript interop
/// and graphics API calls.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// Implementations belong in the Infrastructure or UI layer.
/// </para>
/// <para>
/// <strong>Design Pattern:</strong> Port/Adapter (Hexagonal Architecture).
/// This is a "port" that defines what the application needs from a renderer.
/// </para>
/// </remarks>
public interface IRenderingService : IAsyncDisposable
{
    /// <summary>
    /// Initializes the rendering engine with the specified canvas.
    /// </summary>
    /// <param name="canvasId">The HTML canvas element ID to render to.</param>
    /// <param name="settings">Initial render settings (shadows, post-processing, etc.).</param>
    /// <returns>A task representing the asynchronous initialization.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the canvas element is not found or WebGL/WebGPU is unavailable.
    /// </exception>
    Task InitializeAsync(string canvasId, RenderSettings settings);

    /// <summary>
    /// Creates a visual mesh for a rigid body.
    /// </summary>
    /// <param name="body">The rigid body containing shape, transform, and material data.</param>
    /// <returns>A task representing the asynchronous mesh creation.</returns>
    /// <remarks>
    /// The mesh ID will match <see cref="RigidBody.Id"/> for synchronization
    /// with physics updates.
    /// </remarks>
    Task CreateRigidMeshAsync(RigidBody body);

    /// <summary>
    /// Creates a visual mesh for a soft body.
    /// </summary>
    /// <param name="body">The soft body containing geometry and material data.</param>
    /// <returns>A task representing the asynchronous mesh creation.</returns>
    /// <remarks>
    /// Soft body meshes support dynamic vertex updates via
    /// <see cref="UpdateSoftMeshVerticesAsync"/>.
    /// </remarks>
    Task CreateSoftMeshAsync(SoftBody body);

    /// <summary>
    /// Updates the transform of a mesh.
    /// </summary>
    /// <param name="id">The mesh unique identifier.</param>
    /// <param name="transform">The new transform data (position, rotation, scale).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateMeshTransformAsync(string id, TransformData transform);

    /// <summary>
    /// Updates soft body mesh vertices for deformation rendering.
    /// </summary>
    /// <param name="id">The mesh unique identifier.</param>
    /// <param name="vertices">
    /// The deformed vertex positions as a flat array [x1,y1,z1,x2,y2,z2,...].
    /// </param>
    /// <param name="normals">
    /// Optional vertex normals as a flat array. If null, normals will be
    /// recalculated automatically.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSoftMeshVerticesAsync(string id, float[] vertices, float[]? normals = null);

    /// <summary>
    /// Removes a mesh from the scene.
    /// </summary>
    /// <param name="id">The mesh unique identifier to remove.</param>
    /// <returns>A task representing the asynchronous removal.</returns>
    Task RemoveMeshAsync(string id);

    /// <summary>
    /// Sets the selected object for visual highlighting.
    /// </summary>
    /// <param name="id">
    /// The ID of the object to select, or <c>null</c> to clear selection.
    /// </param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetSelectionAsync(string? id);

    /// <summary>
    /// Updates render settings dynamically.
    /// </summary>
    /// <param name="settings">The new render settings to apply.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateRenderSettingsAsync(RenderSettings settings);

    /// <summary>
    /// Gets information about the active rendering backend.
    /// </summary>
    /// <returns>
    /// Renderer information including backend type (WebGL2, WebGPU) and GPU details.
    /// </returns>
    Task<RendererInfo> GetRendererInfoAsync();

    /// <summary>
    /// Gets the current frames per second.
    /// </summary>
    /// <returns>The current FPS value.</returns>
    Task<float> GetFpsAsync();
}
