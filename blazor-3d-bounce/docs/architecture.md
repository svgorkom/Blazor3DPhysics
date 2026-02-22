# Architecture Overview

This document describes the system architecture of the Blazor 3D Physics application.

## Project Structure

The solution follows Clean Architecture principles with clear separation of concerns:

```
Blazor3DPhysics.sln
├── BlazorClient.Domain/          # Domain Layer (no dependencies)
│   ├── Models/                   # Domain entities and value objects
│   │   ├── PhysicsTypes.cs      # Vector3, Quaternion, Materials, Presets
│   │   └── SceneObjects.cs      # RigidBody, SoftBody, SimulationSettings
│   │                            # RendererBackend, RendererInfo
│   └── Common/                   # Shared domain types
│       └── Result.cs            # Result pattern for functional error handling
│
├── BlazorClient.Application/     # Application Layer (depends on Domain)
│   ├── Commands/                 # CQRS command definitions
│   │   ├── CommandInterfaces.cs # ICommand, ICommandHandler, ICommandDispatcher
│   │   └── CommandDispatcher.cs # Command dispatcher implementation
│   ├── Events/                   # Domain events
│   │   ├── IEventAggregator.cs  # Event aggregator interface
│   │   └── DomainEvents.cs      # Event definitions
│   └── Validation/              # Business rule validation
│       └── IPhysicsValidator.cs # Physics parameter validation interface
│
├── BlazorClient.Infrastructure/  # Infrastructure Layer (depends on Application)
│   ├── Events/                   # Event aggregator implementation
│   │   └── EventAggregator.cs   # Pub/sub event system
│   └── Validation/              # Validation implementations
│       └── PhysicsValidator.cs  # Physics parameter validation
│
└── BlazorClient/                 # UI Layer (Blazor WebAssembly)
    ├── Pages/                    # Razor pages
    │   └── Index.razor          # Main application page
    ├── Components/              # Reusable Blazor components
    │   ├── Viewport.razor       # 3D canvas host
    │   ├── PerformanceOverlay.razor # FPS & renderer backend display
    │   ├── Toolbar.razor        # Control toolbar
    │   ├── Inspector.razor      # Property editor
    │   └── Stats.razor          # Performance statistics
    ├── Services/                # Service implementations (will be moved to Infrastructure)
    │   ├── Commands/            # Command handlers
    │   ├── Interfaces/          # Service interfaces
    │   ├── Factories/           # Factory patterns
    │   └── ...                  # Various services
    ├── Models/                  # Backwards compatibility aliases
    └── wwwroot/                 # Static assets and JavaScript
        └── js/                  # JavaScript interop modules
            ├── rendering.webgpu.js  # WebGPU detection & metrics
            ├── rendering.js         # Babylon.js (WebGPU/WebGL)
            ├── physics.rigid.js     # Rapier rigid bodies
            ├── physics.soft.js      # Ammo soft bodies
            └── interop.js           # Blazor-JS bridge
```

### Dependency Flow

```
BlazorClient (UI)
    ↓
BlazorClient.Infrastructure
    ↓
BlazorClient.Application
    ↓
BlazorClient.Domain (no dependencies)
```

## SOLID Principles Compliance

This codebase follows all five SOLID principles:

| Principle | Implementation |
|-----------|----------------|
| **S**ingle Responsibility | `SimulationLoopService` handles only simulation timing; `Index.razor` handles only UI |
| **O**pen/Closed | Mesh and material creation uses registry pattern - extend without modification |
| **L**iskov Substitution | All physics services implement `IPhysicsService` base interface |
| **I**nterface Segregation | Soft body interfaces split: `IClothPhysicsService`, `IRopePhysicsService`, etc. |
| **D**ependency Inversion | All components depend on abstractions (interfaces), not concrete implementations |

## Rendering Architecture

### WebGPU/WebGL Backend Selection

The rendering system supports multiple backends with automatic selection:

```mermaid
flowchart TB
    Start[Initialize Rendering] --> CheckPref{Preferred Backend?}
    CheckPref -->|Auto| DetectGPU{WebGPU Available?}
    CheckPref -->|WebGPU| TryGPU{Try WebGPU}
    CheckPref -->|WebGL2| UseGL2[Use WebGL2]
    CheckPref -->|WebGL| UseGL1[Use WebGL]
    
    DetectGPU -->|Yes| UseGPU[Use WebGPU]
    DetectGPU -->|No| FallbackGL2[Fallback to WebGL2]
    
    TryGPU -->|Success| UseGPU
    TryGPU -->|Fail| FallbackGL2
    
    FallbackGL2 --> CheckGL2{WebGL2 Available?}
    CheckGL2 -->|Yes| UseGL2
    CheckGL2 -->|No| UseGL1
    
    UseGPU --> Ready[Renderer Ready]
    UseGL2 --> Ready
    UseGL1 --> Ready
```

