using BlazorClient.Models;
using Microsoft.JSInterop;
using System.Text.Json;

namespace BlazorClient.Services;

/// <summary>
/// Service for scene import/export operations.
/// </summary>
public interface ISceneSerializationService
{
    /// <summary>
    /// Serializes the current scene to JSON.
    /// </summary>
    Task<string> ExportToJsonAsync();

    /// <summary>
    /// Deserializes a scene from JSON.
    /// </summary>
    Task<Result<ScenePreset>> ImportFromJsonAsync(string json);

    /// <summary>
    /// Saves the current scene to browser local storage.
    /// </summary>
    Task<Result> SaveToLocalStorageAsync(string name);

    /// <summary>
    /// Loads a scene from browser local storage.
    /// </summary>
    Task<Result<ScenePreset>> LoadFromLocalStorageAsync(string name);

    /// <summary>
    /// Lists all saved scenes in local storage.
    /// </summary>
    Task<IReadOnlyList<SavedSceneInfo>> GetSavedScenesAsync();

    /// <summary>
    /// Deletes a saved scene from local storage.
    /// </summary>
    Task<Result> DeleteSavedSceneAsync(string name);

    /// <summary>
    /// Exports scene to a downloadable file.
    /// </summary>
    Task DownloadSceneAsync(string filename);
}

/// <summary>
/// Information about a saved scene.
/// </summary>
public record SavedSceneInfo(
    string Name,
    DateTime SavedAt,
    int RigidBodyCount,
    int SoftBodyCount);

/// <summary>
/// Implementation of scene serialization service.
/// </summary>
public class SceneSerializationService : ISceneSerializationService
{
    private const string StorageKeyPrefix = "scene_";
    private const string SceneListKey = "saved_scenes";

    private readonly ISceneStateService _sceneState;
    private readonly IJSRuntime _jsRuntime;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SceneSerializationService(ISceneStateService sceneState, IJSRuntime jsRuntime)
    {
        _sceneState = sceneState;
        _jsRuntime = jsRuntime;
    }

    /// <inheritdoc />
    public Task<string> ExportToJsonAsync()
    {
        var preset = _sceneState.ExportPreset("Exported Scene");
        var json = JsonSerializer.Serialize(preset, JsonOptions);
        return Task.FromResult(json);
    }

    /// <inheritdoc />
    public Task<Result<ScenePreset>> ImportFromJsonAsync(string json)
    {
        try
        {
            var preset = JsonSerializer.Deserialize<ScenePreset>(json, JsonOptions);
            
            if (preset == null)
            {
                return Task.FromResult(Result<ScenePreset>.Failure("Failed to parse scene JSON"));
            }

            // Validate version compatibility
            if (!IsVersionCompatible(preset.Version))
            {
                return Task.FromResult(Result<ScenePreset>.Failure(
                    $"Incompatible scene version: {preset.Version}"));
            }

            return Task.FromResult(Result<ScenePreset>.Success(preset));
        }
        catch (JsonException ex)
        {
            return Task.FromResult(Result<ScenePreset>.Failure($"Invalid JSON: {ex.Message}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<ScenePreset>.Failure($"Import failed: {ex.Message}"));
        }
    }

    /// <inheritdoc />
    public async Task<Result> SaveToLocalStorageAsync(string name)
    {
        try
        {
            var json = await ExportToJsonAsync();
            var storageKey = StorageKeyPrefix + SanitizeKey(name);

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", storageKey, json);

            // Update scene list
            var scenes = await GetSavedScenesListAsync();
            var info = new SavedSceneInfo(
                name,
                DateTime.UtcNow,
                _sceneState.RigidBodies.Count,
                _sceneState.SoftBodies.Count);

            // Remove existing entry with same name
            scenes.RemoveAll(s => s.Name == name);
            scenes.Add(info);

            await SaveSceneListAsync(scenes);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to save scene: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<ScenePreset>> LoadFromLocalStorageAsync(string name)
    {
        try
        {
            var storageKey = StorageKeyPrefix + SanitizeKey(name);
            var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", storageKey);

            if (string.IsNullOrEmpty(json))
            {
                return Result<ScenePreset>.Failure($"Scene '{name}' not found");
            }

            return await ImportFromJsonAsync(json);
        }
        catch (Exception ex)
        {
            return Result<ScenePreset>.Failure($"Failed to load scene: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SavedSceneInfo>> GetSavedScenesAsync()
    {
        try
        {
            return await GetSavedScenesListAsync();
        }
        catch
        {
            return Array.Empty<SavedSceneInfo>();
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteSavedSceneAsync(string name)
    {
        try
        {
            var storageKey = StorageKeyPrefix + SanitizeKey(name);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", storageKey);

            // Update scene list
            var scenes = await GetSavedScenesListAsync();
            scenes.RemoveAll(s => s.Name == name);
            await SaveSceneListAsync(scenes);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure($"Failed to delete scene: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task DownloadSceneAsync(string filename)
    {
        var json = await ExportToJsonAsync();

        // Ensure .json extension
        if (!filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            filename += ".json";
        }

        // Use JS to trigger download
        await _jsRuntime.InvokeVoidAsync("downloadFile", filename, json, "application/json");
    }

    private async Task<List<SavedSceneInfo>> GetSavedScenesListAsync()
    {
        var json = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", SceneListKey);

        if (string.IsNullOrEmpty(json))
        {
            return new List<SavedSceneInfo>();
        }

        return JsonSerializer.Deserialize<List<SavedSceneInfo>>(json, JsonOptions) 
               ?? new List<SavedSceneInfo>();
    }

    private async Task SaveSceneListAsync(List<SavedSceneInfo> scenes)
    {
        var json = JsonSerializer.Serialize(scenes, JsonOptions);
        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", SceneListKey, json);
    }

    private static string SanitizeKey(string name)
    {
        // Remove characters that might cause issues in storage keys
        return string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
    }

    private static bool IsVersionCompatible(string version)
    {
        // Simple version check - can be made more sophisticated
        if (string.IsNullOrEmpty(version)) return true;

        var parts = version.Split('.');
        if (parts.Length >= 1 && int.TryParse(parts[0], out var major))
        {
            return major <= 1; // Compatible with version 1.x
        }

        return true;
    }
}
