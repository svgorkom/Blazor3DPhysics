# Soft Body Physics

This document details the soft body physics implementation using Ammo.js (Bullet Physics).

## Physics Engine: Ammo.js

[Ammo.js](https://github.com/kripken/ammo.js/) is a direct port of Bullet Physics to JavaScript via Emscripten. It provides comprehensive soft body support:
- Mass-spring systems
- Finite Element Method (FEM) volumes
- Pressure-based volume preservation
- Two-way rigid-soft coupling

## Soft Body Types

### Cloth

2D surface composed of connected vertices in a grid:

```
o---o---o---o---o
|   |   |   |   |
o---o---o---o---o
|   |   |   |   |
o---o---o---o---o
|   |   |   |   |
o---o---o---o---o
|   |   |   |   |
o---o---o---o---o
```

**Use cases**: Flags, curtains, tablecloths, fabric

### Rope

1D chain of connected vertices:

```
o---o---o---o---o---o---o---o---o---o
                                    |
                                    v (gravity)
```

**Use cases**: Ropes, cables, chains, hair strands

### Volumetric

3D deformable body with internal pressure:

```
    .---.
   /     \
  |  ~~~  |
  | ~~~~~ |
   \     /
    '---'
```

**Use cases**: Jelly, balloons, organs, bouncing balls

## Constraint System

### Structural Constraints

Maintain edge lengths (structural integrity):

```
o---------o
   L?
```

Force: `F = k × (|L| - L?) × direction`

### Shear Constraints

Resist diagonal deformation (prevents skewing):

```
o-----o
|\   /|
| \ / |
| / \ |
|/   \|
o-----o
```

### Bending Constraints

Resist folding (maintains surface smoothness):

```
    o         o
   /|\   ?   /|\
  / | \     / | \
 o  |  o   o--+--o
    |         | resists
    v         v
```

### Volume Constraints (Volumetric Only)

Preserve internal volume through pressure:

```
Pressure = k × (V? - V_current) / V?
```

## Material Parameters

### Stiffness Values (0-1)

| Parameter | Description | Low (0.2) | High (0.9) |
|-----------|-------------|-----------|------------|
| Structural | Edge length preservation | Stretchy | Rigid edges |
| Shear | Diagonal resistance | Flows easily | Resists skew |
| Bending | Fold resistance | Floppy | Stiff folds |

### Damping (0-1)

Velocity reduction factor:
- **0.0**: No energy loss (oscillates forever)
- **0.05**: Light damping (natural movement)
- **0.2**: Noticeable settling
- **0.5+**: Heavy damping (slow motion feel)

### Pressure (Volumetric)

Internal pressure in kPa:
- **0**: No pressure (deflates)
- **20-50**: Soft, squishy
- **50-100**: Normal balloon/jelly
- **100+**: Firm, resists compression

### Constraint Iterations

Solver iterations per step:
- **5**: Fast, less accurate
- **10**: Good balance
- **15-20**: High accuracy, slower
- **30+**: Diminishing returns

## Creating Soft Bodies

### Cloth Creation

```csharp
var cloth = new SoftBody(SoftBodyType.Cloth)
{
    Width = 3.0f,
    Height = 3.0f,
    ResolutionX = 25,  // Vertices along X
    ResolutionY = 25,  // Vertices along Y
    Material = new SoftBodyMaterial
    {
        StructuralStiffness = 0.9f,
        ShearStiffness = 0.8f,
        BendingStiffness = 0.2f,
        Damping = 0.05f,
        SelfCollision = true,
        ConstraintIterations = 10
    }
};
```

### Rope Creation

```csharp
var rope = new SoftBody(SoftBodyType.Rope)
{
    Length = 5.0f,
    Segments = 30,
    Material = new SoftBodyMaterial
    {
        StructuralStiffness = 0.95f,
        BendingStiffness = 0.1f,
        Damping = 0.1f,
        ConstraintIterations = 15
    }
};
rope.PinnedVertices.Add(0); // Pin top
```

### Volumetric Creation

```csharp
var jelly = new SoftBody(SoftBodyType.Volumetric)
{
    Radius = 1.0f,  // Or Width/Height/Depth
    Material = new SoftBodyMaterial
    {
        StructuralStiffness = 0.5f,
        Damping = 0.1f,
        Pressure = 50f,
        VolumeConservation = 0.95f,
        ConstraintIterations = 12
    }
};
```

## Pin Constraints

### Pinning Vertices

Pin vertices to fixed world positions:

```csharp
// Pin by setting mass to 0
await SoftPhysics.PinVertexAsync(bodyId, vertexIndex, worldPosition);
```

### Common Pin Patterns

**Cloth Flag (left edge)**:
```csharp
for (int y = 0; y <= resY; y++)
{
    body.PinnedVertices.Add(y * (resX + 1));
}
```

**Cloth Hammock (corners)**:
```csharp
body.PinnedVertices.AddRange(new[] { 0, resX, (resY * (resX+1)), ((resY+1) * (resX+1) - 1) });
```

**Rope Top**:
```csharp
body.PinnedVertices.Add(0);
```

### Unpinning

```csharp
await SoftPhysics.UnpinVertexAsync(bodyId, vertexIndex);
```

## Collision Handling

### Soft-Rigid Collision

Soft bodies collide with rigid bodies automatically:
- Two-way interaction (rigid affects soft, soft affects rigid)
- Requires collision margin tuning

### Soft-Soft Collision

Between different soft bodies:
```csharp
// Enabled by default via collision flags
sbConfig.set_collisions(CL_SS);  // Soft-Soft
```

### Self-Collision

Within the same soft body:
```csharp
body.Material.SelfCollision = true;
```

**When to enable**:
- Cloth that folds on itself
- Rope that can tangle
- Volumetric bodies that deform significantly

**Performance cost**: ~2-5x depending on resolution

### Collision Margin

Buffer zone for collision detection:
```csharp
body.Material.CollisionMargin = 0.02f;  // 2cm
```

- Too small: Penetration issues
- Too large: Visible "hovering"
- Typical: 1-5% of object size

## Stability Guidelines

### Resolution vs. Iterations

| Resolution | Min Iterations | Recommended |
|------------|----------------|-------------|
| Low (10×10) | 5 | 8-10 |
| Medium (25×25) | 8 | 10-15 |
| High (50×50) | 10 | 15-20 |

### Stiffness vs. Iterations

Higher stiffness requires more iterations:

```
stiffness = 0.5 ? iterations = 8-10
stiffness = 0.9 ? iterations = 15-20
stiffness = 0.99 ? iterations = 25+ (not recommended)
```

### Avoiding Explosions

**Symptoms**: Vertices fly to infinity

**Causes & Solutions**:

1. **High stiffness + low iterations**
   - Lower stiffness or increase iterations

2. **Extreme deformation**
   - Cap maximum velocity
   - Increase damping temporarily

3. **Overlapping geometry**
   - Ensure proper initialization
   - Use collision margins

4. **Large timesteps**
   - Use substeps
   - Reduce timestep

### Tunneling Prevention

Soft bodies can pass through thin surfaces:

**Solutions**:
1. Use collision margins
2. Increase thickness
3. Make rigid colliders thicker
4. Use more substeps

## Importing from GLTF

### Cloth from Mesh

```javascript
// Look for soft body marker in GLTF extras
if (node.extras?.softBody === 'cloth') {
    const vertices = mesh.geometry.attributes.position.array;
    const indices = mesh.geometry.index.array;
    
    createSoftBodyFromMesh(vertices, indices, {
        type: 'cloth',
        ...node.extras.softBodyParams
    });
}
```

### GLTF Extras Convention

```json
{
  "extras": {
    "softBody": "cloth",
    "softBodyParams": {
      "structuralStiffness": 0.9,
      "shearStiffness": 0.8,
      "bendingStiffness": 0.2,
      "damping": 0.05,
      "pinnedVertices": [0, 10, 20]
    }
  }
}
```

## Rendering Updates

### Vertex Buffer Updates

Each frame, deformed vertices are transferred:

```javascript
function updateSoftMeshVertices(id, vertices, normals) {
    mesh.updateVerticesData(PositionKind, vertices);
    
    if (normals) {
        mesh.updateVerticesData(NormalKind, normals);
    } else {
        // Recompute normals
        const computedNormals = [];
        ComputeNormals(vertices, indices, computedNormals);
        mesh.updateVerticesData(NormalKind, computedNormals);
    }
}
```

### Normal Recomputation

Normals should be recomputed for proper lighting:
- Per-frame for dynamic shapes
- Can skip for static soft bodies
- Consider vertex normals from physics engine

## Performance Optimization

### Resolution Trade-offs

| Metric | Low Res (10×10) | High Res (50×50) |
|--------|-----------------|------------------|
| Vertices | 100 | 2500 |
| Constraints | ~400 | ~10000 |
| Update time | 0.1ms | 5-10ms |
| Visual quality | Blocky | Smooth |

### Recommended Resolutions

- **Distant objects**: 10-15
- **Close-up cloth**: 25-35
- **Maximum practical**: 50 (single cloth)

### Self-Collision Optimization

- Use conservative margin (larger = faster broadphase)
- Disable for non-folding cloth
- Consider collision groups

### Level of Detail (LOD)

For many soft bodies:
```javascript
if (distanceToCamera > farThreshold) {
    // Skip soft body updates, use last frame
    return;
}
if (distanceToCamera > mediumThreshold) {
    // Reduce iterations
    iterations = Math.max(5, baseIterations / 2);
}
```

## Common Issues

### Cloth Stretches Too Much

- Increase structural stiffness
- Increase iterations
- Reduce mass

### Rope Doesn't Hang Naturally

- Check pin constraint
- Verify gravity direction
- Adjust bending stiffness

### Jelly Collapses

- Increase pressure
- Enable volume conservation
- Check for collision issues

### Performance Drops with Self-Collision

- Reduce resolution
- Increase collision margin
- Consider disabling for distant objects
