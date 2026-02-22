using BlazorClient.Application.Validation;
using BlazorClient.Domain.Models;

namespace BlazorClient.Infrastructure.Validation;

/// <summary>
/// Implementation of physics parameter validation.
/// </summary>
public class PhysicsValidator : IPhysicsValidator
{
    // Physics stability thresholds (based on docs/physics.md)
    private const float MinMass = 0.01f;
    private const float MaxMass = 10000f;
    private const float MaxMassRatio = 1000f;
    private const float MinScale = 0.05f;
    private const float MaxScale = 10f;
    private const float MaxRestitution = 0.95f;
    private const float MinDamping = 0f;
    private const float MaxDamping = 1f;
    private const float MaxVelocity = 100f;

    // Soft body thresholds
    private const int MinResolution = 5;
    private const int MaxResolution = 50;
    private const float MinStiffness = 0.01f;
    private const float MaxStiffness = 0.99f;
    private const int MinIterations = 5;
    private const int MaxIterations = 30;

    /// <inheritdoc />
    public ValidationResult ValidateRigidBody(RigidBody body)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Mass validation
        if (body.Mass < MinMass)
        {
            warnings.Add($"Mass {body.Mass:F3} is very low (< {MinMass}), may cause instability");
        }
        if (body.Mass > MaxMass)
        {
            warnings.Add($"Mass {body.Mass:F0} is very high (> {MaxMass}), may cause large mass ratio issues");
        }

        // Scale validation
        var scale = body.Transform.Scale;
        if (scale.X < MinScale || scale.Y < MinScale || scale.Z < MinScale)
        {
            warnings.Add($"Scale is very small (< {MinScale}m), CCD recommended");
        }
        if (scale.X > MaxScale || scale.Y > MaxScale || scale.Z > MaxScale)
        {
            errors.Add($"Scale exceeds maximum ({MaxScale}m)");
        }

        // Restitution validation
        if (body.Material.Restitution > MaxRestitution)
        {
            warnings.Add($"Restitution {body.Material.Restitution:F2} is very high, may cause energy gain");
        }
        if (body.Material.Restitution < 0)
        {
            errors.Add("Restitution cannot be negative");
        }

        // Damping validation
        if (body.LinearDamping < MinDamping || body.LinearDamping > MaxDamping)
        {
            errors.Add($"Linear damping must be between {MinDamping} and {MaxDamping}");
        }
        if (body.AngularDamping < MinDamping || body.AngularDamping > MaxDamping)
        {
            errors.Add($"Angular damping must be between {MinDamping} and {MaxDamping}");
        }

        // Velocity validation
        var linearVelMagnitude = MathF.Sqrt(
            body.LinearVelocity.X * body.LinearVelocity.X +
            body.LinearVelocity.Y * body.LinearVelocity.Y +
            body.LinearVelocity.Z * body.LinearVelocity.Z);

        if (linearVelMagnitude > MaxVelocity)
        {
            warnings.Add($"Initial velocity {linearVelMagnitude:F1} m/s is very high, CCD recommended");
        }

        // CCD recommendation
        if ((linearVelMagnitude > 10f || scale.X < 0.1f) && !body.EnableCCD)
        {
            warnings.Add("Consider enabling CCD for fast/small objects to prevent tunneling");
        }

        return ValidationResult.Create(warnings, errors);
    }

    /// <inheritdoc />
    public ValidationResult ValidateSoftBody(SoftBody body)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Resolution validation
        if (body.ResolutionX < MinResolution || body.ResolutionY < MinResolution)
        {
            warnings.Add($"Resolution is low (< {MinResolution}), visual quality may be poor");
        }
        if (body.ResolutionX > MaxResolution || body.ResolutionY > MaxResolution)
        {
            warnings.Add($"Resolution is high (> {MaxResolution}), may impact performance");
        }

        // Stiffness validation
        var stiffness = body.Material.StructuralStiffness;
        if (stiffness < MinStiffness)
        {
            warnings.Add($"Structural stiffness {stiffness:F2} is very low, may cause excessive stretching");
        }
        if (stiffness > MaxStiffness)
        {
            warnings.Add($"Structural stiffness {stiffness:F2} is very high, increase iterations for stability");
        }

        // Iteration validation based on stiffness
        var recommendedIterations = stiffness switch
        {
            < 0.5f => 8,
            < 0.9f => 15,
            _ => 25
        };

        if (body.Material.ConstraintIterations < recommendedIterations && stiffness > 0.5f)
        {
            warnings.Add($"Consider increasing iterations to {recommendedIterations} for stiffness {stiffness:F2}");
        }

        if (body.Material.ConstraintIterations < MinIterations)
        {
            errors.Add($"Constraint iterations must be at least {MinIterations}");
        }
        if (body.Material.ConstraintIterations > MaxIterations)
        {
            warnings.Add($"Constraint iterations > {MaxIterations} has diminishing returns");
        }

        // Damping validation
        if (body.Material.Damping < 0 || body.Material.Damping > 1)
        {
            errors.Add("Damping must be between 0 and 1");
        }

        // Pressure validation (volumetric only)
        if (body.Type == SoftBodyType.Volumetric)
        {
            if (body.Material.Pressure <= 0)
            {
                warnings.Add("Volumetric body has no pressure, may collapse");
            }
            if (body.Material.VolumeConservation < 0.5f)
            {
                warnings.Add("Low volume conservation may cause body to deflate");
            }
        }

        // Self-collision warning
        if (body.Material.SelfCollision && body.ResolutionX > 30)
        {
            warnings.Add("Self-collision on high-resolution mesh may impact performance");
        }

        return ValidationResult.Create(warnings, errors);
    }

    /// <inheritdoc />
    public ValidationResult ValidateSimulationSettings(SimulationSettings settings)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        // Gravity validation
        if (MathF.Abs(settings.Gravity.Y) > 20f)
        {
            warnings.Add("Gravity magnitude is unusually high");
        }

        // Timestep validation
        if (settings.TimeStep > 1f / 60f)
        {
            warnings.Add("Large timestep may cause instability, consider reducing");
        }
        if (settings.TimeStep < 1f / 240f)
        {
            warnings.Add("Very small timestep may impact performance");
        }

        // Substeps validation
        if (settings.SubSteps < 1)
        {
            errors.Add("SubSteps must be at least 1");
        }
        if (settings.SubSteps > 8)
        {
            warnings.Add("High substep count may impact performance");
        }

        // Time scale validation
        if (settings.TimeScale < 0)
        {
            errors.Add("TimeScale cannot be negative");
        }
        if (settings.TimeScale > 2f)
        {
            warnings.Add("High time scale may cause instability");
        }

        // Sleep threshold validation
        if (settings.SleepThreshold < 0)
        {
            errors.Add("Sleep threshold cannot be negative");
        }

        return ValidationResult.Create(warnings, errors);
    }

    /// <inheritdoc />
    public ValidationResult ValidateMassRatio(float mass1, float mass2)
    {
        if (mass1 <= 0 || mass2 <= 0)
        {
            return ValidationResult.WithErrors("Masses must be positive");
        }

        var ratio = MathF.Max(mass1, mass2) / MathF.Min(mass1, mass2);

        if (ratio > MaxMassRatio)
        {
            return ValidationResult.WithWarnings(
                $"Mass ratio {ratio:F0}:1 exceeds recommended maximum {MaxMassRatio}:1, may cause instability");
        }

        return ValidationResult.Success();
    }
}
