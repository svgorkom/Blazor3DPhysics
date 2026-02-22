using BlazorClient.Domain.Models;

namespace BlazorClient.Application.Services;

/// <summary>
/// Interface for GPU-accelerated physics service.
/// </summary>
/// <remarks>
/// <para>
/// Extends <see cref="IRigidPhysicsService"/> with GPU-specific capabilities
/// such as WebGPU compute shader acceleration.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Fallback:</strong> When GPU is unavailable, implementations should
/// fall back to CPU physics (see <see cref="GpuPhysicsConfig.EnableCpuFallback"/>).
/// </para>
/// </remarks>
public interface IGpuPhysicsService : IRigidPhysicsService
{
    /// <summary>
    /// Checks if GPU acceleration is available.
    /// </summary>
    /// <returns>
    /// <c>true</c> if WebGPU or equivalent is available and active;
    /// <c>false</c> if using CPU fallback.
    /// </returns>
    Task<bool> IsGpuAvailableAsync();

    /// <summary>
    /// Gets detailed performance metrics from the GPU physics engine.
    /// </summary>
    /// <returns>
    /// Performance metrics including timing breakdowns and active state.
    /// </returns>
    Task<GpuPhysicsMetrics> GetMetricsAsync();

    /// <summary>
    /// Gets the GPU physics configuration.
    /// </summary>
    GpuPhysicsConfig Config { get; }

    /// <summary>
    /// Whether GPU physics is currently active (vs CPU fallback).
    /// </summary>
    bool IsGpuActive { get; }
}

/// <summary>
/// Performance metrics from the GPU physics engine.
/// </summary>
/// <remarks>
/// <para>
/// Provides detailed timing information for physics pipeline stages.
/// Useful for profiling and optimization.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (DTOs).
/// </para>
/// </remarks>
public class GpuPhysicsMetrics
{
    /// <summary>
    /// Total time for the last physics step in milliseconds.
    /// </summary>
    /// <value>Sum of all physics pipeline stages.</value>
    public float TotalStepTimeMs { get; set; }

    /// <summary>
    /// Integration phase time in milliseconds.
    /// </summary>
    /// <value>Time spent applying forces and updating velocities.</value>
    public float IntegrationTimeMs { get; set; }

    /// <summary>
    /// Broadphase collision detection time in milliseconds.
    /// </summary>
    /// <value>Time spent in spatial partitioning and potential pair detection.</value>
    public float BroadphaseTimeMs { get; set; }

    /// <summary>
    /// Narrowphase collision detection time in milliseconds.
    /// </summary>
    /// <value>Time spent in exact collision testing and contact generation.</value>
    public float NarrowphaseTimeMs { get; set; }

    /// <summary>
    /// Constraint solver time in milliseconds.
    /// </summary>
    /// <value>Time spent resolving collisions and constraints.</value>
    public float SolverTimeMs { get; set; }

    /// <summary>
    /// Number of active collision contacts.
    /// </summary>
    /// <value>Current contact count after narrowphase.</value>
    public int ContactCount { get; set; }

    /// <summary>
    /// Number of active physics bodies.
    /// </summary>
    /// <value>Current body count in the physics world.</value>
    public int BodyCount { get; set; }

    /// <summary>
    /// Whether GPU acceleration is currently active.
    /// </summary>
    /// <value>
    /// <c>true</c> if using GPU compute; <c>false</c> if using CPU fallback.
    /// </value>
    public bool IsGpuActive { get; set; }
}

/// <summary>
/// Configuration for GPU physics engine.
/// </summary>
/// <remarks>
/// <para>
/// Configures GPU physics behavior including memory allocation limits,
/// solver parameters, and fallback behavior.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (configuration).
/// </para>
/// <para>
/// <strong>Memory:</strong> The <see cref="MaxBodies"/> and <see cref="MaxContacts"/>
/// values affect GPU memory allocation. Set appropriately for your use case.
/// </para>
/// </remarks>
public class GpuPhysicsConfig
{
    /// <summary>
    /// Maximum number of rigid bodies supported.
    /// </summary>
    /// <value>
    /// The maximum body count. Default is 16,384. Higher values use more GPU memory.
    /// </value>
    public int MaxBodies { get; set; } = 16384;

