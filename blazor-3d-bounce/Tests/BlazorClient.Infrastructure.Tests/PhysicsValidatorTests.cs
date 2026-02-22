using BlazorClient.Domain.Models;
using BlazorClient.Infrastructure.Validation;
using NUnit.Framework;

namespace BlazorClient.Infrastructure.Tests;

/// <summary>
/// Unit tests for the <see cref="PhysicsValidator"/> class.
/// </summary>
/// <remarks>
/// Tests cover validation of rigid bodies, soft bodies, simulation settings, and mass ratios.
/// Validates both error conditions and warning conditions for physics parameters.
/// </remarks>
[TestFixture]
public class PhysicsValidatorTests
{
    private PhysicsValidator _validator = null!;

    /// <summary>
    /// Initializes a fresh PhysicsValidator instance before each test.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        _validator = new PhysicsValidator();
    }

    #region ValidateRigidBody Tests

    /// <summary>
    /// Verifies that a valid rigid body configuration passes validation.
    /// </summary>
    [Test]
    public void ValidateRigidBody_ValidBody_ShouldReturnSuccess()
    {
        // Arrange
        var body = new RigidBody
        {
            Mass = 1.0f,
            Material = new PhysicsMaterial { Restitution = 0.5f },
            LinearDamping = 0.1f,
            AngularDamping = 0.1f
        };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    /// <summary>
    /// Verifies that very low mass generates a warning about potential instability.
    /// </summary>
    [Test]
    public void ValidateRigidBody_VeryLowMass_ShouldWarn()
    {
        // Arrange
        var body = new RigidBody { Mass = 0.001f };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Mass").And.Some.Contain("very low"));
    }

    /// <summary>
    /// Verifies that very high mass generates a warning about simulation stability.
    /// </summary>
    [Test]
    public void ValidateRigidBody_VeryHighMass_ShouldWarn()
    {
        // Arrange
        var body = new RigidBody { Mass = 50000f };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Mass").And.Some.Contain("very high"));
    }

    /// <summary>
    /// Verifies that very small scale generates a warning about collision detection.
    /// </summary>
    [Test]
    public void ValidateRigidBody_VerySmallScale_ShouldWarn()
    {
        // Arrange
        var body = new RigidBody
        {
            Transform = new TransformData
            {
                Scale = new Vector3(0.01f, 0.01f, 0.01f)
            }
        };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Scale").And.Some.Contain("very small"));
    }

    /// <summary>
    /// Verifies that scale exceeding the maximum returns a validation error.
    /// </summary>
    [Test]
    public void ValidateRigidBody_ScaleTooLarge_ShouldReturnError()
    {
        // Arrange
        var body = new RigidBody
        {
            Transform = new TransformData
            {
                Scale = new Vector3(15f, 15f, 15f)
            }
        };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Scale").And.Some.Contain("exceeds"));
    }

    /// <summary>
    /// Verifies that very high restitution generates a warning about energy gain.
    /// </summary>
    [Test]
    public void ValidateRigidBody_HighRestitution_ShouldWarn()
    {
        // Arrange
        var body = new RigidBody
        {
            Material = new PhysicsMaterial { Restitution = 0.99f }
        };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Restitution").And.Some.Contain("high"));
    }

    /// <summary>
    /// Verifies that negative restitution returns a validation error.
    /// </summary>
    [Test]
    public void ValidateRigidBody_NegativeRestitution_ShouldReturnError()
    {
        // Arrange
        var body = new RigidBody
        {
            Material = new PhysicsMaterial { Restitution = -0.5f }
        };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Restitution").And.Some.Contain("negative"));
    }

    /// <summary>
    /// Verifies that linear damping outside valid range returns a validation error.
    /// </summary>
    [Test]
    public void ValidateRigidBody_InvalidLinearDamping_ShouldReturnError()
    {
        // Arrange
        var body = new RigidBody { LinearDamping = 1.5f };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Linear damping"));
    }

    /// <summary>
    /// Verifies that negative angular damping returns a validation error.
    /// </summary>
    [Test]
    public void ValidateRigidBody_InvalidAngularDamping_ShouldReturnError()
    {
        // Arrange
        var body = new RigidBody { AngularDamping = -0.1f };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Angular damping"));
    }

    /// <summary>
    /// Verifies that very high velocity generates a warning about tunneling.
    /// </summary>
    [Test]
    public void ValidateRigidBody_HighVelocity_ShouldWarn()
    {
        // Arrange
        var body = new RigidBody
        {
            LinearVelocity = new Vector3(150f, 0f, 0f)
        };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("velocity").And.Some.Contain("high"));
    }

    /// <summary>
    /// Verifies that fast small objects without CCD generate a recommendation warning.
    /// </summary>
    [Test]
    public void ValidateRigidBody_FastSmallObjectWithoutCCD_ShouldRecommendCCD()
    {
        // Arrange
        var body = new RigidBody
        {
            LinearVelocity = new Vector3(15f, 0f, 0f),
            EnableCCD = false
        };

        // Act
        var result = _validator.ValidateRigidBody(body);

        // Assert
        Assert.That(result.Warnings, Has.Some.Contain("CCD"));
    }

    #endregion

    #region ValidateSoftBody Tests

    /// <summary>
    /// Verifies that a valid soft body configuration passes validation.
    /// </summary>
    [Test]
    public void ValidateSoftBody_ValidBody_ShouldReturnSuccess()
    {
        // Arrange
        var body = new SoftBody
        {
            ResolutionX = 20,
            ResolutionY = 20,
            Material = new SoftBodyMaterial
            {
                StructuralStiffness = 0.8f,
                ConstraintIterations = 15,
                Damping = 0.1f
            }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    /// <summary>
    /// Verifies that low resolution generates a warning about visual quality.
    /// </summary>
    [Test]
    public void ValidateSoftBody_LowResolution_ShouldWarn()
    {
        // Arrange
        var body = new SoftBody
        {
            ResolutionX = 3,
            ResolutionY = 3,
            Material = new SoftBodyMaterial { ConstraintIterations = 10 }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Resolution").And.Some.Contain("low"));
    }

    /// <summary>
    /// Verifies that high resolution generates a performance warning.
    /// </summary>
    [Test]
    public void ValidateSoftBody_HighResolution_ShouldWarn()
    {
        // Arrange
        var body = new SoftBody
        {
            ResolutionX = 100,
            ResolutionY = 100,
            Material = new SoftBodyMaterial { ConstraintIterations = 10 }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Resolution").And.Some.Contain("high"));
    }

    /// <summary>
    /// Verifies that very low stiffness generates a warning about stretching.
    /// </summary>
    [Test]
    public void ValidateSoftBody_LowStiffness_ShouldWarn()
    {
        // Arrange
        var body = new SoftBody
        {
            Material = new SoftBodyMaterial
            {
                StructuralStiffness = 0.005f,
                ConstraintIterations = 10
            }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("stiffness").And.Some.Contain("low"));
    }

    /// <summary>
    /// Verifies that high stiffness with low iterations recommends more iterations.
    /// </summary>
    [Test]
    public void ValidateSoftBody_HighStiffnessLowIterations_ShouldRecommendMoreIterations()
    {
        // Arrange
        var body = new SoftBody
        {
            Material = new SoftBodyMaterial
            {
                StructuralStiffness = 0.95f,
                ConstraintIterations = 8
            }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("iterations"));
    }

    /// <summary>
    /// Verifies that too few constraint iterations returns a validation error.
    /// </summary>
    [Test]
    public void ValidateSoftBody_TooFewIterations_ShouldReturnError()
    {
        // Arrange
        var body = new SoftBody
        {
            Material = new SoftBodyMaterial { ConstraintIterations = 2 }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("iterations"));
    }

    /// <summary>
    /// Verifies that damping outside valid range returns a validation error.
    /// </summary>
    [Test]
    public void ValidateSoftBody_InvalidDamping_ShouldReturnError()
    {
        // Arrange
        var body = new SoftBody
        {
            Material = new SoftBodyMaterial
            {
                Damping = 1.5f,
                ConstraintIterations = 10
            }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Damping"));
    }

    /// <summary>
    /// Verifies that volumetric soft body without pressure generates a collapse warning.
    /// </summary>
    [Test]
    public void ValidateSoftBody_VolumetricNoPressure_ShouldWarn()
    {
        // Arrange
        var body = new SoftBody
        {
            Type = SoftBodyType.Volumetric,
            Material = new SoftBodyMaterial
            {
                Pressure = 0f,
                ConstraintIterations = 10
            }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("pressure").And.Some.Contain("collapse"));
    }

    /// <summary>
    /// Verifies that self-collision with high resolution generates a performance warning.
    /// </summary>
    [Test]
    public void ValidateSoftBody_SelfCollisionHighRes_ShouldWarn()
    {
        // Arrange
        var body = new SoftBody
        {
            ResolutionX = 40,
            ResolutionY = 20,
            Material = new SoftBodyMaterial
            {
                SelfCollision = true,
                ConstraintIterations = 10
            }
        };

        // Act
        var result = _validator.ValidateSoftBody(body);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Self-collision").And.Some.Contain("performance"));
    }

    #endregion

    #region ValidateSimulationSettings Tests

    /// <summary>
    /// Verifies that valid simulation settings pass validation.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_ValidSettings_ShouldReturnSuccess()
    {
        // Arrange
        var settings = new SimulationSettings();

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.True);
    }

    /// <summary>
    /// Verifies that extreme gravity generates a warning.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_ExtremeGravity_ShouldWarn()
    {
        // Arrange
        var settings = new SimulationSettings
        {
            Gravity = new Vector3(0f, -50f, 0f)
        };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Gravity").And.Some.Contain("high"));
    }

    /// <summary>
    /// Verifies that large timestep generates a stability warning.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_LargeTimestep_ShouldWarn()
    {
        // Arrange
        var settings = new SimulationSettings
        {
            TimeStep = 1f / 30f // 30 FPS timestep
        };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("timestep"));
    }

    /// <summary>
    /// Verifies that very small timestep generates a performance warning.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_VerySmallTimestep_ShouldWarn()
    {
        // Arrange
        var settings = new SimulationSettings
        {
            TimeStep = 1f / 500f
        };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("timestep").And.Some.Contain("performance"));
    }

    /// <summary>
    /// Verifies that zero substeps returns a validation error.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_ZeroSubSteps_ShouldReturnError()
    {
        // Arrange
        var settings = new SimulationSettings { SubSteps = 0 };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("SubSteps"));
    }

    /// <summary>
    /// Verifies that high substep count generates a performance warning.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_HighSubSteps_ShouldWarn()
    {
        // Arrange
        var settings = new SimulationSettings { SubSteps = 10 };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("substep"));
    }

    /// <summary>
    /// Verifies that negative time scale returns a validation error.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_NegativeTimeScale_ShouldReturnError()
    {
        // Arrange
        var settings = new SimulationSettings { TimeScale = -1f };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("TimeScale"));
    }

    /// <summary>
    /// Verifies that high time scale generates a stability warning.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_HighTimeScale_ShouldWarn()
    {
        // Arrange
        var settings = new SimulationSettings { TimeScale = 3f };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("time scale"));
    }

    /// <summary>
    /// Verifies that negative sleep threshold returns a validation error.
    /// </summary>
    [Test]
    public void ValidateSimulationSettings_NegativeSleepThreshold_ShouldReturnError()
    {
        // Arrange
        var settings = new SimulationSettings { SleepThreshold = -0.1f };

        // Act
        var result = _validator.ValidateSimulationSettings(settings);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Sleep threshold"));
    }

    #endregion

    #region ValidateMassRatio Tests

    /// <summary>
    /// Verifies that a reasonable mass ratio passes validation without warnings.
    /// </summary>
    [Test]
    public void ValidateMassRatio_ValidRatio_ShouldReturnSuccess()
    {
        // Act
        var result = _validator.ValidateMassRatio(10f, 100f);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Is.Empty);
    }

    /// <summary>
    /// Verifies that extreme mass ratio generates a stability warning.
    /// </summary>
    [Test]
    public void ValidateMassRatio_ExtremeRatio_ShouldWarn()
    {
        // Act
        var result = _validator.ValidateMassRatio(0.01f, 100f);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Has.Some.Contain("Mass ratio").And.Some.Contain("exceeds"));
    }

    /// <summary>
    /// Verifies that zero mass returns a validation error.
    /// </summary>
    [Test]
    public void ValidateMassRatio_ZeroMass_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateMassRatio(0f, 10f);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Masses must be positive"));
    }

    /// <summary>
    /// Verifies that negative mass returns a validation error.
    /// </summary>
    [Test]
    public void ValidateMassRatio_NegativeMass_ShouldReturnError()
    {
        // Act
        var result = _validator.ValidateMassRatio(-5f, 10f);

        // Assert
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contain("Masses must be positive"));
    }

    /// <summary>
    /// Verifies that equal masses pass validation without warnings.
    /// </summary>
    [Test]
    public void ValidateMassRatio_EqualMasses_ShouldReturnSuccess()
    {
        // Act
        var result = _validator.ValidateMassRatio(50f, 50f);

        // Assert
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Warnings, Is.Empty);
    }

    #endregion
}
