using BlazorClient.Domain.Models;
using NUnit.Framework;

namespace BlazorClient.Domain.Tests;

/// <summary>
/// Unit tests for physics-related value types in the Domain layer.
/// </summary>
/// <remarks>
/// Tests cover Vector3, Quaternion, TransformData, PhysicsMaterial, and SoftBodyMaterial types.
/// </remarks>
[TestFixture]
public class PhysicsTypesTests
{
    #region Vector3 Tests

    /// <summary>
    /// Verifies that Vector3 constructor correctly initializes X, Y, and Z properties.
    /// </summary>
    [Test]
    public void Vector3_Constructor_ShouldSetProperties()
    {
        // Act
        var vector = new Vector3(1.5f, 2.5f, 3.5f);

        // Assert
        Assert.That(vector.X, Is.EqualTo(1.5f));
        Assert.That(vector.Y, Is.EqualTo(2.5f));
        Assert.That(vector.Z, Is.EqualTo(3.5f));
    }

    /// <summary>
    /// Verifies that Vector3.Zero returns a vector with all components set to zero.
    /// </summary>
    [Test]
    public void Vector3_Zero_ShouldReturnZeroVector()
    {
        // Act
        var zero = Vector3.Zero;

        // Assert
        Assert.That(zero.X, Is.EqualTo(0f));
        Assert.That(zero.Y, Is.EqualTo(0f));
        Assert.That(zero.Z, Is.EqualTo(0f));
    }

    /// <summary>
    /// Verifies that Vector3.One returns a vector with all components set to one.
    /// </summary>
    [Test]
    public void Vector3_One_ShouldReturnOneVector()
    {
        // Act
        var one = Vector3.One;

        // Assert
        Assert.That(one.X, Is.EqualTo(1f));
        Assert.That(one.Y, Is.EqualTo(1f));
        Assert.That(one.Z, Is.EqualTo(1f));
    }

    /// <summary>
    /// Verifies that Vector3.Up returns the unit vector pointing in the positive Y direction.
    /// </summary>
    [Test]
    public void Vector3_Up_ShouldReturnUpVector()
    {
        // Act
        var up = Vector3.Up;

        // Assert
        Assert.That(up.X, Is.EqualTo(0f));
        Assert.That(up.Y, Is.EqualTo(1f));
        Assert.That(up.Z, Is.EqualTo(0f));
    }

    /// <summary>
    /// Verifies that Vector3.Down returns the unit vector pointing in the negative Y direction.
    /// </summary>
    [Test]
    public void Vector3_Down_ShouldReturnDownVector()
    {
        // Act
        var down = Vector3.Down;

        // Assert
        Assert.That(down.X, Is.EqualTo(0f));
        Assert.That(down.Y, Is.EqualTo(-1f));
        Assert.That(down.Z, Is.EqualTo(0f));
    }

    /// <summary>
    /// Verifies that Vector3.Gravity returns Earth's standard gravity vector (9.81 m/s² downward).
    /// </summary>
    [Test]
    public void Vector3_Gravity_ShouldReturnGravityVector()
    {
        // Act
        var gravity = Vector3.Gravity;

        // Assert
        Assert.That(gravity.X, Is.EqualTo(0f));
        Assert.That(gravity.Y, Is.EqualTo(-9.81f));
        Assert.That(gravity.Z, Is.EqualTo(0f));
    }

    /// <summary>
    /// Verifies that Vector3.ToArray() returns components in [X, Y, Z] order.
    /// </summary>
    [Test]
    public void Vector3_ToArray_ShouldReturnCorrectArray()
    {
        // Arrange
        var vector = new Vector3(1f, 2f, 3f);

        // Act
        var array = vector.ToArray();

        // Assert
        Assert.That(array, Is.EqualTo(new[] { 1f, 2f, 3f }));
    }

    /// <summary>
    /// Verifies that Vector3.ToString() formats output with two decimal places.
    /// </summary>
    [Test]
    public void Vector3_ToString_ShouldFormatCorrectly()
    {
        // Arrange
        var vector = new Vector3(1.234f, 5.678f, 9.012f);

        // Act
        var result = vector.ToString();

        // Assert
        Assert.That(result, Is.EqualTo("(1.23, 5.68, 9.01)"));
    }

    #endregion

    #region Quaternion Tests

