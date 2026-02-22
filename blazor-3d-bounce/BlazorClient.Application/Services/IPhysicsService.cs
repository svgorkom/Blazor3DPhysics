using BlazorClient.Domain.Models;

namespace BlazorClient.Application.Services;

/// <summary>
/// Base interface for all physics services.
/// </summary>
/// <remarks>
/// <para>
/// This interface follows the Liskov Substitution Principle - all physics services
/// (CPU, GPU, rigid, soft) can be treated uniformly through this base contract.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// Implementations belong in Infrastructure or UI layers.
/// </para>
/// </remarks>
public interface IPhysicsService
{
    /// <summary>
    /// Checks if the physics engine is available and ready for use.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the physics engine is available; otherwise, <c>false</c>.
    /// Rigid physics typically always returns true, while soft physics may return
    /// false if WASM modules fail to load.
    /// </returns>
    Task<bool> IsAvailableAsync();

    /// <summary>
    /// Advances the physics simulation by the specified time step.
    /// </summary>
    /// <param name="deltaTime">Time step in seconds. Typical values are 1/60 or 1/120.</param>
    /// <returns>A task representing the asynchronous physics step operation.</returns>
    Task StepAsync(float deltaTime);

    /// <summary>
    /// Updates global simulation settings such as gravity and time step.
    /// </summary>
    /// <param name="settings">The new simulation settings to apply.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSettingsAsync(SimulationSettings settings);

    /// <summary>
    /// Resets all physics bodies to their initial state.
    /// </summary>
    /// <remarks>
    /// This restores positions, rotations, and velocities to their values
    /// when the bodies were first created.
    /// </remarks>
    /// <returns>A task representing the asynchronous reset operation.</returns>
    Task ResetAsync();

    /// <summary>
    /// Releases all physics resources.
    /// </summary>
    /// <returns>A value task representing the asynchronous dispose operation.</returns>
    ValueTask DisposeAsync();
}

/// <summary>
/// Interface for rigid body physics operations.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="IPhysicsService"/> with rigid body-specific operations
/// including body creation, removal, and force/impulse application.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// </remarks>
public interface IRigidPhysicsService : IPhysicsService
{
    /// <summary>
    /// Initializes the rigid physics engine with the specified settings.
    /// </summary>
    /// <param name="settings">Initial simulation settings including gravity and time step.</param>
    /// <returns>A task representing the asynchronous initialization.</returns>
    Task InitializeAsync(SimulationSettings settings);

    /// <summary>
    /// Creates a new rigid body in the physics world.
    /// </summary>
    /// <param name="body">The rigid body configuration including shape, mass, and material.</param>
    /// <returns>A task representing the asynchronous creation operation.</returns>
    Task CreateRigidBodyAsync(RigidBody body);

    /// <summary>
    /// Removes a rigid body from the physics world.
    /// </summary>
    /// <param name="id">The unique identifier of the body to remove.</param>
    /// <returns>A task representing the asynchronous removal operation.</returns>
    Task RemoveRigidBodyAsync(string id);

    /// <summary>
    /// Updates properties of an existing rigid body.
    /// </summary>
    /// <param name="body">The rigid body with updated properties.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateRigidBodyAsync(RigidBody body);

    /// <summary>
    /// Applies an instantaneous impulse to a rigid body.
    /// </summary>
    /// <param name="id">The unique identifier of the body.</param>
    /// <param name="impulse">The impulse vector in world space (kg·m/s).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplyImpulseAsync(string id, Vector3 impulse);

    /// <summary>
    /// Gets transforms for all rigid bodies in a single batched call.
    /// </summary>
    /// <remarks>
    /// This method is optimized for efficient rendering updates by retrieving
    /// all body transforms in a single interop call.
    /// </remarks>
    /// <returns>Dictionary mapping body IDs to their current transform data.</returns>
    Task<Dictionary<string, TransformData>> GetTransformBatchAsync();

    /// <summary>
    /// Updates the transform of a specific rigid body.
    /// </summary>
    /// <param name="id">The unique identifier of the body.</param>
    /// <param name="transform">The new transform data (position, rotation, scale).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateTransformAsync(string id, TransformData transform);

    /// <summary>
    /// Creates a ground plane for physics collision.
    /// </summary>
    /// <param name="restitution">Bounciness of the ground (0-1).</param>
    /// <param name="friction">Friction coefficient of the ground.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CreateGroundAsync(float restitution = 0.3f, float friction = 0.5f);
}

