using BlazorClient.Application.Commands;
using BlazorClient.Application.Services;
using BlazorClient.Domain.Common;
using BlazorClient.Services;
using BlazorClient.Services.Commands;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using NUnit.Framework;

namespace BlazorClient.Tests;

/// <summary>
/// Unit tests for the <see cref="LoggingCommandDispatcher"/> decorator class.
/// </summary>
/// <remarks>
/// Tests cover command delegation, timing recording, execution history,
/// and statistics tracking functionality.
/// </remarks>
[TestFixture]
public class LoggingCommandDispatcherTests
{
    private ICommandDispatcher _innerDispatcher = null!;
    private IPerformanceMonitor _performanceMonitor = null!;
    private LoggingCommandDispatcher _loggingDispatcher = null!;

    /// <summary>
    /// Initializes test fixtures with mock dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _innerDispatcher = Substitute.For<ICommandDispatcher>();
        _performanceMonitor = Substitute.For<IPerformanceMonitor>();
        _loggingDispatcher = new LoggingCommandDispatcher(_innerDispatcher, _performanceMonitor);
    }

    #region DispatchAsync (void) Tests

    /// <summary>
    /// Verifies that commands are delegated to the inner dispatcher.
    /// </summary>
    [Test]
    public async Task DispatchAsync_ShouldDelegateToInnerDispatcher()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await _loggingDispatcher.DispatchAsync(command);

        // Assert
        await _innerDispatcher.Received(1).DispatchAsync(command, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that successful command results are returned correctly.
    /// </summary>
    [Test]
    public async Task DispatchAsync_SuccessfulCommand_ShouldReturnSuccess()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        var result = await _loggingDispatcher.DispatchAsync(command);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
    }

    /// <summary>
    /// Verifies that failed command results are propagated correctly.
    /// </summary>
    [Test]
    public async Task DispatchAsync_FailedCommand_ShouldReturnFailure()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Inner failure"));

        // Act
        var result = await _loggingDispatcher.DispatchAsync(command);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Inner failure"));
    }

    /// <summary>
    /// Verifies that timing is recorded when detailed profiling is enabled.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WhenProfilingEnabled_ShouldRecordTiming()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _performanceMonitor.DetailedProfilingEnabled.Returns(true);

        // Act
        await _loggingDispatcher.DispatchAsync(command);

        // Assert
        _performanceMonitor.Received(1).RecordTiming(
            Arg.Is<string>(s => s.Contains("ResetSceneCommand")),
            Arg.Any<float>()
        );
    }

    /// <summary>
    /// Verifies that timing is not recorded when detailed profiling is disabled.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WhenProfilingDisabled_ShouldNotRecordTiming()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _performanceMonitor.DetailedProfilingEnabled.Returns(false);

        // Act
        await _loggingDispatcher.DispatchAsync(command);

        // Assert
        _performanceMonitor.DidNotReceive().RecordTiming(Arg.Any<string>(), Arg.Any<float>());
    }

    /// <summary>
    /// Verifies that exceptions from the inner dispatcher are propagated.
    /// </summary>
    [Test]
    public async Task DispatchAsync_WhenExceptionThrown_ShouldLogAndRethrow()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Test exception"));

        // Act & Assert
        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _loggingDispatcher.DispatchAsync(command));

        Assert.That(ex!.Message, Is.EqualTo("Test exception"));
    }

    #endregion

    #region DispatchAsync<TResult> Tests

    /// <summary>
    /// Verifies that result-returning commands are delegated to the inner dispatcher.
    /// </summary>
    [Test]
    public async Task DispatchAsyncWithResult_ShouldDelegateToInnerDispatcher()
    {
        // Arrange
        var command = new SpawnRigidBodyCommand(
            Domain.Models.RigidPrimitiveType.Sphere,
            Domain.Models.MaterialPreset.Rubber,
            Domain.Models.Vector3.Zero);

        _innerDispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("new-id"));

        // Act
        await _loggingDispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command);

        // Assert
        await _innerDispatcher.Received(1)
            .DispatchAsync<SpawnRigidBodyCommand, string>(command, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Verifies that successful result-returning commands return the correct value.
    /// </summary>
    [Test]
    public async Task DispatchAsyncWithResult_SuccessfulCommand_ShouldReturnResult()
    {
        // Arrange
        var command = new SpawnRigidBodyCommand(
            Domain.Models.RigidPrimitiveType.Box,
            Domain.Models.MaterialPreset.Steel,
            Domain.Models.Vector3.Zero);

        _innerDispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command, Arg.Any<CancellationToken>())
            .Returns(Result<string>.Success("created-object-id"));

        // Act
        var result = await _loggingDispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo("created-object-id"));
    }

    #endregion

    #region Execution History Tests

    /// <summary>
    /// Verifies that execution history contains entries for dispatched commands.
    /// </summary>
    [Test]
    public async Task GetExecutionHistory_AfterCommands_ShouldContainLogs()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await _loggingDispatcher.DispatchAsync(command);
        await _loggingDispatcher.DispatchAsync(command);
        var history = _loggingDispatcher.GetExecutionHistory();

        // Assert
        Assert.That(history.Count, Is.EqualTo(2));
        Assert.That(history[0].CommandType, Is.EqualTo("ResetSceneCommand"));
        Assert.That(history[1].CommandType, Is.EqualTo("ResetSceneCommand"));
    }

    /// <summary>
    /// Verifies that successful command executions are recorded correctly.
    /// </summary>
    [Test]
    public async Task GetExecutionHistory_ShouldRecordSuccess()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await _loggingDispatcher.DispatchAsync(command);
        var history = _loggingDispatcher.GetExecutionHistory();

        // Assert
        Assert.That(history[0].Success, Is.True);
        Assert.That(history[0].ErrorMessage, Is.Null);
    }

    /// <summary>
    /// Verifies that failed command executions are recorded with error messages.
    /// </summary>
    [Test]
    public async Task GetExecutionHistory_ShouldRecordFailure()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Something failed"));

        // Act
        await _loggingDispatcher.DispatchAsync(command);
        var history = _loggingDispatcher.GetExecutionHistory();

        // Assert
        Assert.That(history[0].Success, Is.False);
        Assert.That(history[0].ErrorMessage, Is.EqualTo("Something failed"));
    }

    /// <summary>
    /// Verifies that execution history respects the configured maximum size.
    /// </summary>
    [Test]
    public async Task GetExecutionHistory_ShouldRespectMaxSize()
    {
        // Arrange
        var maxHistorySize = 5;
        var dispatcher = new LoggingCommandDispatcher(_innerDispatcher, _performanceMonitor, maxHistorySize);
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act - execute more commands than history size
        for (int i = 0; i < 10; i++)
        {
            await dispatcher.DispatchAsync(command);
        }

        var history = dispatcher.GetExecutionHistory();

        // Assert
        Assert.That(history.Count, Is.EqualTo(maxHistorySize));
    }

    /// <summary>
    /// Verifies that ClearHistory removes all execution history entries.
    /// </summary>
    [Test]
    public void ClearHistory_ShouldRemoveAllLogs()
    {
        // Arrange - add some history first
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        
        _loggingDispatcher.DispatchAsync(command).Wait();
        _loggingDispatcher.DispatchAsync(command).Wait();

        // Act
        _loggingDispatcher.ClearHistory();
        var history = _loggingDispatcher.GetExecutionHistory();

        // Assert
        Assert.That(history, Is.Empty);
    }

    #endregion

    #region GetStats Tests

    /// <summary>
    /// Verifies that stats are empty when no commands have been executed.
    /// </summary>
    [Test]
    public void GetStats_WithNoExecutions_ShouldReturnEmpty()
    {
        // Act
        var stats = _loggingDispatcher.GetStats();

        // Assert
        Assert.That(stats.TotalCommands, Is.EqualTo(0));
        Assert.That(stats.SuccessfulCommands, Is.EqualTo(0));
        Assert.That(stats.FailedCommands, Is.EqualTo(0));
    }

    /// <summary>
    /// Verifies that stats are calculated correctly for mixed success/failure results.
    /// </summary>
    [Test]
    public async Task GetStats_ShouldCalculateCorrectly()
    {
        // Arrange
        var command = new ResetSceneCommand();
        _innerDispatcher.DispatchAsync(command, Arg.Any<CancellationToken>())
            .Returns(Result.Success(), Result.Success(), Result.Failure("Error"));

        // Act
        await _loggingDispatcher.DispatchAsync(command);
        await _loggingDispatcher.DispatchAsync(command);
        await _loggingDispatcher.DispatchAsync(command);

        var stats = _loggingDispatcher.GetStats();

        // Assert
        Assert.That(stats.TotalCommands, Is.EqualTo(3));
        Assert.That(stats.SuccessfulCommands, Is.EqualTo(2));
        Assert.That(stats.FailedCommands, Is.EqualTo(1));
        Assert.That(stats.SuccessRate, Is.EqualTo(200.0 / 3.0).Within(0.1));
    }

    /// <summary>
    /// Verifies that stats are grouped correctly by command type.
    /// </summary>
    [Test]
    public async Task GetStats_ShouldGroupByCommandType()
    {
        // Arrange
        var resetCommand = new ResetSceneCommand();
        var selectCommand = new SelectObjectCommand("test");

        _innerDispatcher.DispatchAsync(resetCommand, Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _innerDispatcher.DispatchAsync(selectCommand, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        // Act
        await _loggingDispatcher.DispatchAsync(resetCommand);
        await _loggingDispatcher.DispatchAsync(resetCommand);
        await _loggingDispatcher.DispatchAsync(selectCommand);

        var stats = _loggingDispatcher.GetStats();

        // Assert
        Assert.That(stats.CommandTypeStats.Count, Is.EqualTo(2));
        
        var resetStats = stats.CommandTypeStats.FirstOrDefault(s => s.CommandType == "ResetSceneCommand");
        Assert.That(resetStats, Is.Not.Null);
        Assert.That(resetStats!.TotalExecutions, Is.EqualTo(2));

        var selectStats = stats.CommandTypeStats.FirstOrDefault(s => s.CommandType == "SelectObjectCommand");
        Assert.That(selectStats, Is.Not.Null);
        Assert.That(selectStats!.TotalExecutions, Is.EqualTo(1));
    }

    #endregion
}
