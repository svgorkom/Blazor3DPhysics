# Blazor 3D Physics - Rigid & Soft Body Simulation

![.NET 8](https://img.shields.io/badge/.NET-8.0-blue)
![Blazor WASM](https://img.shields.io/badge/Blazor-WebAssembly-purple)
![Babylon.js](https://img.shields.io/badge/Babylon.js-Latest-orange)
![Rapier](https://img.shields.io/badge/Rapier.js-0.12-green)
![Ammo.js](https://img.shields.io/badge/Ammo.js-SoftBody-yellow)

A production-quality .NET 8 Blazor WebAssembly application featuring realistic 3D physics simulation with both **rigid bodies** (bouncing meshes with restitution/friction) and **soft bodies** (cloth, rope, and volumetric "jelly" objects).

## ✨ Features

### Rigid Body Physics (Rapier.js)
- 🏀 Realistic bouncing with configurable restitution (bounciness)
- 🧲 Static and dynamic friction simulation
- 💨 Linear and angular damping
- ⚡ Continuous Collision Detection (CCD) for fast-moving objects
- 😴 Automatic sleeping for performance optimization
- 🔷 Multiple primitive shapes: Sphere, Box, Capsule, Cylinder, Cone

### Soft Body Physics (Ammo.js)
- 🧵 **Cloth**: Draped fabrics with structural, shear, and bending constraints
- 🪢 **Rope**: Flexible chains with pin constraints
- 🫧 **Volumetric**: Jelly-like deformable objects with pressure/volume preservation
- 📌 Vertex pinning for anchoring soft bodies
- 💥 Self-collision support
- ⚙️ Configurable stiffness, damping, and iterations

### Rendering (Babylon.js)
- 🎨 PBR materials with metallic/roughness workflow
- 🌅 HDR environment lighting and IBL
- 🔦 Real-time shadow mapping
- 📐 Grid and axis helpers
- 🔍 Object selection highlighting
- 🕸️ Wireframe visualization mode

### User Interface
- 🛠️ Intuitive toolbar with spawn controls
- 📋 Inspector panel for object properties
- 📊 Real-time performance statistics (FPS, physics time)
- 🌙 Dark theme with responsive design
- ⌨️ Keyboard accessible

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Modern web browser (Chrome, Edge, Firefox) with WebGL2 support

### Run the Application

```bash
# Clone or navigate to the project directory
cd blazor-3d-bounce

# Restore dependencies
dotnet restore BlazorClient/BlazorClient.csproj

# Run the application
dotnet run --project BlazorClient/BlazorClient.csproj
```

Open your browser and navigate to `https://localhost:5001` or `http://localhost:5000`.

### Alternative: Using dotnet watch

```bash
dotnet watch run --project BlazorClient/BlazorClient.csproj
```

## 🎮 Controls

### Mouse
- **Left Click + Drag**: Rotate camera
- **Right Click + Drag**: Pan camera
- **Scroll Wheel**: Zoom in/out
- **Click on Object**: Select object

### Keyboard
- **Space**: Play/Pause simulation
- **R**: Reset scene
- **S**: Step simulation (when paused)
- **G**: Toggle grid
- **Delete**: Remove selected object

## 📁 Project Structure

The solution follows **Clean Architecture** principles with clear separation of concerns:

```
blazor-3d-bounce/
├── Blazor3DPhysics.sln          # Solution file
├── BlazorClient.Domain/         # Domain Layer (no dependencies)
│   ├── Models/                  # Domain entities
│   │   ├── PhysicsTypes.cs     # Vector3, materials, presets
│   │   └── SceneObjects.cs     # RigidBody, SoftBody, settings
│   └── Common/
│       └── Result.cs           # Functional error handling
├── BlazorClient.Application/    # Application Layer
│   ├── Commands/               # CQRS commands and handlers
│   ├── Events/                 # Domain events
│   └── Validation/             # Business rules
├── BlazorClient.Infrastructure/ # Infrastructure Layer
│   ├── Events/                 # Event aggregator implementation
│   └── Validation/             # Validator implementations
├── BlazorClient/               # UI Layer (Blazor WebAssembly)
│   ├── Program.cs              # Entry point & DI setup
│   ├── wwwroot/
│   │   ├── index.html          # HTML host
│   │   ├── css/site.css        # Styles
│   │   ├── js/
│   │   │   ├── rendering.js    # Babylon.js rendering
│   │   │   ├── physics.rigid.js # Rapier.js rigid bodies
│   │   │   ├── physics.soft.js  # Ammo.js soft bodies
│   │   │   └── interop.js      # Blazor-JS bridge
│   │   └── assets/             # HDRI, models
│   ├── Components/
│   │   ├── Viewport.razor      # 3D canvas
│   │   ├── Inspector.razor     # Property editor
│   │   ├── Toolbar.razor       # Top toolbar
│   │   └── Stats.razor         # Performance display
│   ├── Services/               # Service implementations
│   │   ├── Commands/           # Command handlers
│   │   ├── Interfaces/         # Service interfaces
│   │   ├── Factories/          # Factory patterns
│   │   ├── RenderingService.cs # Babylon.js wrapper
│   │   ├── PhysicsService.Rigid.cs # Rapier wrapper
│   │   ├── PhysicsService.Soft.cs  # Ammo wrapper
│   │   └── ...
│   └── Models/                 # Backwards compatibility aliases
├── docs/
│   ├── architecture.md         # System design (includes Clean Architecture details)
│   ├── physics.md             # Rigid body physics
│   ├── softbody.md            # Soft body physics
│   ├── usage.md               # User guide
│   ├── perf-tuning.md         # Performance tips
│   └── roadmap.md             # Future plans
├── samples/
│   ├── presets.json           # Material presets
│   └── scenes/
│       ├── bounce-regression.json
│       ├── cloth-over-sphere.json
│       ├── rope-pendulum.json
│       └── jelly-drop.json
└── README.md
```

### Architecture Layers

The solution is organized into four distinct projects following Clean Architecture:

1. **Domain** (`BlazorClient.Domain`): Core business logic, entities, value objects (no dependencies)
2. **Application** (`BlazorClient.Application`): Use cases, commands, events, validation interfaces
3. **Infrastructure** (`BlazorClient.Infrastructure`): External integrations, service implementations
4. **UI** (`BlazorClient`): Blazor WebAssembly components and pages

See [architecture.md](docs/architecture.md) for detailed information about the layered architecture.

## 🎬 Sample Scenes

### 1. Bounce Regression Test
Tests restitution with geometrically decaying bounces. A sphere with e=0.8 should reach ~64% of previous height each bounce.

### 2. Cloth Over Sphere
Cloth soft body draping over a static sphere, demonstrating natural fold formation.

### 3. Rope Pendulum
Rope with pinned top vertex swinging as a pendulum. Period ≈ 2π√(L/g).

### 4. Jelly Drop
Volumetric soft body dropped on ground, showing pressure-based volume preservation.

## ⚙️ Configuration

### Physics Settings
| Setting | Default | Description |
|---------|---------|-------------|
| Gravity Y | -9.81 | Gravitational acceleration (m/s²) |
| Time Step | 1/120 | Fixed physics timestep (s) |
| Sub-steps | 3 | Physics iterations per step |
| Time Scale | 1.0 | Simulation speed multiplier |

### Material Presets
| Material | Restitution | Static Friction | Dynamic Friction |
|----------|-------------|-----------------|------------------|
| Rubber | 0.8 | 0.9 | 0.8 |
| Wood | 0.4 | 0.5 | 0.4 |
| Steel | 0.6 | 0.6 | 0.4 |
| Ice | 0.3 | 0.1 | 0.03 |

## 📐 Physics Models

### Rigid Body Restitution
Post-impact normal velocity: `v_post = -e · v_pre`

Energy retained per bounce: `E_n / E_0 ≈ e²`

### Soft Body Constraints
- **Structural**: Maintains edge lengths
- **Shear**: Resists diagonal deformation
- **Bending**: Resists folding
- **Volume**: Preserves internal pressure (volumetric bodies)

## 🎯 Performance Targets

- **60 FPS** @ 1080p on mid-tier hardware
- ~200 dynamic rigid bodies
- ~2500 soft body vertices (single cloth)
- Batched interop for minimal per-frame overhead

## 🛠️ Troubleshooting

### WebGL Not Available
Ensure your browser supports WebGL2. Check at [get.webgl.org](https://get.webgl.org/).

### Soft Body Physics Unavailable
Ammo.js WASM may fail to load on some systems. The app will fall back to rigid-only mode with a warning indicator.

### Poor Performance
- Reduce shadow map size
- Disable SSAO
- Lower soft body resolution
- Reduce substep count

## 📚 Documentation

- [Architecture Overview](docs/architecture.md)
- [Rigid Body Physics](docs/physics.md)
- [Soft Body Physics](docs/softbody.md)
- [User Guide](docs/usage.md)
- [Performance Tuning](docs/perf-tuning.md)
- [Roadmap](docs/roadmap.md)

## ✅ Verification Checklist

### Rigid Body Tests
- [ ] Sphere with e=0.8 shows geometrically decaying bounces
- [ ] Fast projectile with CCD enabled doesn't tunnel through ground
- [ ] Box stacks are stable without excessive jitter
- [ ] Sleeping bodies wake on collision

### Soft Body Tests
- [ ] Cloth drapes smoothly over sphere
- [ ] Rope pendulum swings with expected period
- [ ] Jelly compresses and preserves volume
- [ ] Pinned vertices remain fixed

### Performance
- [ ] 60 FPS with 10 rigid bodies
- [ ] Responsive UI during simulation
- [ ] No visible memory leaks over time

## 📄 License

MIT License - See LICENSE file for details.

## 🙏 Acknowledgments

- [Babylon.js](https://www.babylonjs.com/) - 3D rendering engine
- [Rapier](https://rapier.rs/) - Rust/WASM rigid body physics
- [Ammo.js](https://github.com/kripken/ammo.js/) - Bullet physics for JavaScript
- [.NET Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - WebAssembly framework
