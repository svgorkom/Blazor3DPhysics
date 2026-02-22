using BlazorClient.Application.Commands;
using BlazorClient.Domain.Models;
using NUnit.Framework;

namespace BlazorClient.Application.Tests;

/// <summary>
/// Unit tests for command record types in the Application layer.
/// </summary>
/// <remarks>
/// Tests verify that command records correctly store and expose their properties,
/// and that they implement the appropriate command interfaces.
/// </remarks>
[TestFixture]
public class CommandTests
{
    #region SpawnRigidBodyCommand Tests

    /// <summary>
    /// Verifies that SpawnRigidBodyCommand stores all provided properties correctly.
    /// </summary>
    [Test]
    public void SpawnRigidBodyCommand_ShouldStoreAllProperties()
    {
        // Arrange
        var position = new Vector3(1f, 2f, 3f);
        var scale = new Vector3(0.5f, 0.5f, 0.5f);

        // Act
        var command = new SpawnRigidBodyCommand(
            RigidPrimitiveType.Box,
            MaterialPreset.Steel,
            position,
            scale
        );

        // Assert
        Assert.That(command.Type, Is.EqualTo(RigidPrimitiveType.Box));
        Assert.That(command.Material, Is.EqualTo(MaterialPreset.Steel));
        Assert.That(command.Position.X, Is.EqualTo(1f));
        Assert.That(command.Position.Y, Is.EqualTo(2f));
        Assert.That(command.Position.Z, Is.EqualTo(3f));
        Assert.That(command.Scale, Is.Not.Null);
        Assert.That(command.Scale!.Value.X, Is.EqualTo(0.5f));
    }

    /// <summary>
    /// Verifies that SpawnRigidBodyCommand Scale property is optional (nullable).
    /// </summary>
    [Test]
    public void SpawnRigidBodyCommand_Scale_ShouldBeOptional()
    {
        // Act
        var command = new SpawnRigidBodyCommand(
            RigidPrimitiveType.Sphere,
            MaterialPreset.Rubber,
            Vector3.Zero
        );

        // Assert
        Assert.That(command.Scale, Is.Null);
    }

    #endregion

    #region SpawnSoftBodyCommand Tests

    /// <summary>
    /// Verifies that SpawnSoftBodyCommand stores all provided properties correctly.
    /// </summary>
    [Test]
    public void SpawnSoftBodyCommand_ShouldStoreAllProperties()
    {
        // Arrange
        var position = new Vector3(0f, 5f, 0f);

        // Act
        var command = new SpawnSoftBodyCommand(
            SoftBodyType.Cloth,
            SoftBodyPreset.FlagOnPole,
            position
        );

        // Assert
        Assert.That(command.Type, Is.EqualTo(SoftBodyType.Cloth));
        Assert.That(command.Preset, Is.EqualTo(SoftBodyPreset.FlagOnPole));
        Assert.That(command.Position.Y, Is.EqualTo(5f));
    }

    #endregion

    #region DeleteObjectCommand Tests

    /// <summary>
    /// Verifies that DeleteObjectCommand correctly stores the object identifier.
    /// </summary>
    [Test]
    public void DeleteObjectCommand_ShouldStoreId()
    {
        // Act
        var command = new DeleteObjectCommand("test-id-123");

        // Assert
        Assert.That(command.Id, Is.EqualTo("test-id-123"));
    }

    #endregion

    #region ApplyImpulseCommand Tests

    /// <summary>
    /// Verifies that ApplyImpulseCommand correctly stores object ID and impulse vector.
    /// </summary>
    [Test]
    public void ApplyImpulseCommand_ShouldStoreIdAndImpulse()
    {
        // Arrange
        var impulse = new Vector3(10f, 0f, 0f);

        // Act
        var command = new ApplyImpulseCommand("body-1", impulse);

        // Assert
        Assert.That(command.Id, Is.EqualTo("body-1"));
        Assert.That(command.Impulse.X, Is.EqualTo(10f));
    }

    #endregion

    #region ApplyForceCommand Tests

    /// <summary>
    /// Verifies that ApplyForceCommand correctly stores object ID and force vector.
    /// </summary>
    [Test]
    public void ApplyForceCommand_ShouldStoreIdAndForce()
    {
        // Arrange
        var force = new Vector3(0f, 100f, 0f);

        // Act
        var command = new ApplyForceCommand("body-2", force);

        // Assert
        Assert.That(command.Id, Is.EqualTo("body-2"));
        Assert.That(command.Force.Y, Is.EqualTo(100f));
    }

    #endregion

    #region ResetSceneCommand Tests

    /// <summary>
    /// Verifies that ResetSceneCommand can be instantiated and implements ICommand.
    /// </summary>
    [Test]
    public void ResetSceneCommand_ShouldBeCreatable()
    {
        // Act
        var command = new ResetSceneCommand();

        // Assert
        Assert.That(command, Is.Not.Null);
        Assert.That(command, Is.InstanceOf<ICommand>());
    }

    #endregion

    #region UpdateSimulationSettingsCommand Tests

