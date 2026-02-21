using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using BlazorClient;
using BlazorClient.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register physics and rendering services
builder.Services.AddScoped<IRenderingService, RenderingService>();
builder.Services.AddScoped<IRigidPhysicsService, RigidPhysicsService>();
builder.Services.AddScoped<ISoftPhysicsService, SoftPhysicsService>();
builder.Services.AddScoped<IInteropService, InteropService>();
builder.Services.AddScoped<ISceneStateService, SceneStateService>();

await builder.Build().RunAsync();
