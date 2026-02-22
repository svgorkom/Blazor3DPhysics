using Microsoft.JSInterop;

namespace BlazorClient.Services;

/// <summary>
/// Implementation of rigid body physics service using Rapier.js.
/// </summary>
/// <remarks>
/// <para>
/// Provides rigid body physics simulation through JavaScript interop
/// with the Rapier.js physics engine.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Presentation/Services Layer.
/// </para>
/// </remarks>
public class RigidPhysicsService : BlazorClient.Application.Services.IRigidPhysicsService
{
    private readonly IJSRuntime _jsRuntime;
    private bool _initialized;

    /// <summary>
    /// Initializes a new rigid physics service.
    /// </summary>
    /// <param name="jsRuntime">The JS runtime for interop.</param>
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

    /// <summary>
    /// Updates properties of an existing rigid body.
    /// </summary>
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

    /// <summary>
    /// Applies a continuous force to a rigid body.
    /// </summary>
    public async Task ApplyForceAsync(string id, Vector3 force)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.applyForce", id, force.ToArray());
    }

    /// <summary>
    /// Sets the linear velocity of a rigid body directly.
    /// </summary>
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
    public async Task<Dictionary<string, TransformData>> GetTransformBatchAsync()
    {
        if (!_initialized) return new Dictionary<string, TransformData>();

        // Get the batched transform from JS and convert to Dictionary
        var batch = await _jsRuntime.InvokeAsync<RigidTransformBatch>("RigidPhysicsModule.getTransformBatch");
        
        var result = new Dictionary<string, TransformData>();
        for (int i = 0; i < batch.Ids.Length; i++)
        {
            var offset = i * 7;
            result[batch.Ids[i]] = new TransformData
            {
                Position = new Vector3(batch.Transforms[offset], batch.Transforms[offset + 1], batch.Transforms[offset + 2]),
                Rotation = new Quaternion(batch.Transforms[offset + 3], batch.Transforms[offset + 4], batch.Transforms[offset + 5], batch.Transforms[offset + 6]),
                Scale = Vector3.One
            };
        }
        return result;
    }

    /// <inheritdoc />
    public async Task UpdateTransformAsync(string id, TransformData transform)
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.updateTransform", id, 
            transform.Position.ToArray(), transform.Rotation.ToArray());
    }

    /// <inheritdoc />
    public async Task ResetAsync()
    {
        if (!_initialized) return;

        await _jsRuntime.InvokeVoidAsync("RigidPhysicsModule.reset");
    }

    /// <summary>
    /// Creates a ground plane for physics collision.
    /// </summary>
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
