namespace BlazorClient.Services.Factories;

/// <summary>
/// Interface for mesh creation strategy.
/// Follows Open/Closed Principle - new mesh types can be added without modifying existing code.
/// </summary>
public interface IMeshCreator
{
    /// <summary>
    /// The primitive type this creator handles.
    /// </summary>
    RigidPrimitiveType PrimitiveType { get; }

    /// <summary>
    /// Gets the JavaScript mesh creation options for this primitive type.
    /// </summary>
    object GetMeshOptions(RigidBody body);
}

/// <summary>
/// Sphere mesh creator.
/// </summary>
public class SphereMeshCreator : IMeshCreator
{
    public RigidPrimitiveType PrimitiveType => RigidPrimitiveType.Sphere;

    public object GetMeshOptions(RigidBody body) => new
    {
        diameter = 1,
        segments = 32
    };
}

/// <summary>
/// Box mesh creator.
/// </summary>
public class BoxMeshCreator : IMeshCreator
{
    public RigidPrimitiveType PrimitiveType => RigidPrimitiveType.Box;

    public object GetMeshOptions(RigidBody body) => new
    {
        size = 1
    };
}

/// <summary>
/// Capsule mesh creator.
/// </summary>
public class CapsuleMeshCreator : IMeshCreator
{
    public RigidPrimitiveType PrimitiveType => RigidPrimitiveType.Capsule;

    public object GetMeshOptions(RigidBody body) => new
    {
        radius = 0.5,
        height = 2,
        tessellation = 32
    };
}

/// <summary>
/// Cylinder mesh creator.
/// </summary>
public class CylinderMeshCreator : IMeshCreator
{
    public RigidPrimitiveType PrimitiveType => RigidPrimitiveType.Cylinder;

    public object GetMeshOptions(RigidBody body) => new
    {
        diameter = 1,
        height = 1,
        tessellation = 32
    };
}

/// <summary>
/// Cone mesh creator.
/// </summary>
public class ConeMeshCreator : IMeshCreator
{
    public RigidPrimitiveType PrimitiveType => RigidPrimitiveType.Cone;

    public object GetMeshOptions(RigidBody body) => new
    {
        diameterTop = 0,
        diameterBottom = 1,
        height = 1,
        tessellation = 32
    };
}

/// <summary>
/// Factory for creating mesh creators.
/// Follows Open/Closed Principle - register new creators without modifying the factory.
/// </summary>
public interface IMeshCreatorFactory
{
    /// <summary>
    /// Gets a mesh creator for the specified primitive type.
    /// </summary>
    IMeshCreator GetCreator(RigidPrimitiveType primitiveType);

    /// <summary>
    /// Registers a new mesh creator.
    /// </summary>
    void RegisterCreator(IMeshCreator creator);
}

/// <summary>
/// Implementation of mesh creator factory with registry pattern.
/// </summary>
public class MeshCreatorFactory : IMeshCreatorFactory
{
    private readonly Dictionary<RigidPrimitiveType, IMeshCreator> _creators = new();
    private readonly IMeshCreator _defaultCreator;

    public MeshCreatorFactory()
    {
        _defaultCreator = new SphereMeshCreator();
        
        // Register default creators
        RegisterCreator(new SphereMeshCreator());
        RegisterCreator(new BoxMeshCreator());
        RegisterCreator(new CapsuleMeshCreator());
        RegisterCreator(new CylinderMeshCreator());
        RegisterCreator(new ConeMeshCreator());
    }

    /// <inheritdoc />
    public IMeshCreator GetCreator(RigidPrimitiveType primitiveType)
    {
        return _creators.TryGetValue(primitiveType, out var creator) ? creator : _defaultCreator;
    }

    /// <inheritdoc />
    public void RegisterCreator(IMeshCreator creator)
    {
        _creators[creator.PrimitiveType] = creator;
    }
}