### Renderer Components

| Component | File | Purpose |
|-----------|------|---------|
| WebGPU Module | `rendering.webgpu.js` | Detection, capabilities, benchmarking |
| Rendering Module | `rendering.js` | Babylon.js scene management |
| Rendering Service | `RenderingService.cs` | C# wrapper for JS interop |
| Performance Overlay | `PerformanceOverlay.razor` | UI for FPS & renderer info |

### Renderer Information Flow

```csharp
// Get renderer info from JavaScript
var rendererInfo = await RenderingService.GetRendererInfoAsync();

// RendererInfo contains:
// - Backend: "WebGPU", "WebGL2", or "WebGL"
// - Vendor: GPU vendor name
// - Renderer: GPU device name
// - IsWebGPU: true if using WebGPU
// - IsFallback: true if fell back from preferred backend
// - FallbackReason: reason for fallback (if any)
```

### Performance Metrics

The `WebGPUModule` tracks detailed performance metrics:

```javascript
// Frame time tracking
WebGPUModule.recordFrameTime(frameTimeMs);

// Get metrics
var metrics = WebGPUModule.getPerformanceMetrics();
// Returns: fps, frameTimeMs, percentile95, percentile99, minFrameTime, maxFrameTime

// Run benchmark comparing backends
var results = await WebGPUModule.runBenchmark(canvasId);
// Returns: webgpu results, webgl2 results, recommendation
```

## High-Level Architecture

```mermaid
flowchart TB
    subgraph UI["UI Layer (BlazorClient)"]
        Pages[Razor Pages]
        Components[Blazor Components]
    end

    subgraph Application["Application Layer (BlazorClient.Application)"]
        Commands[Command Handlers]
        Events[Event Interfaces]
        Validation[Validator Interfaces]
    end

    subgraph Domain["Domain Layer (BlazorClient.Domain)"]
        Models[Domain Models]
        ValueObjects[Value Objects]
        Results[Result Types]
    end

    subgraph Infrastructure["Infrastructure Layer (BlazorClient.Infrastructure)"]
        EventImpl[Event Aggregator]
        ValidatorImpl[Validators]
        Physics[Physics Services]
        Rendering[Rendering Service]
        Interop[JS Interop]
    end

    subgraph External["External (JavaScript)"]
        WebGPU[WebGPU Module]
        Babylon[Babylon.js]
        Rapier[Rapier.js]
        Ammo[Ammo.js]
    end

    UI --> Application
    UI --> Infrastructure
    Application --> Domain
    Infrastructure --> Application
    Infrastructure --> Domain
    Infrastructure --> External
```

## Component Architecture

### Domain Layer (BlazorClient.Domain)

```
BlazorClient.Domain/
├── Models/
│   ├── PhysicsTypes.cs       # Vector3, Quaternion, TransformData
│   │                        # MaterialPreset, PhysicsMaterial
│   │                        # SoftBodyType, SoftBodyMaterial
│   └── SceneObjects.cs      # SceneObject, RigidBody, SoftBody
│                            # SimulationSettings, RenderSettings
│                            # PerformanceStats, ScenePreset
└── Common/
    └── Result.cs            # Result<T>, Result (functional error handling)
```

**Key Characteristics:**
- No external dependencies
- Pure domain logic
- Immutable value objects where appropriate
- Business rules enforced in constructors and methods

### Application Layer (BlazorClient.Application)

```
BlazorClient.Application/
├── Commands/
│   ├── CommandInterfaces.cs     # ICommand, ICommand<TResult>
│   │                            # ICommandHandler<TCommand>
│   │                            # ICommandHandler<TCommand, TResult>
│   │                            # ICommandDispatcher
│   │                            # Command definitions (SpawnRigidBodyCommand, etc.)
│   └── CommandDispatcher.cs     # CommandDispatcher implementation
├── Events/
│   ├── IEventAggregator.cs      # Event aggregator interface
│   └── DomainEvents.cs          # Event, IEventHandler<TEvent>
│                                # ObjectSpawnedEvent, etc.
└── Validation/
    └── IPhysicsValidator.cs     # ValidationResult, IPhysicsValidator
```

