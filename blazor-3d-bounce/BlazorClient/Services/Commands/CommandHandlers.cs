using BlazorClient.Domain.Models;
using BlazorClient.Domain.Common;
using BlazorClient.Application.Commands;
using BlazorClient.Application.Events;
using BlazorClient.Application.Validation;

namespace BlazorClient.Services.Commands;

/// <summary>
/// Handler for spawning rigid bodies.
/// </summary>
public class SpawnRigidBodyCommandHandler : ICommandHandler<SpawnRigidBodyCommand, string>
{
    private readonly ISceneStateService _sceneState;
    private readonly IRigidPhysicsService _rigidPhysics;
    private readonly IRenderingService _rendering;
    private readonly IEventAggregator _events;
    private readonly IPhysicsValidator _validator;
    private readonly IRateLimiter _rateLimiter;

    public SpawnRigidBodyCommandHandler(
        ISceneStateService sceneState,
        IRigidPhysicsService rigidPhysics,
        IRenderingService rendering,
        IEventAggregator events,
        IPhysicsValidator validator,
        IRateLimiter rateLimiter)
    {
        _sceneState = sceneState;
        _rigidPhysics = rigidPhysics;
        _rendering = rendering;
        _events = events;
        _validator = validator;
        _rateLimiter = rateLimiter;
    }

    public async Task<Result<string>> HandleAsync(SpawnRigidBodyCommand command, CancellationToken cancellationToken = default)
    {
        // Check rate limit
        if (!_rateLimiter.TryAcquire("spawn_rigid"))
        {
            var remaining = _rateLimiter.GetRemainingQuota("spawn_rigid");
            return Result<string>.Failure(
                $"Rate limit exceeded for rigid body spawning. Remaining quota: {remaining}. Please try again later.");
        }

        try
        {
            var body = new RigidBody(command.Type, command.Material)
            {
                Transform = new TransformData
                {
                    Position = command.Position,
                    Scale = command.Scale ?? Vector3.One
                }
            };

            // Validate the rigid body before creation
            var validation = _validator.ValidateRigidBody(body);
            
            if (!validation.IsValid)
            {
                var errorMessage = string.Join("; ", validation.Errors);
                _events.Publish(new ErrorOccurredEvent(
                    "Rigid body validation failed",
                    errorMessage,
                    ErrorSeverity.Error));
                return Result<string>.Failure(errorMessage);
            }

            // Log warnings to console
            foreach (var warning in validation.Warnings)
            {
                Console.WriteLine($"[Warning] Rigid body spawn: {warning}");
                _events.Publish(new ErrorOccurredEvent(
                    "Rigid body validation warning",
                    warning,
                    ErrorSeverity.Warning));
            }

            _sceneState.AddRigidBody(body);
            await _rigidPhysics.CreateRigidBodyAsync(body);
            await _rendering.CreateRigidMeshAsync(body);

            _events.Publish(new ObjectSpawnedEvent(body.Id, body.Name, command.Type.ToString()));

            return Result<string>.Success(body.Id);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to spawn rigid body: {ex.Message}");
        }
    }
}

/// <summary>
/// Handler for spawning soft bodies.
/// </summary>
public class SpawnSoftBodyCommandHandler : ICommandHandler<SpawnSoftBodyCommand, string>
{
    private readonly ISceneStateService _sceneState;
    private readonly ISoftPhysicsService _softPhysics;
    private readonly IRenderingService _rendering;
    private readonly IEventAggregator _events;
    private readonly IPhysicsValidator _validator;
    private readonly IRateLimiter _rateLimiter;

    public SpawnSoftBodyCommandHandler(
        ISceneStateService sceneState,
        ISoftPhysicsService softPhysics,
        IRenderingService rendering,
        IEventAggregator events,
        IPhysicsValidator validator,
        IRateLimiter rateLimiter)
    {
        _sceneState = sceneState;
        _softPhysics = softPhysics;
        _rendering = rendering;
        _events = events;
        _validator = validator;
        _rateLimiter = rateLimiter;
    }

