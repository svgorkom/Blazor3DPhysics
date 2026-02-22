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

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

#region Core Services (DIP - depend on abstractions)

builder.Services.AddScoped<IRenderingService, RenderingService>();
builder.Services.AddScoped<IRigidPhysicsService, RigidPhysicsService>();
builder.Services.AddScoped<ISoftPhysicsService, SoftPhysicsService>();
builder.Services.AddScoped<IInteropService, InteropService>();
builder.Services.AddScoped<ISceneStateService, SceneStateService>();

#endregion

#region Simulation Loop (SRP - extracted from Index.razor)

builder.Services.AddScoped<ISimulationLoopService, SimulationLoopService>();

#endregion

#region Factories (OCP - extensible mesh and material creation)

builder.Services.AddSingleton<IMeshCreatorFactory, MeshCreatorFactory>();
builder.Services.AddSingleton<IMaterialCreatorFactory, MaterialCreatorFactory>();

#endregion

#region Segregated Interfaces (ISP - clients depend only on needed interfaces)

builder.Services.AddScoped<IClothPhysicsService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<IRopePhysicsService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<IVolumetricPhysicsService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<IVertexPinningService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<ISoftBodyVertexDataService>(sp => sp.GetRequiredService<ISoftPhysicsService>());

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
    var performanceMonitor = sp.GetRequiredService<IPerformanceMonitor>();
    return new LoggingCommandDispatcher(baseDispatcher, performanceMonitor);
});

// Command handlers
builder.Services.AddScoped<ICommandHandler<SpawnRigidBodyCommand, string>, SpawnRigidBodyCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SpawnSoftBodyCommand, string>, SpawnSoftBodyCommandHandler>();
builder.Services.AddScoped<ICommandHandler<DeleteObjectCommand>, DeleteObjectCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ResetSceneCommand>, ResetSceneCommandHandler>();
builder.Services.AddScoped<ICommandHandler<SelectObjectCommand>, SelectObjectCommandHandler>();
builder.Services.AddScoped<ICommandHandler<ApplyImpulseCommand>, ApplyImpulseCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateSimulationSettingsCommand>, UpdateSimulationSettingsCommandHandler>();
builder.Services.AddScoped<ICommandHandler<UpdateRenderSettingsCommand>, UpdateRenderSettingsCommandHandler>();

#endregion

#region Performance & Monitoring

// Configure performance monitor with options
builder.Services.AddSingleton<IPerformanceMonitor>(sp =>
{
    var options = new PerformanceMonitorOptions
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

#region Rate Limiting (DoS protection)

builder.Services.AddSingleton<IRateLimiter>(sp =>
{
    var options = new RateLimiterOptions
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

builder.Services.AddScoped<ISceneSerializationService, SceneSerializationService>();

#endregion

#region JS Interop Optimization

builder.Services.AddScoped<IJsModuleCache, JsModuleCache>();

#endregion

await builder.Build().RunAsync();
