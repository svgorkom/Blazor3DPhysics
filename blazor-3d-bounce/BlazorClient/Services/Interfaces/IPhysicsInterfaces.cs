using BlazorClient.Models;

namespace BlazorClient.Services;

/// <summary>
/// Base interface for all physics services.
/// Follows Liskov Substitution Principle - all physics services can be treated uniformly.
/// </summary>
public interface IPhysicsService
{
    /// <summary>
    /// Checks if the physics engine is available.
    /// Rigid physics always returns true, soft physics may return false if WASM fails to load.
    /// </summary>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Steps the physics simulation.
    /// </summary>
    Task StepAsync(float deltaTime);

    /// <summary>
    /// Updates global simulation settings.
    /// </summary>
    Task UpdateSettingsAsync(SimulationSettings settings);

    /// <summary>
    /// Resets all bodies to their initial state.
    /// </summary>
    Task ResetAsync();

    /// <summary>
    /// Disposes physics resources.
    /// </summary>
    ValueTask DisposeAsync();
}

/// <summary>
/// Interface for cloth physics creation.
/// Follows Interface Segregation Principle - clients only depend on cloth-specific methods.
/// </summary>
public interface IClothPhysicsService
{
    /// <summary>
    /// Creates a cloth soft body.
    /// </summary>
    Task CreateClothAsync(SoftBody body);
}

/// <summary>
/// Interface for rope physics creation.
/// Follows Interface Segregation Principle - clients only depend on rope-specific methods.
/// </summary>
public interface IRopePhysicsService
{
    /// <summary>
    /// Creates a rope soft body.
    /// </summary>
    Task CreateRopeAsync(SoftBody body);
}

/// <summary>
/// Interface for volumetric physics creation.
/// Follows Interface Segregation Principle - clients only depend on volumetric-specific methods.
/// </summary>
public interface IVolumetricPhysicsService
{
    /// <summary>
    /// Creates a volumetric (jelly) soft body.
    /// </summary>
    Task CreateVolumetricAsync(SoftBody body);
}

/// <summary>
/// Interface for vertex pinning operations.
/// Follows Interface Segregation Principle - separates pinning from body creation.
/// </summary>
public interface IVertexPinningService
{
    /// <summary>
    /// Pins a vertex to a fixed world position.
    /// </summary>
    Task PinVertexAsync(string id, int vertexIndex, Vector3 worldPosition);

    /// <summary>
    /// Unpins a vertex.
    /// </summary>
    Task UnpinVertexAsync(string id, int vertexIndex);
}

/// <summary>
/// Interface for soft body vertex data retrieval.
/// Follows Interface Segregation Principle - separates data retrieval from physics operations.
/// </summary>
public interface ISoftBodyVertexDataService
{
    /// <summary>
    /// Gets deformed vertices for all soft bodies.
    /// </summary>
    Task<Dictionary<string, SoftBodyVertexData>> GetDeformedVerticesAsync();

    /// <summary>
    /// Gets deformed vertices for a specific soft body.
    /// </summary>
    Task<SoftBodyVertexData> GetDeformedVerticesAsync(string id);
}
