using BlazorClient.Domain.Models;

namespace BlazorClient.Application.Validation;

/// <summary>
/// Result of a validation operation.
/// </summary>
public record ValidationResult
{
    /// <summary>
    /// Whether the validation passed (no errors).
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Warning messages (validation passed but with concerns).
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Error messages (validation failed).
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a validation result with warnings.
    /// </summary>
    public static ValidationResult WithWarnings(params string[] warnings) => new()
    {
        IsValid = true,
        Warnings = warnings
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult WithErrors(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };

    /// <summary>
    /// Creates a validation result with both warnings and errors.
    /// </summary>
    public static ValidationResult Create(IEnumerable<string> warnings, IEnumerable<string> errors)
    {
        var errorList = errors.ToList();
        return new ValidationResult
        {
            IsValid = errorList.Count == 0,
            Warnings = warnings.ToList(),
            Errors = errorList
        };
    }
}

/// <summary>
/// Validates physics parameters to prevent simulation instability.
/// </summary>
public interface IPhysicsValidator
{
    /// <summary>
    /// Validates a rigid body configuration.
    /// </summary>
    ValidationResult ValidateRigidBody(RigidBody body);

    /// <summary>
    /// Validates a soft body configuration.
    /// </summary>
    ValidationResult ValidateSoftBody(SoftBody body);

    /// <summary>
    /// Validates simulation settings.
    /// </summary>
    ValidationResult ValidateSimulationSettings(SimulationSettings settings);

    /// <summary>
    /// Validates a mass ratio between two bodies.
    /// </summary>
    ValidationResult ValidateMassRatio(float mass1, float mass2);
}
