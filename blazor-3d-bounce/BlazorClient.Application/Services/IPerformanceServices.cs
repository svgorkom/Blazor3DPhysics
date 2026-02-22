namespace BlazorClient.Application.Services;

/// <summary>
/// Configuration options for the performance monitor.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (configuration).
/// </para>
/// </remarks>
public class PerformanceMonitorOptions
{
    /// <summary>
    /// Whether detailed profiling is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to record all timing categories; <c>false</c> to record
    /// only essential categories (Frame, Physics). Default is <c>false</c>.
    /// </value>
    /// <remarks>
    /// Enable this during development for detailed performance analysis.
    /// Disable in production to reduce overhead.
    /// </remarks>
    public bool DetailedProfilingEnabled { get; set; } = false;

    /// <summary>
    /// Number of samples to keep for averaging calculations.
    /// </summary>
    /// <value>
    /// The number of recent samples to retain. Default is 60 (approximately
    /// 1 second at 60 FPS).
    /// </value>
    public int SampleCount { get; set; } = 60;

    /// <summary>
    /// Time window for FPS calculation in milliseconds.
    /// </summary>
    /// <value>
    /// The time window over which to calculate FPS. Default is 1000ms (1 second).
    /// </value>
    public int FpsWindowMs { get; set; } = 1000;

    /// <summary>
    /// Whether to track memory usage.
    /// </summary>
    /// <value>
    /// <c>true</c> to track GC heap memory; <c>false</c> to skip memory tracking.
    /// Default is <c>true</c>.
    /// </value>
    public bool TrackMemory { get; set; } = true;

    /// <summary>
    /// Whether to track garbage collection.
    /// </summary>
    /// <value>
    /// <c>true</c> to track GC collection counts; <c>false</c> to skip.
    /// Default is <c>true</c>.
    /// </value>
    public bool TrackGarbageCollection { get; set; } = true;

    /// <summary>
    /// Whether to log performance warnings to the console.
    /// </summary>
    /// <value>
    /// <c>true</c> to log warnings when thresholds are exceeded; <c>false</c> to disable.
    /// Default is <c>true</c>.
    /// </value>
    public bool LogPerformanceWarnings { get; set; } = true;

    /// <summary>
    /// FPS threshold below which to warn.
    /// </summary>
    /// <value>
    /// FPS values below this threshold trigger a warning. Default is 30 FPS.
    /// </value>
    public float FpsWarningThreshold { get; set; } = 30f;

    /// <summary>
    /// Frame time threshold above which to warn (in milliseconds).
    /// </summary>
    /// <value>
    /// Frame times above this threshold trigger a warning. Default is 33ms (~30 FPS).
    /// </value>
    public float FrameTimeWarningThresholdMs { get; set; } = 33f;
}

/// <summary>
/// Snapshot of performance metrics at a point in time.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (DTOs).
/// </para>
/// <para>
/// Use this for displaying performance information in the UI or
/// for logging/diagnostics.
/// </para>
/// </remarks>
public class PerformanceSnapshot
{
    /// <summary>
    /// Current frames per second.
    /// </summary>
    /// <value>The calculated FPS over the configured time window.</value>
    public float Fps { get; set; }

    /// <summary>
    /// Average frame time in milliseconds.
    /// </summary>
    /// <value>The average time per frame over recent samples.</value>
    public float AverageFrameTimeMs { get; set; }

    /// <summary>
    /// Physics calculation time in milliseconds.
    /// </summary>
    /// <value>Average time spent in physics calculations per frame.</value>
    public float PhysicsTimeMs { get; set; }

    /// <summary>
    /// Render time in milliseconds.
    /// </summary>
    /// <value>Average time spent in rendering per frame.</value>
    public float RenderTimeMs { get; set; }

    /// <summary>
    /// Interop time in milliseconds.
    /// </summary>
    /// <value>Average time spent in JavaScript interop per frame.</value>
    public float InteropTimeMs { get; set; }

    /// <summary>
    /// Number of active rigid bodies.
    /// </summary>
    /// <value>Current count of rigid bodies in the scene.</value>
    public int RigidBodyCount { get; set; }

    /// <summary>
    /// Number of active soft bodies.
    /// </summary>
    /// <value>Current count of soft bodies in the scene.</value>
    public int SoftBodyCount { get; set; }

    /// <summary>
    /// Memory used in bytes.
    /// </summary>
    /// <value>Current GC heap memory usage in bytes.</value>
    public long MemoryUsedBytes { get; set; }

    /// <summary>
    /// Number of GC collections since last snapshot.
    /// </summary>
    /// <value>Incremental GC collection count.</value>
    public int GcCollections { get; set; }
}

