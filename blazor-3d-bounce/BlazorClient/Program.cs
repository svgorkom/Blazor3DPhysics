using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorClient;
using BlazorClient.Services;
using BlazorClient.Services.Factories;
using BlazorClient.Services.Commands;
using BlazorClient.Application.Commands;
using BlazorClient.Application.Events;
using BlazorClient.Application.Validation;
using BlazorClient.Infrastructure.Events;
using BlazorClient.Infrastructure.Validation;
using BlazorClient.Infrastructure.Services;

// Type aliases to resolve ambiguity
using AppPerformanceMonitorOptions = BlazorClient.Application.Services.PerformanceMonitorOptions;
using AppRateLimiterOptions = BlazorClient.Application.Services.RateLimiterOptions;
using AppGpuPhysicsConfig = BlazorClient.Application.Services.GpuPhysicsConfig;
using IAppPerformanceMonitor = BlazorClient.Application.Services.IPerformanceMonitor;
using IAppRateLimiter = BlazorClient.Application.Services.IRateLimiter;

// Application layer service interfaces
using IRenderingServiceApp = BlazorClient.Application.Services.IRenderingService;
using IRigidPhysicsServiceApp = BlazorClient.Application.Services.IRigidPhysicsService;
using ISoftPhysicsServiceApp = BlazorClient.Application.Services.ISoftPhysicsService;
using ISceneStateServiceApp = BlazorClient.Application.Services.ISceneStateService;
using ISimulationLoopServiceApp = BlazorClient.Application.Services.ISimulationLoopService;
using IInteropServiceApp = BlazorClient.Application.Services.IInteropService;
using IGpuPhysicsServiceApp = BlazorClient.Application.Services.IGpuPhysicsService;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

#region Core Services (DIP - depend on abstractions)

// Rendering service (Babylon.js integration)
builder.Services.AddScoped<IRenderingServiceApp, RenderingService>();

// CPU physics (fallback for when GPU is unavailable)
builder.Services.AddScoped<CpuPhysicsService>();

// CPU-based rigid physics (original implementation)
builder.Services.AddScoped<RigidPhysicsService>();

// GPU physics configuration (Application layer type)
builder.Services.AddSingleton<AppGpuPhysicsConfig>(sp => new AppGpuPhysicsConfig
{
    MaxBodies = 16384,
    MaxContacts = 65536,
    SolverIterations = 8,
    GridCellSize = 2.0f,
    EnableCCD = false,
    EnableWarmStarting = true,
    EnableCpuFallback = true
});

// GPU-accelerated physics with CPU fallback
builder.Services.AddScoped<IGpuPhysicsServiceApp>(sp =>
{
    var jsRuntime = sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>();
    var cpuFallback = sp.GetRequiredService<CpuPhysicsService>();
    var config = sp.GetRequiredService<AppGpuPhysicsConfig>();
    return new GpuPhysicsService(jsRuntime, cpuFallback, config);
});

// Register GPU physics as the primary rigid physics service
builder.Services.AddScoped<IRigidPhysicsServiceApp>(sp => sp.GetRequiredService<IGpuPhysicsServiceApp>());

builder.Services.AddScoped<ISoftPhysicsServiceApp, SoftPhysicsService>();
builder.Services.AddScoped<IInteropServiceApp, InteropService>();
builder.Services.AddScoped<ISceneStateServiceApp, SceneStateService>();

#endregion

#region Simulation Loop (SRP - extracted from Index.razor)

builder.Services.AddScoped<ISimulationLoopServiceApp, SimulationLoopService>();

#endregion

#region Factories (OCP - extensible mesh and material creation)

// Use local factory interfaces from BlazorClient.Services.Factories
builder.Services.AddSingleton<BlazorClient.Services.Factories.IMeshCreatorFactory, MeshCreatorFactory>();
builder.Services.AddSingleton<BlazorClient.Services.Factories.IMaterialCreatorFactory, MaterialCreatorFactory>();