    /// <summary>
    /// Verifies that UpdateSimulationSettingsCommand correctly stores simulation settings.
    /// </summary>
    [Test]
    public void UpdateSimulationSettingsCommand_ShouldStoreSettings()
    {
        // Arrange
        var settings = new SimulationSettings
        {
            TimeScale = 2.0f,
            IsPaused = true
        };

        // Act
        var command = new UpdateSimulationSettingsCommand(settings);

        // Assert
        Assert.That(command.Settings, Is.SameAs(settings));
        Assert.That(command.Settings.TimeScale, Is.EqualTo(2.0f));
        Assert.That(command.Settings.IsPaused, Is.True);
    }

    #endregion

    #region UpdateRenderSettingsCommand Tests

    /// <summary>
    /// Verifies that UpdateRenderSettingsCommand correctly stores render settings.
    /// </summary>
    [Test]
    public void UpdateRenderSettingsCommand_ShouldStoreSettings()
    {
        // Arrange
        var settings = new RenderSettings
        {
            EnableShadows = false,
            ShowWireframe = true
        };

        // Act
        var command = new UpdateRenderSettingsCommand(settings);

        // Assert
        Assert.That(command.Settings, Is.SameAs(settings));
        Assert.That(command.Settings.EnableShadows, Is.False);
        Assert.That(command.Settings.ShowWireframe, Is.True);
    }

    #endregion

    #region SelectObjectCommand Tests

    /// <summary>
    /// Verifies that SelectObjectCommand correctly stores the selected object ID.
    /// </summary>
    [Test]
    public void SelectObjectCommand_ShouldStoreObjectId()
    {
        // Act
        var command = new SelectObjectCommand("selected-object");

        // Assert
        Assert.That(command.Id, Is.EqualTo("selected-object"));
    }

    /// <summary>
    /// Verifies that SelectObjectCommand allows null ID for deselection.
    /// </summary>
    [Test]
    public void SelectObjectCommand_ShouldAllowNullForDeselection()
    {
        // Act
        var command = new SelectObjectCommand(null);

        // Assert
        Assert.That(command.Id, Is.Null);
    }

    #endregion

    #region PinVertexCommand Tests

    /// <summary>
    /// Verifies that PinVertexCommand correctly stores body ID, vertex index, and world position.
    /// </summary>
    [Test]
    public void PinVertexCommand_ShouldStoreAllProperties()
    {
        // Arrange
        var position = new Vector3(1f, 2f, 3f);

        // Act
        var command = new PinVertexCommand("soft-body-1", 42, position);

        // Assert
        Assert.That(command.BodyId, Is.EqualTo("soft-body-1"));
        Assert.That(command.VertexIndex, Is.EqualTo(42));
        Assert.That(command.WorldPosition.X, Is.EqualTo(1f));
        Assert.That(command.WorldPosition.Y, Is.EqualTo(2f));
        Assert.That(command.WorldPosition.Z, Is.EqualTo(3f));
    }

    #endregion

    #region UnpinVertexCommand Tests

    /// <summary>
    /// Verifies that UnpinVertexCommand correctly stores body ID and vertex index.
    /// </summary>
    [Test]
    public void UnpinVertexCommand_ShouldStoreBodyIdAndVertexIndex()
    {
        // Act
        var command = new UnpinVertexCommand("soft-body-2", 10);

        // Assert
        Assert.That(command.BodyId, Is.EqualTo("soft-body-2"));
        Assert.That(command.VertexIndex, Is.EqualTo(10));
    }

    #endregion

    #region ICommand Interface Tests

    /// <summary>
    /// Verifies that all void-returning commands implement the ICommand interface.
    /// </summary>
    [Test]
    public void AllVoidCommands_ShouldImplementICommand()
    {
        // Assert
        Assert.That(new ResetSceneCommand(), Is.InstanceOf<ICommand>());
        Assert.That(new DeleteObjectCommand("x"), Is.InstanceOf<ICommand>());
        Assert.That(new ApplyImpulseCommand("x", Vector3.Zero), Is.InstanceOf<ICommand>());
        Assert.That(new ApplyForceCommand("x", Vector3.Zero), Is.InstanceOf<ICommand>());
        Assert.That(new UpdateSimulationSettingsCommand(new SimulationSettings()), Is.InstanceOf<ICommand>());
        Assert.That(new UpdateRenderSettingsCommand(new RenderSettings()), Is.InstanceOf<ICommand>());
        Assert.That(new SelectObjectCommand("x"), Is.InstanceOf<ICommand>());
        Assert.That(new PinVertexCommand("x", 0, Vector3.Zero), Is.InstanceOf<ICommand>());
        Assert.That(new UnpinVertexCommand("x", 0), Is.InstanceOf<ICommand>());
    }

    /// <summary>
    /// Verifies that commands returning a result implement ICommand&lt;TResult&gt;.
    /// </summary>
    [Test]
    public void CommandsWithResult_ShouldImplementICommandOfResult()
    {
        // Assert
        Assert.That(new SpawnRigidBodyCommand(RigidPrimitiveType.Sphere, MaterialPreset.Rubber, Vector3.Zero),
            Is.InstanceOf<ICommand<string>>());
        Assert.That(new SpawnSoftBodyCommand(SoftBodyType.Cloth, SoftBodyPreset.DrapedCloth, Vector3.Zero),
            Is.InstanceOf<ICommand<string>>());
    }

    #endregion
}
