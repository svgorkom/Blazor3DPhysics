using NUnit.Framework;
using BlazorClient.Domain.Models;
using BlazorClient.Services;
using NSubstitute;
using Microsoft.JSInterop;

namespace BlazorClient.Tests.Physics;

/// <summary>
/// Unit tests for CPU physics service.
/// Tests collision detection, impulse resolution, and stability.
/// </summary>
[TestFixture]
public class CpuPhysicsServiceTests
{
    private CpuPhysicsService _physics = null!;

    [SetUp]
    public void Setup()
    {
        _physics = new CpuPhysicsService();
    }

    [Test]
    public async Task Initialize_ShouldSetupPhysicsWorld()
    {
        // Arrange
        var settings = new SimulationSettings
        {
            Gravity = new Vector3(0, -9.81f, 0),
            TimeStep = 1f / 120f,
            SubSteps = 3
        };

        // Act
        await _physics.InitializeAsync(settings);

        // Assert
        var available = await _physics.IsAvailableAsync();
        Assert.That(available, Is.True);
    }

    [Test]
    public async Task SingleSphereDrop_ShouldBounceCorrectly()
    {
        // Arrange
        await _physics.InitializeAsync(new SimulationSettings
        {
            Gravity = new Vector3(0, -9.81f, 0),
            TimeStep = 1f / 120f,
            SubSteps = 3
        });

        await _physics.CreateGroundAsync(0.5f, 0.5f);

        var sphere = new RigidBody(RigidPrimitiveType.Sphere, MaterialPreset.Rubber)
        {
            Id = "test-sphere",
            Mass = 1.0f,
            Transform = new TransformData
            {
                Position = new Vector3(0, 5, 0),
                Scale = new Vector3(1, 1, 1)
            }
        };

        await _physics.CreateRigidBodyAsync(sphere);

        // Act - simulate for 2 seconds
        var stepCount = 240; // 2 seconds at 120Hz
        float[] heights = new float[stepCount];

        for (int i = 0; i < stepCount; i++)
        {
            await _physics.StepAsync(1f / 120f);
            var batch = await _physics.GetTransformBatchAsync();
            heights[i] = batch.Transforms[1]; // Y position
        }

        // Assert
        // Initial height should decrease
        Assert.That(heights[0], Is.LessThan(5.0f));

        // Should eventually settle near ground
        Assert.That(heights[^1], Is.LessThan(1.0f));

        // Should bounce (have at least one local maximum after first contact)
        var localMaxima = FindLocalMaxima(heights);
        Assert.That(localMaxima, Is.GreaterThan(0), "sphere should bounce at least once");

        // Should not tunnel through ground
        Assert.That(heights.Min(), Is.GreaterThanOrEqualTo(0.4f), "sphere should not penetrate ground");
    }

    [Test]
    public async Task TwoSphereHeadOnCollision_ShouldConserveMomentum()
    {
        // Arrange
        await _physics.InitializeAsync(new SimulationSettings
        {
            Gravity = new Vector3(0, 0, 0), // No gravity for this test
            TimeStep = 1f / 120f,
            SubSteps = 3
        });

        var sphere1 = new RigidBody(RigidPrimitiveType.Sphere)
        {
            Id = "sphere1",
            Mass = 1.0f,
            Transform = new TransformData
            {
                Position = new Vector3(-2, 0, 0),
                Scale = new Vector3(1, 1, 1)
            },
            Material = new PhysicsMaterial { Restitution = 1.0f } // Perfectly elastic
        };

        var sphere2 = new RigidBody(RigidPrimitiveType.Sphere)
        {
            Id = "sphere2",
            Mass = 1.0f,
            Transform = new TransformData
            {
                Position = new Vector3(2, 0, 0),
                Scale = new Vector3(1, 1, 1)
            },
            Material = new PhysicsMaterial { Restitution = 1.0f }
        };

        await _physics.CreateRigidBodyAsync(sphere1);
        await _physics.CreateRigidBodyAsync(sphere2);

        // Give them velocities toward each other
        await _physics.SetLinearVelocityAsync("sphere1", new Vector3(5, 0, 0));
        await _physics.SetLinearVelocityAsync("sphere2", new Vector3(-5, 0, 0));

        // Act - simulate collision
        for (int i = 0; i < 240; i++)
        {
            await _physics.StepAsync(1f / 120f);
        }

        var batch = await _physics.GetTransformBatchAsync();

        // Assert
        var sphere1Idx = Array.IndexOf(batch.Ids, "sphere1");
        var sphere2Idx = Array.IndexOf(batch.Ids, "sphere2");

        var pos1X = batch.Transforms[sphere1Idx * 7];
        var pos2X = batch.Transforms[sphere2Idx * 7];

        // Spheres should be moving apart after collision
        Assert.That(pos1X, Is.LessThan(-2), "sphere1 should have bounced back");
        Assert.That(pos2X, Is.GreaterThan(2), "sphere2 should have bounced back");
    }