**Key Characteristics:**
- Defines application use cases via commands
- Declares domain events
- Validation interfaces
- Depends only on Domain layer

### Infrastructure Layer (BlazorClient.Infrastructure)

```
BlazorClient.Infrastructure/
├── Events/
│   └── EventAggregator.cs       # Pub/sub implementation
└── Validation/
    └── PhysicsValidator.cs      # Physics validation implementation
```

**Key Characteristics:**
- Implements Application interfaces
- Handles external integrations (JS Interop, etc.)
- Service implementations
- Depends on Application and Domain layers

### UI Layer (BlazorClient)

```
BlazorClient/
├── Pages/
│   └── Index.razor              # Main page, UI coordination only (SRP)
├── Components/
│   ├── Viewport.razor           # Canvas host
│   ├── Toolbar.razor            # Spawn controls, playback
│   ├── Inspector.razor          # Property editing
│   ├── Stats.razor              # Performance display
│   └── PerformanceOverlay.razor  # FPS & renderer backend display
├── Services/                    # (Many will move to Infrastructure)
│   ├── Interfaces/
│   │   └── IPhysicsInterfaces.cs # Segregated physics interfaces (ISP)
│   ├── Factories/
│   │   ├── MeshCreatorFactory.cs    # Extensible mesh creation (OCP)
│   │   └── MaterialCreatorFactory.cs # Extensible material creation (OCP)
│   ├── Commands/
│   │   ├── CommandHandlers.cs       # Command handler implementations
│   │   └── LoggingCommandDispatcher.cs # Decorator for logging
│   ├── SimulationLoopService.cs  # Physics loop timing only (SRP)
│   ├── RenderingService.cs       # Babylon.js wrapper
│   ├── PhysicsService.Rigid.cs   # Rapier wrapper (implements IPhysicsService)
│   ├── PhysicsService.Soft.cs    # Ammo wrapper (implements segregated interfaces)
│   ├── InteropService.cs         # Batched JS calls
│   ├── SceneStateService.cs      # Central state
│   ├── SceneSerializationService.cs # Scene import/export
│   ├── PerformanceMonitor.cs     # Performance tracking
│   ├── ObjectPool.cs             # Array/object pooling
│   └── JsModuleCache.cs          # JS module caching
├── Models/                       # Backwards compatibility (global usings)
│   ├── PhysicsTypes.cs          # Aliases to Domain.Models
│   ├── SceneObjects.cs          # Aliases to Domain.Models
│   └── Result.cs                # Alias to Domain.Common
└── wwwroot/
    └── js/
        ├── rendering.webgpu.js  # WebGPU detection & metrics
        ├── rendering.js         # Babylon.js (WebGPU/WebGL)
        ├── physics.rigid.js     # Rapier rigid bodies
        ├── physics.soft.js      # Ammo soft bodies
        └── interop.js           # Blazor-JS bridge
```

## Backwards Compatibility

The original `BlazorClient/Models/` folder files now contain `global using` directives to maintain backwards compatibility:

```csharp
// BlazorClient/Models/PhysicsTypes.cs
global using Vector3 = BlazorClient.Domain.Models.Vector3;
global using Quaternion = BlazorClient.Domain.Models.Quaternion;
// ... etc.
```

This allows existing code to continue working without modification while new code can use the proper namespaces.

## Architectural Patterns

| Pattern | Implementation | Purpose |
|---------|----------------|---------|
| **CQRS-Lite** | `ICommandDispatcher`, Command Handlers | Separates commands from queries |
| **Event Aggregator** | `IEventAggregator` | Decoupled component communication |
| **Result Pattern** | `Result<T>`, `Result` | Functional error handling |
| **Object Pooling** | `ArrayPool<T>`, `ObjectPool<T>` | Reduces allocations in hot paths |
| **Factory Pattern** | `IMeshCreatorFactory`, `IMaterialCreatorFactory` | Extensible object creation |

## Command Pattern (CQRS-Lite)

### Command Flow

```mermaid
sequenceDiagram
    participant UI as Blazor Component
    participant Dispatcher as ICommandDispatcher
    participant Handler as ICommandHandler
    participant Services as Domain Services
    participant Events as IEventAggregator

    UI->>Dispatcher: DispatchAsync(command)
    Dispatcher->>Handler: HandleAsync(command)
    Handler->>Services: Execute business logic
    Handler->>Events: Publish domain event
    Events-->>UI: Event notification
    Handler-->>Dispatcher: Result<T>
    Dispatcher-->>UI: Result<T>
```