    public async Task<Result<string>> HandleAsync(SpawnSoftBodyCommand command, CancellationToken cancellationToken = default)
    {
        // Check rate limit
        if (!_rateLimiter.TryAcquire("spawn_soft"))
        {
            var remaining = _rateLimiter.GetRemainingQuota("spawn_soft");
            return Result<string>.Failure(
                $"Rate limit exceeded for soft body spawning. Remaining quota: {remaining}. Please try again later.");
        }

        try
        {
            if (!await _softPhysics.IsAvailableAsync())
            {
                return Result<string>.Failure("Soft body physics is not available");
            }

            var body = new SoftBody(command.Type, command.Preset)
            {
                Transform = new TransformData
                {
                    Position = command.Position
                }
            };

            // Default pins for different types
            if (command.Type == SoftBodyType.Cloth)
            {
                body.PinnedVertices.Add(0);
                body.PinnedVertices.Add(body.ResolutionX);
            }
            else if (command.Type == SoftBodyType.Rope)
            {
                body.PinnedVertices.Add(0);
            }

            // Validate the soft body before creation
            var validation = _validator.ValidateSoftBody(body);
            
            if (!validation.IsValid)
            {
                var errorMessage = string.Join("; ", validation.Errors);
                _events.Publish(new ErrorOccurredEvent(
                    "Soft body validation failed",
                    errorMessage,
                    ErrorSeverity.Error));
                return Result<string>.Failure(errorMessage);
            }

            // Log warnings to console
            foreach (var warning in validation.Warnings)
            {
                Console.WriteLine($"[Warning] Soft body spawn: {warning}");
                _events.Publish(new ErrorOccurredEvent(
                    "Soft body validation warning",
                    warning,
                    ErrorSeverity.Warning));
            }

            _sceneState.AddSoftBody(body);

            switch (command.Type)
            {
                case SoftBodyType.Cloth:
                    await _softPhysics.CreateClothAsync(body);
                    break;
                case SoftBodyType.Rope:
                    await _softPhysics.CreateRopeAsync(body);
                    break;
                case SoftBodyType.Volumetric:
                    await _softPhysics.CreateVolumetricAsync(body);
                    break;
            }

            await _rendering.CreateSoftMeshAsync(body);

            _events.Publish(new ObjectSpawnedEvent(body.Id, body.Name, command.Type.ToString()));

            return Result<string>.Success(body.Id);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed to spawn soft body: {ex.Message}");
        }
    }
}

/// <summary>
/// Handler for deleting objects.
/// </summary>
public class DeleteObjectCommandHandler : ICommandHandler<DeleteObjectCommand>
{
    private readonly ISceneStateService _sceneState;
    private readonly IRigidPhysicsService _rigidPhysics;
    private readonly ISoftPhysicsService _softPhysics;
    private readonly IRenderingService _rendering;
    private readonly IEventAggregator _events;

    public DeleteObjectCommandHandler(
        ISceneStateService sceneState,
        IRigidPhysicsService rigidPhysics,
        ISoftPhysicsService softPhysics,
        IRenderingService rendering,
        IEventAggregator events)
    {
        _sceneState = sceneState;
        _rigidPhysics = rigidPhysics;
        _softPhysics = softPhysics;
        _rendering = rendering;
        _events = events;
    }

    public async Task<Result> HandleAsync(DeleteObjectCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await _rigidPhysics.RemoveRigidBodyAsync(command.Id);
            await _softPhysics.RemoveSoftBodyAsync(command.Id);
            await _rendering.RemoveMeshAsync(command.Id);

            _sceneState.RemoveObject(command.Id);

            _events.Publish(new ObjectDeletedEvent(command.Id));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete object: {ex.Message}");
        }
    }
}

/// <summary>
/// Handler for resetting the scene.
/// </summary>
public class ResetSceneCommandHandler : ICommandHandler<ResetSceneCommand>
{
    private readonly ISceneStateService _sceneState;
    private readonly IRigidPhysicsService _rigidPhysics;
    private readonly ISoftPhysicsService _softPhysics;
    private readonly IRenderingService _rendering;
    private readonly IEventAggregator _events;

    public ResetSceneCommandHandler(
        ISceneStateService sceneState,
        IRigidPhysicsService rigidPhysics,
        ISoftPhysicsService softPhysics,
        IRenderingService rendering,
        IEventAggregator events)
    {
        _sceneState = sceneState;
        _rigidPhysics = rigidPhysics;
        _softPhysics = softPhysics;
        _rendering = rendering;
        _events = events;
    }

    public async Task<Result> HandleAsync(ResetSceneCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await _rigidPhysics.ResetAsync();
            await _softPhysics.ResetAsync();

            foreach (var body in _sceneState.RigidBodies)
            {
                await _rendering.RemoveMeshAsync(body.Id);
            }
            foreach (var body in _sceneState.SoftBodies)
            {
                await _rendering.RemoveMeshAsync(body.Id);
            }

            _sceneState.ClearScene();

            _events.Publish(new SceneResetEvent());

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to reset scene: {ex.Message}");
        }
    }
}

