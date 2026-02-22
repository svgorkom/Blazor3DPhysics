# Deep Physics Engine Design Document

## Executive Summary

This document describes a production-ready GPU-accelerated physics engine upgrade for the Blazor 3D Physics application. The system replaces frame-based Euler integration with continuous, stable integration using semi-implicit methods and GPU compute shaders.

## 1. Design Summary

### 1.1 State Vector Definition

Each rigid body state is represented as:

```
State S = {
    position: vec3<f32>,      // World position (x, y, z)
    rotation: vec4<f32>,      // Quaternion (x, y, z, w)
    linearVelocity: vec3<f32>,  // Linear velocity (m/s)
    angularVelocity: vec3<f32>, // Angular velocity (rad/s)
    inverseMass: f32,         // 1/mass (0 for static)
    inverseInertia: vec3<f32>,  // Diagonal inverse inertia tensor
    restitution: f32,         // Coefficient of restitution [0, 1]
    friction: f32,            // Coefficient of friction [0, 1]
    colliderType: u32,        // 0=Sphere, 1=AABB, 2=Capsule
    colliderData: vec4<f32>,  // Type-specific (radius, half-extents, etc.)
}
```

### 1.2 Equations of Motion

**Newton-Euler equations:**
```
dv/dt = F_total / m + g
dω/dt = I^(-1) * (τ_total - ω × (I * ω))
dx/dt = v
dq/dt = 0.5 * ω̃ * q
```

Where:
- `v` = linear velocity
- `ω` = angular velocity  
- `F_total` = sum of external forces
- `τ_total` = sum of external torques
- `g` = gravity vector
- `I` = inertia tensor
- `q` = orientation quaternion
- `ω̃` = quaternion representation of angular velocity

### 1.3 Semi-Implicit Euler Integration

For stability, we use semi-implicit (symplectic) Euler:

```
v(t+dt) = v(t) + a(t) * dt
x(t+dt) = x(t) + v(t+dt) * dt   // Note: using updated velocity

ω(t+dt) = ω(t) + α(t) * dt
q(t+dt) = normalize(q(t) + 0.5 * ω̃(t+dt) * q(t) * dt)
```

### 1.4 Sub-Stepping Strategy

When `dt > maxTimeStep` or velocity exceeds threshold:

```
numSubSteps = ceil(dt / maxTimeStep)
subDt = dt / numSubSteps
for i in 0..numSubSteps:
    integrate(subDt)
    detectCollisions()
    resolveCollisions()
```

**Default parameters:**
- `maxTimeStep = 1/120 = 0.00833s`
- `velocityThreshold = 10 m/s` (triggers CCD)
- `maxSubSteps = 8`

---

## 2. Collision Detection

### 2.1 Broad Phase: GPU Spatial Hashing

**Grid Parameters:**
- Cell size: `2 * maxObjectRadius` (typically 2.0 units)
- Grid dimensions: 64×64×64 = 262,144 cells
- Max objects per cell: 32

**Hash Function:**
```wgsl
fn hashPosition(pos: vec3<f32>, cellSize: f32) -> u32 {
    let cell = vec3<i32>(floor(pos / cellSize));
    let prime1 = 73856093u;
    let prime2 = 19349663u;
    let prime3 = 83492791u;
    return (u32(cell.x) * prime1) ^ (u32(cell.y) * prime2) ^ (u32(cell.z) * prime3);
}
```

**Algorithm:**
1. **Build Phase:** Each object writes its ID to cell(s) it overlaps
2. **Count Phase:** Prefix sum to determine cell start indices  
3. **Compact Phase:** Write (cellId, objectId) pairs
4. **Query Phase:** For each object, check overlapping cells for pairs

### 2.2 Narrow Phase: Collision Tests

#### 2.2.1 Sphere-Sphere

```
d = posB - posA
dist² = dot(d, d)
radiusSum = radiusA + radiusB

if dist² < radiusSum²:
    dist = sqrt(dist²)
    normal = d / dist
    penetration = radiusSum - dist
    contactPoint = posA + normal * radiusA
```

#### 2.2.2 Sphere-Sphere Swept (CCD)

Time of impact using quadratic formula:

```
relVel = velA - velB
relPos = posA - posB
radiusSum = radiusA + radiusB

a = dot(relVel, relVel)
b = 2 * dot(relVel, relPos)
c = dot(relPos, relPos) - radiusSum²

discriminant = b² - 4*a*c
if discriminant >= 0:
    t = (-b - sqrt(discriminant)) / (2*a)
    if 0 <= t <= dt:
        return t  // Time of impact
```

#### 2.2.3 AABB-AABB

```
// Check overlap on each axis
for axis in [x, y, z]:
    if maxA[axis] < minB[axis] || maxB[axis] < minA[axis]:
        return false  // Separating axis found

// Find minimum penetration axis
minPenetration = INF
for axis in [x, y, z]:
    penA = maxA[axis] - minB[axis]
    penB = maxB[axis] - minA[axis]
    pen = min(penA, penB)
    if pen < minPenetration:
        minPenetration = pen
        normal = axis direction (sign based on which penetration)
```

