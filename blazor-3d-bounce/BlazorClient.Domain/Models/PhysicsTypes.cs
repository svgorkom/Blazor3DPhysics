using System.Globalization;

namespace BlazorClient.Domain.Models;

/// <summary>
/// Represents a 3D vector with X, Y, Z components.
/// </summary>
public struct Vector3
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static Vector3 Zero => new(0, 0, 0);
    public static Vector3 One => new(1, 1, 1);
    public static Vector3 Up => new(0, 1, 0);
    public static Vector3 Down => new(0, -1, 0);
    public static Vector3 Gravity => new(0, -9.81f, 0);

    public float[] ToArray() => new[] { X, Y, Z };

    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", X, Y, Z);
}

/// <summary>
/// Represents a quaternion rotation.
/// </summary>
public struct Quaternion
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float W { get; set; }

    public Quaternion(float x, float y, float z, float w)
    {
        X = x;
        Y = y;
        Z = z;
        W = w;
    }

    public static Quaternion Identity => new(0, 0, 0, 1);

    public float[] ToArray() => new[] { X, Y, Z, W };
}

/// <summary>
/// Transform data for physics bodies.
/// </summary>
public class TransformData
{
    public Vector3 Position { get; set; } = Vector3.Zero;
    public Quaternion Rotation { get; set; } = Quaternion.Identity;
    public Vector3 Scale { get; set; } = Vector3.One;
}

/// <summary>
/// Material presets for rigid bodies.
/// </summary>
public enum MaterialPreset
{
    Rubber,
    Wood,
    Steel,
    Ice,
    Custom
}

/// <summary>
/// Types of rigid body primitives.
/// </summary>
public enum RigidPrimitiveType
{
    Sphere,
    Box,
    Capsule,
    Cylinder,
    Cone
}

/// <summary>
/// Types of soft body primitives.
/// </summary>
public enum SoftBodyType
{
    Cloth,
    Rope,
    Volumetric
}

/// <summary>
/// Soft body presets.
/// </summary>
public enum SoftBodyPreset
{
    DrapedCloth,
    RopePendulum,
    JellyCube,
    FlagOnPole,
    ClothStack,
    Custom
}

/// <summary>
/// Physics material properties for rigid bodies.
/// </summary>
public class PhysicsMaterial
{
    public string Name { get; set; } = "Default";
    public float Restitution { get; set; } = 0.5f;
    public float StaticFriction { get; set; } = 0.5f;
    public float DynamicFriction { get; set; } = 0.4f;
    public float Density { get; set; } = 1000f; // kg/m³

    public static PhysicsMaterial Rubber => new()
    {
        Name = "Rubber",
        Restitution = 0.8f,
        StaticFriction = 0.9f,
        DynamicFriction = 0.8f,
        Density = 1100f
    };

    public static PhysicsMaterial Wood => new()
    {
        Name = "Wood",
        Restitution = 0.4f,
        StaticFriction = 0.5f,
        DynamicFriction = 0.4f,
        Density = 700f
    };

    public static PhysicsMaterial Steel => new()
    {
        Name = "Steel",
        Restitution = 0.6f,
        StaticFriction = 0.6f,
        DynamicFriction = 0.4f,
        Density = 7800f
    };

    public static PhysicsMaterial Ice => new()
    {
        Name = "Ice",
        Restitution = 0.3f,
        StaticFriction = 0.1f,
        DynamicFriction = 0.03f,
        Density = 920f
    };

    public static PhysicsMaterial FromPreset(MaterialPreset preset) => preset switch
    {
        MaterialPreset.Rubber => Rubber,
        MaterialPreset.Wood => Wood,
        MaterialPreset.Steel => Steel,
        MaterialPreset.Ice => Ice,
        _ => new PhysicsMaterial()
    };
}

/// <summary>
/// Soft body material properties.
/// </summary>
public class SoftBodyMaterial
{
    public string Name { get; set; } = "Default";
    public float MassDensity { get; set; } = 1.0f;
    public float StructuralStiffness { get; set; } = 0.9f;
    public float ShearStiffness { get; set; } = 0.9f;
    public float BendingStiffness { get; set; } = 0.5f;
    public float Damping { get; set; } = 0.05f;
    public float Pressure { get; set; } = 0f; // For volumetric bodies
    public float VolumeConservation { get; set; } = 1.0f;
    public bool SelfCollision { get; set; } = false;
    public float CollisionMargin { get; set; } = 0.01f;
    public float Thickness { get; set; } = 0.01f;
    public int ConstraintIterations { get; set; } = 10;
    public float TearThreshold { get; set; } = 0f; // 0 = no tearing

    public static SoftBodyMaterial Cloth => new()
    {
        Name = "Cloth",
        MassDensity = 0.5f,
        StructuralStiffness = 0.9f,
        ShearStiffness = 0.8f,
        BendingStiffness = 0.2f,
        Damping = 0.05f,
        SelfCollision = true,
        ConstraintIterations = 10
    };

    public static SoftBodyMaterial Rope => new()
    {
        Name = "Rope",
        MassDensity = 0.3f,
        StructuralStiffness = 0.95f,
        ShearStiffness = 0f,
        BendingStiffness = 0.1f,
        Damping = 0.1f,
        SelfCollision = false,
        ConstraintIterations = 15
    };

    public static SoftBodyMaterial Jelly => new()
    {
        Name = "Jelly",
        MassDensity = 1.0f,
        StructuralStiffness = 0.5f,
        ShearStiffness = 0.4f,
        BendingStiffness = 0.3f,
        Damping = 0.1f,
        Pressure = 50f,
        VolumeConservation = 0.95f,
        SelfCollision = true,
        ConstraintIterations = 12
    };

    public static SoftBodyMaterial FromPreset(SoftBodyPreset preset) => preset switch
    {
        SoftBodyPreset.DrapedCloth or SoftBodyPreset.FlagOnPole or SoftBodyPreset.ClothStack => Cloth,
        SoftBodyPreset.RopePendulum => Rope,
        SoftBodyPreset.JellyCube => Jelly,
        _ => new SoftBodyMaterial()
    };
}