### Available Commands

| Command | Handler | Description |
|---------|---------|-------------|
| `SpawnRigidBodyCommand` | `SpawnRigidBodyCommandHandler` | Creates a rigid body |
| `SpawnSoftBodyCommand` | `SpawnSoftBodyCommandHandler` | Creates a soft body |
| `DeleteObjectCommand` | `DeleteObjectCommandHandler` | Deletes an object |
| `ResetSceneCommand` | `ResetSceneCommandHandler` | Clears the scene |
| `SelectObjectCommand` | `SelectObjectCommandHandler` | Selects an object |
| `ApplyImpulseCommand` | `ApplyImpulseCommandHandler` | Applies force to body |
| `UpdateSimulationSettingsCommand` | Handler | Updates physics settings |
| `UpdateRenderSettingsCommand` | Handler | Updates render settings |

## Event Aggregator

### Domain Events

| Event | Published When |
|-------|----------------|
| `ObjectSpawnedEvent` | Object created |
| `ObjectDeletedEvent` | Object deleted |
| `ObjectSelectedEvent` | Selection changed |
| `SimulationPausedEvent` | Play/pause toggled |
| `SimulationSettingsChangedEvent` | Settings updated |
| `RenderSettingsChangedEvent` | Render settings updated |
| `PhysicsSteppedEvent` | After each physics step |
| `SceneResetEvent` | Scene cleared |
| `SceneLoadedEvent` | Scene imported |
| `ErrorOccurredEvent` | Error detected |

### Usage Example

```csharp
// Subscribe to events
_subscription = _events.Subscribe<ObjectSpawnedEvent>(e => 
{
    Console.WriteLine($"Object spawned: {e.Name}");
});

// Publish events
_events.Publish(new ObjectSpawnedEvent(body.Id, body.Name, "Sphere"));

// Unsubscribe
_subscription.Dispose();
```

## Result Pattern

Functional error handling without exceptions:

```csharp
// Command returns Result<T>
var result = await _dispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command);

// Pattern matching
result.Match(
    onSuccess: id => Console.WriteLine($"Created: {id}"),
    onFailure: error => Console.WriteLine($"Failed: {error}")
);

// Chaining
result
    .OnSuccess(id => UpdateUI(id))
    .OnFailure(error => ShowError(error));

// Implicit bool conversion
if (result)
{
    // Success path
}
```

## Performance Optimization

### Object Pooling

```csharp
// Rent array from pool
var array = _transformPool.Rent(1024);
try
{
    // Use array
    ProcessTransforms(array);
}
finally
{
    // Return to pool
    _transformPool.Return(array);
}

// Or use disposable pattern
using var pooled = _transformPool.RentDisposable(1024);
ProcessTransforms(pooled.Array);
```

### Performance Monitor

```csharp
// Record timing manually
_monitor.RecordTiming("Physics", elapsedMs);

// Or use automatic measurement
using (_monitor.MeasureTiming("Render"))
{
    await RenderFrame();
}

// Get snapshot
var stats = _monitor.GetSnapshot();
Console.WriteLine($"FPS: {stats.Fps}, Physics: {stats.PhysicsTimeMs}ms");
```

### JS Module Cache

```csharp
// Get cached module reference
var module = await _moduleCache.GetModuleAsync("./js/physics.rigid.js");
await module.invokeVoidAsync("step", deltaTime);

// Preload modules
await _moduleCache.PreloadModulesAsync(
    "./js/rendering.js",
    "./js/physics.rigid.js",
    "./js/physics.soft.js"
);
```

## Validation

### Physics Parameter Validation

```csharp
var validator = serviceProvider.GetService<IPhysicsValidator>();

var result = validator.ValidateRigidBody(body);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"Error: {error}");
}

foreach (var warning in result.Warnings)
    Console.WriteLine($"Warning: {warning}");
```

### Validated Parameters

- Mass ranges and ratios
- Scale limits
- Restitution bounds
- Damping values
- Velocity limits
- Soft body resolution
- Stiffness vs iterations
- Pressure settings

## Scene Serialization

### Save/Load Operations

```csharp
// Save to local storage
await _serialization.SaveToLocalStorageAsync("MyScene");

// Load from local storage
var result = await _serialization.LoadFromLocalStorageAsync("MyScene");
result.OnSuccess(preset => _sceneState.LoadPreset(preset));

// Export to JSON
var json = await _serialization.ExportToJsonAsync();

// Download as file
await _serialization.DownloadSceneAsync("scene.json");

// List saved scenes
var scenes = await _serialization.GetSavedScenesAsync();
```

