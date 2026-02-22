using BlazorClient.Application.Services;
using BlazorClient.Domain.Common;

namespace BlazorClient.Services;

/// <summary>
/// Extension methods for <see cref="IRateLimiter"/> providing
/// convenient rate-limited execution patterns.
/// </summary>
/// <remarks>
/// <para>
/// These extensions wrap actions and async functions with rate limiting,
/// automatically handling the check-then-execute pattern.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> UI Layer (extensions).
/// </para>
/// </remarks>
public static class RateLimiterExtensions
{
    /// <summary>
    /// Executes an action if rate limit allows.
    /// </summary>
    /// <param name="rateLimiter">The rate limiter.</param>
    /// <param name="key">The rate limit key.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>Success if executed, failure if rate limited or action threw.</returns>
    public static Result ExecuteWithRateLimit(
        this IRateLimiter rateLimiter,
        string key,
        Action action)
    {
        if (!rateLimiter.TryAcquire(key))
        {
            return Result.Failure($"Rate limit exceeded for key: {key}");
        }

        try
        {
            action();
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Executes an async function if rate limit allows.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="rateLimiter">The rate limiter.</param>
    /// <param name="key">The rate limit key.</param>
    /// <param name="func">The async function to execute.</param>
    /// <returns>The function result if executed, failure if rate limited.</returns>
    public static async Task<Result<T>> ExecuteWithRateLimitAsync<T>(
        this IRateLimiter rateLimiter,
        string key,
        Func<Task<Result<T>>> func)
    {
        if (!rateLimiter.TryAcquire(key))
        {
            var remaining = rateLimiter.GetRemainingQuota(key);
            return Result<T>.Failure($"Rate limit exceeded for key: {key}. Remaining quota: {remaining}");
        }

        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            return Result<T>.Failure(ex.Message);
        }
    }
}
