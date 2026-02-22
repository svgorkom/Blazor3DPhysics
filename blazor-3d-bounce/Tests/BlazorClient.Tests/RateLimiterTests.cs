using BlazorClient.Application.Services;
using BlazorClient.Infrastructure.Services;
using NUnit.Framework;

namespace BlazorClient.Tests;

/// <summary>
/// Unit tests for the <see cref="RateLimiter"/> class.
/// </summary>
/// <remarks>
/// Tests cover rate limiting, quota management, reset functionality, 
/// concurrency control, and disposal behavior.
/// </remarks>
[TestFixture]
public class RateLimiterTests
{
    #region TryAcquire Tests

    /// <summary>
    /// Verifies that TryAcquire returns true when within the rate limit.
    /// </summary>
    [Test]
    public void TryAcquire_WithinLimit_ShouldReturnTrue()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 10, Window = TimeSpan.FromMinutes(1) };
        using var limiter = new RateLimiter(options);

        // Act
        var result = limiter.TryAcquire("test-key");

        // Assert
        Assert.That(result, Is.True);
    }

    /// <summary>
    /// Verifies that TryAcquire returns false when the rate limit is exceeded.
    /// </summary>
    [Test]
    public void TryAcquire_AtLimit_ShouldReturnFalse()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 3, Window = TimeSpan.FromMinutes(1) };
        using var limiter = new RateLimiter(options);

        // Act - exhaust the limit
        limiter.TryAcquire("test-key");
        limiter.TryAcquire("test-key");
        limiter.TryAcquire("test-key");
        var result = limiter.TryAcquire("test-key");

        // Assert
        Assert.That(result, Is.False);
    }

    /// <summary>
    /// Verifies that different keys have separate rate limits.
    /// </summary>
    [Test]
    public void TryAcquire_DifferentKeys_ShouldHaveSeparateLimits()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 1, Window = TimeSpan.FromMinutes(1) };
        using var limiter = new RateLimiter(options);

        // Act
        var result1 = limiter.TryAcquire("key1");
        var result2 = limiter.TryAcquire("key2");
        var result3 = limiter.TryAcquire("key1"); // Should be blocked

        // Assert
        Assert.That(result1, Is.True);
        Assert.That(result2, Is.True);
        Assert.That(result3, Is.False);
    }

    /// <summary>
    /// Verifies that TryAcquire throws when called after disposal.
    /// </summary>
    [Test]
    public void TryAcquire_AfterDispose_ShouldThrow()
    {
        // Arrange
        var limiter = new RateLimiter();
        limiter.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => limiter.TryAcquire("test-key"));
    }

    #endregion

    #region GetRemainingQuota Tests

    /// <summary>
    /// Verifies that a new key has full quota available.
    /// </summary>
    [Test]
    public void GetRemainingQuota_NewKey_ShouldReturnMaxRequests()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 50 };
        using var limiter = new RateLimiter(options);

        // Act
        var remaining = limiter.GetRemainingQuota("new-key");

        // Assert
        Assert.That(remaining, Is.EqualTo(50));
    }

    /// <summary>
    /// Verifies that remaining quota decreases after requests.
    /// </summary>
    [Test]
    public void GetRemainingQuota_AfterSomeRequests_ShouldDecrease()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 10 };
        using var limiter = new RateLimiter(options);

        limiter.TryAcquire("test-key");
        limiter.TryAcquire("test-key");
        limiter.TryAcquire("test-key");

        // Act
        var remaining = limiter.GetRemainingQuota("test-key");

        // Assert
        Assert.That(remaining, Is.EqualTo(7));
    }

    /// <summary>
    /// Verifies that remaining quota is zero when limit is reached.
    /// </summary>
    [Test]
    public void GetRemainingQuota_AtLimit_ShouldReturnZero()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 2 };
        using var limiter = new RateLimiter(options);

        limiter.TryAcquire("test-key");
        limiter.TryAcquire("test-key");

        // Act
        var remaining = limiter.GetRemainingQuota("test-key");

        // Assert
        Assert.That(remaining, Is.EqualTo(0));
    }

    #endregion

    #region Reset Tests

    /// <summary>
    /// Verifies that Reset restores quota for a specific key.
    /// </summary>
    [Test]
    public void Reset_ShouldRestoreQuota()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 3 };
        using var limiter = new RateLimiter(options);

        limiter.TryAcquire("test-key");
        limiter.TryAcquire("test-key");
        limiter.TryAcquire("test-key");

        // Act
        limiter.Reset("test-key");

        // Assert
        Assert.That(limiter.GetRemainingQuota("test-key"), Is.EqualTo(3));
        Assert.That(limiter.TryAcquire("test-key"), Is.True);
    }

    /// <summary>
    /// Verifies that ResetAll restores quota for all keys.
    /// </summary>
    [Test]
    public void ResetAll_ShouldRestoreAllQuotas()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxRequests = 1 };
        using var limiter = new RateLimiter(options);

        limiter.TryAcquire("key1");
        limiter.TryAcquire("key2");

        // Act
        limiter.ResetAll();

        // Assert
        Assert.That(limiter.TryAcquire("key1"), Is.True);
        Assert.That(limiter.TryAcquire("key2"), Is.True);
    }

    #endregion

    #region Concurrency Slot Tests

    /// <summary>
    /// Verifies that AcquireConcurrencySlotAsync limits concurrent operations.
    /// </summary>
    [Test]
    public async Task AcquireConcurrencySlotAsync_ShouldLimitConcurrency()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxConcurrent = 2 };
        using var limiter = new RateLimiter(options);
        var concurrentCount = 0;
        var maxConcurrent = 0;
        var lockObj = new object();

        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            using var slot = await limiter.AcquireConcurrencySlotAsync();
            lock (lockObj)
            {
                concurrentCount++;
                maxConcurrent = Math.Max(maxConcurrent, concurrentCount);
            }

            await Task.Delay(50); // Simulate work

            lock (lockObj)
            {
                concurrentCount--;
            }
        });

        // Act
        await Task.WhenAll(tasks);

        // Assert - max concurrent should not exceed the limit
        Assert.That(maxConcurrent, Is.LessThanOrEqualTo(2));
    }

    /// <summary>
    /// Verifies that concurrency slots are released when disposed.
    /// </summary>
    [Test]
    public async Task AcquireConcurrencySlotAsync_ShouldReleaseOnDispose()
    {
        // Arrange
        var options = new RateLimiterOptions { MaxConcurrent = 1 };
        using var limiter = new RateLimiter(options);

        // Act - acquire and release
        using (await limiter.AcquireConcurrencySlotAsync())
        {
            // Slot is held
        }

        // Should be able to acquire again
        var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        using var slot = await limiter.AcquireConcurrencySlotAsync(cts.Token);

        // Assert - we got here without timeout
        Assert.Pass();
    }

    #endregion

    #region RateLimiterOptions Tests

    /// <summary>
    /// Verifies that RateLimiterOptions has reasonable default values.
    /// </summary>
    [Test]
    public void RateLimiterOptions_DefaultValues_ShouldBeReasonable()
    {
        // Act
        var options = new RateLimiterOptions();

        // Assert
        Assert.That(options.MaxRequests, Is.EqualTo(100));
        Assert.That(options.Window, Is.EqualTo(TimeSpan.FromMinutes(1)));
        Assert.That(options.MaxConcurrent, Is.EqualTo(10));
    }

    #endregion

    #region Dispose Tests

    /// <summary>
    /// Verifies that Dispose can be called multiple times without throwing.
    /// </summary>
    [Test]
    public void Dispose_MultipleTimes_ShouldNotThrow()
    {
        // Arrange
        var limiter = new RateLimiter();

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            limiter.Dispose();
            limiter.Dispose();
            limiter.Dispose();
        });
    }

    #endregion
}
