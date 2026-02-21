# Rigid Body Physics

This document details the rigid body physics implementation using Rapier.js.

## Physics Engine: Rapier.js

[Rapier](https://rapier.rs/) is a fast, deterministic physics engine written in Rust and compiled to WebAssembly. It provides:
- High performance (SIMD when available)
- Deterministic simulation
- Continuous collision detection (CCD)
- Island-based sleeping

## Restitution Model

### Coefficient of Restitution (e)

The coefficient of restitution determines how "bouncy" a collision is:

```
e = 0    ? Perfectly inelastic (no bounce)
e = 1    ? Perfectly elastic (full bounce)
e = 0.8  ? Typical rubber ball
```

### Velocity After Impact

For a collision with a static surface:
```
v_post_normal = -e × v_pre_normal
```

For two moving bodies:
```
v_post_normal = -e × v_relative_normal
```

### Energy Decay

Energy retained after n bounces:
```
E_n / E_0 ? e^(2n)
```

Example: e=0.8, bouncing from h=5m
| Bounce | Expected Height | Calculation |
|--------|----------------|-------------|
| 0 | 5.00 m | Initial |
| 1 | 3.20 m | 5 × 0.64 |
| 2 | 2.05 m | 5 × 0.64² |
| 3 | 1.31 m | 5 × 0.64³ |
| 4 | 0.84 m | 5 × 0.64? |

### Combined Restitution

When two bodies collide, Rapier uses geometric mean by default:
```
e_combined = ?(e_a × e_b)
```

## Friction Model

### Coulomb Friction

Rapier implements Coulomb friction with separate static and dynamic coefficients.

**Static Friction (??)**
- Prevents motion from starting
- Must overcome: `F_applied > ?? × N`

**Dynamic Friction (??)**
- Resistance during motion
- Force: `F_friction = ?? × N`
- Typically ?? < ??

### Combined Friction

```
?_combined = ?(?_a × ?_b)
```

### Material Presets

| Material | Static ? | Dynamic ? | Use Case |
|----------|----------|-----------|----------|
| Rubber | 0.9 | 0.8 | High grip |
| Wood | 0.5 | 0.4 | Medium |
| Steel | 0.6 | 0.4 | Medium-high |
| Ice | 0.1 | 0.03 | Very slippery |

## Damping

### Linear Damping

Reduces linear velocity over time:
```
v_new = v_old × (1 - linearDamping × dt)
```

Typical values:
- 0.0: No damping (space)
- 0.01: Minimal air resistance
- 0.1: Noticeable drag
- 1.0: Heavy damping

### Angular Damping

Reduces rotational velocity:
```
?_new = ?_old × (1 - angularDamping × dt)
```

## Continuous Collision Detection (CCD)

### The Tunneling Problem

Fast-moving objects can pass through thin surfaces between timesteps:

```
Frame N:   ?--------------------------|---- wall
Frame N+1:                            |    ?
                                      ^ Object passed through!
```

### CCD Solution

Rapier's CCD uses time-of-impact (TOI) detection:
1. Predict motion path
2. Find earliest collision time
3. Advance to collision, resolve, continue

### When to Enable CCD

Enable CCD for:
- Small, fast projectiles
- Objects moving >10 m/s
- Thin collision surfaces

```csharp
body.EnableCCD = velocity.Length() > 10f || scale < 0.1f;
```

### CCD Performance Cost

- ~2-5x more expensive per body
- Only enable when necessary
- Consider alternative: thicker colliders

## Timestep Strategy

### Fixed Timestep

We use a fixed timestep for determinism:

```csharp
const float fixedDt = 1f / 120f;  // 120 Hz physics
int subSteps = 3;

void PhysicsUpdate(float deltaTime)
{
    accumulator += deltaTime;
    
    while (accumulator >= fixedDt)
    {
        for (int i = 0; i < subSteps; i++)
        {
            world.Step(fixedDt / subSteps);
        }
        accumulator -= fixedDt;
    }
}
```

### Why 120 Hz?

- Fast enough for stiff contacts
- Allows 60 FPS render with margin
- Good balance of accuracy vs performance

### Sub-steps

Sub-stepping improves stability for:
- Stiff springs
- High-speed collisions
- Stacked objects

Typical values: 2-8 substeps

## Scaling Guidelines

### Size Recommendations

Keep object sizes in a realistic range:
```
Minimum: 0.05 m (5 cm)
Maximum: 10 m
Optimal: 0.1 - 5 m
```

### Why Size Matters

Very small objects:
- Need smaller timesteps
- More susceptible to numerical error
- CCD becomes essential

Very large objects:
- May need higher mass
- Collision margins become visible
- Consider breaking into components

### Mass Considerations

```
Too light (< 0.01 kg): Unstable stacking
Too heavy (> 10000 kg): Large mass ratios cause issues
Optimal ratio: < 1000:1 between interacting bodies
```

## Sleeping System

### Purpose

Sleeping deactivates bodies at rest to save CPU.

### Sleep Criteria

Body sleeps when:
- Linear velocity < threshold
- Angular velocity < threshold
- Sustained for sleep delay time

```javascript
const sleepThreshold = 0.01;  // m/s and rad/s
const sleepDelay = 1.0;       // seconds
```

### Wake Conditions

Bodies wake when:
- Another body collides with them
- External force/impulse applied
- User modification

### Configuration

```csharp
settings.EnableSleeping = true;
settings.SleepThreshold = 0.01f;
```

## Collision Shapes

### Primitives

| Shape | Use Case | Performance |
|-------|----------|-------------|
| Sphere | Balls, particles | Fastest |
| Box | Crates, walls | Fast |
| Capsule | Characters, pills | Fast |
| Cylinder | Barrels, pipes | Medium |
| Cone | Projectiles | Medium |

### Convex Hull

For custom meshes:
```javascript
const hull = RAPIER.ColliderDesc.convexHull(vertices);
```

Limitations:
- Must be convex (no concave regions)
- Vertex count affects performance
- Consider simplification

### Triangle Mesh

For static geometry only:
```javascript
const trimesh = RAPIER.ColliderDesc.trimesh(vertices, indices);
```

Use for:
- Terrain
- Static level geometry
- Complex static shapes

## Body Types

### Dynamic

Standard simulated body:
```csharp
body.IsStatic = false;
body.Mass = 1.0f;
```

### Static (Fixed)

Never moves, infinite mass:
```csharp
body.IsStatic = true;
```

Use for: Ground, walls, platforms

### Kinematic (Future)

Moved by code, not physics:
- Animated platforms
- Moving obstacles
- Player controllers

## Common Issues & Solutions

### Jittering Stacks

**Problem**: Stacked boxes vibrate

**Solutions**:
1. Increase substeps
2. Lower restitution (< 0.3)
3. Add damping (0.1-0.3)
4. Enable sleeping

### Tunneling

**Problem**: Fast objects pass through walls

**Solutions**:
1. Enable CCD
2. Reduce timestep
3. Thicken collision geometry
4. Cap maximum velocity

### Explosive Separations

**Problem**: Objects fly apart on contact

**Solutions**:
1. Check for overlapping spawn positions
2. Cap maximum restitution (< 0.95)
3. Verify mass ratios
4. Add damping

### Unstable Large Mass Ratios

**Problem**: Light object on heavy one causes issues

**Solutions**:
1. Keep mass ratios < 1000:1
2. Use artificial constraints
3. Consider kinematic for very heavy objects
