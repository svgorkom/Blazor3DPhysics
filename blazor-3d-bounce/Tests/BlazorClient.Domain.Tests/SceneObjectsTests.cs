using BlazorClient.Domain.Models;
using NUnit.Framework;

namespace BlazorClient.Domain.Tests;

/// <summary>
/// Unit tests for scene object types including RigidBody, SoftBody, and related settings classes.
/// </summary>
/// <remarks>
/// Tests cover default values, constructors, presets, and property initialization.
/// </remarks>
[TestFixture]
public class SceneObjectsTests
{
    #region SceneObject Tests (via RigidBody)

    /// <summary>
    /// Verifies that SceneObject base class provides correct default values.
    /// </summary>
    [Test]
    public void SceneObject_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var body = new RigidBody();

        // Assert
        Assert.That(body.Id, Is.Not.Null.And.Not.Empty);
        Assert.That(body.Name, Is.EqualTo("RigidBody"));
        Assert.That(body.Transform, Is.Not.Null);
        Assert.That(body.IsSelected, Is.False);
        Assert.That(body.IsVisible, Is.True);
        Assert.That(body.CreatedAt, Is.LessThanOrEqualTo(DateTime.UtcNow));
    }

    /// <summary>
    /// Verifies that each SceneObject receives a unique GUID identifier.
    /// </summary>
    [Test]
    public void SceneObject_Id_ShouldBeUniqueGuid()
    {
        // Act
        var body1 = new RigidBody();
        var body2 = new RigidBody();

        // Assert
        Assert.That(body1.Id, Is.Not.EqualTo(body2.Id));
        Assert.That(Guid.TryParse(body1.Id, out _), Is.True);
    }

    #endregion

    #region RigidBody Tests

    /// <summary>
    /// Verifies that RigidBody default constructor sets appropriate defaults.
    /// </summary>
    [Test]
    public void RigidBody_DefaultConstructor_ShouldSetDefaults()
    {
        // Act
        var body = new RigidBody();

        // Assert
        Assert.That(body.PrimitiveType, Is.EqualTo(RigidPrimitiveType.Sphere));
        Assert.That(body.Material, Is.Not.Null);
        Assert.That(body.Mass, Is.EqualTo(1.0f));
        Assert.That(body.IsStatic, Is.False);
        Assert.That(body.IsSleeping, Is.False);
        Assert.That(body.EnableCCD, Is.False);
        Assert.That(body.LinearDamping, Is.EqualTo(0.01f));
        Assert.That(body.AngularDamping, Is.EqualTo(0.01f));
    }

    /// <summary>
    /// Verifies that RigidBody parameterized constructor sets type and material correctly.
    /// </summary>
    [Test]
    public void RigidBody_ParameterizedConstructor_ShouldSetTypeAndMaterial()
    {
        // Act
        var body = new RigidBody(RigidPrimitiveType.Box, MaterialPreset.Steel);

        // Assert
        Assert.That(body.PrimitiveType, Is.EqualTo(RigidPrimitiveType.Box));
        Assert.That(body.Material.Name, Is.EqualTo("Steel"));
        Assert.That(body.Name, Does.StartWith("Box_"));
    }

    /// <summary>
    /// Verifies that RigidBody constructor works correctly for various type/material combinations.
    /// </summary>
    /// <param name="type">The rigid body primitive type.</param>
    /// <param name="preset">The material preset.</param>
    [Test]
    [TestCase(RigidPrimitiveType.Sphere, MaterialPreset.Rubber)]
    [TestCase(RigidPrimitiveType.Box, MaterialPreset.Wood)]
    [TestCase(RigidPrimitiveType.Capsule, MaterialPreset.Steel)]
    [TestCase(RigidPrimitiveType.Cylinder, MaterialPreset.Ice)]
    [TestCase(RigidPrimitiveType.Cone, MaterialPreset.Rubber)]
    public void RigidBody_ParameterizedConstructor_ShouldSetCorrectValues(RigidPrimitiveType type, MaterialPreset preset)
    {
        // Act
        var body = new RigidBody(type, preset);

        // Assert
        Assert.That(body.PrimitiveType, Is.EqualTo(type));
        Assert.That(body.Material.Name, Is.EqualTo(PhysicsMaterial.FromPreset(preset).Name));
        Assert.That(body.Name, Does.Contain(type.ToString()));
    }

    /// <summary>
    /// Verifies that RigidBody linear velocity defaults to zero.
    /// </summary>
    [Test]
    public void RigidBody_LinearVelocity_DefaultShouldBeZero()
    {
        // Act
        var body = new RigidBody();

        // Assert
        Assert.That(body.LinearVelocity.X, Is.EqualTo(0f));
        Assert.That(body.LinearVelocity.Y, Is.EqualTo(0f));
        Assert.That(body.LinearVelocity.Z, Is.EqualTo(0f));
    }

    /// <summary>
    /// Verifies that RigidBody angular velocity defaults to zero.
    /// </summary>
    [Test]
    public void RigidBody_AngularVelocity_DefaultShouldBeZero()
    {
        // Act
        var body = new RigidBody();

        // Assert
        Assert.That(body.AngularVelocity.X, Is.EqualTo(0f));
        Assert.That(body.AngularVelocity.Y, Is.EqualTo(0f));
        Assert.That(body.AngularVelocity.Z, Is.EqualTo(0f));
    }

    #endregion

    #region SoftBody Tests

    /// <summary>
    /// Verifies that SoftBody default constructor sets appropriate defaults.
    /// </summary>
    [Test]
    public void SoftBody_DefaultConstructor_ShouldSetDefaults()
    {
        // Act
        var body = new SoftBody();

        // Assert
        Assert.That(body.Type, Is.EqualTo(SoftBodyType.Cloth));
        Assert.That(body.Material, Is.Not.Null);
        Assert.That(body.ResolutionX, Is.EqualTo(20));
        Assert.That(body.ResolutionY, Is.EqualTo(20));
        Assert.That(body.Segments, Is.EqualTo(20));
        Assert.That(body.Width, Is.EqualTo(2.0f));
        Assert.That(body.Height, Is.EqualTo(2.0f));
        Assert.That(body.Name, Is.EqualTo("SoftBody"));
    }

    /// <summary>
    /// Verifies that SoftBody parameterized constructor sets type and material.
    /// </summary>
    [Test]
    public void SoftBody_ParameterizedConstructor_ShouldSetTypeAndMaterial()
    {
        // Act
        var body = new SoftBody(SoftBodyType.Cloth, SoftBodyPreset.DrapedCloth);

        // Assert
        Assert.That(body.Type, Is.EqualTo(SoftBodyType.Cloth));
        Assert.That(body.Material.Name, Is.EqualTo("Cloth"));
        Assert.That(body.Name, Does.StartWith("Cloth_"));
    }

    /// <summary>
    /// Verifies that FlagOnPole preset pins the left edge vertices.
    /// </summary>
    [Test]
    public void SoftBody_FlagOnPole_ShouldPinLeftEdge()
    {
        // Act
        var body = new SoftBody(SoftBodyType.Cloth, SoftBodyPreset.FlagOnPole);

        // Assert
        Assert.That(body.PinnedVertices, Is.Not.Empty);
        Assert.That(body.PinnedVertices.Count, Is.EqualTo(body.ResolutionY + 1));
    }

    /// <summary>
    /// Verifies that RopePendulum preset pins the top vertex.
    /// </summary>
    [Test]
    public void SoftBody_Rope_ShouldPinTopVertex()
    {
        // Act
        var body = new SoftBody(SoftBodyType.Rope, SoftBodyPreset.RopePendulum);

        // Assert
        Assert.That(body.PinnedVertices, Contains.Item(0));
    }

    /// <summary>
    /// Verifies that SoftBody pinned vertices collection is empty by default.
    /// </summary>
    [Test]
    public void SoftBody_PinnedVertices_DefaultShouldBeEmpty()
    {
        // Act
        var body = new SoftBody();

        // Assert
        Assert.That(body.PinnedVertices, Is.Not.Null);
        Assert.That(body.PinnedVertices, Is.Empty);
    }

    /// <summary>
    /// Verifies that SoftBody anchor positions collection is empty by default.
    /// </summary>
    [Test]
    public void SoftBody_AnchorPositions_DefaultShouldBeEmpty()
    {
        // Act
        var body = new SoftBody();

        // Assert
        Assert.That(body.AnchorPositions, Is.Not.Null);
        Assert.That(body.AnchorPositions, Is.Empty);
    }

    #endregion

    #region SimulationSettings Tests

    /// <summary>
    /// Verifies that SimulationSettings has correct default values.
    /// </summary>
    [Test]
    public void SimulationSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var settings = new SimulationSettings();

        // Assert
        Assert.That(settings.Gravity.Y, Is.EqualTo(-9.81f).Within(0.01f));
        Assert.That(settings.TimeStep, Is.EqualTo(1f / 120f));
        Assert.That(settings.SubSteps, Is.EqualTo(3));
        Assert.That(settings.IsPaused, Is.False);
        Assert.That(settings.TimeScale, Is.EqualTo(1.0f));
        Assert.That(settings.EnableSleeping, Is.True);
        Assert.That(settings.SleepThreshold, Is.EqualTo(0.01f));
    }

    #endregion

    #region RenderSettings Tests

    /// <summary>
    /// Verifies that RenderSettings has correct default values.
    /// </summary>
    [Test]
    public void RenderSettings_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var settings = new RenderSettings();

        // Assert
        Assert.That(settings.EnableShadows, Is.True);
        Assert.That(settings.ShadowMapSize, Is.EqualTo(2048));
        Assert.That(settings.EnableSSAO, Is.False);
        Assert.That(settings.EnableFXAA, Is.True);
        Assert.That(settings.ShowGrid, Is.True);
        Assert.That(settings.ShowAxes, Is.True);
        Assert.That(settings.ShowWireframe, Is.False);
        Assert.That(settings.ShowBoundingBoxes, Is.False);
        Assert.That(settings.ShowDebugOverlay, Is.False);
    }

    #endregion

    #region PerformanceStats Tests

    /// <summary>
    /// Verifies that PerformanceStats has zero default values.
    /// </summary>
    [Test]
    public void PerformanceStats_DefaultValues_ShouldBeZero()
    {
        // Act
        var stats = new PerformanceStats();

        // Assert
        Assert.That(stats.Fps, Is.EqualTo(0f));
        Assert.That(stats.FrameTimeMs, Is.EqualTo(0f));
        Assert.That(stats.PhysicsTimeMs, Is.EqualTo(0f));
        Assert.That(stats.RenderTimeMs, Is.EqualTo(0f));
        Assert.That(stats.RigidBodyCount, Is.EqualTo(0));
        Assert.That(stats.SoftBodyCount, Is.EqualTo(0));
        Assert.That(stats.TotalVertices, Is.EqualTo(0));
        Assert.That(stats.TotalTriangles, Is.EqualTo(0));
        Assert.That(stats.UsedMemoryMB, Is.EqualTo(0));
    }

    #endregion

    #region ScenePreset Tests

    /// <summary>
    /// Verifies that ScenePreset has correct default values.
    /// </summary>
    [Test]
    public void ScenePreset_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var preset = new ScenePreset();

        // Assert
        Assert.That(preset.Name, Is.EqualTo("Untitled"));
        Assert.That(preset.Description, Is.EqualTo(""));
        Assert.That(preset.Version, Is.EqualTo("1.0"));
        Assert.That(preset.Settings, Is.Not.Null);
        Assert.That(preset.RenderSettings, Is.Not.Null);
        Assert.That(preset.RigidBodies, Is.Not.Null);
        Assert.That(preset.SoftBodies, Is.Not.Null);
    }

    #endregion
}
