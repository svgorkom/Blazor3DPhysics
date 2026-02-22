namespace BlazorClient.Domain.Models;

/// <summary>
/// Available rendering backends.
/// </summary>
public enum RendererBackend
{
    /// <summary>
    /// Automatically select the best available backend (WebGPU if available, then WebGL2).
    /// </summary>
    Auto,
    
    /// <summary>
    /// WebGPU rendering (modern, high-performance).
    /// </summary>
    WebGPU,
    
    /// <summary>
    /// WebGL 2.0 rendering (widely supported).
    /// </summary>
    WebGL2,
    
    /// <summary>
    /// WebGL 1.0 rendering (legacy fallback).
    /// </summary>
    WebGL
}

/// <summary>
/// Base class for all scene objects (rigid and soft bodies).
/// </summary>
public abstract class SceneObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Object";
    public TransformData Transform { get; set; } = new();
    public bool IsSelected { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a rigid body in the physics simulation.
/// </summary>
public class RigidBody : SceneObject
{
    public RigidPrimitiveType PrimitiveType { get; set; } = RigidPrimitiveType.Sphere;
    public PhysicsMaterial Material { get; set; } = new();
    public float Mass { get; set; } = 1.0f;
    public bool IsStatic { get; set; } = false;
    public bool IsSleeping { get; set; } = false;
    public bool EnableCCD { get; set; } = false;
    public float LinearDamping { get; set; } = 0.01f;
    public float AngularDamping { get; set; } = 0.01f;
    public Vector3 LinearVelocity { get; set; } = Vector3.Zero;
    public Vector3 AngularVelocity { get; set; } = Vector3.Zero;
    public string? MeshPath { get; set; }

    public RigidBody()
    {
        Name = "RigidBody";
    }

    public RigidBody(RigidPrimitiveType type, MaterialPreset preset = MaterialPreset.Rubber)
    {
        PrimitiveType = type;
        Material = PhysicsMaterial.FromPreset(preset);
        Name = $"{type}_{Id[..8]}";
    }
}

/// <summary>
/// Represents a soft body in the physics simulation.
/// </summary>
public class SoftBody : SceneObject
{
    public SoftBodyType Type { get; set; } = SoftBodyType.Cloth;
    public SoftBodyMaterial Material { get; set; } = new();
    
    // Mesh resolution
    public int ResolutionX { get; set; } = 20;
    public int ResolutionY { get; set; } = 20;
    public int Segments { get; set; } = 20;
    
    // Dimensions
    public float Width { get; set; } = 2.0f;
    public float Height { get; set; } = 2.0f;
    public float Depth { get; set; } = 2.0f; // For volumetric
    public float Length { get; set; } = 5.0f;
    public float Radius { get; set; } = 0.5f; // For volumetric sphere
    
    // Pinned vertices (indices)
    public List<int> PinnedVertices { get; set; } = new();
    
    // Anchor points for pins (world space)
    public List<Vector3> AnchorPositions { get; set; } = new();
    
    // Runtime data
    public float[]? DeformedVertices { get; set; }
    public float[]? DeformedNormals { get; set; }
    
    public string? MeshPath { get; set; }

    public SoftBody()
    {
        Name = "SoftBody";
    }

    public SoftBody(SoftBodyType type, SoftBodyPreset preset = SoftBodyPreset.DrapedCloth)
    {
        Type = type;
        Material = SoftBodyMaterial.FromPreset(preset);
        Name = $"{type}_{Id[..8]}";
        
        // Set default pins based on type
        if (type == SoftBodyType.Cloth && preset == SoftBodyPreset.FlagOnPole)
        {
            // Pin left edge for flag
            for (int i = 0; i <= ResolutionY; i++)
            {
                PinnedVertices.Add(i * (ResolutionX + 1));
            }
        }
    }
}

/// <summary>
/// Global simulation settings.
/// </summary>
public class SimulationSettings
{
    public Vector3 Gravity { get; set; } = Vector3.Gravity;
    public float TimeStep { get; set; } = 1f / 120f;
    public int SubSteps { get; set; } = 3;
    public bool IsPaused { get; set; } = false;
    public float TimeScale { get; set; } = 1.0f;
    public bool EnableSleeping { get; set; } = true;
    public float SleepThreshold { get; set; } = 0.01f;
}

/// <summary>
/// Rendering quality settings.
/// </summary>
public class RenderSettings
{
    /// <summary>
    /// Preferred rendering backend. Auto will select WebGPU if available, then WebGL2.
    /// </summary>
    public RendererBackend PreferredBackend { get; set; } = RendererBackend.Auto;
    
    public bool EnableShadows { get; set; } = true;
    public int ShadowMapSize { get; set; } = 2048;
    public bool EnableSSAO { get; set; } = false;
    public bool EnableFXAA { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool ShowAxes { get; set; } = true;
    public bool ShowWireframe { get; set; } = false;
    public bool ShowBoundingBoxes { get; set; } = false;
    public bool ShowDebugOverlay { get; set; } = false;
    public string? HdriPath { get; set; } = "assets/environment.hdr";
    
    /// <summary>
    /// Show the active renderer backend in the performance overlay.
    /// </summary>
    public bool ShowRendererInfo { get; set; } = true;
}

/// <summary>
/// Information about the active rendering backend.
/// </summary>
public class RendererInfo
{
    /// <summary>
    /// The active rendering backend (WebGPU, WebGL2, WebGL).
    /// </summary>
    public string Backend { get; set; } = "Unknown";
    
    /// <summary>
    /// GPU vendor name.
    /// </summary>
    public string? Vendor { get; set; }
    
    /// <summary>
    /// GPU renderer name.
    /// </summary>
    public string? Renderer { get; set; }
    
    /// <summary>
    /// Backend version string.
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// True if using WebGPU backend.
    /// </summary>
    public bool IsWebGPU { get; set; }
    
    /// <summary>
    /// True if fell back from preferred backend.
    /// </summary>
    public bool IsFallback { get; set; }
    
    /// <summary>
    /// Reason for fallback, if applicable.
    /// </summary>
    public string? FallbackReason { get; set; }
}

/// <summary>
/// Performance statistics.
/// </summary>
public class PerformanceStats
{
    public float Fps { get; set; }
    public float FrameTimeMs { get; set; }
    public float PhysicsTimeMs { get; set; }
    public float RenderTimeMs { get; set; }
    public int RigidBodyCount { get; set; }
    public int SoftBodyCount { get; set; }
    public int TotalVertices { get; set; }
    public int TotalTriangles { get; set; }
    public long UsedMemoryMB { get; set; }
    
    /// <summary>
    /// Active rendering backend name.
    /// </summary>
    public string ActiveBackend { get; set; } = "Unknown";
    
    /// <summary>
    /// 95th percentile frame time (ms).
    /// </summary>
    public float FrameTime95thPercentile { get; set; }
    
    /// <summary>
    /// 99th percentile frame time (ms).
    /// </summary>
    public float FrameTime99thPercentile { get; set; }
}

/// <summary>
/// Scene preset data for import/export.
/// </summary>
public class ScenePreset
{
    public string Name { get; set; } = "Untitled";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public SimulationSettings Settings { get; set; } = new();
    public RenderSettings RenderSettings { get; set; } = new();
    public List<RigidBody> RigidBodies { get; set; } = new();
    public List<SoftBody> SoftBodies { get; set; } = new();
}