    /// <summary>
    /// Maximum number of collision contacts supported.
    /// </summary>
    /// <value>
    /// The maximum contact count per frame. Default is 65,536.
    /// </value>
    public int MaxContacts { get; set; } = 65536;

    /// <summary>
    /// Number of solver iterations per physics step.
    /// </summary>
    /// <value>
    /// Higher values improve stability but reduce performance. Default is 8.
    /// </value>
    /// <remarks>
    /// Typical values: 4-8 for games, 8-16 for more accurate simulations.
    /// </remarks>
    public int SolverIterations { get; set; } = 8;

    /// <summary>
    /// Spatial hash grid cell size.
    /// </summary>
    /// <value>
    /// Cell size in world units. Default is 2.0. Should be approximately
    /// 2x the largest object radius for optimal performance.
    /// </value>
    public float GridCellSize { get; set; } = 2.0f;

    /// <summary>
    /// Whether to enable continuous collision detection (CCD).
    /// </summary>
    /// <value>
    /// <c>true</c> to enable CCD for fast-moving objects; <c>false</c> to disable.
    /// Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// CCD prevents tunneling but adds computational overhead.
    /// Enable for fast-moving objects like bullets.
    /// </remarks>
    public bool EnableCCD { get; set; } = false;

    /// <summary>
    /// Whether to enable warm starting for the constraint solver.
    /// </summary>
    /// <value>
    /// <c>true</c> to use previous frame's solution as starting point;
    /// <c>false</c> to start fresh. Default is <c>true</c>.
    /// </value>
    /// <remarks>
    /// Warm starting improves solver convergence for stable stacking.
    /// </remarks>
    public bool EnableWarmStarting { get; set; } = true;

    /// <summary>
    /// Whether to fall back to CPU physics if GPU is unavailable.
    /// </summary>
    /// <value>
    /// <c>true</c> to use CPU fallback; <c>false</c> to fail if GPU unavailable.
    /// Default is <c>true</c>.
    /// </value>
    public bool EnableCpuFallback { get; set; } = true;
}

/// <summary>
/// Factory for creating mesh geometry.
/// </summary>
/// <remarks>
/// <para>
/// Implements the Factory pattern for creating mesh geometry options
/// that can be passed to the rendering engine.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Extensibility:</strong> Register custom mesh types via
/// <see cref="Register"/> for custom shapes.
/// </para>
/// </remarks>
public interface IMeshCreatorFactory
{
    /// <summary>
    /// Registers a mesh creator for a type.
    /// </summary>
    /// <param name="type">The mesh type name (e.g., "sphere", "box", "custom").</param>
    /// <param name="creator">
    /// The creator function that takes options and returns mesh configuration.
    /// </param>
    void Register(string type, Func<object, object> creator);

    /// <summary>
    /// Creates a mesh of the specified type.
    /// </summary>
    /// <param name="type">The mesh type name.</param>
    /// <param name="options">Creation options specific to the mesh type.</param>
    /// <returns>
    /// The mesh options for JavaScript, or <c>null</c> if type not found.
    /// </returns>
    object? Create(string type, object options);

    /// <summary>
    /// Gets all registered mesh types.
    /// </summary>
    /// <returns>Enumerable of registered type names.</returns>
    IEnumerable<string> GetRegisteredTypes();
}

/// <summary>
/// Factory for creating materials.
/// </summary>
/// <remarks>
/// <para>
/// Implements the Factory pattern for creating material options
/// that can be passed to the rendering engine.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// </remarks>
public interface IMaterialCreatorFactory
{
    /// <summary>
    /// Registers a material creator for a type.
    /// </summary>
    /// <param name="type">The material type name (e.g., "standard", "pbr", "unlit").</param>
    /// <param name="creator">
    /// The creator function that takes options and returns material configuration.
    /// </param>
    void Register(string type, Func<object, object> creator);

    /// <summary>
    /// Creates a material of the specified type.
    /// </summary>
    /// <param name="type">The material type name.</param>
    /// <param name="options">Creation options specific to the material type.</param>
    /// <returns>
    /// The material options for JavaScript, or <c>null</c> if type not found.
    /// </returns>
    object? Create(string type, object options);

    /// <summary>
    /// Gets all registered material types.
    /// </summary>
    /// <returns>Enumerable of registered type names.</returns>
    IEnumerable<string> GetRegisteredTypes();
}