/// <summary>
/// Vertex data for soft body mesh rendering.
/// </summary>
/// <remarks>
/// Contains deformed vertex positions and normals for soft body visualization.
/// Used for cloth, jelly, and other deformable body types.
/// </remarks>
public class SoftBodyVertexData
{
    /// <summary>
    /// Deformed vertex positions as a flat array [x1,y1,z1,x2,y2,z2,...].
    /// </summary>
    /// <remarks>
    /// The array length should be divisible by 3 (3 floats per vertex).
    /// </remarks>
    public float[] Vertices { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Vertex normals as a flat array [nx1,ny1,nz1,nx2,ny2,nz2,...].
    /// </summary>
    /// <remarks>
    /// The array length should match <see cref="Vertices"/> (3 floats per normal).
    /// Normals should be unit vectors for correct lighting.
    /// </remarks>
    public float[] Normals { get; set; } = Array.Empty<float>();
}

/// <summary>
/// Interface for soft body physics operations.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="IPhysicsService"/> and aggregates interfaces for cloth,
/// volumetric bodies, vertex pinning, and vertex data retrieval.
/// </para>
/// <para>
/// This follows the Interface Segregation Principle by composing multiple
/// focused interfaces rather than one large interface.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// </remarks>
public interface ISoftPhysicsService : IPhysicsService, IClothPhysicsService, IVolumetricPhysicsService, IVertexPinningService, ISoftBodyVertexDataService
{
    /// <summary>
    /// Initializes the soft physics engine with the specified settings.
    /// </summary>
    /// <param name="settings">Initial simulation settings.</param>
    /// <returns>A task representing the asynchronous initialization.</returns>
    Task InitializeAsync(SimulationSettings settings);

    /// <summary>
    /// Removes a soft body from the physics world.
    /// </summary>
    /// <param name="id">The unique identifier of the soft body to remove.</param>
    /// <returns>A task representing the asynchronous removal operation.</returns>
    Task RemoveSoftBodyAsync(string id);

    /// <summary>
    /// Updates soft body material properties.
    /// </summary>
    /// <param name="body">The soft body with updated properties.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UpdateSoftBodyAsync(SoftBody body);
}

/// <summary>
/// Interface for cloth physics creation.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Interface Segregation Principle - clients that only need to
/// create cloth bodies can depend on this focused interface.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// </remarks>
public interface IClothPhysicsService
{
    /// <summary>
    /// Creates a cloth soft body.
    /// </summary>
    /// <param name="body">The soft body configuration with cloth-specific parameters.</param>
    /// <returns>A task representing the asynchronous creation operation.</returns>
    Task CreateClothAsync(SoftBody body);
}

/// <summary>
/// Interface for volumetric (jelly) physics creation.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Interface Segregation Principle - clients that only need to
/// create volumetric bodies can depend on this focused interface.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// </remarks>
public interface IVolumetricPhysicsService
{
    /// <summary>
    /// Creates a volumetric (jelly-like) soft body.
    /// </summary>
    /// <param name="body">The soft body configuration with volumetric parameters.</param>
    /// <returns>A task representing the asynchronous creation operation.</returns>
    Task CreateVolumetricAsync(SoftBody body);
}

/// <summary>
/// Interface for vertex pinning operations on soft bodies.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Interface Segregation Principle - separates pinning operations
/// from body creation for clients that only need pinning functionality.
/// </para>
/// <para>
/// Pinned vertices are fixed to world positions and won't move during simulation,
/// useful for creating hanging cloth, fixed edges, etc.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// </remarks>
public interface IVertexPinningService
{
    /// <summary>
    /// Pins a vertex to a fixed world position.
    /// </summary>
    /// <param name="id">The soft body unique identifier.</param>
    /// <param name="vertexIndex">The zero-based index of the vertex to pin.</param>
    /// <param name="worldPosition">The world position to pin the vertex to.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PinVertexAsync(string id, int vertexIndex, Vector3 worldPosition);

    /// <summary>
    /// Unpins a previously pinned vertex, allowing it to move freely.
    /// </summary>
    /// <param name="id">The soft body unique identifier.</param>
    /// <param name="vertexIndex">The zero-based index of the vertex to unpin.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task UnpinVertexAsync(string id, int vertexIndex);
}

/// <summary>
/// Interface for retrieving deformed vertex data from soft bodies.
/// </summary>
/// <remarks>
/// <para>
/// Follows the Interface Segregation Principle - separates data retrieval
/// from physics operations for clients that only need rendering data.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// </remarks>
public interface ISoftBodyVertexDataService
{
    /// <summary>
    /// Gets deformed vertices for all soft bodies.
    /// </summary>
    /// <returns>Dictionary mapping body IDs to their vertex data.</returns>
    Task<Dictionary<string, SoftBodyVertexData>> GetDeformedVerticesAsync();

    /// <summary>
    /// Gets deformed vertices for a specific soft body.
    /// </summary>
    /// <param name="id">The soft body unique identifier.</param>
    /// <returns>The vertex data for the specified body.</returns>
    Task<SoftBodyVertexData> GetDeformedVerticesAsync(string id);
}
