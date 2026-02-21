using System.Collections.Concurrent;
using BlazorClient.Models;

namespace BlazorClient.Services;

/// <summary>
/// Rate limiter for controlling request frequency.
/// Prevents abuse and DoS attacks through excessive object spawning.
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Checks if an action is allowed within the rate limit.
    /// </summary>
    /// <param name="key">Identifier for the rate-limited action (e.g., "spawn_rigid").</param>
    /// <returns>True if the action is allowed, false if rate limit exceeded.</returns>
    bool TryAcquire(string key);

    /// <summary>
    /// Gets the remaining quota for a key.
    /// </summary>
    /// <param name="key">The rate limit key.</param>
    /// <returns>Number of requests remaining in the current window.</returns>
    int GetRemainingQuota(string key);

    /// <summary>
    /// Resets the rate limit for a specific key.
    /// </summary>
    /// <param name="key">The rate limit key to reset.</param>
    void Reset(string key);

    /// <summary>
    /// Resets all rate limits.
    /// </summary>
    void ResetAll();
}

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public class RateLimiterOptions
{
    /// <summary>
    /// Maximum number of requests allowed in the time window.
    /// </summary>
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Time window for rate limiting.
    /// </summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum concurrent operations (semaphore limit).
    /// </summary>
    public int MaxConcurrent { get; set; } = 10;
}

/// <summary>
/// Implementation of sliding window rate limiter.
/// Thread-safe and efficient for Blazor WebAssembly single-threaded environment.
/// </summary>
public class RateLimiter : IRateLimiter, IDisposable
{
    private readonly RateLimiterOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitWindow> _windows = new();
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    public RateLimiter(RateLimiterOptions? options = null)
    {
        _options = options ?? new RateLimiterOptions();
        _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrent, _options.MaxConcurrent);

        // Cleanup expired windows every minute
        _cleanupTimer = new Timer(CleanupExpiredWindows, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <inheritdoc />
    public bool TryAcquire(string key)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RateLimiter));

        var window = _windows.GetOrAdd(key, _ => new RateLimitWindow(_options.MaxRequests, _options.Window));
        return window.TryAcquire();
    }

    /// <inheritdoc />
    public int GetRemainingQuota(string key)
    {
        if (_windows.TryGetValue(key, out var window))
        {
            return window.GetRemainingQuota();
        }

        return _options.MaxRequests;
    }

    /// <inheritdoc />
    public void Reset(string key)
    {
        if (_windows.TryRemove(key, out var window))
        {
            window.Dispose();
        }
    }

    /// <inheritdoc />
    public void ResetAll()
    {
        foreach (var kvp in _windows)
        {
            kvp.Value.Dispose();
        }
        _windows.Clear();
    }

    /// <summary>
    /// Acquires a concurrency slot (for limiting simultaneous operations).
    /// </summary>
    /// <returns>A disposable that releases the slot when disposed.</returns>
    public async Task<IDisposable> AcquireConcurrencySlotAsync(CancellationToken cancellationToken = default)
    {
        await _concurrencySemaphore.WaitAsync(cancellationToken);
        return new ConcurrencySlot(_concurrencySemaphore);
    }

    private void CleanupExpiredWindows(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();

        foreach (var kvp in _windows)
        {
            if (kvp.Value.IsExpired(now))
            {
                expiredKeys.Add(kvp.Key);
            }
        }

        foreach (var key in expiredKeys)
        {
            if (_windows.TryRemove(key, out var window))
            {
                window.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cleanupTimer.Dispose();
        _concurrencySemaphore.Dispose();

        foreach (var window in _windows.Values)
        {
            window.Dispose();
        }
        _windows.Clear();
    }

    /// <summary>
    /// Represents a sliding time window for rate limiting.
    /// </summary>
    private class RateLimitWindow : IDisposable
    {
        private readonly int _maxRequests;
        private readonly TimeSpan _window;
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();
        private DateTime _lastAccess;

        public RateLimitWindow(int maxRequests, TimeSpan window)
        {
            _maxRequests = maxRequests;
            _window = window;
            _lastAccess = DateTime.UtcNow;
        }

        public bool TryAcquire()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                _lastAccess = now;

                // Remove expired timestamps
                var cutoff = now - _window;
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                {
                    _timestamps.Dequeue();
                }

                // Check if we're within the rate limit
                if (_timestamps.Count < _maxRequests)
                {
                    _timestamps.Enqueue(now);
                    return true;
                }

                return false;
            }
        }

        public int GetRemainingQuota()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var cutoff = now - _window;

                // Remove expired timestamps
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                {
                    _timestamps.Dequeue();
                }

                return Math.Max(0, _maxRequests - _timestamps.Count);
            }
        }

        public bool IsExpired(DateTime now)
        {
            lock (_lock)
            {
                // Consider window expired if no activity for 2x the window duration
                return (now - _lastAccess) > (_window * 2);
            }
        }

        public void Dispose()
        {
            lock (_lock)
            {
                _timestamps.Clear();
            }
        }
    }

    /// <summary>
    /// Disposable wrapper for concurrency semaphore slot.
    /// </summary>
    private class ConcurrencySlot : IDisposable
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public ConcurrencySlot(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _semaphore.Release();
        }
    }
}

/// <summary>
/// Extension methods for rate limiter integration.
/// </summary>
public static class RateLimiterExtensions
{
    /// <summary>
    /// Executes an action with rate limiting.
    /// </summary>
    public static Result ExecuteWithRateLimit(this IRateLimiter rateLimiter, string key, Action action)
    {
        if (!rateLimiter.TryAcquire(key))
        {
            return Result.Failure($"Rate limit exceeded for '{key}'. Please try again later.");
        }

        try
        {
            action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Action failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes an async action with rate limiting.
    /// </summary>
    public static async Task<Result<T>> ExecuteWithRateLimitAsync<T>(
        this IRateLimiter rateLimiter, 
        string key, 
        Func<Task<Result<T>>> action)
    {
        if (!rateLimiter.TryAcquire(key))
        {
            var remaining = rateLimiter.GetRemainingQuota(key);
            return Result<T>.Failure(
                $"Rate limit exceeded for '{key}'. Remaining quota: {remaining}. Please try again later.");
        }

        return await action();
    }
}
