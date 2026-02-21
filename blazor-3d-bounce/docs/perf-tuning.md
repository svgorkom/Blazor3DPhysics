# Performance Tuning Guide

This guide helps optimize the Blazor 3D Physics application for best performance.

## Performance Targets

| Metric | Target | Acceptable | Warning |
|--------|--------|------------|---------|
| Frame Rate | 60 FPS | 45+ FPS | <30 FPS |
| Frame Time | <16.7ms | <22ms | >33ms |
| Physics Time | <4ms | <8ms | >12ms |
| Memory | <200MB | <400MB | >600MB |

## Profiling Tools

### Browser DevTools

1. Press F12 to open DevTools
2. Go to **Performance** tab
3. Record during simulation
4. Analyze flame graph

### Key Metrics to Watch

- **Scripting**: JS execution time
- **Rendering**: GPU/WebGL time
- **Idle**: Available headroom

### Stats Bar

The in-app stats bar shows:
- **FPS**: Frames per second
- **Physics**: Time spent in physics (ms)
- **Rigid/Soft Count**: Active body counts

## Interop Optimization

### Batching Strategy

**Problem**: Many JS interop calls are expensive

**Solution**: Batch all updates into single calls

```csharp
// Bad: One call per body
foreach (var body in bodies)
{
    await JS.InvokeVoidAsync("updateTransform", body.Id, body.Position);
}

// Good: Single batched call
var transforms = bodies.SelectMany(b => b.GetTransformArray()).ToArray();
var ids = bodies.Select(b => b.Id).ToArray();
await JS.InvokeVoidAsync("updateTransforms", transforms, ids);
```

### Typed Arrays

Use typed arrays for better transfer:

```javascript
// Efficient: Float32Array
const transforms = new Float32Array(bodyCount * 7);

// Avoid: Regular arrays of objects
const transforms = bodies.map(b => ({ x: b.x, y: b.y, z: b.z }));
```

### Minimize Calls Per Frame

Target: **2-4 interop calls per frame**

- 1 call: Rigid body transforms
- 1 call: Soft body vertices (batched)
- 1 call: Performance stats (optional, throttled)

## Rendering Optimization

### Shadow Quality

| Quality | Shadow Map | Performance |
|---------|------------|-------------|
| Off | 0 | +20% FPS |
| Low | 512 | +10% FPS |
| Medium | 1024 | Baseline |
| High | 2048 | -10% FPS |
| Ultra | 4096 | -25% FPS |

```csharp
renderSettings.ShadowMapSize = 1024; // Medium quality
renderSettings.EnableShadows = false; // Disable for performance
```

### Post Processing

| Effect | Cost | When to Disable |
|--------|------|-----------------|
| FXAA | ~5% | Rarely needed |
| SSAO | ~15% | First to disable |
| Bloom | ~10% | Optional |

```csharp
renderSettings.EnableFXAA = true;  // Cheap
renderSettings.EnableSSAO = false; // Expensive
```

### Mesh Complexity

| Object Type | Recommended Triangles |
|-------------|----------------------|
| Debug/Preview | 100-500 |
| Standard | 500-2000 |
| Hero Objects | 2000-10000 |

### Level of Detail

For distant objects:
```javascript
if (distanceToCamera > 50) {
    mesh.visibility = 0.5; // Could swap to lower LOD
}
```

## Physics Optimization

### Object Count Guidelines

| Scenario | Rigid Bodies | Soft Bodies |
|----------|-------------|-------------|
| High Performance | 50 | 1-2 |
| Standard | 100-200 | 3-5 |
| Complex | 300+ | 1 |

### Substep Tuning

More substeps = more accurate but slower:

```csharp
// Fast, less accurate
settings.SubSteps = 1;

// Balanced
settings.SubSteps = 3;

// Accurate, slower
settings.SubSteps = 6;
```

**When to increase**:
- Stiff contacts (stacking)
- High-speed collisions
- Soft body stability issues

### Sleeping System

Enable sleeping to reduce idle body cost:

```csharp
settings.EnableSleeping = true;
settings.SleepThreshold = 0.01f;
```

Bodies at rest cost nearly nothing.

### CCD Usage

CCD is expensive—enable only when needed:

```csharp
// Only for fast-moving small objects
body.EnableCCD = body.LinearVelocity.Length() > 10f;
```

