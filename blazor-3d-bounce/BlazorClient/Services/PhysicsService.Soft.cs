using BlazorClient.Models;
using Microsoft.JSInterop;

namespace BlazorClient.Services;

/// <summary>
/// Interface for soft body physics using Ammo.js.
/// </summary>
public interface ISoftPhysicsService
{
    /// <summary>
    /// Initializes the soft body physics world.
    /// </summary>
    Task InitializeAsync(SimulationSettings settings);

    /// <summary>
    /// Creates a cloth soft body.
    /// </summary>
    Task CreateClothAsync(SoftBody body);

    /// <summary>
    /// Creates a rope soft body.
    /// </summary>
    Task CreateRopeAsync(SoftBody body);

    /// <summary>
    /// Creates a volumetric (jelly) soft body.
    /// </summary>
    Task CreateVolumetricAsync(SoftBody body);

    /// <summary>
    /// Removes a soft body from the physics world.
    /// </summary>
    Task RemoveSoftBodyAsync(string id);

    /// <summary>
    /// Updates soft body material properties.
    /// </summary>
    Task UpdateSoftBodyAsync(SoftBody body);

    /// <summary>
    /// Pins a vertex to a fixed world position.
    /// </summary>
    Task PinVertexAsync(string id, int vertexIndex, Vector3 worldPosition);

    /// <summary>
    /// Unpins a vertex.
    /// </summary>
    Task UnpinVertexAsync(string id, int vertexIndex);

    /// <summary>
    /// Updates global simulation settings.
    /// </summary>
    Task UpdateSettingsAsync(SimulationSettings settings);

    /// <summary>
    /// Steps the soft body simulation.
    /// </summary>
    Task StepAsync(float deltaTime);

    /// <summary>
    /// Gets deformed vertices for all soft bodies.
    /// </summary>
    Task<Dictionary<string, SoftBodyVertexData>> GetDeformedVerticesAsync();

    /// <summary>
    /// Gets deformed vertices for a specific soft body.
    /// </summary>
    Task<SoftBodyVertexData> GetDeformedVerticesAsync(string id);

    /// <summary>
    /// Resets all soft bodies to their initial state.
    /// </summary>
    Task ResetAsync();

    /// <summary>
    /// Checks if soft body physics is available (fallback detection).
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Disposes physics resources.
    /// </summary>
    ValueTask DisposeAsync();
}

/// <summary>
/// Deformed vertex data for a soft body.
/// </summary>
public class SoftBodyVertexData
{
    /// <summary>
    /// Flat array of vertex positions: [x, y, z, x, y, z, ...].
    /// </summary>
    public float[] Vertices { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Flat array of vertex normals: [nx, ny, nz, ...].
    /// </summary>
    public float[]? Normals { get; set; }

    /// <summary>
    /// Number of vertices.
    /// </summary>
    public int VertexCount => Vertices.Length / 3;
}

/// <summary>
/// Implementation of soft body physics service using Ammo.js.
/// </summary>
public class SoftPhysicsService : ISoftPhysicsService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;
    private bool _isAvailable;

    public SoftPhysicsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async Task InitializeAsync(SimulationSettings settings)
    {
        if (_initialized) return;

        try
        {
            var config = new
            {
                gravity = settings.Gravity.ToArray(),
                timeStep = settings.TimeStep,
                subSteps = settings.SubSteps
            };

            _isAvailable = await _jsRuntime.InvokeAsync<bool>("SoftPhysicsModule.initialize", config);
            _initialized = true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Soft body physics initialization failed: {ex.Message}");
            _isAvailable = false;
            _initialized = true;
        }
    }

