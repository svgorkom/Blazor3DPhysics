using System.Numerics;
using System.Collections.Concurrent;
using System.Diagnostics;
using BlazorClient.Domain.Models;
using SysVector3 = System.Numerics.Vector3;
using SysQuaternion = System.Numerics.Quaternion;
using DomainVector3 = BlazorClient.Domain.Models.Vector3;
using DomainQuaternion = BlazorClient.Domain.Models.Quaternion;

namespace BlazorClient.Services;

/// <summary>
/// CPU-based physics service with SIMD optimization.
/// Used as fallback when GPU physics is unavailable.
/// </summary>
public class CpuPhysicsService : IRigidPhysicsService, IAsyncDisposable
{
    // Physics state
    private readonly ConcurrentDictionary<string, CpuRigidBody> _bodies = new();
    private readonly ConcurrentDictionary<string, CpuRigidBody> _initialStates = new();
    private readonly List<Contact> _contacts = new();
    private readonly object _contactLock = new();

    // Simulation settings
    private SysVector3 _gravity = new(0, -9.81f, 0);
    private float _timeStep = 1f / 120f;
    private int _subSteps = 3;
    private int _solverIterations = 8;
    private bool _initialized;

    // Ground collision
    private float _groundY = 0f;
    private float _groundRestitution = 0.3f;
    private float _groundFriction = 0.5f;

    // Performance
    private readonly Stopwatch _stepTimer = new();

    // Constants
    private const float EPSILON = 1e-6f;
    private const float BAUMGARTE_BIAS = 0.2f;
    private const float SLOP = 0.005f;
    private const float MAX_CORRECTION = 0.2f;

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync() => Task.FromResult(_initialized);

