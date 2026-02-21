using System.Diagnostics;

namespace BlazorClient.Services;

/// <summary>
/// Performance snapshot containing current metrics.
/// </summary>
public record PerformanceSnapshot(
    float Fps,
    float AverageFrameTimeMs,
    float PhysicsTimeMs,
    float RenderTimeMs,
    float InteropTimeMs,
    int RigidBodyCount,
    int SoftBodyCount,
    long MemoryUsedBytes,
    int GcCollections);

/// <summary>
/// Interface for centralized performance monitoring.
/// </summary>
public interface IPerformanceMonitor
{
    /// <summary>
    /// Records a timing measurement for a specific category.
    /// </summary>
    void RecordTiming(string category, float milliseconds);

    /// <summary>
    /// Starts a timing measurement that will be recorded on dispose.
    /// </summary>
    IDisposable MeasureTiming(string category);

    /// <summary>
    /// Gets the average timing for a category over recent samples.
    /// </summary>
    float GetAverageTiming(string category);

    /// <summary>
    /// Gets the current performance snapshot.
    /// </summary>
    PerformanceSnapshot GetSnapshot();

    /// <summary>
    /// Updates the body counts for the snapshot.
    /// </summary>
    void UpdateBodyCounts(int rigidCount, int softCount);

    /// <summary>
    /// Records a frame for FPS calculation.
    /// </summary>
    void RecordFrame();

    /// <summary>
    /// Whether detailed profiling is enabled.
    /// </summary>
    bool DetailedProfilingEnabled { get; set; }

    /// <summary>
    /// Resets all recorded metrics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Implementation of performance monitoring service.
/// </summary>
public class PerformanceMonitor : IPerformanceMonitor
{
    private const int SampleCount = 60; // Keep last 60 samples for averaging
    private const int FpsWindowMs = 1000; // Calculate FPS over 1 second

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

    public bool DetailedProfilingEnabled { get; set; }

    public PerformanceMonitor()
    {
        _lastFpsCalculation = Stopwatch.GetTimestamp();
        _lastGcCount = GC.CollectionCount(0);

        // Pre-create common categories
        GetOrCreateBuffer("Physics");
        GetOrCreateBuffer("Render");
        GetOrCreateBuffer("Interop");
        GetOrCreateBuffer("Frame");
    }

    /// <inheritdoc />
    public void RecordTiming(string category, float milliseconds
    )
    {
        var buffer = GetOrCreateBuffer(category);
        lock (_lock)
        {
            buffer.Add(milliseconds);
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
        var gcCount = GC.CollectionCount(0) - _lastGcCount;

        return new PerformanceSnapshot(
            Fps: _currentFps,
            AverageFrameTimeMs: GetAverageTiming("Frame"),
            PhysicsTimeMs: GetAverageTiming("Physics"),
            RenderTimeMs: GetAverageTiming("Render"),
            InteropTimeMs: GetAverageTiming("Interop"),
            RigidBodyCount: _rigidBodyCount,
            SoftBodyCount: _softBodyCount,
            MemoryUsedBytes: GC.GetTotalMemory(false),
            GcCollections: gcCount
        );
    }

    /// <inheritdoc />
    public void UpdateBodyCounts(int rigidCount, int softCount)
    {
        _rigidBodyCount = rigidCount;
        _softBodyCount = softCount;
    }

    /// <inheritdoc />
    public void RecordFrame()
    {
        _frameCount++;

        var now = Stopwatch.GetTimestamp();
        var elapsed = (now - _lastFpsCalculation) * 1000.0 / Stopwatch.Frequency;

        if (elapsed >= FpsWindowMs)
        {
            _currentFps = (float)(_frameCount * 1000.0 / elapsed);
            _frameCount = 0;
            _lastFpsCalculation = now;
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
        _lastGcCount = GC.CollectionCount(0);
    }

    private CircularBuffer<float> GetOrCreateBuffer(string category)
    {
        lock (_lock)
        {
            if (!_timings.TryGetValue(category, out var buffer))
            {
                buffer = new CircularBuffer<float>(SampleCount);
                _timings[category] = buffer;
            }
            return buffer;
        }
    }

    /// <summary>
    /// Disposable timing scope that records duration on dispose.
    /// </summary>
    private class TimingScope : IDisposable
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
/// Simple circular buffer for storing recent samples.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal class CircularBuffer<T>
{
    private readonly T[] _buffer;
    private int _head;
    private int _count;

    public CircularBuffer(int capacity)
    {
        _buffer = new T[capacity];
    }

    public int Count => _count;
    public int Capacity => _buffer.Length;

    public void Add(T item)
    {
        _buffer[_head] = item;
        _head = (_head + 1) % _buffer.Length;
        if (_count < _buffer.Length)
            _count++;
    }

    public void Clear()
    {
        _head = 0;
        _count = 0;
        Array.Clear(_buffer, 0, _buffer.Length);
    }

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

    public IEnumerable<T> GetItems()
    {
        for (var i = 0; i < _count; i++)
        {
            var index = (_head - _count + i + _buffer.Length) % _buffer.Length;
            yield return _buffer[index];
        }
    }
}