### Collision Shape Complexity

| Shape | Relative Cost |
|-------|--------------|
| Sphere | 1x |
| Box | 1.2x |
| Capsule | 1.3x |
| Cylinder | 1.5x |
| Convex Hull | 2-5x |
| Triangle Mesh | 5-20x |

Use simple shapes where possible.

## Soft Body Optimization

### Resolution Guidelines

| Resolution | Vertices | Cost | Use Case |
|------------|----------|------|----------|
| 10×10 | 100 | Low | Preview/Background |
| 20×20 | 400 | Medium | Standard |
| 30×30 | 900 | High | Close-up |
| 50×50 | 2500 | Very High | Single focus |

### Iteration Count

Balance accuracy vs performance:

```csharp
// Fast, less accurate
material.ConstraintIterations = 5;

// Balanced
material.ConstraintIterations = 10;

// Accurate, slower
material.ConstraintIterations = 20;
```

### Self-Collision

Self-collision is expensive:

```csharp
// Enable only if cloth folds on itself
material.SelfCollision = true;  // +2-5x cost

// Increase margin to reduce narrow-phase checks
material.CollisionMargin = 0.05f;
```

### Vertex Buffer Updates

Optimize soft body rendering:

```javascript
// Use updateable meshes
const mesh = CreateGround("cloth", { updatable: true });

// Batch vertex updates
mesh.updateVerticesData(PositionKind, allVertices);
mesh.updateVerticesData(NormalKind, allNormals);
```

## Memory Management

### Object Pooling

Reuse buffers instead of allocating:

```csharp
// Bad: Allocate each frame
var transforms = new float[bodies.Count * 7];

// Good: Reuse buffer
if (_transformBuffer.Length < requiredSize)
{
    _transformBuffer = new float[requiredSize * 2]; // Grow with margin
}
```

### Avoid GC Spikes

Minimize allocations in hot paths:

```csharp
// Bad: Creates garbage
body.Position.ToString();
$"Body {body.Id} at {body.Position}";

// Good: Use cached strings or StringBuilder
```

### Dispose Properly

Clean up physics bodies:

```csharp
await RigidPhysics.RemoveRigidBodyAsync(body.Id);
await Rendering.RemoveMeshAsync(body.Id);
SceneState.RemoveObject(body.Id);
```

## Browser-Specific Tips

### Chrome
- Enable "Hardware Acceleration"
- Check `chrome://gpu` for WebGL status
- Consider Chrome Canary for latest features

### Firefox
- Enable WebGL in `about:config`
- Check for driver issues

### Edge
- Similar to Chrome (Chromium-based)
- Good WebAssembly performance

## Quality Presets

### Ultra Quality
```csharp
settings.SubSteps = 6;
renderSettings.ShadowMapSize = 4096;
renderSettings.EnableSSAO = true;
renderSettings.EnableFXAA = true;
softMaterial.ConstraintIterations = 20;
softMaterial.SelfCollision = true;
```

### High Quality (Default)
```csharp
settings.SubSteps = 3;
renderSettings.ShadowMapSize = 2048;
renderSettings.EnableSSAO = false;
renderSettings.EnableFXAA = true;
softMaterial.ConstraintIterations = 10;
softMaterial.SelfCollision = true;
```

### Performance Mode
```csharp
settings.SubSteps = 2;
renderSettings.ShadowMapSize = 512;
renderSettings.EnableSSAO = false;
renderSettings.EnableFXAA = false;
softMaterial.ConstraintIterations = 5;
softMaterial.SelfCollision = false;
```

### Minimum Spec
```csharp
settings.SubSteps = 1;
renderSettings.EnableShadows = false;
renderSettings.ShowGrid = false;
softMaterial.ConstraintIterations = 3;
```

## Diagnostic Checklist

When performance is poor:

1. [ ] Check FPS in stats bar
2. [ ] Check physics time—is it the bottleneck?
3. [ ] Count active objects (reduce if >200)
4. [ ] Check for soft body self-collision
5. [ ] Verify sleeping is working
6. [ ] Reduce shadow quality
7. [ ] Profile in browser DevTools
8. [ ] Check for console errors
9. [ ] Test in different browser
10. [ ] Monitor memory usage over time
