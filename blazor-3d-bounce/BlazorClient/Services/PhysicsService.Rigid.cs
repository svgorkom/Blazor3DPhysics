using Microsoft.JSInterop;

namespace BlazorClient.Services;

/// <summary>
/// Interface for rigid body physics using Rapier.js.
/// Extends IPhysicsService for Liskov Substitution Principle compliance.
/// </summary>
public interface IRigidPhysicsService : IPhysicsService
{
    /// <summary>
    /// Initializes the rigid body physics world.
    /// </summary>
    Task InitializeAsync(SimulationSettings settings);

    /// <summary>
    /// Creates a rigid body in the physics world.
    /// </summary>
    Task CreateRigidBodyAsync(RigidBody body);

    /// <summary>
    /// Removes a rigid body from the physics world.
    /// </summary>
    Task RemoveRigidBodyAsync(string id);

    /// <summary>
    /// Updates rigid body properties.
    /// </summary>
    Task UpdateRigidBodyAsync(RigidBody body);

    /// <summary>
    /// Applies an impulse to a rigid body.
    /// </summary>
    Task ApplyImpulseAsync(string id, Vector3 impulse);

    /// <summary>
    /// Applies a force to a rigid body.
    /// </summary>
    Task ApplyForceAsync(string id, Vector3 force);

    /// <summary>
    /// Sets the linear velocity of a rigid body.
    /// </summary>
    Task SetLinearVelocityAsync(string id, Vector3 velocity);

    /// <summary>
    /// Gets all rigid body transforms as a flat array for batching.
    /// </summary>
    Task<RigidTransformBatch> GetTransformBatchAsync();

    /// <summary>
    /// Creates the ground plane collider.
    /// </summary>
    Task CreateGroundAsync(float restitution = 0.3f, float friction = 0.5f);
}

/// <summary>
/// Batch of rigid body transforms for efficient transfer.
/// </summary>
public class RigidTransformBatch
{
    /// <summary>
    /// Flat array of transforms: [px, py, pz, rx, ry, rz, rw] per body.
    /// </summary>
    public float[] Transforms { get; set; } = Array.Empty<float>();

    /// <summary>
    /// IDs corresponding to each transform in order.
    /// </summary>
    public string[] Ids { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Implementation of rigid body physics service using Rapier.js.
/// </summary>
public class RigidPhysicsService : IRigidPhysicsService, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    public RigidPhysicsService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync()
    {
        // Rigid physics is always available once initialized
        return Task.FromResult(_initialized);
    }

    /// <inheritdoc />
    public async Task InitializeAsync(SimulationSettings settings)
    {
        if (_initialized) return;

        var config = new
        {
            gravity = settings.Gravity.ToArray(),
            timeStep = settings.TimeStep,
            subSteps = settings.SubSteps,
            enableSleeping = settings.EnableSleeping,
            sleepThreshold = settings.SleepThreshold
        };

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.initialize", config);
        _initialized = true;
    }

    /// <inheritdoc />
    public async Task CreateRigidBodyAsync(RigidBody body)
    {
        if (!_initialized) return;

        var bodyData = new
        {
            id = body.Id,
            primitiveType = body.PrimitiveType.ToString().ToLower(),
            position = body.Transform.Position.ToArray(),
            rotation = body.Transform.Rotation.ToArray(),
            scale = body.Transform.Scale.ToArray(),
            mass = body.Mass,
            isStatic = body.IsStatic,
            restitution = body.Material.Restitution,
            staticFriction = body.Material.StaticFriction,
            dynamicFriction = body.Material.DynamicFriction,
            density = body.Material.Density,
            linearDamping = body.LinearDamping,
            angularDamping = body.AngularDamping,
            enableCCD = body.EnableCCD,
            linearVelocity = body.LinearVelocity.ToArray(),
            angularVelocity = body.AngularVelocity.ToArray()
        };

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.createRigidBody", bodyData);
    }

    /// <inheritdoc />
    public async Task RemoveRigidBodyAsync(string id)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.removeRigidBody", id);
    }

    /// <inheritdoc />
    public async Task UpdateRigidBodyAsync(RigidBody body)
    {
        if (!_initialized) return;

        var updates = new
        {
            id = body.Id,
            mass = body.Mass,
            restitution = body.Material.Restitution,
            staticFriction = body.Material.StaticFriction,
            dynamicFriction = body.Material.DynamicFriction,
            linearDamping = body.LinearDamping,
            angularDamping = body.AngularDamping,
            enableCCD = body.EnableCCD
        };

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.updateRigidBody", updates);
    }

    /// <inheritdoc />
    public async Task ApplyImpulseAsync(string id, Vector3 impulse)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.applyImpulse", id, impulse.ToArray());
    }

    /// <inheritdoc />
    public async Task ApplyForceAsync(string id, Vector3 force)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.applyForce", id, force.ToArray());
    }

    /// <inheritdoc />
    public async Task SetLinearVelocityAsync(string id, Vector3 velocity)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.setLinearVelocity", id, velocity.ToArray());
    }

    /// <inheritdoc />
    public async Task UpdateSettingsAsync(SimulationSettings settings)
    {
        if (!_initialized) return;

        var config = new
        {
            gravity = settings.Gravity.ToArray(),
            timeStep = settings.TimeStep,
            subSteps = settings.SubSteps,
            enableSleeping = settings.EnableSleeping,
            sleepThreshold = settings.SleepThreshold
        };

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.updateSettings", config);
    }

    /// <inheritdoc />
    public async Task StepAsync(float deltaTime)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.step", deltaTime);
    }

    /// <inheritdoc />
    public async Task<RigidTransformBatch> GetTransformBatchAsync()
    {
        if (!_initialized) return new RigidTransformBatch();

        return await _jsRuntime.InvokeAsync<RigidTransformBatch>("RigidPhysicsModule.getTransformBatch");
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.reset");
    }

    /// <inheritdoc />
    public async Task CreateGroundAsync(float restitution = 0.3f, float friction = 0.5f)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.createGround", restitution, friction);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_initialized)
        {
            await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.dispose");
        }
    }
}
