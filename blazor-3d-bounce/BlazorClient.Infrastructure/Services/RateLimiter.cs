using System.Collections.Concurrent;
using BlazorClient.Application.Services;
using BlazorClient.Domain.Common;

namespace BlazorClient.Infrastructure.Services;

/// <summary>
/// Implementation of sliding window rate limiter.
/// </summary>
/// <remarks>
/// <para>
/// Provides rate limiting using a sliding time window algorithm.
/// Each key maintains its own independent rate limit window.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Infrastructure Layer (implementations).
/// Implements <see cref="IRateLimiter"/> from the Application layer.
/// </para>
/// <para>
/// <strong>Algorithm:</strong> Sliding window - tracks individual request
/// timestamps and removes expired ones on each check.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Thread-safe implementation suitable for
/// concurrent access, though Blazor WebAssembly is single-threaded.
/// </para>
/// </remarks>
public class RateLimiter : IRateLimiter, IDisposable
{
    private readonly RateLimiterOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitWindow> _windows = new();
    private readonly SemaphoreSlim _concurrencySemaphore;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new rate limiter with the specified options.
    /// </summary>
    /// <param name="options">
    /// Configuration options. If null, default options are used.
    /// </param>
    public RateLimiter(RateLimiterOptions? options = null)
    {
        _options = options ?? new RateLimiterOptions();
        _concurrencySemaphore = new SemaphoreSlim(_options.MaxConcurrent, _options.MaxConcurrent);

        // Cleanup expired windows every minute to prevent memory leaks
        _cleanupTimer = new Timer(CleanupExpiredWindows, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    /// <inheritdoc />
    public bool TryAcquire(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

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
    /// Acquires a concurrency slot for limiting simultaneous operations.
    /// </summary>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A disposable that releases the slot when disposed.
    /// </returns>
    /// <remarks>
    /// Use this to limit the number of concurrent heavy operations
    /// (e.g., file processing, complex calculations).
    /// </remarks>
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

    /// <summary>
    /// Disposes the rate limiter and all its resources.
    /// </summary>
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
    private sealed class RateLimitWindow : IDisposable
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
    private sealed class ConcurrencySlot : IDisposable
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
/// <remarks>
/// Provides convenient wrappers for executing actions with rate limiting.
/// </remarks>
public static class RateLimiterExtensions
{
    /// <summary>
    /// Executes a synchronous action with rate limiting.
    /// </summary>
    /// <param name="rateLimiter">The rate limiter.</param>
    /// <param name="key">The rate limit key.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>
    /// A result indicating success or rate limit failure.
    /// </returns>
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
    /// Executes an asynchronous action with rate limiting.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="rateLimiter">The rate limiter.</param>
    /// <param name="key">The rate limit key.</param>
    /// <param name="action">The async action to execute.</param>
    /// <returns>
    /// A result containing the action result or rate limit failure.
    /// </returns>
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
