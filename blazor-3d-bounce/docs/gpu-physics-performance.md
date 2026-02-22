# GPU Physics Performance Guide

## Memory Layout and Alignment

### Buffer Alignment Requirements

WebGPU requires specific alignment for buffers:

| Type | Alignment | Size |
|------|-----------|------|
| `f32` | 4 bytes | 4 bytes |
| `vec2<f32>` | 8 bytes | 8 bytes |
| `vec3<f32>` | 16 bytes | 12 bytes |
| `vec4<f32>` | 16 bytes | 16 bytes |
| `mat4x4<f32>` | 16 bytes | 64 bytes |
| struct | 16 bytes | varies |

### Optimal Struct Packing

**Bad (48 bytes with padding):**
```wgsl
struct BadBody {
    position: vec3<f32>,  // 12 bytes
    // 4 bytes padding
    velocity: vec3<f32>,  // 12 bytes
    // 4 bytes padding
    mass: f32,            // 4 bytes
    // 12 bytes padding to align next struct
}
```

**Good (32 bytes, no wasted space):**
```wgsl
struct GoodBody {
    position: vec3<f32>,  // 12 bytes
    mass: f32,            // 4 bytes (fills the gap)
    velocity: vec3<f32>,  // 12 bytes
    _padding: f32,        // 4 bytes (explicit padding)
}
```

## Workgroup Size Selection

### Recommended Sizes by GPU Vendor

| Vendor | Optimal | Notes |
|--------|---------|-------|
| NVIDIA | 64 or 128 | Warp size = 32 |
| AMD | 64 | Wavefront = 64 |
| Intel | 32 or 64 | Variable SIMD |
| Apple M1/M2 | 32 or 64 | Metal-like |

### Our Defaults

```wgsl
// Integration, narrow phase, solver
@compute @workgroup_size(64)

// Grid operations with atomics
@compute @workgroup_size(64)  // Lower to reduce contention

// Prefix sum, bulk operations
@compute @workgroup_size(256)  // Higher for throughput
```

## Avoiding Atomic Contention

### Problem: Spatial Hash Grid Writes

When many objects hash to the same cell, atomics serialize:

```wgsl
// Slow - high contention
atomicAdd(&cellCounts[hash], 1u);
```

### Solutions

1. **Counting Sort** (what we use)
   - Pass 1: Count objects per cell (atomics)
   - Pass 2: Prefix sum for offsets
   - Pass 3: Write objects to sorted positions

2. **Graph Coloring** (for solver)
   - Pre-compute contact graph on CPU
   - Color contacts so same-color are independent
   - Dispatch one pass per color (no atomics needed)

3. **Jacobi Iteration** (simpler)
   - Read from buffer A, write to buffer B
   - Swap buffers between iterations
   - Converges slower but simpler

## Precision Considerations

### 32-bit vs 16-bit

| Use Case | Recommended | Notes |
|----------|-------------|-------|
| Positions | `f32` | Accumulation errors with f16 |
| Velocities | `f32` | Integration stability |
| Normals | `f16` or `f32` | f16 sufficient |
| Impulses | `f32` | Accumulation |
| Grid coords | `i32` | Integer math faster |

### Large World Support

For worlds > 1km, consider:
- Double-precision on CPU
- Camera-relative positions on GPU
- Chunked simulation

## Memory Budget

### Per-Body Memory

```
RigidBody struct:     112 bytes
AABB:                  32 bytes
External forces:       16 bytes
Total per body:       160 bytes

10,000 bodies:        1.6 MB
```

### Per-Contact Memory

```
Contact struct:        64 bytes
Max 65,536 contacts:  4.2 MB
```

### Grid Memory

```
Cell counts:          256 KB (64K cells × 4 bytes)
Cell bodies:          8 MB (64K × 32 objects × 4 bytes)
Cell offsets:         256 KB
Total grid:           ~8.5 MB
```

### Total for 10K Bodies

```
Bodies:               1.6 MB
Contacts:             4.2 MB
Grid:                 8.5 MB
Pairs:                0.5 MB
Staging:              1.6 MB
Total:               ~16.4 MB
```

## Dispatch Strategies

### Minimize Dispatches

```javascript
// Bad: Many small dispatches
commandEncoder.dispatchWorkgroups(1);
commandEncoder.dispatchWorkgroups(1);
// ... 10 more

// Good: Batched in single submit
const commandEncoder = device.createCommandEncoder();
// All passes use same encoder
device.queue.submit([commandEncoder.finish()]);
```

### Pipeline Barriers

Implicit barriers between passes. Structure for minimal stalls:

```
Integration (independent per body)
    ↓
Broad Phase Build (grid writes)
    ↓ [barrier - grid must complete]
Broad Phase Query (grid reads)
    ↓
Narrow Phase (pair testing)
    ↓ [barrier - contacts must complete]
Solver Iterations (contact reads/writes)
    ↓
Position Correction
```

## CPU Fallback Performance

### SIMD Optimization

The CPU fallback uses `System.Numerics`:

```csharp
// Uses SIMD automatically
var a = new System.Numerics.Vector3(1, 2, 3);
var b = new System.Numerics.Vector3(4, 5, 6);
var c = a + b;  // Single SIMD instruction
```

### Parallelization

```csharp
// Integration is parallel
Parallel.For(0, bodies.Length, i => {
    IntegrateBody(bodies[i], dt);
});

// Collision detection parallel for queries
// (writes need synchronization)
```

### When CPU is Faster

- < 100 bodies: GPU dispatch overhead dominates
- Simple scenes: Few contacts
- WebGL fallback: No compute shaders

## Profiling Tips

### Browser DevTools

1. Chrome: `chrome://gpu` for WebGPU info
2. Performance tab: Frame timing
3. Console: Our timing logs

### GPU Timing (Future)

```javascript
// Requires timestamp-query feature
const querySet = device.createQuerySet({
    type: 'timestamp',
    count: 2
});
```

### Key Metrics to Watch

| Metric | Warning | Critical |
|--------|---------|----------|
| Frame time | >16ms | >33ms |
| Physics step | >4ms | >8ms |
| Contact count | >10000 | >50000 |
| Penetration | >5mm | >20mm |

## Scaling Guidelines

### Object Count vs Grid Size

| Objects | Grid Cell Size | Hash Table Size |
|---------|----------------|-----------------|
| <1000 | 2.0 | 16K cells |
| 1000-5000 | 2.0 | 64K cells |
| 5000-10000 | 2.5 | 64K cells |
| >10000 | 3.0+ | 128K cells |

### Solver Iterations vs Stability

| Scenario | Iterations | Notes |
|----------|------------|-------|
| Free-falling | 4 | Few contacts |
| Light stacking | 6 | 2-3 layers |
| Heavy stacking | 8-10 | 5+ layers |
| High restitution | 4 | Bouncy |
| Friction-heavy | 8+ | Sliding contacts |

## Trade-offs Summary

| Choice | Pros | Cons |
|--------|------|------|
| More iterations | Stable stacking | Slower |
| Larger grid cells | Less memory | More false positives |
| GPU physics | Scales to 10K+ | Dispatch overhead |
| CPU physics | Low overhead | Doesn't scale |
| f16 precision | Half memory | Less precision |
| Warm starting | Faster convergence | Contact caching complexity |
