using Microsoft.JSInterop;

namespace BlazorClient.Services;

/// <summary>
/// Caches JavaScript module references to avoid repeated lookups.
/// Improves interop performance by reusing module references.
/// </summary>
public interface IJsModuleCache : IAsyncDisposable
{
    /// <summary>
    /// Gets or imports a JavaScript module.
    /// </summary>
    /// <param name="modulePath">The path to the module.</param>
    /// <returns>The module reference.</returns>
    ValueTask<IJSObjectReference> GetModuleAsync(string modulePath);

    /// <summary>
    /// Pre-loads multiple modules for faster access later.
    /// </summary>
    Task PreloadModulesAsync(params string[] modulePaths);

    /// <summary>
    /// Checks if a module is already cached.
    /// </summary>
    bool IsCached(string modulePath);

    /// <summary>
    /// Removes a module from the cache and disposes it.
    /// </summary>
    Task RemoveModuleAsync(string modulePath);

    /// <summary>
    /// Gets statistics about cached modules.
    /// </summary>
    JsModuleCacheStats GetStats();
}

/// <summary>
/// Statistics about the JS module cache.
/// </summary>
public record JsModuleCacheStats(
    int CachedModuleCount,
    IReadOnlyList<string> CachedModulePaths);

/// <summary>
/// Implementation of JS module caching.
/// </summary>
public class JsModuleCache : IJsModuleCache
{
    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<string, IJSObjectReference> _modules = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private bool _disposed;

    public JsModuleCache(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public async ValueTask<IJSObjectReference> GetModuleAsync(string modulePath)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(JsModuleCache));

        // Fast path - check without lock
        if (_modules.TryGetValue(modulePath, out var cachedModule))
        {
            return cachedModule;
        }

        // Slow path - acquire lock and import
        await _semaphore.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_modules.TryGetValue(modulePath, out cachedModule))
            {
                return cachedModule;
            }

            var module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", modulePath);
            _modules[modulePath] = module;
            return module;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task PreloadModulesAsync(params string[] modulePaths)
    {
        var tasks = modulePaths.Select(path => GetModuleAsync(path).AsTask());
        await Task.WhenAll(tasks);
    }

    /// <inheritdoc />
    public bool IsCached(string modulePath)
    {
        return _modules.ContainsKey(modulePath);
    }

    /// <inheritdoc />
    public async Task RemoveModuleAsync(string modulePath)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_modules.TryGetValue(modulePath, out var module))
            {
                _modules.Remove(modulePath);
                await module.DisposeAsync();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <inheritdoc />
    public JsModuleCacheStats GetStats()
    {
        return new JsModuleCacheStats(
            _modules.Count,
            _modules.Keys.ToList());
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _semaphore.WaitAsync();
        try
        {
            foreach (var module in _modules.Values)
            {
                try
                {
                    await module.DisposeAsync();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }
            _modules.Clear();
        }
        finally
        {
            _semaphore.Release();
            _semaphore.Dispose();
        }
    }
}
