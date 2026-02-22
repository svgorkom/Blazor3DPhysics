# User Guide

This guide explains how to use the Blazor 3D Physics application.

## Getting Started

### Launching the Application

1. Run the application with `dotnet run --project BlazorClient/BlazorClient.csproj`
2. Open your browser to `https://localhost:5001`
3. Wait for the physics engines to initialize (loading indicator)

### Interface Overview

```
┌────────────────────────────────────────────────────────────────────────────┐
│  🎮 Blazor 3D Physics  │ ▶️ ⏸️ 🔄 │ Spawn │ Presets │     │ ← Toolbar      │
├────────────────────────────────────────────────────────────────────────────┤
│             │                                   │                          │
│  Spawn      │                                   │   Inspector              │
│  Panel      │         3D Viewport               │    Panel                 │
│             │                                   │                          │
│  Object     │                                   │  Properties              │
│  List       │                                   │   Settings               │
│             │                                   │                          │
├────────────────────────────────────────────────────────────────────────────┤
│  FPS: 60 │ Physics: 2.1ms │ Rigid: 5 │ Soft: 2             │ ← Stats       │
└────────────────────────────────────────────────────────────────────────────┘
```

## Spawning Objects

### Rigid Bodies

Click the spawn buttons in the toolbar or sidebar:

| Button | Shape | Description |
|--------|-------|-------------|
| ⚫ Sphere | Ball | Best for bouncing demos |
| 🔲 Box | Cube | Good for stacking |
| 💊 Capsule | Pill shape | Character-like |
| 🛢️ Cylinder | Barrel | Rolls naturally |

Objects spawn at a random position above the ground plane.

### Soft Bodies

| Button | Type | Description |
|--------|------|-------------|
| 🧵 Cloth | Fabric | Falls and drapes |
| 🪢 Rope | Chain | Hangs from pinned top |
| 🫧 Jelly | Volume | Bouncy, squishy ball |

**Note**: Soft body buttons are disabled if Ammo.js fails to load.

## Camera Controls

### Mouse

| Action | Control |
|--------|---------|
| Rotate | Left-click + drag |
| Pan | Right-click + drag |
| Zoom | Scroll wheel |
| Select | Click on object |

### Keyboard

| Key | Action |
|-----|--------|
| Space | Play/Pause simulation |
| R | Reset scene |
| S | Step (when paused) |
| Delete | Remove selected object |

## Simulation Controls

### Play/Pause

- Click **▶️** to start simulation
- Click **⏸️** to pause
- When paused, objects freeze in place

### Reset

- Click **🔄** to clear all objects and restart
- Returns to initial state

### Step

- Available only when paused
- Advances simulation by one frame
- Useful for debugging

### Time Scale

Adjust in the Inspector panel:
- **0.25x**: Slow motion
- **1.0x**: Real time
- **2.0x**: Fast forward

## Editing Properties

### Selecting Objects

1. Click on object in viewport, OR
2. Click on object in the Object List

Selected object shows teal highlight.

### Rigid Body Properties

| Property | Range | Effect |
|----------|-------|--------|
| Mass | 0.1-100 kg | Heavier = harder to move |
| Restitution | 0-1 | Higher = bouncier |
| Static Friction | 0-1 | Resistance to starting motion |
| Dynamic Friction | 0-1 | Resistance during motion |
| Linear Damping | 0-1 | Slows linear velocity |
| Angular Damping | 0-1 | Slows rotation |
| Enable CCD | Toggle | Prevents tunneling |
| Static | Toggle | Makes immovable |

### Soft Body Properties

| Property | Range | Effect |
|----------|-------|--------|
| Structural Stiffness | 0-1 | Edge length preservation |
| Shear Stiffness | 0-1 | Diagonal resistance (cloth) |
| Bending Stiffness | 0-1 | Fold resistance |
| Damping | 0-1 | Energy dissipation |
| Pressure | 0-200 kPa | Internal pressure (jelly) |
| Volume Conservation | 0-1 | Preserve volume (jelly) |
| Self Collision | Toggle | Collide with self |
| Iterations | 1-30 | Solver accuracy |

### Global Settings

| Setting | Effect |
|---------|--------|
| Gravity Y | Downward acceleration |
| Time Scale | Simulation speed |
| Sub-steps | Physics accuracy |

### Render Settings

| Setting | Effect |
|---------|--------|
| Shadows | Toggle shadow rendering |
| Grid | Show/hide ground grid |
| Axes | Show/hide axis gizmo |
| Wireframe | Toggle wireframe mode |
| FXAA | Anti-aliasing |

## Using Presets

### Loading Presets

1. Click the Presets dropdown in toolbar
2. Select a preset:
   - **Bounce Regression**: Bouncing sphere test
   - **Cloth Over Sphere**: Draping demo
   - **Rope Pendulum**: Hanging rope
   - **Jelly Drop**: Soft volume test

### Material Presets

In the Inspector, select material presets for rigid bodies:
- **Rubber**: High bounce, high grip
- **Wood**: Medium properties
- **Steel**: Hard, slippery when wet
- **Ice**: Very slippery

## Importing Models

### Drag and Drop

1. Drag a `.gltf` or `.glb` file onto the viewport
2. Model loads as a static object
3. Edit properties in Inspector

### Supported Formats

- GLTF 2.0 (`.gltf` + `.bin`)
- GLB (binary GLTF)
- Draco-compressed meshes

## Performance Monitoring

### Stats Bar

| Metric | Good | Warning | Bad |
|--------|------|---------|-----|
| FPS | 55+ | 30-55 | <30 |
| Physics | <4ms | 4-8ms | >8ms |

### If Performance Drops

1. Reduce object count
2. Disable shadows
3. Lower soft body resolution
4. Reduce substeps
5. Disable self-collision

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| Space | Play/Pause |
| R | Reset |
| S | Step (paused) |
| Delete | Remove selected |
| G | Toggle grid |
| Escape | Deselect |

## Tips & Tricks

### Creating Stable Stacks

1. Use low restitution (< 0.3)
2. Add damping (0.1-0.3)
3. Avoid perfectly aligned edges
4. Let sleeping system work

### Making Bouncy Balls

1. Use Rubber preset
2. Set restitution to 0.8-0.95
3. Enable CCD for fast motion
4. Use sphere shape

### Natural Cloth Behavior

1. Pin only necessary vertices
2. Use moderate stiffness (~0.7-0.9)
3. Enable self-collision for folding
4. Start with iterations = 10

### Rope Physics

1. Always pin at least one end
2. More segments = smoother curve
3. Lower bending stiffness for flexibility
4. Increase iterations for stability

### Jelly/Volume Effects

1. Set pressure based on desired firmness
2. Enable volume conservation
3. Use moderate stiffness (0.3-0.6)
4. Higher iterations prevent collapse

## Troubleshooting

### Objects Fall Through Ground

- Check if ground exists
- Enable CCD for fast objects
- Verify scale is reasonable

### Soft Bodies Explode

- Lower stiffness
- Increase iterations
- Add damping
- Check for overlapping objects

### Poor Performance

- See Performance Monitoring section
- Reduce active object count
- Lower quality settings

### Soft Body Unavailable

- Check browser console for errors
- Ammo.js WASM may have failed to load
- Try refreshing the page
- Use a different browser
