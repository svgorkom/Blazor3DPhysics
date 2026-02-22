using BlazorClient.Domain.Common;
using BlazorClient.Services;
using BlazorClient.Application.Services;
using NSubstitute;
using NUnit.Framework;

namespace BlazorClient.Tests;

/// <summary>
/// Unit tests for the <see cref="RateLimiterExtensions"/> class.
/// </summary>
/// <remarks>
/// Tests cover the ExecuteWithRateLimit extension methods for both sync and async operations.
/// </remarks>
[TestFixture]
public class RateLimiterExtensionsTests
{
    private IRateLimiter _rateLimiter = null!;

    /// <summary>
    /// Initializes test fixtures with mock dependencies before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _rateLimiter = Substitute.For<IRateLimiter>();
    }

    #region ExecuteWithRateLimit Tests

    /// <summary>
    /// Verifies that ExecuteWithRateLimit executes the action when allowed.
    /// </summary>
    [Test]
    public void ExecuteWithRateLimit_WhenAllowed_ShouldExecuteAction()
    {
        // Arrange
        _rateLimiter.TryAcquire("test-key").Returns(true);
        var actionExecuted = false;

        // Act
        var result = _rateLimiter.ExecuteWithRateLimit("test-key", () => actionExecuted = true);

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(actionExecuted, Is.True);
    }

    /// <summary>
    /// Verifies that ExecuteWithRateLimit does not execute when rate limited.
    /// </summary>
    [Test]
    public void ExecuteWithRateLimit_WhenRateLimited_ShouldNotExecuteAction()
    {
        // Arrange
        _rateLimiter.TryAcquire("test-key").Returns(false);
        var actionExecuted = false;

        // Act
        var result = _rateLimiter.ExecuteWithRateLimit("test-key", () => actionExecuted = true);

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("Rate limit exceeded"));
        Assert.That(actionExecuted, Is.False);
    }

    /// <summary>
    /// Verifies that ExecuteWithRateLimit returns failure when action throws.
    /// </summary>
    [Test]
    public void ExecuteWithRateLimit_WhenActionThrows_ShouldReturnFailure()
    {
        // Arrange
        _rateLimiter.TryAcquire("test-key").Returns(true);

        // Act
        var result = _rateLimiter.ExecuteWithRateLimit("test-key", 
            () => throw new InvalidOperationException("Action failed"));

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("Action failed"));
    }

    #endregion

    #region ExecuteWithRateLimitAsync Tests

    /// <summary>
    /// Verifies that ExecuteWithRateLimitAsync executes and returns result when allowed.
    /// </summary>
    [Test]
    public async Task ExecuteWithRateLimitAsync_WhenAllowed_ShouldExecuteAction()
    {
        // Arrange
        _rateLimiter.TryAcquire("test-key").Returns(true);
        var expectedValue = 42;

        // Act
        var result = await _rateLimiter.ExecuteWithRateLimitAsync<int>(
            "test-key", 
            () => Task.FromResult(Result<int>.Success(expectedValue))
        );

        // Assert
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value, Is.EqualTo(expectedValue));
    }

    /// <summary>
    /// Verifies that ExecuteWithRateLimitAsync returns failure with quota info when rate limited.
    /// </summary>
    [Test]
    public async Task ExecuteWithRateLimitAsync_WhenRateLimited_ShouldReturnFailure()
    {
        // Arrange
        _rateLimiter.TryAcquire("test-key").Returns(false);
        _rateLimiter.GetRemainingQuota("test-key").Returns(0);

        // Act
        var result = await _rateLimiter.ExecuteWithRateLimitAsync<int>(
            "test-key", 
            () => Task.FromResult(Result<int>.Success(42))
        );

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Does.Contain("Rate limit exceeded"));
        Assert.That(result.Error, Does.Contain("Remaining quota: 0"));
    }

    /// <summary>
    /// Verifies that ExecuteWithRateLimitAsync propagates inner failure results.
    /// </summary>
    [Test]
    public async Task ExecuteWithRateLimitAsync_WhenActionReturnsFailure_ShouldPropagateFailure()
    {
        // Arrange
        _rateLimiter.TryAcquire("test-key").Returns(true);

        // Act
        var result = await _rateLimiter.ExecuteWithRateLimitAsync<int>(
            "test-key", 
            () => Task.FromResult(Result<int>.Failure("Inner failure"))
        );

        // Assert
        Assert.That(result.IsFailure, Is.True);
        Assert.That(result.Error, Is.EqualTo("Inner failure"));
    }

    #endregion
}
