namespace BlazorClient.Domain.Models;

/// <summary>
/// Capabilities of available rendering backends.
/// </summary>
public class RendererCapabilities
{
    public BackendCapability WebGPU { get; set; } = new();
    public BackendCapability WebGL2 { get; set; } = new();
    public BackendCapability WebGL { get; set; } = new();
}

/// <summary>
/// Capability information for a single backend.
/// </summary>
public class BackendCapability
{
    public bool IsSupported { get; set; }
    public string? Vendor { get; set; }
    public string? Renderer { get; set; }
    public string? Version { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Performance metrics from the renderer.
/// </summary>
public class RendererPerformanceMetrics
{
    public string Backend { get; set; } = "Unknown";
    public float Fps { get; set; }
    public float FrameTimeMs { get; set; }
    public float Percentile95 { get; set; }
    public float Percentile99 { get; set; }
    public float MinFrameTime { get; set; }
    public float MaxFrameTime { get; set; }
    public int DrawCalls { get; set; }
    public int TriangleCount { get; set; }
}

/// <summary>
/// Results from a rendering benchmark.
/// </summary>
public class BenchmarkResults
{
    public BenchmarkResult? WebGPU { get; set; }
    public BenchmarkResult? WebGL2 { get; set; }
    public string? Recommendation { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Benchmark result for a single backend.
/// </summary>
public class BenchmarkResult
{
    public float AvgFrameTime { get; set; }
    public float MinFrameTime { get; set; }
    public float MaxFrameTime { get; set; }
    public int Iterations { get; set; }
    public string? Error { get; set; }
}