    /// <inheritdoc />
    public async Task CreateClothAsync(SoftBody body)
    {
        if (!_initialized || !_isAvailable) return;

        var clothData = new
        {
            id = body.Id,
            position = body.Transform.Position.ToArray(),
            width = body.Width,
            height = body.Height,
            resolutionX = body.ResolutionX,
            resolutionY = body.ResolutionY,
            mass = body.Material.MassDensity * body.Width * body.Height,
            structuralStiffness = body.Material.StructuralStiffness,
            shearStiffness = body.Material.ShearStiffness,
            bendingStiffness = body.Material.BendingStiffness,
            damping = body.Material.Damping,
            selfCollision = body.Material.SelfCollision,
            collisionMargin = body.Material.CollisionMargin,
            thickness = body.Material.Thickness,
            iterations = body.Material.ConstraintIterations,
            pinnedVertices = body.PinnedVertices.ToArray()
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.createCloth", clothData);
    }

    /// <inheritdoc />
    public async Task CreateRopeAsync(SoftBody body)
    {
        if (!_initialized || !_isAvailable) return;

        var ropeData = new
        {
            id = body.Id,
            position = body.Transform.Position.ToArray(),
            length = body.Length,
            segments = body.Segments,
            radius = body.Material.Thickness,
            mass = body.Material.MassDensity * body.Length,
            structuralStiffness = body.Material.StructuralStiffness,
            bendingStiffness = body.Material.BendingStiffness,
            damping = body.Material.Damping,
            iterations = body.Material.ConstraintIterations,
            pinnedVertices = body.PinnedVertices.ToArray()
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.createRope", ropeData);
    }

    /// <inheritdoc />
    public async Task CreateVolumetricAsync(SoftBody body)
    {
        if (!_initialized || !_isAvailable) return;

        var volumeData = new
        {
            id = body.Id,
            position = body.Transform.Position.ToArray(),
            width = body.Width,
            height = body.Height,
            depth = body.Depth,
            radius = body.Radius,
            resolutionX = body.ResolutionX,
            resolutionY = body.ResolutionY,
            mass = body.Material.MassDensity * body.Width * body.Height * body.Depth,
            structuralStiffness = body.Material.StructuralStiffness,
            shearStiffness = body.Material.ShearStiffness,
            bendingStiffness = body.Material.BendingStiffness,
            damping = body.Material.Damping,
            pressure = body.Material.Pressure,
            volumeConservation = body.Material.VolumeConservation,
            selfCollision = body.Material.SelfCollision,
            collisionMargin = body.Material.CollisionMargin,
            iterations = body.Material.ConstraintIterations
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.createVolumetric", volumeData);
    }

    /// <inheritdoc />
    public async Task RemoveSoftBodyAsync(string id)
    {
        if (!_initialized || !_isAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.removeSoftBody", id);
    }

    /// <inheritdoc />
    public async Task UpdateSoftBodyAsync(SoftBody body)
    {
        if (!_initialized || !_isAvailable) return;

        var updates = new
        {
            id = body.Id,
            structuralStiffness = body.Material.StructuralStiffness,
            shearStiffness = body.Material.ShearStiffness,
            bendingStiffness = body.Material.BendingStiffness,
            damping = body.Material.Damping,
            pressure = body.Material.Pressure,
            volumeConservation = body.Material.VolumeConservation,
            selfCollision = body.Material.SelfCollision,
            iterations = body.Material.ConstraintIterations
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.updateSoftBody", updates);
    }

    /// <inheritdoc />
    public async Task PinVertexAsync(string id, int vertexIndex, Vector3 worldPosition)
    {
        if (!_initialized || !_isAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.pinVertex", id, vertexIndex, worldPosition.ToArray());
    }

    /// <inheritdoc />
    public async Task UnpinVertexAsync(string id, int vertexIndex)
    {
        if (!_initialized || !_isAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.unpinVertex", id, vertexIndex);
    }

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(SimulationSettings settings)
    {
        if (!_initialized || !_isAvailable) return;

        var config = new
        {
            gravity = settings.Gravity.ToArray(),
            timeStep = settings.TimeStep,
            subSteps = settings.SubSteps
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.updateSettings", config);
    }

    /// <inheritdoc />
    public async Task StepAsync(float deltaTime)
    {
        if (!_initialized || !_isAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.step", deltaTime);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, SoftBodyVertexData>> GetDeformedVerticesAsync()
    {
        if (!_initialized || !_isAvailable) 
            return new Dictionary<string, SoftBodyVertexData>();

        return await _jsRuntime.InvokeAsync<Dictionary<string, SoftBodyVertexData>>(
            "SoftPhysicsModule.getAllDeformedVertices");
    }

    /// <inheritdoc />
    public async Task<SoftBodyVertexData> GetDeformedVerticesAsync(string id)
    {
        if (!_initialized || !_isAvailable) 
            return new SoftBodyVertexData();

        return await _jsRuntime.InvokeAsync<SoftBodyVertexData>(
            "SoftPhysicsModule.getDeformedVertices", id);
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        if (!_initialized || !_isAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.reset");
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync()
    {
        if (!_initialized)
        {
            return false;
        }

        return _isAvailable;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_initialized && _isAvailable)
        {
            await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.dispose");
        }
    }
}
