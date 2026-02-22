namespace BlazorClient.Services.Factories;

/// <summary>
/// Interface for material creation strategy.
/// Follows Open/Closed Principle - new materials can be added without modifying existing code.
/// </summary>
public interface IMaterialCreator
{
    /// <summary>
    /// The material preset this creator handles.
    /// </summary>
    MaterialPreset Preset { get; }

    /// <summary>
    /// Gets the JavaScript material options for this preset.
    /// </summary>
    object GetMaterialOptions();
}

/// <summary>
/// Rubber material creator.
/// </summary>
public class RubberMaterialCreator : IMaterialCreator
{
    public MaterialPreset Preset => MaterialPreset.Rubber;

    public object GetMaterialOptions() => new
    {
        albedoColor = new[] { 0.8f, 0.2f, 0.2f },
        metallic = 0.0f,
        roughness = 0.7f
    };
}

/// <summary>
/// Wood material creator.
/// </summary>
public class WoodMaterialCreator : IMaterialCreator
{
    public MaterialPreset Preset => MaterialPreset.Wood;

    public object GetMaterialOptions() => new
    {
        albedoColor = new[] { 0.6f, 0.4f, 0.2f },
        metallic = 0.0f,
        roughness = 0.8f
    };
}

/// <summary>
/// Steel material creator.
/// </summary>
public class SteelMaterialCreator : IMaterialCreator
{
    public MaterialPreset Preset => MaterialPreset.Steel;

    public object GetMaterialOptions() => new
    {
        albedoColor = new[] { 0.7f, 0.7f, 0.75f },
        metallic = 0.9f,
        roughness = 0.3f
    };
}

/// <summary>
/// Ice material creator.
/// </summary>
public class IceMaterialCreator : IMaterialCreator
{
    public MaterialPreset Preset => MaterialPreset.Ice;

    public object GetMaterialOptions() => new
    {
        albedoColor = new[] { 0.7f, 0.9f, 1.0f },
        metallic = 0.0f,
        roughness = 0.1f,
        alpha = 0.8f
    };
}

/// <summary>
/// Default/custom material creator.
/// </summary>
public class DefaultMaterialCreator : IMaterialCreator
{
    public MaterialPreset Preset => MaterialPreset.Custom;

    public object GetMaterialOptions() => new
    {
        albedoColor = new[] { 0.5f, 0.5f, 0.6f },
        metallic = 0.2f,
        roughness = 0.5f
    };
}

/// <summary>
/// Factory for creating material creators.
/// Follows Open/Closed Principle - register new creators without modifying the factory.
/// </summary>
public interface IMaterialCreatorFactory
{
    /// <summary>
    /// Gets a material creator for the specified preset.
    /// </summary>
    IMaterialCreator GetCreator(MaterialPreset preset);

    /// <summary>
    /// Registers a new material creator.
    /// </summary>
    void RegisterCreator(IMaterialCreator creator);
}

/// <summary>
/// Implementation of material creator factory with registry pattern.
/// </summary>
public class MaterialCreatorFactory : IMaterialCreatorFactory
{
    private readonly Dictionary<MaterialPreset, IMaterialCreator> _creators = new();
    private readonly IMaterialCreator _defaultCreator;

    public MaterialCreatorFactory()
    {
        _defaultCreator = new DefaultMaterialCreator();
        
        // Register default creators
        RegisterCreator(new RubberMaterialCreator());
        RegisterCreator(new WoodMaterialCreator());
        RegisterCreator(new SteelMaterialCreator());
        RegisterCreator(new IceMaterialCreator());
        RegisterCreator(new DefaultMaterialCreator());
    }

    /// <inheritdoc />
    public IMaterialCreator GetCreator(MaterialPreset preset)
    {
        return _creators.TryGetValue(preset, out var creator) ? creator : _defaultCreator;
    }

    /// <inheritdoc />
    public void RegisterCreator(IMaterialCreator creator)
    {
        _creators[creator.Preset] = creator;
    }
}