#### 2.2.4 Sphere-AABB

```
// Find closest point on AABB to sphere center
closest = clamp(sphereCenter, aabbMin, aabbMax)
d = sphereCenter - closest
dist² = dot(d, d)

if dist² < radius²:
    if dist² < EPSILON:  // Center inside AABB
        // Find closest face
        ...
    else:
        dist = sqrt(dist²)
        normal = d / dist
        penetration = radius - dist
        contactPoint = closest
```

### 2.3 Contact Manifold

```
struct Contact {
    bodyA: u32,
    bodyB: u32,
    normal: vec3<f32>,     // Points from A to B
    penetration: f32,
    contactPoint: vec3<f32>,
    tangent1: vec3<f32>,   // Friction direction 1
    tangent2: vec3<f32>,   // Friction direction 2
    normalImpulse: f32,    // Accumulated for warmstarting
    tangentImpulse1: f32,
    tangentImpulse2: f32,
}
```

---

## 3. Collision Response

### 3.1 Impulse-Based Resolution

**Normal Impulse:**

```
// Relative velocity at contact point
rA = contactPoint - posA
rB = contactPoint - posB
vRelA = velA + cross(ωA, rA)
vRelB = velB + cross(ωB, rB)
vRel = vRelA - vRelB

// Normal component
vn = dot(vRel, normal)

// Only resolve if approaching
if vn < 0:
    // Effective mass
    invMassSum = invMassA + invMassB
    rAxN = cross(rA, normal)
    rBxN = cross(rB, normal)
    angularTerm = dot(rAxN, invInertiaA * rAxN) + dot(rBxN, invInertiaB * rBxN)
    effectiveMass = 1.0 / (invMassSum + angularTerm)
    
    // Restitution
    e = min(restitutionA, restitutionB)
    
    // Normal impulse magnitude
    j = -(1 + e) * vn * effectiveMass
    
    // Apply impulse
    impulse = j * normal
    velA += impulse * invMassA
    velB -= impulse * invMassB
    ωA += invInertiaA * cross(rA, impulse)
    ωB -= invInertiaB * cross(rB, impulse)
```

### 3.2 Friction Impulse (Coulomb Model)

```
// Tangential velocity
vt = vRel - vn * normal
vtMag = length(vt)

if vtMag > EPSILON:
    tangent = vt / vtMag
    
    // Tangent effective mass
    rAxT = cross(rA, tangent)
    rBxT = cross(rB, tangent)
    angularTermT = dot(rAxT, invInertiaA * rAxT) + dot(rBxT, invInertiaB * rBxT)
    effectiveMassT = 1.0 / (invMassSum + angularTermT)
    
    // Friction impulse
    jt = -vtMag * effectiveMassT
    
    // Coulomb clamp
    mu = sqrt(frictionA * frictionB)
    maxFriction = mu * j
    jt = clamp(jt, -maxFriction, maxFriction)
    
    // Apply friction impulse
    frictionImpulse = jt * tangent
    velA += frictionImpulse * invMassA
    velB -= frictionImpulse * invMassB
    ωA += invInertiaA * cross(rA, frictionImpulse)
    ωB -= invInertiaB * cross(rB, frictionImpulse)
```

### 3.3 Positional Correction (Baumgarte Stabilization)

To prevent penetration accumulation without energy injection:

```
// Parameters
baumgarteBias = 0.2      // Correction strength [0.1, 0.3]
slopThreshold = 0.005    // Penetration tolerance (5mm)
maxCorrection = 0.2      // Max position adjustment per step

// Only correct if penetration exceeds threshold
if penetration > slopThreshold:
    correction = baumgarteBias * (penetration - slopThreshold)
    correction = min(correction, maxCorrection)
    correction = correction / (invMassA + invMassB)
    
    posA -= correction * invMassA * normal
    posB += correction * invMassB * normal
```

**Split Impulse Alternative** (more stable for stacking):
```
// Separate position correction from velocity
biasVelocity = baumgarteBias * penetration / dt
// Add bias only to position update, not actual velocity
```

---

## 4. GPU Pipeline Architecture

### 4.1 Buffer Layout

```
// Bodies buffer (read/write)
struct Body {
    position: vec3<f32>,        // 12 bytes
    padding1: f32,              // 4 bytes (alignment)
    rotation: vec4<f32>,        // 16 bytes
    linearVelocity: vec3<f32>,  // 12 bytes
    padding2: f32,              // 4 bytes
    angularVelocity: vec3<f32>, // 12 bytes
    padding3: f32,              // 4 bytes
    inverseMass: f32,           // 4 bytes
    restitution: f32,           // 4 bytes
    friction: f32,              // 4 bytes
    colliderType: u32,          // 4 bytes
    colliderData: vec4<f32>,    // 16 bytes (radius, half-extents, etc.)
    inverseInertia: vec3<f32>,  // 12 bytes
    flags: u32,                 // 4 bytes (sleeping, static, etc.)
}
// Total: 112 bytes per body (aligned to 16 bytes)

// Contacts buffer
struct Contact {
    bodyA: u32,
    bodyB: u32,
    normal: vec3<f32>,
    penetration: f32,
    contactPoint: vec3<f32>,
    accumulatedImpulse: f32,
}
// Total: 40 bytes per contact

// Grid cells buffer
struct GridCell {
    objectCount: atomic<u32>,
    objectIds: array<u32, 32>,
}
```