    /// <inheritdoc />
    public Task InitializeAsync(SimulationSettings settings)
    {
        _gravity = new SysVector3(settings.Gravity.X, settings.Gravity.Y, settings.Gravity.Z);
        _timeStep = settings.TimeStep;
        _subSteps = settings.SubSteps;
        _solverIterations = _subSteps * 4;
        _initialized = true;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateRigidBodyAsync(RigidBody body)
    {
        var cpuBody = new CpuRigidBody
        {
            Id = body.Id,
            Position = ToSysVector3(body.Transform.Position),
            Rotation = ToSysQuaternion(body.Transform.Rotation),
            Scale = ToSysVector3(body.Transform.Scale),
            LinearVelocity = ToSysVector3(body.LinearVelocity),
            AngularVelocity = ToSysVector3(body.AngularVelocity),
            InverseMass = body.IsStatic ? 0 : 1f / body.Mass,
            InverseInertia = ComputeInverseInertia(body),
            Restitution = body.Material.Restitution,
            StaticFriction = body.Material.StaticFriction,
            DynamicFriction = body.Material.DynamicFriction,
            LinearDamping = body.LinearDamping,
            AngularDamping = body.AngularDamping,
            IsStatic = body.IsStatic,
            ColliderType = GetColliderType(body.PrimitiveType),
            ColliderRadius = body.Transform.Scale.X * 0.5f,
            ColliderHalfExtents = new SysVector3(
                body.Transform.Scale.X * 0.5f,
                body.Transform.Scale.Y * 0.5f,
                body.Transform.Scale.Z * 0.5f)
        };

        _bodies[body.Id] = cpuBody;
        _initialStates[body.Id] = cpuBody.Clone();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveRigidBodyAsync(string id)
    {
        _bodies.TryRemove(id, out _);
        _initialStates.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateRigidBodyAsync(RigidBody body)
    {
        if (_bodies.TryGetValue(body.Id, out var cpuBody))
        {
            cpuBody.InverseMass = body.IsStatic ? 0 : 1f / body.Mass;
            cpuBody.Restitution = body.Material.Restitution;
            cpuBody.StaticFriction = body.Material.StaticFriction;
            cpuBody.DynamicFriction = body.Material.DynamicFriction;
            cpuBody.LinearDamping = body.LinearDamping;
            cpuBody.AngularDamping = body.AngularDamping;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyImpulseAsync(string id, DomainVector3 impulse)
    {
        if (_bodies.TryGetValue(id, out var body) && body.InverseMass > 0)
        {
            body.LinearVelocity += ToSysVector3(impulse) * body.InverseMass;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ApplyForceAsync(string id, DomainVector3 force)
    {
        var impulse = new DomainVector3(
            force.X * _timeStep,
            force.Y * _timeStep,
            force.Z * _timeStep);
        return ApplyImpulseAsync(id, impulse);
    }

    /// <inheritdoc />
    public Task SetLinearVelocityAsync(string id, DomainVector3 velocity)
    {
        if (_bodies.TryGetValue(id, out var body) && !body.IsStatic)
        {
            body.LinearVelocity = ToSysVector3(velocity);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateSettingsAsync(SimulationSettings settings)
    {
        _gravity = new SysVector3(settings.Gravity.X, settings.Gravity.Y, settings.Gravity.Z);
        _timeStep = settings.TimeStep;
        _subSteps = settings.SubSteps;
        _solverIterations = _subSteps * 4;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StepAsync(float deltaTime)
    {
        _stepTimer.Restart();

        var dt = deltaTime > 0 ? deltaTime : _timeStep;
        var bodies = _bodies.Values.ToArray();

        // Sub-stepping
        var numSubSteps = Math.Max(1, (int)Math.Ceiling(dt / _timeStep));
        numSubSteps = Math.Min(numSubSteps, 8);
        var subDt = dt / numSubSteps;

        for (int step = 0; step < numSubSteps; step++)
        {
            // 1. Integration
            IntegrateBodies(bodies, subDt);

            // 2. Collision Detection
            DetectCollisions(bodies);

            // 3. Collision Resolution
            SolveCollisions(bodies, subDt);

            // 4. Position Correction
            CorrectPositions();
        }

        _stepTimer.Stop();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RigidTransformBatch> GetTransformBatchAsync()
    {
        var bodies = _bodies.Values.ToArray();
        var transforms = new float[bodies.Length * 7];
        var ids = new string[bodies.Length];

        for (int i = 0; i < bodies.Length; i++)
        {
            var body = bodies[i];
            var offset = i * 7;

            transforms[offset + 0] = body.Position.X;
            transforms[offset + 1] = body.Position.Y;
            transforms[offset + 2] = body.Position.Z;
            transforms[offset + 3] = body.Rotation.X;
            transforms[offset + 4] = body.Rotation.Y;
            transforms[offset + 5] = body.Rotation.Z;
            transforms[offset + 6] = body.Rotation.W;

            ids[i] = body.Id;
        }

        return Task.FromResult(new RigidTransformBatch
        {
            Transforms = transforms,
            Ids = ids
        });
    }

    /// <inheritdoc />
    public Task ResetAsync()
    {
        foreach (var kvp in _initialStates)
        {
            if (_bodies.TryGetValue(kvp.Key, out var body))
            {
                var initial = kvp.Value;
                body.Position = initial.Position;
                body.Rotation = initial.Rotation;
                body.LinearVelocity = SysVector3.Zero;
                body.AngularVelocity = SysVector3.Zero;
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateGroundAsync(float restitution = 0.3f, float friction = 0.5f)
    {
        _groundRestitution = restitution;
        _groundFriction = friction;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _bodies.Clear();
        _initialStates.Clear();
        _initialized = false;
        return ValueTask.CompletedTask;
    }

    // ========================================
    // Integration
    // ========================================

    private void IntegrateBodies(CpuRigidBody[] bodies, float dt)
    {
        // Use Parallel.For for SIMD optimization on multiple bodies
        Parallel.For(0, bodies.Length, i =>
        {
            var body = bodies[i];
            if (body.IsStatic) return;

            // Semi-implicit Euler
            // Update velocity first
            body.LinearVelocity += _gravity * dt;

            // Apply damping
            var linearDamp = MathF.Exp(-body.LinearDamping * dt);
            var angularDamp = MathF.Exp(-body.AngularDamping * dt);
            body.LinearVelocity *= linearDamp;
            body.AngularVelocity *= angularDamp;

            // Update position with new velocity
            body.Position += body.LinearVelocity * dt;

            // Update rotation
            if (body.AngularVelocity.LengthSquared() > EPSILON)
            {
                var omega = body.AngularVelocity;
                var omegaQuat = new SysQuaternion(omega.X * 0.5f * dt,
                                                   omega.Y * 0.5f * dt,
                                                   omega.Z * 0.5f * dt, 0);
                body.Rotation = SysQuaternion.Normalize(body.Rotation + omegaQuat * body.Rotation);
            }
        });
    }

    // ========================================
    // Collision Detection
    // ========================================

    private void DetectCollisions(CpuRigidBody[] bodies)
    {
        lock (_contactLock)
        {
            _contacts.Clear();

            // Ground collisions
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                if (body.IsStatic) continue;

                var contact = TestGroundCollision(body);
                if (contact != null)
                {
                    _contacts.Add(contact);
                }
            }

            // Body-body collisions (O(n²) - acceptable for CPU fallback)
            for (int i = 0; i < bodies.Length; i++)
            {
                for (int j = i + 1; j < bodies.Length; j++)
                {
                    var a = bodies[i];
                    var b = bodies[j];

                    // Skip static-static
                    if (a.IsStatic && b.IsStatic) continue;

                    var contact = TestCollision(a, b);
                    if (contact != null)
                    {
                        _contacts.Add(contact);
                    }
                }
            }
        }
    }

    private Contact? TestGroundCollision(CpuRigidBody body)
    {
        float bottomY;
        if (body.ColliderType == ColliderType.Sphere)
        {
            bottomY = body.Position.Y - body.ColliderRadius;
        }
        else
        {
            bottomY = body.Position.Y - body.ColliderHalfExtents.Y;
        }

        if (bottomY < _groundY)
        {
            return new Contact
            {
                BodyA = body,
                BodyB = null,  // Ground
                Normal = SysVector3.UnitY,
                Penetration = _groundY - bottomY,
                ContactPoint = new SysVector3(body.Position.X, _groundY, body.Position.Z),
                Restitution = MathF.Min(body.Restitution, _groundRestitution),
                Friction = MathF.Sqrt(body.DynamicFriction * _groundFriction)
            };
        }

        return null;
    }

    private Contact? TestCollision(CpuRigidBody a, CpuRigidBody b)
    {
        if (a.ColliderType == ColliderType.Sphere && b.ColliderType == ColliderType.Sphere)
        {
            return TestSphereSphere(a, b);
        }
        else if (a.ColliderType == ColliderType.AABB && b.ColliderType == ColliderType.AABB)
        {
            return TestAABBAABB(a, b);
        }
        else if (a.ColliderType == ColliderType.Sphere && b.ColliderType == ColliderType.AABB)
        {
            return TestSphereAABB(a, b);
        }
        else if (a.ColliderType == ColliderType.AABB && b.ColliderType == ColliderType.Sphere)
        {
            var contact = TestSphereAABB(b, a);
            if (contact != null)
            {
                contact.Normal = -contact.Normal;
                (contact.BodyA, contact.BodyB) = (contact.BodyB, contact.BodyA);
            }
            return contact;
        }

        return null;
    }

    private Contact? TestSphereSphere(CpuRigidBody a, CpuRigidBody b)
    {
        var d = b.Position - a.Position;
        var distSq = d.LengthSquared();
        var radiusSum = a.ColliderRadius + b.ColliderRadius;

        if (distSq >= radiusSum * radiusSum)
            return null;

        var dist = MathF.Sqrt(distSq);
        SysVector3 normal;
        float penetration;
        SysVector3 contactPoint;

        if (dist < EPSILON)
        {
            normal = SysVector3.UnitY;
            penetration = radiusSum;
            contactPoint = a.Position;
        }
        else
        {
            normal = d / dist;
            penetration = radiusSum - dist;
            contactPoint = a.Position + normal * a.ColliderRadius;
        }

        return new Contact
        {
            BodyA = a,
            BodyB = b,
            Normal = normal,
            Penetration = penetration,
            ContactPoint = contactPoint,
            Restitution = MathF.Min(a.Restitution, b.Restitution),
            Friction = MathF.Sqrt(a.DynamicFriction * b.DynamicFriction)
        };
    }

    private Contact? TestAABBAABB(CpuRigidBody a, CpuRigidBody b)
    {
        var minA = a.Position - a.ColliderHalfExtents;
        var maxA = a.Position + a.ColliderHalfExtents;
        var minB = b.Position - b.ColliderHalfExtents;
        var maxB = b.Position + b.ColliderHalfExtents;

        // Separating axis test
        if (maxA.X < minB.X || maxB.X < minA.X) return null;
        if (maxA.Y < minB.Y || maxB.Y < minA.Y) return null;
        if (maxA.Z < minB.Z || maxB.Z < minA.Z) return null;

        // Find minimum penetration axis
        var penX = MathF.Min(maxA.X - minB.X, maxB.X - minA.X);
        var penY = MathF.Min(maxA.Y - minB.Y, maxB.Y - minA.Y);
        var penZ = MathF.Min(maxA.Z - minB.Z, maxB.Z - minA.Z);

        SysVector3 normal;
        float penetration;

        if (penX < penY && penX < penZ)
        {
            penetration = penX;
            normal = (a.Position.X < b.Position.X) ? -SysVector3.UnitX : SysVector3.UnitX;
        }
        else if (penY < penZ)
        {
            penetration = penY;
            normal = (a.Position.Y < b.Position.Y) ? -SysVector3.UnitY : SysVector3.UnitY;
        }
        else
        {
            penetration = penZ;
            normal = (a.Position.Z < b.Position.Z) ? -SysVector3.UnitZ : SysVector3.UnitZ;
        }

        var contactPoint = (SysVector3.Max(minA, minB) + SysVector3.Min(maxA, maxB)) * 0.5f;

        return new Contact
        {
            BodyA = a,
            BodyB = b,
            Normal = normal,
            Penetration = penetration,
            ContactPoint = contactPoint,
            Restitution = MathF.Min(a.Restitution, b.Restitution),
            Friction = MathF.Sqrt(a.DynamicFriction * b.DynamicFriction)
        };
    }

    private Contact? TestSphereAABB(CpuRigidBody sphere, CpuRigidBody box)
    {
        var closest = SysVector3.Clamp(
            sphere.Position,
            box.Position - box.ColliderHalfExtents,
            box.Position + box.ColliderHalfExtents);

        var diff = sphere.Position - closest;
        var distSq = diff.LengthSquared();

        if (distSq >= sphere.ColliderRadius * sphere.ColliderRadius)
            return null;

        SysVector3 normal;
        float penetration;

        var dist = MathF.Sqrt(distSq);
        if (dist < EPSILON)
        {
            // Sphere center inside box
            var distToFaces = box.ColliderHalfExtents - SysVector3.Abs(sphere.Position - box.Position);
            if (distToFaces.X < distToFaces.Y && distToFaces.X < distToFaces.Z)
            {
                normal = new SysVector3(MathF.Sign(sphere.Position.X - box.Position.X), 0, 0);
                penetration = sphere.ColliderRadius + distToFaces.X;
            }
            else if (distToFaces.Y < distToFaces.Z)
            {
                normal = new SysVector3(0, MathF.Sign(sphere.Position.Y - box.Position.Y), 0);
                penetration = sphere.ColliderRadius + distToFaces.Y;
            }
            else
            {
                normal = new SysVector3(0, 0, MathF.Sign(sphere.Position.Z - box.Position.Z));
                penetration = sphere.ColliderRadius + distToFaces.Z;
            }
        }
        else
        {
            normal = diff / dist;
            penetration = sphere.ColliderRadius - dist;
        }

        return new Contact
        {
            BodyA = sphere,
            BodyB = box,
            Normal = normal,
            Penetration = penetration,
            ContactPoint = sphere.Position - normal * sphere.ColliderRadius,
            Restitution = MathF.Min(sphere.Restitution, box.Restitution),
            Friction = MathF.Sqrt(sphere.DynamicFriction * box.DynamicFriction)
        };
    }

    // ========================================
    // Collision Resolution
    // ========================================

    private void SolveCollisions(CpuRigidBody[] bodies, float dt)
    {
        lock (_contactLock)
        {
            for (int iter = 0; iter < _solverIterations; iter++)
            {
                foreach (var contact in _contacts)
                {
                    SolveContact(contact, dt);
                }
            }
        }
    }

    private void SolveContact(Contact contact, float dt)
    {
        var a = contact.BodyA;
        var b = contact.BodyB;

        var invMassA = a.InverseMass;
        var invMassB = b?.InverseMass ?? 0;
        var invMassSum = invMassA + invMassB;

        if (invMassSum < EPSILON) return;

        var rA = contact.ContactPoint - a.Position;
        var rB = b != null ? contact.ContactPoint - b.Position : SysVector3.Zero;

        // Relative velocity at contact
        var vA = a.LinearVelocity + SysVector3.Cross(a.AngularVelocity, rA);
        var vB = b != null ? b.LinearVelocity + SysVector3.Cross(b.AngularVelocity, rB) : SysVector3.Zero;
        var vRel = vA - vB;

        var vn = SysVector3.Dot(vRel, contact.Normal);

        // Only resolve if approaching
        if (vn >= 0) return;

        // Effective mass
        var rAxN = SysVector3.Cross(rA, contact.Normal);
        var rBxN = b != null ? SysVector3.Cross(rB, contact.Normal) : SysVector3.Zero;

        var angularTermA = SysVector3.Dot(rAxN, a.InverseInertia * rAxN);
        var angularTermB = b != null ? SysVector3.Dot(rBxN, b.InverseInertia * rBxN) : 0;
        var effectiveMass = 1f / (invMassSum + angularTermA + angularTermB);

        // Bias for penetration
        var bias = MathF.Max(0, contact.Penetration - SLOP) * BAUMGARTE_BIAS / dt;

        // Normal impulse
        var j = -(1 + contact.Restitution) * vn * effectiveMass;
        j += bias * effectiveMass;
        j = MathF.Max(0, j);  // Can only push apart

        var impulse = j * contact.Normal;

        a.LinearVelocity += impulse * invMassA;
        a.AngularVelocity += a.InverseInertia * SysVector3.Cross(rA, impulse);

        if (b != null)
        {
            b.LinearVelocity -= impulse * invMassB;
            b.AngularVelocity -= b.InverseInertia * SysVector3.Cross(rB, impulse);
        }

        // Friction impulse
        var vRelUpdated = (a.LinearVelocity + SysVector3.Cross(a.AngularVelocity, rA)) -
                          (b != null ? b.LinearVelocity + SysVector3.Cross(b.AngularVelocity, rB) : SysVector3.Zero);

        var vt = vRelUpdated - SysVector3.Dot(vRelUpdated, contact.Normal) * contact.Normal;
        var vtMag = vt.Length();

        if (vtMag > EPSILON)
        {
            var tangent = vt / vtMag;

            var rAxT = SysVector3.Cross(rA, tangent);
            var rBxT = b != null ? SysVector3.Cross(rB, tangent) : SysVector3.Zero;
            var angTermTA = SysVector3.Dot(rAxT, a.InverseInertia * rAxT);
            var angTermTB = b != null ? SysVector3.Dot(rBxT, b.InverseInertia * rBxT) : 0;
            var effectiveMassT = 1f / (invMassSum + angTermTA + angTermTB);

            var jt = -vtMag * effectiveMassT;

            // Coulomb friction clamp
            var maxFriction = contact.Friction * j;
            jt = Math.Clamp(jt, -maxFriction, maxFriction);

            var frictionImpulse = jt * tangent;

            a.LinearVelocity += frictionImpulse * invMassA;
            a.AngularVelocity += a.InverseInertia * SysVector3.Cross(rA, frictionImpulse);

            if (b != null)
            {
                b.LinearVelocity -= frictionImpulse * invMassB;
                b.AngularVelocity -= b.InverseInertia * SysVector3.Cross(rB, frictionImpulse);
            }
        }
    }

    private void CorrectPositions()
    {
        lock (_contactLock)
        {
            foreach (var contact in _contacts)
            {
                if (contact.Penetration <= SLOP) continue;

                var a = contact.BodyA;
                var b = contact.BodyB;

                var invMassSum = a.InverseMass + (b?.InverseMass ?? 0);
                if (invMassSum < EPSILON) continue;

                var correction = (contact.Penetration - SLOP) * BAUMGARTE_BIAS;
                correction = MathF.Min(correction, MAX_CORRECTION);

                var correctionVec = contact.Normal * (correction / invMassSum);

                a.Position += correctionVec * a.InverseMass;
                if (b != null)
                {
                    b.Position -= correctionVec * b.InverseMass;
                }
            }
        }
    }

    // ========================================
    // Utility
    // ========================================

    private static SysVector3 ToSysVector3(DomainVector3 v) => new(v.X, v.Y, v.Z);
    private static SysQuaternion ToSysQuaternion(DomainQuaternion q) => new(q.X, q.Y, q.Z, q.W);

    private static SysVector3 ComputeInverseInertia(RigidBody body)
    {
        if (body.IsStatic) return SysVector3.Zero;

        var mass = body.Mass;
        var scale = body.Transform.Scale;

        switch (body.PrimitiveType)
        {
            case RigidPrimitiveType.Sphere:
                var r = scale.X * 0.5f;
                var I = 0.4f * mass * r * r;
                return new SysVector3(1f / I, 1f / I, 1f / I);

            case RigidPrimitiveType.Box:
                var Ix = (mass / 12f) * (scale.Y * scale.Y + scale.Z * scale.Z);
                var Iy = (mass / 12f) * (scale.X * scale.X + scale.Z * scale.Z);
                var Iz = (mass / 12f) * (scale.X * scale.X + scale.Y * scale.Y);
                return new SysVector3(1f / Ix, 1f / Iy, 1f / Iz);

            default:
                return new SysVector3(1f / mass, 1f / mass, 1f / mass);
        }
    }

    private static ColliderType GetColliderType(RigidPrimitiveType type)
    {
        return type switch
        {
            RigidPrimitiveType.Sphere => ColliderType.Sphere,
            RigidPrimitiveType.Box => ColliderType.AABB,
            _ => ColliderType.Sphere
        };
    }

    // ========================================
    // Internal Types
    // ========================================

    private enum ColliderType
    {
        Sphere,
        AABB,
        Capsule
    }

    private class CpuRigidBody
    {
        public string Id { get; set; } = string.Empty;
        public SysVector3 Position { get; set; }
        public SysQuaternion Rotation { get; set; }
        public SysVector3 Scale { get; set; }
        public SysVector3 LinearVelocity { get; set; }
        public SysVector3 AngularVelocity { get; set; }
        public float InverseMass { get; set; }
        public SysVector3 InverseInertia { get; set; }
        public float Restitution { get; set; }
        public float StaticFriction { get; set; }
        public float DynamicFriction { get; set; }
        public float LinearDamping { get; set; }
        public float AngularDamping { get; set; }
        public bool IsStatic { get; set; }
        public ColliderType ColliderType { get; set; }
        public float ColliderRadius { get; set; }
        public SysVector3 ColliderHalfExtents { get; set; }

        public CpuRigidBody Clone()
        {
            return new CpuRigidBody
            {
                Id = Id,
                Position = Position,
                Rotation = Rotation,
                Scale = Scale,
                LinearVelocity = SysVector3.Zero,
                AngularVelocity = SysVector3.Zero,
                InverseMass = InverseMass,
                InverseInertia = InverseInertia,
                Restitution = Restitution,
                StaticFriction = StaticFriction,
                DynamicFriction = DynamicFriction,
                LinearDamping = LinearDamping,
                AngularDamping = AngularDamping,
                IsStatic = IsStatic,
                ColliderType = ColliderType,
                ColliderRadius = ColliderRadius,
                ColliderHalfExtents = ColliderHalfExtents
            };
        }
    }

    private class Contact
    {
        public CpuRigidBody BodyA { get; set; } = null!;
        public CpuRigidBody? BodyB { get; set; }
        public SysVector3 Normal { get; set; }
        public float Penetration { get; set; }
        public SysVector3 ContactPoint { get; set; }
        public float Restitution { get; set; }
        public float Friction { get; set; }
    }
}