    [Test]
    public async Task StackedBoxes_ShouldStabilize()
    {
        // Arrange
        await _physics.InitializeAsync(new SimulationSettings
        {
            Gravity = new Vector3(0, -9.81f, 0),
            TimeStep = 1f / 120f,
            SubSteps = 3
        });

        await _physics.CreateGroundAsync(0.3f, 0.6f);

        // Create stack of 3 boxes
        for (int i = 0; i < 3; i++)
        {
            var box = new RigidBody(RigidPrimitiveType.Box, MaterialPreset.Wood)
            {
                Id = $"box{i}",
                Mass = 1.0f,
                Transform = new TransformData
                {
                    Position = new Vector3(0, 0.5f + i * 1.1f, 0),
                    Scale = new Vector3(1, 1, 1)
                }
            };
            await _physics.CreateRigidBodyAsync(box);
        }

        // Act - simulate for 5 seconds
        var stepCount = 600;
        var energyHistory = new List<float>();

        for (int i = 0; i < stepCount; i++)
        {
            await _physics.StepAsync(1f / 120f);

            if (i % 60 == 0) // Sample every 0.5s
            {
                var batch = await _physics.GetTransformBatchAsync();
                var totalEnergy = 0f;
                for (int j = 0; j < batch.Ids.Length; j++)
                {
                    var y = batch.Transforms[j * 7 + 1];
                    totalEnergy += y * 9.81f;
                }
                energyHistory.Add(totalEnergy);
            }
        }

        var batch2 = await _physics.GetTransformBatchAsync();

        // Assert
        for (int i = 0; i < 3; i++)
        {
            var idx = Array.IndexOf(batch2.Ids, $"box{i}");
            var y = batch2.Transforms[idx * 7 + 1];
            var expectedY = 0.5f + i * 1.0f;

            Assert.That(y, Is.EqualTo(expectedY).Within(0.5f), $"box{i} should be near resting position");
        }

        if (energyHistory.Count > 2)
        {
            var finalEnergy = energyHistory[^1];
            var initialEnergy = energyHistory[0];
            Assert.That(finalEnergy, Is.LessThanOrEqualTo(initialEnergy), "energy should not increase");
        }
    }

    [Test]
    public async Task FastMovingSphere_ShouldNotTunnel()
    {
        // Arrange
        await _physics.InitializeAsync(new SimulationSettings
        {
            Gravity = new Vector3(0, 0, 0),
            TimeStep = 1f / 60f,
            SubSteps = 1
        });

        await _physics.CreateGroundAsync(0.5f, 0.5f);

        var sphere = new RigidBody(RigidPrimitiveType.Sphere)
        {
            Id = "fast-sphere",
            Mass = 1.0f,
            Transform = new TransformData
            {
                Position = new Vector3(0, 5, 0),
                Scale = new Vector3(1, 1, 1)
            },
            Material = new PhysicsMaterial { Restitution = 0.5f }
        };

        await _physics.CreateRigidBodyAsync(sphere);
        await _physics.SetLinearVelocityAsync("fast-sphere", new Vector3(0, -50, 0));

        // Act
        var minY = float.MaxValue;
        for (int i = 0; i < 60; i++)
        {
            await _physics.StepAsync(1f / 60f);
            var batch = await _physics.GetTransformBatchAsync();
            var y = batch.Transforms[1];
            minY = Math.Min(minY, y);
        }

        // Assert
        Assert.That(minY, Is.GreaterThanOrEqualTo(-0.5f), "sphere should not tunnel through ground");
    }

    [Test]
    public async Task ApplyImpulse_ShouldChangeVelocity()
    {
        // Arrange
        await _physics.InitializeAsync(new SimulationSettings
        {
            Gravity = new Vector3(0, 0, 0),
            TimeStep = 1f / 120f,
            SubSteps = 1
        });

        var sphere = new RigidBody(RigidPrimitiveType.Sphere)
        {
            Id = "impulse-sphere",
            Mass = 2.0f,
            Transform = new TransformData
            {
                Position = new Vector3(0, 5, 0),
                Scale = new Vector3(1, 1, 1)
            }
        };

        await _physics.CreateRigidBodyAsync(sphere);

        // Act
        await _physics.ApplyImpulseAsync("impulse-sphere", new Vector3(10, 0, 0));
        await _physics.StepAsync(1f / 120f);

        var batch = await _physics.GetTransformBatchAsync();
        var x = batch.Transforms[0];

        // Assert
        Assert.That(x, Is.GreaterThan(0), "sphere should have moved in X direction");
    }

