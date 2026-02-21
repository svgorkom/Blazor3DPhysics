using BlazorClient.Models;

namespace BlazorClient.Services;

/// <summary>
/// Interface for managing scene state and objects.
/// </summary>
public interface ISceneStateService
{
    /// <summary>
    /// Current simulation settings.
    /// </summary>
    SimulationSettings Settings { get; }

    /// <summary>
    /// Current render settings.
    /// </summary>
    RenderSettings RenderSettings { get; }

    /// <summary>
    /// All rigid bodies in the scene.
    /// </summary>
    IReadOnlyList<RigidBody> RigidBodies { get; }

    /// <summary>
    /// All soft bodies in the scene.
    /// </summary>
    IReadOnlyList<SoftBody> SoftBodies { get; }

    /// <summary>
    /// Currently selected object ID.
    /// </summary>
    string? SelectedObjectId { get; }

    /// <summary>
    /// Gets the selected rigid body if one is selected.
    /// </summary>
    RigidBody? SelectedRigidBody { get; }

    /// <summary>
    /// Gets the selected soft body if one is selected.
    /// </summary>
    SoftBody? SelectedSoftBody { get; }

    /// <summary>
    /// Current performance statistics.
    /// </summary>
    PerformanceStats Stats { get; }

    /// <summary>
    /// Event raised when state changes.
    /// </summary>
    event Action? OnStateChanged;

    /// <summary>
    /// Adds a rigid body to the scene.
    /// </summary>
    void AddRigidBody(RigidBody body);

    /// <summary>
    /// Adds a soft body to the scene.
    /// </summary>
    void AddSoftBody(SoftBody body);

    /// <summary>
    /// Removes an object from the scene.
    /// </summary>
    void RemoveObject(string id);

    /// <summary>
    /// Clears all objects from the scene.
    /// </summary>
    void ClearScene();

    /// <summary>
    /// Selects an object by ID.
    /// </summary>
    void SelectObject(string? id);

    /// <summary>
    /// Updates simulation settings.
    /// </summary>
    void UpdateSettings(SimulationSettings settings);

    /// <summary>
    /// Updates render settings.
    /// </summary>
    void UpdateRenderSettings(RenderSettings settings);

    /// <summary>
    /// Updates performance statistics.
    /// </summary>
    void UpdateStats(PerformanceStats stats);

    /// <summary>
    /// Loads a scene preset.
    /// </summary>
    void LoadPreset(ScenePreset preset);

    /// <summary>
    /// Exports the current scene as a preset.
    /// </summary>
    ScenePreset ExportPreset(string name);

    /// <summary>
    /// Notifies that state has changed.
    /// </summary>
    void NotifyStateChanged();
}

/// <summary>
/// Implementation of scene state management.
/// </summary>
public class SceneStateService : ISceneStateService
{
    private readonly List<RigidBody> _rigidBodies = new();
    private readonly List<SoftBody> _softBodies = new();
    private SimulationSettings _settings = new();
    private RenderSettings _renderSettings = new();
    private PerformanceStats _stats = new();
    private string? _selectedObjectId;

    public SimulationSettings Settings => _settings;
    public RenderSettings RenderSettings => _renderSettings;
    public IReadOnlyList<RigidBody> RigidBodies => _rigidBodies;
    public IReadOnlyList<SoftBody> SoftBodies => _softBodies;
    public string? SelectedObjectId => _selectedObjectId;
    public PerformanceStats Stats => _stats;

    public RigidBody? SelectedRigidBody => 
        _selectedObjectId != null ? _rigidBodies.FirstOrDefault(b => b.Id == _selectedObjectId) : null;

    public SoftBody? SelectedSoftBody => 
        _selectedObjectId != null ? _softBodies.FirstOrDefault(b => b.Id == _selectedObjectId) : null;

    public event Action? OnStateChanged;

    public void AddRigidBody(RigidBody body)
    {
        _rigidBodies.Add(body);
        _stats.RigidBodyCount = _rigidBodies.Count;
        NotifyStateChanged();
    }

    public void AddSoftBody(SoftBody body)
    {
        _softBodies.Add(body);
        _stats.SoftBodyCount = _softBodies.Count;
        NotifyStateChanged();
    }

    public void RemoveObject(string id)
    {
        var rigidBody = _rigidBodies.FirstOrDefault(b => b.Id == id);
        if (rigidBody != null)
        {
            _rigidBodies.Remove(rigidBody);
            _stats.RigidBodyCount = _rigidBodies.Count;
        }

        var softBody = _softBodies.FirstOrDefault(b => b.Id == id);
        if (softBody != null)
        {
            _softBodies.Remove(softBody);
            _stats.SoftBodyCount = _softBodies.Count;
        }

        if (_selectedObjectId == id)
        {
            _selectedObjectId = null;
        }

        NotifyStateChanged();
    }

    public void ClearScene()
    {
        _rigidBodies.Clear();
        _softBodies.Clear();
        _selectedObjectId = null;
        _stats.RigidBodyCount = 0;
        _stats.SoftBodyCount = 0;
        NotifyStateChanged();
    }

    public void SelectObject(string? id)
    {
        // Deselect previous
        foreach (var body in _rigidBodies.Where(b => b.IsSelected))
        {
            body.IsSelected = false;
        }
        foreach (var body in _softBodies.Where(b => b.IsSelected))
        {
            body.IsSelected = false;
        }

        _selectedObjectId = id;

        if (id != null)
        {
            var rigidBody = _rigidBodies.FirstOrDefault(b => b.Id == id);
            if (rigidBody != null) rigidBody.IsSelected = true;

            var softBody = _softBodies.FirstOrDefault(b => b.Id == id);
            if (softBody != null) softBody.IsSelected = true;
        }

        NotifyStateChanged();
    }

    public void UpdateSettings(SimulationSettings settings)
    {
        _settings = settings;
        NotifyStateChanged();
    }

    public void UpdateRenderSettings(RenderSettings settings)
    {
        _renderSettings = settings;
        NotifyStateChanged();
    }

    public void UpdateStats(PerformanceStats stats)
    {
        _stats = stats;
        _stats.RigidBodyCount = _rigidBodies.Count;
        _stats.SoftBodyCount = _softBodies.Count;
        // Don't notify for stats to avoid unnecessary rerenders
    }

    public void LoadPreset(ScenePreset preset)
    {
        ClearScene();
        
        _settings = preset.Settings;
        _renderSettings = preset.RenderSettings;

        foreach (var body in preset.RigidBodies)
        {
            _rigidBodies.Add(body);
        }

        foreach (var body in preset.SoftBodies)
        {
            _softBodies.Add(body);
        }

        _stats.RigidBodyCount = _rigidBodies.Count;
        _stats.SoftBodyCount = _softBodies.Count;

        NotifyStateChanged();
    }

    public ScenePreset ExportPreset(string name)
    {
        return new ScenePreset
        {
            Name = name,
            Settings = _settings,
            RenderSettings = _renderSettings,
            RigidBodies = _rigidBodies.ToList(),
            SoftBodies = _softBodies.ToList(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
