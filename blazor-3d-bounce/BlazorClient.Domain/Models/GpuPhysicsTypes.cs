namespace BlazorClient.Domain.Models;

/// <summary>
/// GPU buffer data for a rigid body (matches WGSL struct layout).
/// This struct is used for efficient GPU buffer transfers.
/// </summary>
public struct GpuRigidBody
{
    // Position (12 bytes) + inverse mass (4 bytes)
    public Vector3 Position;
    public float InverseMass;

    // Rotation quaternion (16 bytes)
    public Quaternion Rotation;

    // Linear velocity (12 bytes) + restitution (4 bytes)
    public Vector3 LinearVelocity;
    public float Restitution;

    // Angular velocity (12 bytes) + friction (4 bytes)
    public Vector3 AngularVelocity;
    public float Friction;

    // Inverse inertia (12 bytes) + collider type (4 bytes)
    public Vector3 InverseInertia;
    public uint ColliderType;

    // Collider data (16 bytes) - radius for sphere, half-extents for AABB
    public Vector4 ColliderData;

    // Linear damping (4 bytes) + angular damping (4 bytes) + flags (4 bytes) + padding (4 bytes)
    public float LinearDamping;
    public float AngularDamping;
    public uint Flags;
    public float _padding;

    /// <summary>
    /// Total size in bytes (must match WGSL struct).
    /// </summary>
    public const int SizeInBytes = 112;
}

/// <summary>
/// GPU buffer data for a contact (matches WGSL struct layout).
/// </summary>
public struct GpuContact
{
    public uint BodyA;
    public uint BodyB;
    public uint Flags;
    public uint _pad0;

    public Vector3 Normal;
    public float Penetration;

    public Vector3 ContactPoint;
    public float NormalImpulse;

    public Vector3 Tangent1;
    public float TangentImpulse1;

    public Vector3 Tangent2;
    public float TangentImpulse2;

    /// <summary>
    /// Total size in bytes (must match WGSL struct).
    /// </summary>
    public const int SizeInBytes = 64;
}

/// <summary>
/// Simulation parameters for GPU physics (matches WGSL uniform struct).
/// </summary>
public struct GpuSimParams
{
    public Vector3 Gravity;
    public float DeltaTime;

    public uint NumBodies;
    public uint NumContacts;
    public uint SolverIterations;
    public uint EnableCCD;

    public float GridCellSize;
    public uint GridDimX;
    public uint GridDimY;
    public uint GridDimZ;

    /// <summary>
    /// Total size in bytes (must match WGSL struct).
    /// </summary>
    public const int SizeInBytes = 64;
}

/// <summary>
/// Collider types matching GPU constants.
/// </summary>
public static class GpuColliderType
{
    public const uint Sphere = 0;
    public const uint AABB = 1;
    public const uint Capsule = 2;
}

/// <summary>
/// Body flags matching GPU constants.
/// </summary>
[Flags]
public enum GpuBodyFlags : uint
{
    None = 0,
    Static = 1,
    Sleeping = 2,
    CcdEnabled = 4
}

/// <summary>
/// Vector4 for GPU data (WGSL vec4).
/// </summary>
public struct Vector4
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    public Vector4(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Vector4 Zero => new(0, 0, 0, 0);

    public float[] ToArray() => new[] { X, Y, Z, W };
}

/// <summary>
/// Axis-aligned bounding box for collision detection.
/// </summary>
public struct AABB
{
    public Vector3 Min { get; set; }
    public Vector3 Max { get; set; }

    public AABB(Vector3 min, Vector3 max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>
    /// Creates an AABB from center and half-extents.
    /// </summary>
    public static AABB FromCenterExtents(Vector3 center, Vector3 halfExtents)
    {
        return new AABB(
            new Vector3(center.X - halfExtents.X, center.Y - halfExtents.Y, center.Z - halfExtents.Z),
            new Vector3(center.X + halfExtents.X, center.Y + halfExtents.Y, center.Z + halfExtents.Z)
        );
    }

    /// <summary>
    /// Tests if this AABB overlaps with another.
    /// </summary>
    public bool Overlaps(AABB other)
    {
        return Min.X <= other.Max.X && Max.X >= other.Min.X &&
               Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
               Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
    }

    /// <summary>
    /// Expands the AABB to include a point.
    /// </summary>
    public AABB ExpandToInclude(Vector3 point)
    {
        return new AABB(
            new Vector3(
                Math.Min(Min.X, point.X),
                Math.Min(Min.Y, point.Y),
                Math.Min(Min.Z, point.Z)
            ),
            new Vector3(
                Math.Max(Max.X, point.X),
                Math.Max(Max.Y, point.Y),
                Math.Max(Max.Z, point.Z)
            )
        );
    }

    /// <summary>
    /// Returns the center of the AABB.
    /// </summary>
    public Vector3 Center => new(
        (Min.X + Max.X) * 0.5f,
        (Min.Y + Max.Y) * 0.5f,
        (Min.Z + Max.Z) * 0.5f
    );

    /// <summary>
    /// Returns the half-extents of the AABB.
    /// </summary>
    public Vector3 HalfExtents => new(
        (Max.X - Min.X) * 0.5f,
        (Max.Y - Min.Y) * 0.5f,
        (Max.Z - Min.Z) * 0.5f
    );
}

/// <summary>
/// Contact point information for collision response.
/// </summary>
public class ContactPoint
{
    /// <summary>
    /// First body in the collision.
    /// </summary>
    public string BodyAId { get; set; } = string.Empty;

    /// <summary>
    /// Second body in the collision (or null for ground).
    /// </summary>
    public string? BodyBId { get; set; }

    /// <summary>
    /// Contact normal (points from A to B).
    /// </summary>
    public Vector3 Normal { get; set; }

    /// <summary>
    /// Penetration depth.
    /// </summary>
    public float Penetration { get; set; }

    /// <summary>
    /// World-space contact position.
    /// </summary>
    public Vector3 Position { get; set; }

    /// <summary>
    /// Accumulated normal impulse (for warm-starting).
    /// </summary>
    public float NormalImpulse { get; set; }

    /// <summary>
    /// Accumulated friction impulse.
    /// </summary>
    public Vector3 FrictionImpulse { get; set; }
}

/// <summary>
/// Physics validation metrics for testing.
/// </summary>
public class PhysicsValidationMetrics
{
    /// <summary>
    /// Maximum penetration depth observed (should be near 0).
    /// </summary>
    public float MaxPenetration { get; set; }

    /// <summary>
    /// Maximum impulse magnitude applied.
    /// </summary>
    public float MaxImpulseMagnitude { get; set; }

    /// <summary>
    /// Total kinetic energy in the system.
    /// </summary>
    public float TotalKineticEnergy { get; set; }

    /// <summary>
    /// Total potential energy in the system.
    /// </summary>
    public float TotalPotentialEnergy { get; set; }

    /// <summary>
    /// Energy drift per second (should be near 0 for stable simulation).
    /// </summary>
    public float EnergyDriftPerSecond { get; set; }

    /// <summary>
    /// Number of tunneling events detected.
    /// </summary>
    public int TunnelingEvents { get; set; }

    /// <summary>
    /// Number of bodies currently sleeping.
    /// </summary>
    public int SleepingBodies { get; set; }

    /// <summary>
    /// Average solver iterations before convergence.
    /// </summary>
    public float AverageSolverIterations { get; set; }

    /// <summary>
    /// Frame time statistics.
    /// </summary>
    public float AveragePhysicsTimeMs { get; set; }
    public float MaxPhysicsTimeMs { get; set; }
    public float MinPhysicsTimeMs { get; set; }
}
