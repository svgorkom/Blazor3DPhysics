using Microsoft.JSInterop;

namespace BlazorClient.Services;

/// <summary>
/// Interface for soft body physics.
/// Extends IPhysicsService and segregated interfaces for full SOLID compliance.
/// </summary>
public interface ISoftPhysicsService : IPhysicsService, IClothPhysicsService, 
    IVolumetricPhysicsService, IVertexPinningService, ISoftBodyVertexDataService
{
    /// <summary>
    /// Initializes the soft body physics world.
    /// </summary>
    Task InitializeAsync(SimulationSettings settings);

    /// <summary>
    /// Removes a soft body from the physics world.
    /// </summary>
    Task RemoveSoftBodyAsync(string id);

    /// <summary>
    /// Updates soft body material properties.
    /// </summary>
    Task UpdateSoftBodyAsync(SoftBody body);
    
    /// <summary>
    /// Gets whether soft body physics is available (cached, non-async).
    /// </summary>
    bool IsAvailable { get; }
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
/// Implementation of soft body physics service.
/// Optimized for minimal interop overhead.
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

    /// <summary>
    /// Cached availability check - no async call needed after init.
    /// </summary>
    public bool IsAvailable => _initialized && _isAvailable;

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync()
    {
        return Task.FromResult(_initialized && _isAvailable);
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
        if (!IsAvailable) return;

        var clothData = new
        {
            id = body.Id,
            position = body.Transform.Position.ToArray(),
            width = body.Width,
            height = body.Height,
            // Cap resolution for performance
            resolutionX = Math.Min(body.ResolutionX, 15),
            resolutionY = Math.Min(body.ResolutionY, 15),
            structuralStiffness = body.Material.StructuralStiffness,
            shearStiffness = body.Material.ShearStiffness,
            bendingStiffness = body.Material.BendingStiffness,
            damping = body.Material.Damping,
            iterations = Math.Min(body.Material.ConstraintIterations, 6),
            pinnedVertices = body.PinnedVertices.ToArray()
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.createCloth", clothData);
    }

    /// <inheritdoc />
    public async Task CreateVolumetricAsync(SoftBody body)
    {
        if (!IsAvailable) return;

        var volumeData = new
        {
            id = body.Id,
            position = body.Transform.Position.ToArray(),
            radius = body.Radius,
            resolutionX = Math.Min(body.ResolutionX, 8),
            structuralStiffness = body.Material.StructuralStiffness,
            damping = body.Material.Damping,
            pressure = body.Material.Pressure,
            iterations = Math.Min(body.Material.ConstraintIterations, 6)
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.createVolumetric", volumeData);
    }

    /// <inheritdoc />
    public async Task RemoveSoftBodyAsync(string id)
    {
        if (!IsAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.removeSoftBody", id);
    }

    /// <inheritdoc />
    public async Task UpdateSoftBodyAsync(SoftBody body)
    {
        if (!IsAvailable) return;

        var updates = new
        {
            id = body.Id,
            damping = body.Material.Damping,
            iterations = Math.Min(body.Material.ConstraintIterations, 8)
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.updateSoftBody", updates);
    }

    /// <inheritdoc />
    public async Task PinVertexAsync(string id, int vertexIndex, Vector3 worldPosition)
    {
        if (!IsAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.pinVertex", id, vertexIndex, worldPosition.ToArray());
    }

    /// <inheritdoc />
    public async Task UnpinVertexAsync(string id, int vertexIndex)
    {
        if (!IsAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.unpinVertex", id, vertexIndex);
    }

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(SimulationSettings settings)
    {
        if (!IsAvailable) return;

        var config = new
        {
            gravity = settings.Gravity.ToArray(),
            timeStep = settings.TimeStep
        };

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.updateSettings", config);
    }

    /// <inheritdoc />
    public async Task StepAsync(float deltaTime)
    {
        if (!IsAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.step", deltaTime);
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, SoftBodyVertexData>> GetDeformedVerticesAsync()
    {
        if (!IsAvailable) 
            return new Dictionary<string, SoftBodyVertexData>();

        // Single batched call to get all vertex data
        return await _jsRuntime.InvokeAsync<Dictionary<string, SoftBodyVertexData>>(
            "SoftPhysicsModule.getAllDeformedVertices");
    }

    /// <inheritdoc />
    public async Task<SoftBodyVertexData> GetDeformedVerticesAsync(string id)
    {
        if (!IsAvailable) 
            return new SoftBodyVertexData();

        var vertices = await _jsRuntime.InvokeAsync<float[]?>("SoftPhysicsModule.getDeformedVertices", id);
        return new SoftBodyVertexData 
        { 
            Vertices = vertices ?? Array.Empty<float>(),
            Normals = null
        };
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        if (!IsAvailable) return;

        await _jsRuntime.InvokeVoidAsync("SoftPhysicsModule.reset");
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
