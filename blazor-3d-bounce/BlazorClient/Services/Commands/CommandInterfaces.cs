using BlazorClient.Models;

namespace BlazorClient.Services.Commands;

#region Command Interfaces

/// <summary>
/// Marker interface for commands (actions that change state).
/// </summary>
public interface ICommand { }

/// <summary>
/// Command that returns a result.
/// </summary>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ICommand<TResult> : ICommand { }

/// <summary>
/// Handler for commands without a return value.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
public interface ICommandHandler<TCommand> where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Handler for commands with a return value.
/// </summary>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResult">The result type.</typeparam>
public interface ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

#endregion

#region Command Dispatcher

/// <summary>
/// Dispatches commands to their handlers.
/// </summary>
public interface ICommandDispatcher
{
    /// <summary>
    /// Dispatches a command to its handler.
    /// </summary>
    Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand;

    /// <summary>
    /// Dispatches a command that returns a result.
    /// </summary>
    Task<Result<TResult>> DispatchAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand<TResult>;
}

/// <summary>
/// Implementation of command dispatcher using service provider.
/// </summary>
public class CommandDispatcher : ICommandDispatcher
{
    private readonly IServiceProvider _serviceProvider;

    public CommandDispatcher(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<Result> DispatchAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand
    {
        var handler = _serviceProvider.GetService(typeof(ICommandHandler<TCommand>)) as ICommandHandler<TCommand>;
        
        if (handler == null)
        {
            return Result.Failure($"No handler registered for command type {typeof(TCommand).Name}");
        }

        try
        {
            return await handler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure($"Command execution failed: {ex.Message}");
        }
    }

    public async Task<Result<TResult>> DispatchAsync<TCommand, TResult>(TCommand command, CancellationToken cancellationToken = default) 
        where TCommand : ICommand<TResult>
    {
        var handler = _serviceProvider.GetService(typeof(ICommandHandler<TCommand, TResult>)) as ICommandHandler<TCommand, TResult>;
        
        if (handler == null)
        {
            return Result<TResult>.Failure($"No handler registered for command type {typeof(TCommand).Name}");
        }

        try
        {
            return await handler.HandleAsync(command, cancellationToken);
        }
        catch (Exception ex)
        {
            return Result<TResult>.Failure($"Command execution failed: {ex.Message}");
        }
    }
}

#endregion

#region Physics Commands

/// <summary>
/// Command to spawn a rigid body.
/// </summary>
public record SpawnRigidBodyCommand(
    RigidPrimitiveType Type, 
    MaterialPreset Material, 
    Vector3 Position,
    Vector3? Scale = null) : ICommand<string>;

/// <summary>
/// Command to spawn a soft body.
/// </summary>
public record SpawnSoftBodyCommand(
    SoftBodyType Type, 
    SoftBodyPreset Preset, 
    Vector3 Position) : ICommand<string>;

/// <summary>
/// Command to delete an object.
/// </summary>
public record DeleteObjectCommand(string Id) : ICommand;

/// <summary>
/// Command to apply an impulse to a rigid body.
/// </summary>
public record ApplyImpulseCommand(string Id, Vector3 Impulse) : ICommand;

/// <summary>
/// Command to apply a force to a rigid body.
/// </summary>
public record ApplyForceCommand(string Id, Vector3 Force) : ICommand;

/// <summary>
/// Command to reset the scene.
/// </summary>
public record ResetSceneCommand : ICommand;

/// <summary>
/// Command to update simulation settings.
/// </summary>
public record UpdateSimulationSettingsCommand(SimulationSettings Settings) : ICommand;

/// <summary>
/// Command to update render settings.
/// </summary>
public record UpdateRenderSettingsCommand(RenderSettings Settings) : ICommand;

/// <summary>
/// Command to select an object.
/// </summary>
public record SelectObjectCommand(string? Id) : ICommand;

/// <summary>
/// Command to pin a soft body vertex.
/// </summary>
public record PinVertexCommand(string BodyId, int VertexIndex, Vector3 WorldPosition) : ICommand;

/// <summary>
/// Command to unpin a soft body vertex.
/// </summary>
public record UnpinVertexCommand(string BodyId, int VertexIndex) : ICommand;

#endregion
