using BlazorClient.Domain.Models;

namespace BlazorClient.Services;

/// <summary>
/// Implementation of scene state management.
/// </summary>
/// <remarks>
/// <para>
/// Provides centralized state management for all scene objects, settings,
/// and selection state. Acts as a single source of truth for the UI.
/// </para>
/// <para>
/// <strong>Architecture Layer:</strong> Presentation/Services Layer.
/// Implements the extended <see cref="ISceneStateService"/> interface
/// which inherits from <see cref="BlazorClient.Application.Services.ISceneStateService"/>.
/// </para>
/// <para>
/// <strong>Thread Safety:</strong> Not thread-safe. Designed for Blazor
/// WebAssembly's single-threaded environment.
/// </para>
/// </remarks>
public class SceneStateService : ISceneStateService
{
    private readonly List<RigidBody> _rigidBodies = new();
    private readonly List<SoftBody> _softBodies = new();
    private SimulationSettings _settings = new();
    private RenderSettings _renderSettings = new();
    private PerformanceStats _stats = new();
    private string? _selectedObjectId;

    /// <inheritdoc />
    public SimulationSettings Settings => _settings;

    /// <inheritdoc />
    public RenderSettings RenderSettings => _renderSettings;

    /// <inheritdoc />
    public IReadOnlyList<RigidBody> RigidBodies => _rigidBodies;

    /// <inheritdoc />
    public IReadOnlyList<SoftBody> SoftBodies => _softBodies;

    /// <inheritdoc />
    public string? SelectedObjectId => _selectedObjectId;

    /// <inheritdoc />
    public PerformanceStats Stats => _stats;

    /// <inheritdoc />
    public RigidBody? SelectedRigidBody => 
        _selectedObjectId != null ? _rigidBodies.FirstOrDefault(b => b.Id == _selectedObjectId) : null;

    /// <inheritdoc />
    public SoftBody? SelectedSoftBody => 
        _selectedObjectId != null ? _softBodies.FirstOrDefault(b => b.Id == _selectedObjectId) : null;

    /// <inheritdoc />
    public event Action? OnStateChanged;

    /// <inheritdoc />
    public void AddRigidBody(RigidBody body)
    {
        _rigidBodies.Add(body);
        _stats.RigidBodyCount = _rigidBodies.Count;
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void AddSoftBody(SoftBody body)
    {
        _softBodies.Add(body);
        _stats.SoftBodyCount = _softBodies.Count;
        NotifyStateChanged();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void ClearScene()
    {
        _rigidBodies.Clear();
        _softBodies.Clear();
        _selectedObjectId = null;
        _stats.RigidBodyCount = 0;
        _stats.SoftBodyCount = 0;
        NotifyStateChanged();
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public SceneObject? GetObject(string id)
    {
        var rigidBody = _rigidBodies.FirstOrDefault(b => b.Id == id);
        if (rigidBody != null) return rigidBody;

        var softBody = _softBodies.FirstOrDefault(b => b.Id == id);
        return softBody;
    }

    /// <inheritdoc />
    public void UpdateSettings(SimulationSettings settings)
    {
        _settings = settings;
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void UpdateRenderSettings(RenderSettings settings)
    {
        _renderSettings = settings;
        NotifyStateChanged();
    }

    /// <inheritdoc />
    public void UpdateStats(PerformanceStats stats)
    {
        _stats = stats;
        _stats.RigidBodyCount = _rigidBodies.Count;
        _stats.SoftBodyCount = _softBodies.Count;
        // Don't notify for stats to avoid unnecessary rerenders
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public ScenePreset ExportToPreset()
    {
        return ExportPreset("Untitled");
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
    public void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
