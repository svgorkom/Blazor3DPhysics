# GPU Physics Integration Checklist

## Phase 1: Core Infrastructure ✓

- [x] Create WGSL compute shader modules
  - [x] `physics_common.wgsl` - Shared types and utilities
  - [x] `physics_integrate.wgsl` - Semi-implicit Euler integration
  - [x] `physics_broadphase.wgsl` - Spatial hash grid collision
  - [x] `physics_narrowphase.wgsl` - Exact collision tests
  - [x] `physics_solver.wgsl` - Impulse-based constraint solver

- [x] Create JavaScript GPU physics module
  - [x] WebGPU device initialization
  - [x] Buffer management (bodies, contacts, grid)
  - [x] Shader compilation and pipeline creation
  - [x] Compute dispatch coordination
  - [x] Transform readback for rendering

- [x] Create C# services
  - [x] `GpuPhysicsService` - Main GPU physics service
  - [x] `CpuPhysicsService` - SIMD-optimized CPU fallback
  - [x] `GpuPhysicsConfig` - Configuration options
  - [x] `GpuPhysicsMetrics` - Performance metrics

- [x] Domain model extensions
  - [x] `GpuRigidBody` - GPU buffer layout struct
  - [x] `GpuContact` - Contact buffer struct
  - [x] `GpuSimParams` - Uniform buffer struct
  - [x] `AABB` - Bounding box utilities

## Phase 2: DI Registration ✓

- [x] Register CPU physics as fallback
- [x] Register GPU physics config
- [x] Register GPU physics service with fallback
- [x] Wire as primary `IRigidPhysicsService`

## Phase 3: Testing ✓

- [x] Create physics test suite
  - [x] Single sphere drop test
  - [x] Two sphere head-on collision
  - [x] Stacked boxes stability
  - [x] Fast moving sphere tunneling test
  - [x] Impulse application test
  - [x] Reset state test
  - [x] Sphere-AABB collision test
  - [x] Energy conservation validation
  - [x] Momentum conservation validation

## Phase 4: Documentation ✓

- [x] Physics engine design document
- [x] Math formulas and algorithms
- [x] Integration checklist
- [x] Binding scheme documentation

## Phase 5: Architectural Improvements ✓

- [x] **Consistent Binding Scheme**: All shaders now use a documented, consistent binding layout
- [x] **Dummy Reads for Bindings**: Entry points that don't naturally use `params` now include dummy reads to ensure consistent auto-derived layouts
- [x] **Safe Bind Group Creation**: Helper function with error handling and logging
- [x] **WebGPU Error Handlers**: Device lost and uncaptured error callbacks
- [x] **Compute Pass Labels**: All passes labeled for easier debugging

## Phase 6: Future Enhancements

- [ ] Add warm-starting between frames
- [ ] Implement contact caching
- [ ] Add sleeping islands for performance
- [ ] Implement constraint graph coloring
- [ ] Add CCD swept tests for fast objects
- [ ] Performance profiling and optimization
- [ ] WebGPU timestamp queries for GPU timing
- [ ] Add debug visualization for contacts
- [ ] **Explicit Pipeline Layouts**: Consider replacing `layout: 'auto'` with explicit layouts for production

## Performance Validation Targets

| Metric | Target | Test Method |
|--------|--------|-------------|
| 10,000 spheres | <16ms/frame | Spawn stress test |
| Max penetration | <5mm | Validation metrics |
| Energy drift | <1%/minute | Long-running test |
| Solver convergence | <10 iterations | Contact stress test |

## Binding Scheme Reference

All physics shaders follow a consistent binding scheme to avoid issues with WebGPU's automatic layout derivation.

### Integration Pipeline (Group 0)
| Binding | Resource | Type |
|---------|----------|------|
| 0 | bodies | storage (read_write) |
| 1 | params | uniform |
| 2 | externalForces | storage (read) |

### Broadphase Pipeline (Group 0)
| Binding | Resource | Type |
|---------|----------|------|
| 0 | bodies | storage (read) |
| 1 | params | uniform |
| 2 | cellCounts | storage (read_write) |
| 3 | cellBodies | storage (read_write) |
| 4 | cellOffsets | storage (read_write) |
| 5 | pairs | storage (read_write) |
| 6 | pairCount | storage (read_write) |
| 7 | bodyAABBs | storage (read_write) |

### Narrowphase Pipeline (Group 0)
| Binding | Resource | Type |
|---------|----------|------|
| 0 | bodies | storage (read) |
| 1 | params | uniform |
| 2 | pairs | storage (read) |
| 3 | pairCount | storage (read) |
| 4 | contacts | storage (read_write) |
| 5 | contactCount | storage (read_write) |

### Solver Pipeline (Group 0)
| Binding | Resource | Type |
|---------|----------|------|
| 0 | bodies | storage (read_write) |
| 1 | params | uniform |
| 2 | contacts | storage (read_write) |
| 3 | contactCount | storage (read) |

**Important**: All entry points must access `params` (binding 1) to ensure consistent auto-derived layouts. If an entry point doesn't naturally use `params`, add a dummy read:

```wgsl
// Ensure binding 1 is in the layout
let _ = params.deltaTime;
```

## Quick Start

### Enable GPU Physics

GPU physics is enabled by default when WebGPU is available. To explicitly configure:

```csharp
// In Program.cs
builder.Services.AddSingleton<GpuPhysicsConfig>(sp => new GpuPhysicsConfig
{
    MaxBodies = 16384,
    SolverIterations = 8,
    GridCellSize = 2.0f,
    EnableCpuFallback = true  // Falls back to SIMD CPU physics
});
```

### Check GPU Status

```csharp
@inject IGpuPhysicsService GpuPhysics

@if (await GpuPhysics.IsGpuAvailableAsync())
{
    <span>GPU Physics Active</span>
}
else
{
    <span>CPU Fallback Active</span>
}
```

### Get Performance Metrics

```csharp
var metrics = await GpuPhysics.GetMetricsAsync();
Console.WriteLine($"Physics: {metrics.TotalStepTimeMs}ms");
Console.WriteLine($"Contacts: {metrics.ContactCount}");
Console.WriteLine($"GPU Active: {metrics.IsGpuActive}");
```

## Troubleshooting

### GPU Physics Not Available

1. Check browser WebGPU support: `navigator.gpu` must exist
2. Check for WebGPU adapter: High-performance adapter required
3. Check console for shader compilation errors
4. Ensure WGSL files are in `wwwroot/shaders/`

### Bind Group Errors

If you see errors like "binding index X not present in the bind group layout":

1. Check that the shader entry point actually accesses all required bindings
2. Add dummy reads for any bindings that might be optimized out
3. Verify the JavaScript bind group entries match the shader declarations
4. Use the `createBindGroupSafe` helper for better error messages

### Performance Issues

1. Reduce `SolverIterations` (default 8, minimum 4)
2. Increase `GridCellSize` for sparse scenes
3. Enable sleeping for stable stacks
4. Consider CPU fallback for <100 bodies

### Instability/Jitter

1. Increase `SolverIterations`
2. Reduce simulation `TimeStep`
3. Increase `SubSteps` in SimulationSettings
4. Check for extremely different mass ratios