    /// <summary>
    /// Verifies that Quaternion constructor correctly initializes X, Y, Z, and W properties.
    /// </summary>
    [Test]
    public void Quaternion_Constructor_ShouldSetProperties()
    {
        // Act
        var quat = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f);

        // Assert
        Assert.That(quat.X, Is.EqualTo(0.1f));
        Assert.That(quat.Y, Is.EqualTo(0.2f));
        Assert.That(quat.Z, Is.EqualTo(0.3f));
        Assert.That(quat.W, Is.EqualTo(0.9f));
    }

    /// <summary>
    /// Verifies that Quaternion.Identity returns the identity quaternion (0, 0, 0, 1).
    /// </summary>
    [Test]
    public void Quaternion_Identity_ShouldReturnIdentityQuaternion()
    {
        // Act
        var identity = Quaternion.Identity;

        // Assert
        Assert.That(identity.X, Is.EqualTo(0f));
        Assert.That(identity.Y, Is.EqualTo(0f));
        Assert.That(identity.Z, Is.EqualTo(0f));
        Assert.That(identity.W, Is.EqualTo(1f));
    }

    /// <summary>
    /// Verifies that Quaternion.ToArray() returns components in [X, Y, Z, W] order.
    /// </summary>
    [Test]
    public void Quaternion_ToArray_ShouldReturnCorrectArray()
    {
        // Arrange
        var quat = new Quaternion(0.1f, 0.2f, 0.3f, 0.9f);

        // Act
        var array = quat.ToArray();

        // Assert
        Assert.That(array, Is.EqualTo(new[] { 0.1f, 0.2f, 0.3f, 0.9f }));
    }

    #endregion

    #region TransformData Tests

    /// <summary>
    /// Verifies that TransformData default constructor initializes with identity transform.
    /// </summary>
    [Test]
    public void TransformData_DefaultValues_ShouldBeCorrect()
    {
        // Act
        var transform = new TransformData();

        // Assert
        Assert.That(transform.Position.X, Is.EqualTo(0f));
        Assert.That(transform.Position.Y, Is.EqualTo(0f));
        Assert.That(transform.Position.Z, Is.EqualTo(0f));
        Assert.That(transform.Rotation.W, Is.EqualTo(1f));
        Assert.That(transform.Scale.X, Is.EqualTo(1f));
        Assert.That(transform.Scale.Y, Is.EqualTo(1f));
        Assert.That(transform.Scale.Z, Is.EqualTo(1f));
    }

    #endregion

    #region PhysicsMaterial Tests

    /// <summary>
    /// Verifies that PhysicsMaterial.Rubber preset has correct physical properties.
    /// </summary>
    [Test]
    public void PhysicsMaterial_Rubber_ShouldHaveCorrectValues()
    {
        // Act
        var rubber = PhysicsMaterial.Rubber;

        // Assert
        Assert.That(rubber.Name, Is.EqualTo("Rubber"));
        Assert.That(rubber.Restitution, Is.EqualTo(0.8f));
        Assert.That(rubber.StaticFriction, Is.EqualTo(0.9f));
        Assert.That(rubber.DynamicFriction, Is.EqualTo(0.8f));
        Assert.That(rubber.Density, Is.EqualTo(1100f));
    }

    /// <summary>
    /// Verifies that PhysicsMaterial.Wood preset has correct physical properties.
    /// </summary>
    [Test]
    public void PhysicsMaterial_Wood_ShouldHaveCorrectValues()
    {
        // Act
        var wood = PhysicsMaterial.Wood;

        // Assert
        Assert.That(wood.Name, Is.EqualTo("Wood"));
        Assert.That(wood.Restitution, Is.EqualTo(0.4f));
        Assert.That(wood.Density, Is.EqualTo(700f));
    }

    /// <summary>
    /// Verifies that PhysicsMaterial.Steel preset has correct physical properties.
    /// </summary>
    [Test]
    public void PhysicsMaterial_Steel_ShouldHaveCorrectValues()
    {
        // Act
        var steel = PhysicsMaterial.Steel;

        // Assert
        Assert.That(steel.Name, Is.EqualTo("Steel"));
        Assert.That(steel.Restitution, Is.EqualTo(0.6f));
        Assert.That(steel.Density, Is.EqualTo(7800f));
    }

    /// <summary>
    /// Verifies that PhysicsMaterial.Ice preset has correct low-friction properties.
    /// </summary>
    [Test]
    public void PhysicsMaterial_Ice_ShouldHaveCorrectValues()
    {
        // Act
        var ice = PhysicsMaterial.Ice;

        // Assert
        Assert.That(ice.Name, Is.EqualTo("Ice"));
        Assert.That(ice.StaticFriction, Is.EqualTo(0.1f));
        Assert.That(ice.DynamicFriction, Is.EqualTo(0.03f));
    }

    /// <summary>
    /// Verifies that PhysicsMaterial.FromPreset returns the correct material for each preset.
    /// </summary>
    /// <param name="preset">The material preset to test.</param>
    /// <param name="expectedName">The expected material name.</param>
    [Test]
    [TestCase(MaterialPreset.Rubber, "Rubber")]
    [TestCase(MaterialPreset.Wood, "Wood")]
    [TestCase(MaterialPreset.Steel, "Steel")]
    [TestCase(MaterialPreset.Ice, "Ice")]
    public void PhysicsMaterial_FromPreset_ShouldReturnCorrectMaterial(MaterialPreset preset, string expectedName)
    {
        // Act
        var material = PhysicsMaterial.FromPreset(preset);

        // Assert
        Assert.That(material.Name, Is.EqualTo(expectedName));
    }

    /// <summary>
    /// Verifies that PhysicsMaterial.FromPreset returns default material for Custom preset.
    /// </summary>
    [Test]
    public void PhysicsMaterial_FromPreset_Custom_ShouldReturnDefault()
    {
        // Act
        var material = PhysicsMaterial.FromPreset(MaterialPreset.Custom);

        // Assert
        Assert.That(material.Name, Is.EqualTo("Default"));
    }

    #endregion

    #region SoftBodyMaterial Tests

    /// <summary>
    /// Verifies that SoftBodyMaterial.Cloth preset has correct cloth simulation properties.
    /// </summary>
    [Test]
    public void SoftBodyMaterial_Cloth_ShouldHaveCorrectValues()
    {
        // Act
        var cloth = SoftBodyMaterial.Cloth;

        // Assert
        Assert.That(cloth.Name, Is.EqualTo("Cloth"));
        Assert.That(cloth.StructuralStiffness, Is.EqualTo(0.9f));
        Assert.That(cloth.SelfCollision, Is.True);
    }

    /// <summary>
    /// Verifies that SoftBodyMaterial.Rope preset has correct rope simulation properties.
    /// </summary>
    [Test]
    public void SoftBodyMaterial_Rope_ShouldHaveCorrectValues()
    {
        // Act
        var rope = SoftBodyMaterial.Rope;

        // Assert
        Assert.That(rope.Name, Is.EqualTo("Rope"));
        Assert.That(rope.ShearStiffness, Is.EqualTo(0f));
        Assert.That(rope.SelfCollision, Is.False);
    }

    /// <summary>
    /// Verifies that SoftBodyMaterial.Jelly preset has correct volumetric soft body properties.
    /// </summary>
    [Test]
    public void SoftBodyMaterial_Jelly_ShouldHaveCorrectValues()
    {
        // Act
        var jelly = SoftBodyMaterial.Jelly;

        // Assert
        Assert.That(jelly.Name, Is.EqualTo("Jelly"));
        Assert.That(jelly.Pressure, Is.EqualTo(50f));
        Assert.That(jelly.VolumeConservation, Is.EqualTo(0.95f));
    }

    /// <summary>
    /// Verifies that SoftBodyMaterial.FromPreset returns the correct material for each preset.
    /// </summary>
    /// <param name="preset">The soft body preset to test.</param>
    /// <param name="expectedName">The expected material name.</param>
    [Test]
    [TestCase(SoftBodyPreset.DrapedCloth, "Cloth")]
    [TestCase(SoftBodyPreset.FlagOnPole, "Cloth")]
    [TestCase(SoftBodyPreset.ClothStack, "Cloth")]
    [TestCase(SoftBodyPreset.RopePendulum, "Rope")]
    [TestCase(SoftBodyPreset.JellyCube, "Jelly")]
    public void SoftBodyMaterial_FromPreset_ShouldReturnCorrectMaterial(SoftBodyPreset preset, string expectedName)
    {
        // Act
        var material = SoftBodyMaterial.FromPreset(preset);

        // Assert
        Assert.That(material.Name, Is.EqualTo(expectedName));
    }

    #endregion
}
