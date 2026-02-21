using System.Collections.Concurrent;

namespace BlazorClient.Services;

/// <summary>
/// Generic array pool to reduce allocations in hot paths (e.g., physics loop).
/// Thread-safe for potential future multi-threading scenarios.
/// </summary>
/// <typeparam name="T">The array element type.</typeparam>
public class ArrayPool<T>
{
    private readonly ConcurrentBag<T[]> _pool = new();
    private readonly int _maxPoolSize;
    private readonly int _defaultArraySize;

    /// <summary>
    /// Creates a new array pool.
    /// </summary>
    /// <param name="defaultArraySize">Default size for new arrays.</param>
    /// <param name="maxPoolSize">Maximum number of arrays to keep in the pool.</param>
    public ArrayPool(int defaultArraySize = 1024, int maxPoolSize = 10)
    {
        _defaultArraySize = defaultArraySize;
        _maxPoolSize = maxPoolSize;
    }

    /// <summary>
    /// Rents an array from the pool or creates a new one.
    /// </summary>
    /// <param name="minimumLength">Minimum required length.</param>
    /// <returns>An array of at least the specified length.</returns>
    public T[] Rent(int minimumLength)
    {
        if (_pool.TryTake(out var array) && array.Length >= minimumLength)
        {
            return array;
        }

        // If the taken array was too small, we don't return it (it's lost)
        // Create a new array with the required size
        return new T[Math.Max(minimumLength, _defaultArraySize)];
    }

    /// <summary>
    /// Returns an array to the pool for reuse.
    /// </summary>
    /// <param name="array">The array to return.</param>
    /// <param name="clearArray">Whether to clear the array before pooling.</param>
    public void Return(T[] array, bool clearArray = false)
    {
        if (array == null || _pool.Count >= _maxPoolSize)
            return;

        if (clearArray)
        {
            Array.Clear(array, 0, array.Length);
        }

        _pool.Add(array);
    }

    /// <summary>
    /// Gets the current number of pooled arrays.
    /// </summary>
    public int PooledCount => _pool.Count;

    /// <summary>
    /// Clears all pooled arrays.
    /// </summary>
    public void Clear()
    {
        while (_pool.TryTake(out _)) { }
    }
}

/// <summary>
/// Generic object pool for reusing instances.
/// </summary>
/// <typeparam name="T">The object type.</typeparam>
public class ObjectPool<T> where T : class, new()
{
    private readonly ConcurrentBag<T> _pool = new();
    private readonly int _maxPoolSize;
    private readonly Func<T>? _factory;
    private readonly Action<T>? _resetAction;

    /// <summary>
    /// Creates a new object pool.
    /// </summary>
    /// <param name="maxPoolSize">Maximum number of objects to keep in the pool.</param>
    /// <param name="factory">Optional factory function for creating new instances.</param>
    /// <param name="resetAction">Optional action to reset an object before reuse.</param>
    public ObjectPool(int maxPoolSize = 10, Func<T>? factory = null, Action<T>? resetAction = null)
    {
        _maxPoolSize = maxPoolSize;
        _factory = factory;
        _resetAction = resetAction;
    }

    /// <summary>
    /// Rents an object from the pool or creates a new one.
    /// </summary>
    public T Rent()
    {
        if (_pool.TryTake(out var item))
        {
            return item;
        }

        return _factory?.Invoke() ?? new T();
    }

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// </summary>
    public void Return(T item)
    {
        if (item == null || _pool.Count >= _maxPoolSize)
            return;

        _resetAction?.Invoke(item);
        _pool.Add(item);
    }

    /// <summary>
    /// Gets the current number of pooled objects.
    /// </summary>
    public int PooledCount => _pool.Count;
}

/// <summary>
/// Pooled array wrapper that automatically returns to pool on dispose.
/// </summary>
/// <typeparam name="T">The array element type.</typeparam>
public readonly struct PooledArray<T> : IDisposable
{
    private readonly ArrayPool<T> _pool;

    /// <summary>
    /// The rented array.
    /// </summary>
    public T[] Array { get; }

    /// <summary>
    /// The actual used length (may be less than array length).
    /// </summary>
    public int Length { get; }

    internal PooledArray(ArrayPool<T> pool, T[] array, int length)
    {
        _pool = pool;
        Array = array;
        Length = length;
    }

    /// <summary>
    /// Returns the array to the pool.
    /// </summary>
    public void Dispose()
    {
        _pool.Return(Array);
    }

    /// <summary>
    /// Gets a span of the used portion of the array.
    /// </summary>
    public Span<T> AsSpan() => Array.AsSpan(0, Length);

    /// <summary>
    /// Gets a read-only span of the used portion of the array.
    /// </summary>
    public ReadOnlySpan<T> AsReadOnlySpan() => Array.AsSpan(0, Length);
}

/// <summary>
/// Extension methods for array pools.
/// </summary>
public static class ArrayPoolExtensions
{
    /// <summary>
    /// Rents an array and wraps it in a disposable struct.
    /// </summary>
    public static PooledArray<T> RentDisposable<T>(this ArrayPool<T> pool, int length)
    {
        var array = pool.Rent(length);
        return new PooledArray<T>(pool, array, length);
    }
}
