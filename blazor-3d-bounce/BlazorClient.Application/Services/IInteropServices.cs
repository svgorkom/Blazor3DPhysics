namespace BlazorClient.Application.Services;

/// <summary>
/// Interface for JavaScript interop operations.
/// </summary>
/// <remarks>
/// <para>
/// Abstracts JavaScript interop to allow for testing and potential
/// future non-JS implementations.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// The implementation wraps <see cref="Microsoft.JSInterop.IJSRuntime"/>.
/// </para>
/// <para>
/// <strong>Best Practice:</strong> Prefer specific service interfaces
/// (e.g., <see cref="IRenderingService"/>) over direct interop calls.
/// Use this interface for general-purpose JS calls not covered by
/// other services.
/// </para>
/// </remarks>
public interface IInteropService : IAsyncDisposable
{
    /// <summary>
    /// Initializes the interop bridge.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    /// <remarks>
    /// Call this before making any interop calls. This typically loads
    /// required JavaScript modules.
    /// </remarks>
    Task InitializeAsync();

    /// <summary>
    /// Invokes a JavaScript function without a return value.
    /// </summary>
    /// <param name="identifier">
    /// The function identifier (e.g., "MyModule.myFunction").
    /// </param>
    /// <param name="args">The function arguments (will be JSON-serialized).</param>
    /// <returns>A task representing the asynchronous invocation.</returns>
    Task InvokeVoidAsync(string identifier, params object?[] args);

    /// <summary>
    /// Invokes a JavaScript function with a return value.
    /// </summary>
    /// <typeparam name="T">The expected return type (JSON-deserializable).</typeparam>
    /// <param name="identifier">
    /// The function identifier (e.g., "MyModule.myFunction").
    /// </param>
    /// <param name="args">The function arguments (will be JSON-serialized).</param>
    /// <returns>The function result deserialized to type <typeparamref name="T"/>.</returns>
    Task<T> InvokeAsync<T>(string identifier, params object?[] args);
}

/// <summary>
/// Interface for caching JavaScript module references.
/// </summary>
/// <remarks>
/// <para>
/// Caches ES module imports to avoid repeated module loading overhead.
/// Module references are <see cref="Microsoft.JSInterop.IJSObjectReference"/>
/// instances that can be used for invoking module functions.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Performance:</strong> Preload frequently-used modules during
/// initialization to avoid loading delays during runtime.
/// </para>
/// </remarks>
public interface IJsModuleCache : IAsyncDisposable
{
    /// <summary>
    /// Gets a cached module reference, loading it if not already cached.
    /// </summary>
    /// <param name="modulePath">
    /// The module path relative to wwwroot (e.g., "./js/myModule.js").
    /// </param>
    /// <returns>The module reference for invoking module functions.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the module fails to load.
    /// </exception>
    Task<Microsoft.JSInterop.IJSObjectReference> GetModuleAsync(string modulePath);

    /// <summary>
    /// Preloads multiple modules for faster subsequent access.
    /// </summary>
    /// <param name="modulePaths">The module paths to preload.</param>
    /// <returns>A task representing the asynchronous preload operation.</returns>
    /// <remarks>
    /// Call this during application initialization to warm the cache.
    /// </remarks>
    Task PreloadModulesAsync(params string[] modulePaths);

    /// <summary>
    /// Clears all cached module references.
    /// </summary>
    /// <returns>A task representing the asynchronous clear operation.</returns>
    /// <remarks>
    /// This disposes all cached module references. They will be
    /// reloaded on next access.
    /// </remarks>
    Task ClearCacheAsync();
}
