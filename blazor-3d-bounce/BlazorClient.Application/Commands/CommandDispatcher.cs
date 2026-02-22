using BlazorClient.Domain.Common;

namespace BlazorClient.Application.Commands;

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
