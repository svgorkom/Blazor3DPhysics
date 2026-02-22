using System.Diagnostics;
using BlazorClient.Application.Services;

namespace BlazorClient.Infrastructure.Services;

/// <summary>
/// Implementation of performance monitoring service.
/// </summary>
/// <remarks>
/// <para>
/// Provides centralized performance tracking for physics, rendering, and interop operations.
/// Uses circular buffers for efficient sample storage with constant memory usage.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Infrastructure Layer (implementations).
/// Implements <see cref="IPerformanceMonitor"/> from the Application layer.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> This implementation is thread-safe for Blazor WebAssembly's
/// single-threaded environment. For server-side Blazor, additional synchronization may be needed.
/// </para>
/// </remarks>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly PerformanceMonitorOptions _options;
    private readonly Dictionary<string, CircularBuffer<float>> _timings = new();
    private readonly object _lock = new();

    // FPS tracking
    private int _frameCount;
    private long _lastFpsCalculation;
    private float _currentFps;

    // Body counts
    private int _rigidBodyCount;
    private int _softBodyCount;

    // GC tracking
    private int _lastGcCount;

    /// <inheritdoc />
    public bool DetailedProfilingEnabled 
    { 
        get => _options.DetailedProfilingEnabled; 
        set => _options.DetailedProfilingEnabled = value; 
    }

    /// <inheritdoc />
    public PerformanceMonitorOptions Options => _options;

    /// <summary>
    /// Initializes a new instance with default options.
    /// </summary>
    public PerformanceMonitor() : this(new PerformanceMonitorOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance with the specified options.
    /// </summary>
    /// <param name="options">The configuration options.</param>
    public PerformanceMonitor(PerformanceMonitorOptions options)
    {
        _options = options ?? new PerformanceMonitorOptions();
        _lastFpsCalculation = Stopwatch.GetTimestamp();
        
        if (_options.TrackGarbageCollection)
        {
            _lastGcCount = GC.CollectionCount(0);
        }

        // Pre-create common categories for better performance
        GetOrCreateBuffer("Physics");
        GetOrCreateBuffer("Render");
        GetOrCreateBuffer("Interop");
        GetOrCreateBuffer("Frame");
    }

    /// <inheritdoc />
    public void RecordTiming(string category, float milliseconds)
    {
        if (!DetailedProfilingEnabled && category != "Frame" && category != "Physics")
        {
            // Skip recording for non-essential categories when profiling is disabled
            return;
        }

        var buffer = GetOrCreateBuffer(category);
        lock (_lock)
        {
            buffer.Add(milliseconds);
        }

        // Log performance warnings if enabled
        if (_options.LogPerformanceWarnings)
        {
            CheckPerformanceWarnings(category, milliseconds);
        }
    }

    /// <inheritdoc />
    public IDisposable MeasureTiming(string category)
    {
        return new TimingScope(this, category);
    }

    /// <inheritdoc />
    public float GetAverageTiming(string category)
    {
        lock (_lock)
        {
            if (!_timings.TryGetValue(category, out var buffer) || buffer.Count == 0)
                return 0f;

            return buffer.Average();
        }
    }

    /// <inheritdoc />
    public PerformanceSnapshot GetSnapshot()
    {
        var gcCount = 0;
        if (_options.TrackGarbageCollection)
        {
            gcCount = GC.CollectionCount(0) - _lastGcCount;
        }

        var memoryUsed = 0L;
        if (_options.TrackMemory)
        {
            memoryUsed = GC.GetTotalMemory(false);
        }

        return new PerformanceSnapshot
        {
            Fps = _currentFps,
            AverageFrameTimeMs = GetAverageTiming("Frame"),
            PhysicsTimeMs = GetAverageTiming("Physics"),
            RenderTimeMs = GetAverageTiming("Render"),
            InteropTimeMs = GetAverageTiming("Interop"),
            RigidBodyCount = _rigidBodyCount,
            SoftBodyCount = _softBodyCount,
            MemoryUsedBytes = memoryUsed,
            GcCollections = gcCount
        };
    }

    /// <inheritdoc />
    public void RecordObjectCounts(int rigidBodies, int softBodies)
    {
        _rigidBodyCount = rigidBodies;
        _softBodyCount = softBodies;
    }

    /// <inheritdoc />
    public void UpdateBodyCounts(int rigidBodies, int softBodies)
    {
        RecordObjectCounts(rigidBodies, softBodies);
    }

    /// <summary>
    /// Records a frame for FPS calculation.
    /// </summary>
    /// <remarks>
    /// Call this once per frame to update FPS calculations.
    /// </remarks>
    public void RecordFrame()
    {
        _frameCount++;

        var now = Stopwatch.GetTimestamp();
        var elapsed = (now - _lastFpsCalculation) * 1000.0 / Stopwatch.Frequency;

        if (elapsed >= _options.FpsWindowMs)
        {
            _currentFps = (float)(_frameCount * 1000.0 / elapsed);
            _frameCount = 0;
            _lastFpsCalculation = now;

            // Check FPS warnings
            if (_options.LogPerformanceWarnings && _currentFps < _options.FpsWarningThreshold)
            {
                Console.WriteLine($"[Performance Warning] Low FPS detected: {_currentFps:F1} (threshold: {_options.FpsWarningThreshold})");
            }
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_lock)
        {
            foreach (var buffer in _timings.Values)
            {
                buffer.Clear();
            }
        }

        _frameCount = 0;
        _currentFps = 0;
        _lastFpsCalculation = Stopwatch.GetTimestamp();
        
        if (_options.TrackGarbageCollection)
        {
            _lastGcCount = GC.CollectionCount(0);
        }
    }

    private CircularBuffer<float> GetOrCreateBuffer(string category)
    {
        lock (_lock)
        {
            if (!_timings.TryGetValue(category, out var buffer))
            {
                buffer = new CircularBuffer<float>(_options.SampleCount);
                _timings[category] = buffer;
            }
            return buffer;
        }
    }

    private void CheckPerformanceWarnings(string category, float milliseconds)
    {
        if (category == "Frame" && milliseconds > _options.FrameTimeWarningThresholdMs)
        {
            Console.WriteLine($"[Performance Warning] High frame time: {milliseconds:F2}ms (threshold: {_options.FrameTimeWarningThresholdMs}ms)");
        }
        else if (category == "Physics" && milliseconds > 16f) // Physics should complete within 16ms
        {
            Console.WriteLine($"[Performance Warning] High physics time: {milliseconds:F2}ms");
        }
    }

    /// <summary>
    /// Disposable timing scope that records duration on dispose.
    /// </summary>
    private sealed class TimingScope : IDisposable
    {
        private readonly PerformanceMonitor _monitor;
        private readonly string _category;
        private readonly long _startTimestamp;

        public TimingScope(PerformanceMonitor monitor, string category)
        {
            _monitor = monitor;
            _category = category;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var elapsed = Stopwatch.GetTimestamp() - _startTimestamp;
            var milliseconds = (float)(elapsed * 1000.0 / Stopwatch.Frequency);
            _monitor.RecordTiming(_category, milliseconds);
        }
    }
}