/// <summary>
/// Handler for selecting objects.
/// </summary>
public class SelectObjectCommandHandler : ICommandHandler<SelectObjectCommand>
{
    private readonly ISceneStateService _sceneState;
    private readonly IRenderingService _rendering;
    private readonly IEventAggregator _events;

    public SelectObjectCommandHandler(
        ISceneStateService sceneState,
        IRenderingService rendering,
        IEventAggregator events)
    {
        _sceneState = sceneState;
        _rendering = rendering;
        _events = events;
    }

    public async Task<Result> HandleAsync(SelectObjectCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            var previousId = _sceneState.SelectedObjectId;

            _sceneState.SelectObject(command.Id);
            await _rendering.SetSelectionAsync(command.Id);

            _events.Publish(new ObjectSelectedEvent(command.Id, previousId));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to select object: {ex.Message}");
        }
    }
}

/// <summary>
/// Handler for applying impulse to rigid bodies.
/// </summary>
public class ApplyImpulseCommandHandler : ICommandHandler<ApplyImpulseCommand>
{
    private readonly IRigidPhysicsService _rigidPhysics;

    public ApplyImpulseCommandHandler(IRigidPhysicsService rigidPhysics)
    {
        _rigidPhysics = rigidPhysics;
    }

    public async Task<Result> HandleAsync(ApplyImpulseCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            await _rigidPhysics.ApplyImpulseAsync(command.Id, command.Impulse);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to apply impulse: {ex.Message}");
        }
    }
}

/// <summary>
/// Handler for updating simulation settings.
/// </summary>
public class UpdateSimulationSettingsCommandHandler : ICommandHandler<UpdateSimulationSettingsCommand>
{
    private readonly ISceneStateService _sceneState;
    private readonly IRigidPhysicsService _rigidPhysics;
    private readonly ISoftPhysicsService _softPhysics;
    private readonly IEventAggregator _events;
    private readonly IPhysicsValidator _validator;

    public UpdateSimulationSettingsCommandHandler(
        ISceneStateService sceneState,
        IRigidPhysicsService rigidPhysics,
        ISoftPhysicsService softPhysics,
        IEventAggregator events,
        IPhysicsValidator validator)
    {
        _sceneState = sceneState;
        _rigidPhysics = rigidPhysics;
        _softPhysics = softPhysics;
        _events = events;
        _validator = validator;
    }

    public async Task<Result> HandleAsync(UpdateSimulationSettingsCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate simulation settings
            var validation = _validator.ValidateSimulationSettings(command.Settings);
            
            if (!validation.IsValid)
            {
                var errorMessage = string.Join("; ", validation.Errors);
                _events.Publish(new ErrorOccurredEvent(
                    "Simulation settings validation failed",
                    errorMessage,
                    ErrorSeverity.Error));
                return Result.Failure(errorMessage);
            }

            // Log warnings
            foreach (var warning in validation.Warnings)
            {
                Console.WriteLine($"[Warning] Simulation settings: {warning}");
                _events.Publish(new ErrorOccurredEvent(
                    "Simulation settings warning",
                    warning,
                    ErrorSeverity.Warning));
            }

            _sceneState.UpdateSettings(command.Settings);
            await _rigidPhysics.UpdateSettingsAsync(command.Settings);
            await _softPhysics.UpdateSettingsAsync(command.Settings);

            _events.Publish(new SimulationSettingsChangedEvent(command.Settings));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to update settings: {ex.Message}");
        }
    }
}

/// <summary>
/// Handler for updating render settings.
/// </summary>
public class UpdateRenderSettingsCommandHandler : ICommandHandler<UpdateRenderSettingsCommand>
{
    private readonly ISceneStateService _sceneState;
    private readonly IRenderingService _rendering;
    private readonly IEventAggregator _events;

    public UpdateRenderSettingsCommandHandler(
        ISceneStateService sceneState,
        IRenderingService rendering,
        IEventAggregator events)
    {
        _sceneState = sceneState;
        _rendering = rendering;
        _events = events;
    }

    public async Task<Result> HandleAsync(UpdateRenderSettingsCommand command, CancellationToken cancellationToken = default)
    {
        try
        {
            _sceneState.UpdateRenderSettings(command.Settings);
            await _rendering.UpdateRenderSettingsAsync(command.Settings);

            _events.Publish(new RenderSettingsChangedEvent(command.Settings));

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to update render settings: {ex.Message}");
        }
    }
}
