using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorClient;
using BlazorClient.Services;
using BlazorClient.Services.Factories;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register physics and rendering services (DIP - depend on abstractions)
builder.Services.AddScoped<IRenderingService, RenderingService>();
builder.Services.AddScoped<IRigidPhysicsService, RigidPhysicsService>();
builder.Services.AddScoped<ISoftPhysicsService, SoftPhysicsService>();
builder.Services.AddScoped<IInteropService, InteropService>();
builder.Services.AddScoped<ISceneStateService, SceneStateService>();

// Register simulation loop service (SRP - extracted from Index.razor)
builder.Services.AddScoped<ISimulationLoopService, SimulationLoopService>();

// Register factories (OCP - extensible mesh and material creation)
builder.Services.AddSingleton<IMeshCreatorFactory, MeshCreatorFactory>();
builder.Services.AddSingleton<IMaterialCreatorFactory, MaterialCreatorFactory>();

// Register segregated interfaces pointing to the same soft physics implementation (ISP)
// These allow clients to depend only on the interfaces they need
builder.Services.AddScoped<IClothPhysicsService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<IRopePhysicsService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<IVolumetricPhysicsService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<IVertexPinningService>(sp => sp.GetRequiredService<ISoftPhysicsService>());
builder.Services.AddScoped<ISoftBodyVertexDataService>(sp => sp.GetRequiredService<ISoftPhysicsService>());

await builder.Build().RunAsync();