### 4.2 Compute Pipeline Stages

```
Frame Pipeline:
┌─────────────────────────────────────────────────────────────┐
│  1. Integration Kernel                                      │
│     - Apply forces (gravity, user forces)                   │
│     - Semi-implicit Euler velocity update                   │
│     - Semi-implicit Euler position update                   │
│     Workgroups: ceil(numBodies / 64)                       │
├─────────────────────────────────────────────────────────────┤
│  2. Broad Phase: Grid Build                                 │
│     - Hash body positions to cells                          │
│     - Atomic increment cell counts                          │
│     Workgroups: ceil(numBodies / 64)                       │
├─────────────────────────────────────────────────────────────┤
│  3. Broad Phase: Prefix Sum (for compact pairs)             │
│     - Parallel prefix sum on cell counts                    │
│     Workgroups: ceil(numCells / 256)                       │
├─────────────────────────────────────────────────────────────┤
│  4. Broad Phase: Generate Pairs                             │
│     - For each body, check neighboring cells                │
│     - Output candidate pairs                                │
│     Workgroups: ceil(numBodies / 64)                       │
├─────────────────────────────────────────────────────────────┤
│  5. Narrow Phase: Collision Detection                       │
│     - Test each candidate pair                              │
│     - Generate contact manifolds                            │
│     Workgroups: ceil(numPairs / 64)                        │
├─────────────────────────────────────────────────────────────┤
│  6. Constraint Solver (iterative)                           │
│     for i in 0..solverIterations:                          │
│       - Compute impulses                                    │
│       - Apply impulses (using graph coloring or Jacobi)     │
│     Workgroups: ceil(numContacts / 64)                     │
├─────────────────────────────────────────────────────────────┤
│  7. Position Correction                                     │
│     - Baumgarte stabilization                               │
│     Workgroups: ceil(numContacts / 64)                     │
└─────────────────────────────────────────────────────────────┘
```

### 4.3 Avoiding Atomics with Graph Coloring

To avoid race conditions when multiple contacts affect the same body:

1. **Pre-compute contact graph** on CPU
2. **Color contacts** such that no two same-color contacts share a body
3. **Dispatch one pass per color**

For real-time, use **Jacobi iteration** instead:
- Read from previous iteration's velocities
- Write to new velocity buffer
- Swap buffers between iterations

---

## 5. Performance Targets

| Metric | Target | Notes |
|--------|--------|-------|
| Bodies (spheres) | 10,000+ | GPU broad phase essential |
| Bodies (mixed) | 5,000+ | More narrow phase overhead |
| Frame time | <16.67ms | 60 FPS |
| Physics step | <4ms | Leaves headroom for rendering |
| Contact pairs | 50,000+ | With spatial hashing |
| Energy drift | <1%/minute | With proper integration |
| Max penetration | <5mm | With Baumgarte correction |

### 5.1 Workgroup Size Recommendations

| Kernel | Workgroup Size | Notes |
|--------|---------------|-------|
| Integration | 64 | Optimal for most GPUs |
| Grid Build | 64 | Atomic contention with 256 |
| Prefix Sum | 256 | Reduce passes |
| Narrow Phase | 64 | Memory bound |
| Solver | 64 | Balanced |

### 5.2 Memory Alignment

- Use `vec4<f32>` for positions (16-byte alignment)
- Pad structures to 16-byte boundaries
- Use storage buffers with `std430` layout

---

## 6. Fallback CPU Path

For devices without compute shader support:

```csharp
if (!webGpuComputeAvailable)
{
    // Use JavaScript physics (current Rapier.js path)
    // Or .NET SIMD-optimized CPU physics
    useCpuPhysics = true;
}
```

CPU path uses:
- System.Numerics.Vector3/Quaternion for SIMD
- Parallel.ForEach for multi-threading
- Same algorithms, sequential execution

---

## 7. Integration Checklist

- [ ] Create WGSL shader modules
- [ ] Implement C# GPU buffer management
- [ ] Add JS interop for WebGPU compute
- [ ] Implement integration kernel
- [ ] Implement broad phase kernels
- [ ] Implement narrow phase kernel
- [ ] Implement solver kernel
- [ ] Add CPU fallback path
- [ ] Add energy tracking metrics
- [ ] Add penetration depth metrics
- [ ] Create test scenarios
- [ ] Performance benchmarking
- [ ] Documentation updates

---

## 8. References

- Erin Catto, "Physics for Game Programmers" (GDC presentations)
- Randy Gaul, "Game Physics" tutorials
- NVIDIA, "GPU Gems 3: Chapter 32 - Broad-Phase Collision Detection"
- Bullet Physics documentation
- Box2D documentation