/// <summary>
/// Interface for performance monitoring and profiling.
/// </summary>
/// <remarks>
/// <para>
/// Provides centralized performance tracking for physics, rendering,
/// and interop operations.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong> Use <see cref="MeasureTiming"/> for
/// automatic timing of code blocks:
/// <code>
/// using (performanceMonitor.MeasureTiming("Physics"))
/// {
///     await physicsService.StepAsync(deltaTime);
/// }
/// </code>
/// </para>
/// </remarks>
public interface IPerformanceMonitor
{
    /// <summary>
    /// Gets or sets whether detailed profiling is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to record all categories; <c>false</c> for essential only.
    /// </value>
    bool DetailedProfilingEnabled { get; set; }

    /// <summary>
    /// Gets the monitor configuration options.
    /// </summary>
    /// <value>The current configuration options.</value>
    PerformanceMonitorOptions Options { get; }

    /// <summary>
    /// Records a timing measurement for a category.
    /// </summary>
    /// <param name="category">
    /// The category name (e.g., "Physics", "Render", "Interop").
    /// </param>
    /// <param name="elapsedMs">The elapsed time in milliseconds.</param>
    void RecordTiming(string category, float elapsedMs);

    /// <summary>
    /// Creates a timing scope that automatically records duration on dispose.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>
    /// A disposable that records the elapsed time when disposed.
    /// </returns>
    IDisposable MeasureTiming(string category);

    /// <summary>
    /// Gets the average timing for a category over recent samples.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>The average timing in milliseconds, or 0 if no samples.</returns>
    float GetAverageTiming(string category);

    /// <summary>
    /// Gets a snapshot of current performance metrics.
    /// </summary>
    /// <returns>A snapshot containing all current metrics.</returns>
    PerformanceSnapshot GetSnapshot();

    /// <summary>
    /// Records the current object counts for the snapshot.
    /// </summary>
    /// <param name="rigidBodies">Number of rigid bodies.</param>
    /// <param name="softBodies">Number of soft bodies.</param>
    void RecordObjectCounts(int rigidBodies, int softBodies);

    /// <summary>
    /// Updates body counts (alias for RecordObjectCounts).
    /// </summary>
    /// <param name="rigidBodies">Number of rigid bodies.</param>
    /// <param name="softBodies">Number of soft bodies.</param>
    void UpdateBodyCounts(int rigidBodies, int softBodies);

    /// <summary>
    /// Records the end of a frame for FPS calculation.
    /// </summary>
    void RecordFrame();

    /// <summary>
    /// Resets all recorded metrics.
    /// </summary>
    void Reset();
}

/// <summary>
/// Configuration options for the rate limiter.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (configuration).
/// </para>
/// </remarks>
public class RateLimiterOptions
{
    /// <summary>
    /// Maximum requests allowed per time window.
    /// </summary>
    /// <value>
    /// The maximum number of requests before rate limiting kicks in.
    /// Default is 100.
    /// </value>
    public int MaxRequests { get; set; } = 100;

    /// <summary>
    /// Time window duration for rate limiting.
    /// </summary>
    /// <value>
    /// The sliding window duration. Default is 1 minute.
    /// </value>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Maximum concurrent operations allowed.
    /// </summary>
    /// <value>
    /// The maximum number of simultaneous operations. Default is 10.
    /// </value>
    public int MaxConcurrent { get; set; } = 10;
}

/// <summary>
/// Interface for rate limiting operations.
/// </summary>
/// <remarks>
/// <para>
/// Prevents abuse through excessive operations (e.g., spawning thousands
/// of objects rapidly).
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Algorithm:</strong> Sliding window rate limiting.
/// </para>
/// </remarks>
public interface IRateLimiter
{
    /// <summary>
    /// Attempts to acquire a permit for the specified key.
    /// </summary>
    /// <param name="key">
    /// The rate limit key (e.g., "spawn_rigid", "save_scene").
    /// </param>
    /// <returns>
    /// <c>true</c> if the operation is allowed; <c>false</c> if rate limited.
    /// </returns>
    bool TryAcquire(string key);

    /// <summary>
    /// Gets the remaining quota for a key.
    /// </summary>
    /// <param name="key">The rate limit key.</param>
    /// <returns>
    /// The number of requests remaining in the current window.
    /// </returns>
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
/// Interface for a generic object pool.
/// </summary>
/// <typeparam name="T">The type of objects to pool. Must be a reference type.</typeparam>
/// <remarks>
/// <para>
/// Reduces garbage collection pressure by reusing objects instead of
/// allocating new ones.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Application Layer (contracts/ports).
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Implementations should be thread-safe
/// if used in concurrent scenarios.
/// </para>
/// </remarks>
public interface IObjectPool<T> where T : class
{
    /// <summary>
    /// Rents an object from the pool.
    /// </summary>
    /// <returns>
    /// A pooled object. May be a new instance if the pool is empty.
    /// </returns>
    /// <remarks>
    /// The caller is responsible for returning the object via
    /// <see cref="Return"/> when done.
    /// </remarks>
    T Rent();

    /// <summary>
    /// Returns an object to the pool for reuse.
    /// </summary>
    /// <param name="obj">The object to return.</param>
    /// <remarks>
    /// Do not use the object after returning it to the pool.
    /// The object may be reset or reused immediately.
    /// </remarks>
    void Return(T obj);
}