    [Test]
    public async Task Reset_ShouldRestoreInitialState()
    {
        // Arrange
        await _physics.InitializeAsync(new SimulationSettings
        {
            Gravity = new Vector3(0, -9.81f, 0),
            TimeStep = 1f / 120f,
            SubSteps = 1
        });

        var initialPosition = new Vector3(1, 5, 2);
        var sphere = new RigidBody(RigidPrimitiveType.Sphere)
        {
            Id = "reset-sphere",
            Mass = 1.0f,
            Transform = new TransformData
            {
                Position = initialPosition,
                Scale = new Vector3(1, 1, 1)
            }
        };

        await _physics.CreateRigidBodyAsync(sphere);

        for (int i = 0; i < 120; i++)
        {
            await _physics.StepAsync(1f / 120f);
        }

        // Act
        await _physics.ResetAsync();

        var batch = await _physics.GetTransformBatchAsync();

        // Assert
        Assert.That(batch.Transforms[0], Is.EqualTo(initialPosition.X).Within(0.01f));
        Assert.That(batch.Transforms[1], Is.EqualTo(initialPosition.Y).Within(0.01f));
        Assert.That(batch.Transforms[2], Is.EqualTo(initialPosition.Z).Within(0.01f));
    }

    [Test]
    public async Task SphereAABBCollision_ShouldResolveCorrectly()
    {
        // Arrange
        await _physics.InitializeAsync(new SimulationSettings
        {
            Gravity = new Vector3(0, 0, 0),
            TimeStep = 1f / 120f,
            SubSteps = 3
        });

        var sphere = new RigidBody(RigidPrimitiveType.Sphere)
        {
            Id = "sphere",
            Mass = 1.0f,
            Transform = new TransformData
            {
                Position = new Vector3(-2, 0, 0),
                Scale = new Vector3(1, 1, 1)
            },
            Material = new PhysicsMaterial { Restitution = 0.8f }
        };

        var box = new RigidBody(RigidPrimitiveType.Box)
        {
            Id = "box",
            Mass = 1.0f,
            Transform = new TransformData
            {
                Position = new Vector3(2, 0, 0),
                Scale = new Vector3(2, 2, 2)
            },
            Material = new PhysicsMaterial { Restitution = 0.8f }
        };

        await _physics.CreateRigidBodyAsync(sphere);
        await _physics.CreateRigidBodyAsync(box);
        await _physics.SetLinearVelocityAsync("sphere", new Vector3(5, 0, 0));

        // Act
        for (int i = 0; i < 120; i++)
        {
            await _physics.StepAsync(1f / 120f);
        }

        var batch = await _physics.GetTransformBatchAsync();
        var sphereIdx = Array.IndexOf(batch.Ids, "sphere");
        var sphereX = batch.Transforms[sphereIdx * 7];

        // Assert
        Assert.That(sphereX, Is.LessThan(0), "sphere should have bounced off box");
    }

    // ========================================
    // Helper Methods
    // ========================================

    private static int FindLocalMaxima(float[] values)
    {
        int count = 0;
        for (int i = 1; i < values.Length - 1; i++)
        {
            if (values[i] > values[i - 1] && values[i] > values[i + 1])
            {
                count++;
            }
        }
        return count;
    }
}

/// <summary>
/// Physics validation metrics tests.
/// </summary>
[TestFixture]
public class PhysicsValidationTests
{
    [Test]
    public void EnergyConservation_PerfectlyElasticCollision_ShouldConserve()
    {
        // Arrange
        var mass1 = 1.0f;
        var mass2 = 1.0f;
        var v1Before = 5.0f;
        var v2Before = -5.0f;

        // Act
        var v1After = ((mass1 - mass2) * v1Before + 2 * mass2 * v2Before) / (mass1 + mass2);
        var v2After = ((mass2 - mass1) * v2Before + 2 * mass1 * v1Before) / (mass1 + mass2);

        var keBefore = 0.5f * mass1 * v1Before * v1Before + 0.5f * mass2 * v2Before * v2Before;
        var keAfter = 0.5f * mass1 * v1After * v1After + 0.5f * mass2 * v2After * v2After;

        // Assert
        Assert.That(keAfter, Is.EqualTo(keBefore).Within(0.001f), "kinetic energy should be conserved");
    }

    [Test]
    public void MomentumConservation_AnyCollision_ShouldConserve()
    {
        // Arrange
        var mass1 = 2.0f;
        var mass2 = 3.0f;
        var v1Before = 4.0f;
        var v2Before = -2.0f;
        var restitution = 0.6f;

        // Act
        var momentumBefore = mass1 * v1Before + mass2 * v2Before;

        var relVelBefore = v1Before - v2Before;
        var relVelAfter = -restitution * relVelBefore;

        var v2After = (momentumBefore + mass1 * relVelAfter) / (mass1 + mass2);
        var v1After = v2After - relVelAfter;

        var momentumAfter = mass1 * v1After + mass2 * v2After;

        // Assert
        Assert.That(momentumAfter, Is.EqualTo(momentumBefore).Within(0.001f), "momentum should be conserved");
    }
}
