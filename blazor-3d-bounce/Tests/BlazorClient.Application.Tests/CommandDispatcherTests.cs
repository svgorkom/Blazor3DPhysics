using BlazorClient.Application.Commands;
using BlazorClient.Domain.Common;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace BlazorClient.Application.Tests;

/// <summary>
/// Unit tests for the <see cref="CommandDispatcher"/> class.
/// </summary>
/// <remarks>
/// Tests cover command dispatching, handler resolution, error handling, and cancellation token propagation.
/// </remarks>
[TestFixture]
public class CommandDispatcherTests
{
    private IServiceProvider _serviceProvider = null!;
    private CommandDispatcher _dispatcher = null!;

    /// <summary>
    /// Initializes test fixtures before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _serviceProvider = Substitute.For<IServiceProvider>();
        _dispatcher = new CommandDispatcher(_serviceProvider);
    }

    #region DispatchAsync (void) Tests

    /// <summary>
    /// Verifies that DispatchAsync executes the registered handler for a command.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WhenHandlerExists_ShouldExecuteHandler()
    {
        // Arrange
        var command = new ResetSceneCommand();
        var handler = Substitute.For<ICommandHandler<ResetSceneCommand>>();
        handler.HandleAsync(command, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

        _serviceProvider.GetService(typeof(ICommandHandler<ResetSceneCommand>))
            .Returns(handler);

        // Act
        var result = await _dispatcher.DispatchAsync(command);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        await handler.Received(1).HandleAsync(command, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that DispatchAsync returns failure when no handler is registered.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WhenHandlerNotFound_ShouldReturnFailure()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _serviceProvider.GetService(typeof(ICommandHandler<ResetSceneCommand>))
            .Returns(null);

        // Act
        var result = await _dispatcher.DispatchAsync(command);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("No handler registered"));
        Assert.That(result.Error, Does.Contain("ResetSceneCommand"));
    }

    /// <summary>
    /// Verifies that DispatchAsync returns failure when handler throws an exception.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WhenHandlerThrowsException_ShouldReturnFailure()
    {
        // Arrange
        var command = new ResetSceneCommand();
        var handler = Substitute.For<ICommandHandler<ResetSceneCommand>>();
        handler.HandleAsync(command, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Something went wrong"));

        _serviceProvider.GetService(typeof(ICommandHandler<ResetSceneCommand>))
            .Returns(handler);

        // Act
        var result = await _dispatcher.DispatchAsync(command);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("Command execution failed"));
        Assert.That(result.Error, Does.Contain("Something went wrong"));
    }

    /// <summary>
    /// Verifies that DispatchAsync propagates handler failure results.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WhenHandlerReturnsFailure_ShouldPropagateFailure()
    {
        // Arrange
        var command = new ResetSceneCommand();
        var handler = Substitute.For<ICommandHandler<ResetSceneCommand>>();
        handler.HandleAsync(command, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Failure("Handler failure")));

        _serviceProvider.GetService(typeof(ICommandHandler<ResetSceneCommand>))
            .Returns(handler);

        // Act
        var result = await _dispatcher.DispatchAsync(command);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Handler failure"));
    }

    #endregion

    #region DispatchAsync<TResult> Tests

    /// <summary>
    /// Verifies that DispatchAsync with result type returns the handler's result value.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WithResult_WhenHandlerExists_ShouldReturnValue()
    {
        // Arrange
        var command = new SpawnRigidBodyCommand(
            Domain.Models.RigidPrimitiveType.Sphere,
            Domain.Models.MaterialPreset.Rubber,
            Domain.Models.Vector3.Zero);

        var handler = Substitute.For<ICommandHandler<SpawnRigidBodyCommand, string>>();
        handler.HandleAsync(command, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<string>.Success("new-object-id")));

        _serviceProvider.GetService(typeof(ICommandHandler<SpawnRigidBodyCommand, string>))
            .Returns(handler);

        // Act
        var result = await _dispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("new-object-id"));
    }

    /// <summary>
    /// Verifies that DispatchAsync with result type returns failure when no handler is registered.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WithResult_WhenHandlerNotFound_ShouldReturnFailure()
    {
        // Arrange
        var command = new SpawnRigidBodyCommand(
            Domain.Models.RigidPrimitiveType.Sphere,
            Domain.Models.MaterialPreset.Rubber,
            Domain.Models.Vector3.Zero);

        _serviceProvider.GetService(typeof(ICommandHandler<SpawnRigidBodyCommand, string>))
            .Returns(null);

        // Act
        var result = await _dispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("No handler registered"));
    }

    /// <summary>
    /// Verifies that DispatchAsync with result type returns failure when handler throws.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WithResult_WhenHandlerThrows_ShouldReturnFailure()
    {
        // Arrange
        var command = new SpawnRigidBodyCommand(
            Domain.Models.RigidPrimitiveType.Sphere,
            Domain.Models.MaterialPreset.Rubber,
            Domain.Models.Vector3.Zero);

        var handler = Substitute.For<ICommandHandler<SpawnRigidBodyCommand, string>>();
        handler.HandleAsync(command, Arg.Any<CancellationToken>())
            .Throws(new ArgumentException("Invalid argument"));

        _serviceProvider.GetService(typeof(ICommandHandler<SpawnRigidBodyCommand, string>))
            .Returns(handler);

        // Act
        var result = await _dispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("Command execution failed"));
    }

    #endregion

    #region CancellationToken Tests

    /// <summary>
    /// Verifies that DispatchAsync correctly passes the cancellation token to handlers.
    /// </summary>
    [Test]
    public async Task DispatchAsync_ShouldPassCancellationToken()
    {
        // Arrange
        var command = new ResetSceneCommand();
        var handler = Substitute.For<ICommandHandler<ResetSceneCommand>>();
        var cts = new CancellationTokenSource();

        handler.HandleAsync(command, cts.Token)
            .Returns(Task.FromResult(Result.Success()));

        _serviceProvider.GetService(typeof(ICommandHandler<ResetSceneCommand>))
            .Returns(handler);

        // Act
        await _dispatcher.DispatchAsync(command, cts.Token);

        // Assert
        await handler.Received(1).HandleAsync(command, cts.Token);
    }

    #endregion
}