## Service Interfaces (SOLID Compliant)

### Base Physics Interface (LSP)

```csharp
public interface IPhysicsService
{
    Task<bool> IsAvailableAsync();
    Task StepAsync(float deltaTime);
    Task UpdateSettingsAsync(SimulationSettings settings);
    Task ResetAsync();
    ValueTask DisposeAsync();
}
```

### Segregated Soft Body Interfaces (ISP)

```csharp
public interface IClothPhysicsService
{
    Task CreateClothAsync(SoftBody body);
}

public interface IRopePhysicsService
{
    Task CreateRopeAsync(SoftBody body);
}

public interface IVolumetricPhysicsService
{
    Task CreateVolumetricAsync(SoftBody body);
}

public interface IVertexPinningService
{
    Task PinVertexAsync(string id, int vertexIndex, Vector3 worldPosition);
    Task UnpinVertexAsync(string id, int vertexIndex);
}
```

## Dependency Injection Setup

```csharp
// Core services
builder.Services.AddScoped<IRenderingService, RenderingService>();
builder.Services.AddScoped<IRigidPhysicsService, RigidPhysicsService>();
builder.Services.AddScoped<ISoftPhysicsService, SoftPhysicsService>();
builder.Services.AddScoped<ISceneStateService, SceneStateService>();

// Event Aggregator (singleton for cross-component events)
builder.Services.AddSingleton<IEventAggregator, EventAggregator>();

// Command Dispatcher
builder.Services.AddScoped<ICommandDispatcher, CommandDispatcher>();

// Command Handlers
builder.Services.AddScoped<ICommandHandler<SpawnRigidBodyCommand, string>, SpawnRigidBodyCommandHandler>();
// ... other handlers

// Performance & Pooling
builder.Services.AddSingleton<IPerformanceMonitor, PerformanceMonitor>();
builder.Services.AddSingleton<ArrayPool<float>>();

// Validation
builder.Services.AddSingleton<IPhysicsValidator, PhysicsValidator>();

// Serialization
builder.Services.AddScoped<ISceneSerializationService, SceneSerializationService>();

// JS Optimization
builder.Services.AddScoped<IJsModuleCache, JsModuleCache>();
```

## Data Flow

### Simulation Loop

```mermaid
sequenceDiagram
    participant Timer as Timer (60 Hz)
    participant Loop as SimulationLoopService
    participant Monitor as PerformanceMonitor
    participant Rigid as IRigidPhysicsService
    participant Soft as ISoftPhysicsService
    participant Events as IEventAggregator

    Timer->>Loop: Tick
    Loop->>Monitor: MeasureTiming("Frame")
    Loop->>Loop: Accumulate deltaTime
    
    loop While accumulator >= fixedDt
        Loop->>Monitor: MeasureTiming("Physics")
        Loop->>Rigid: StepAsync(fixedDt)
        Loop->>Soft: StepAsync(fixedDt)
        Loop->>Loop: accumulator -= fixedDt
    end
    
    Loop->>Monitor: MeasureTiming("Interop")
    Loop->>Rigid: GetTransformBatchAsync()
    Loop->>Soft: GetDeformedVerticesAsync()
    
    Loop->>Events: Publish(PhysicsSteppedEvent)
    Loop->>Loop: OnSimulationStateChanged()
```

## Error Handling

### Result-Based Error Handling

```csharp
var result = await _dispatcher.DispatchAsync<SpawnRigidBodyCommand, string>(command);

if (result.IsFailure)
{
    _events.Publish(new ErrorOccurredEvent(
        "Failed to spawn object",
        result.Error,
        ErrorSeverity.Warning));
}
```

### Graceful Degradation

```csharp
try
{
    await SoftPhysics.InitializeAsync(Settings);
    _softBodyAvailable = await SoftPhysics.IsAvailableAsync();
}
catch (Exception ex)
{
    _events.Publish(new ErrorOccurredEvent(
        "Soft physics unavailable",
        ex.Message,
        ErrorSeverity.Warning));
    _softBodyAvailable = false;
}
```

## Memory Management

- **Object Pooling**: `ArrayPool<float>` for transform arrays
- **Result Pattern**: Stack-allocated structs avoid heap allocation
- **Circular Buffers**: Performance monitor uses fixed-size buffers
- **Dispose Pattern**: All services implement `IAsyncDisposable`
- **Event Cleanup**: Subscriptions are disposable for automatic cleanup