/// <summary>
/// Simple circular buffer for storing recent samples with O(1) operations.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <remarks>
/// <para>
/// Provides fixed-size sample storage that automatically overwrites
/// oldest samples when full.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. External synchronization required.
/// </para>
/// </remarks>
internal sealed class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    /// <summary>
    /// Initializes a new circular buffer with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of elements to store.</param>
    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    /// <summary>
    /// Gets the current number of elements in the buffer.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the maximum capacity of the buffer.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Adds an item to the buffer, overwriting the oldest item if full.
    /// </summary>
    /// <param name="item">The item to add.</param>
    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
            _count++;
    }

    /// <summary>
    /// Clears all items from the buffer.
    /// </summary>
    public void Clear()
    {
        _head = 0;
        _count = 0;
        Array.Clear(_buffer, 0, _buffer.Length);
    }

    /// <summary>
    /// Calculates the average of all items in the buffer.
    /// </summary>
    /// <returns>The average value, or 0 if empty.</returns>
    public float Average()
    {
        if (_count == 0) return 0f;

        var sum = 0.0;
        for (var i = 0; i < _count; i++)
        {
            sum += Convert.ToDouble(_buffer[i]);
        }
        return (float)(sum / _count);
    }

    /// <summary>
    /// Gets all items in the buffer in order from oldest to newest.
    /// </summary>
    /// <returns>Enumerable of items.</returns>
    public IEnumerable<T> GetItems()
    {
        for (var i = 0; i < _count; i++)
        {
            var index = (_head - _count + i + _buffer.Length) % _buffer.Length;
            yield return _buffer[index];
        }
    }
}