#endregion

#region Segregated Interfaces (ISP - clients depend only on needed interfaces)

// These use Application layer interfaces, resolved from Services layer implementations
builder.Services.AddScoped<BlazorClient.Application.Services.IClothPhysicsService>(sp => 
    sp.GetRequiredService<ISoftPhysicsServiceApp>());
builder.Services.AddScoped<BlazorClient.Application.Services.IVolumetricPhysicsService>(sp => 
    sp.GetRequiredService<ISoftPhysicsServiceApp>());
builder.Services.AddScoped<BlazorClient.Application.Services.IVertexPinningService>(sp => 
    sp.GetRequiredService<ISoftPhysicsServiceApp>());

#endregion

#region Event Aggregator (Decoupled component communication)

builder.Services.AddSingleton<IEventAggregator, EventAggregator>();

#endregion

#region Command Dispatcher (CQRS-lite pattern with logging)

// Register the base dispatcher
builder.Services.AddScoped<CommandDispatcher>();

// Register the logging decorator as the primary ICommandDispatcher
builder.Services.AddScoped<ICommandDispatcher>(sp =>
{
    var baseDispatcher = sp.GetRequiredService<CommandDispatcher>();
    var performanceMonitor = sp.GetRequiredService<IAppPerformanceMonitor>();
    return new LoggingCommandDispatcher(baseDispatcher, performanceMonitor);
});

// Command handlers (from UI Services layer)
builder.Services.AddScoped<ICommandHandler<SpawnRigidBodyCommand, string>, SpawnRigidBodyCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SpawnSoftBodyCommand, string>, SpawnSoftBodyCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteObjectCommand>, DeleteObjectCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ResetSceneCommand>, ResetSceneCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SelectObjectCommand>, SelectObjectCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ApplyImpulseCommand>, ApplyImpulseCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateSimulationSettingsCommand>, UpdateSimulationSettingsCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateRenderSettingsCommand>, UpdateRenderSettingsCommandHandler>();

#endregion

#region Performance & Monitoring (Infrastructure layer implementations)

// Configure performance monitor with options (Application layer types, Infrastructure implementation)
builder.Services.AddSingleton<IAppPerformanceMonitor>(sp =>
{
    var options = new AppPerformanceMonitorOptions
    {
        DetailedProfilingEnabled = false, // Enable in dev if needed
        SampleCount = 60,
        FpsWindowMs = 1000,
        TrackMemory = true,
        TrackGarbageCollection = true,
        LogPerformanceWarnings = true,
        FpsWarningThreshold = 30f,
        FrameTimeWarningThresholdMs = 33f
    };
    return new PerformanceMonitor(options);
});

#endregion

#region Rate Limiting (DoS protection - Infrastructure layer implementation)

builder.Services.AddSingleton<IAppRateLimiter>(sp =>
{
    var options = new AppRateLimiterOptions
    {
        MaxRequests = 100, // 100 requests per window
        Window = TimeSpan.FromMinutes(1),
        MaxConcurrent = 10 // Max 10 concurrent operations
    };
    return new RateLimiter(options);
});

#endregion

#region Object Pools (Performance optimization)

builder.Services.AddSingleton<ArrayPool<float>>(sp => new ArrayPool<float>(1024, 10));
builder.Services.AddSingleton<ArrayPool<byte>>(sp => new ArrayPool<byte>(4096, 5));

#endregion

#region Validation (Physics parameter validation)

builder.Services.AddSingleton<IPhysicsValidator, PhysicsValidator>();

#endregion

#region Serialization (Scene import/export)

builder.Services.AddScoped<BlazorClient.Services.ISceneSerializationService, SceneSerializationService>();

#endregion

#region JS Interop Optimization

builder.Services.AddScoped<BlazorClient.Services.IJsModuleCache, JsModuleCache>();

#endregion

await builder.Build().RunAsync();
