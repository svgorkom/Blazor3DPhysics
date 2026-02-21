# Developer Quick Reference Guide

Quick reference for using the new features added in the improvement phase.

---

## ??? Rate Limiting

### Check if an operation is allowed:
```csharp
if (!_rateLimiter.TryAcquire("my_operation"))
{
    // Rate limit exceeded
    var remaining = _rateLimiter.GetRemainingQuota("my_operation");
    Console.WriteLine($"Please wait. Remaining: {remaining}");
}
```

### Configuration (in Program.cs):
```csharp
var options = new RateLimiterOptions
{
    MaxRequests = 100,                    // Max requests in window
    Window = TimeSpan.FromMinutes(1),     // Time window
    MaxConcurrent = 10                    // Max simultaneous
};
```

### Rate Limit Keys:
- `"spawn_rigid"` - Rigid body spawning
- `"spawn_soft"` - Soft body spawning
- Add your own for custom operations

---

## ? Validation

### Validate before creation:
```csharp
var validation = _validator.ValidateRigidBody(body);

if (!validation.IsValid)
{
    // Errors - operation fails
    foreach (var error in validation.Errors)
    {
        Console.Error.WriteLine(error);
    }
    return;
}

// Warnings - operation succeeds but logs concerns
foreach (var warning in validation.Warnings)
{
    Console.WriteLine($"Warning: {warning}");
}
```

### Available Validators:
```csharp
// Rigid body validation
ValidationResult ValidateRigidBody(RigidBody body);

// Soft body validation
ValidationResult ValidateSoftBody(SoftBody body);

// Simulation settings validation
ValidationResult ValidateSimulationSettings(SimulationSettings settings);

// Mass ratio validation (between two bodies)
ValidationResult ValidateMassRatio(float mass1, float mass2);
```

---

## ?? Performance Monitoring

### Get current metrics:
```csharp
var snapshot = _performanceMonitor.GetSnapshot();

Console.WriteLine($"FPS: {snapshot.Fps:F1}");
Console.WriteLine($"Frame Time: {snapshot.AverageFrameTimeMs:F2}ms");
Console.WriteLine($"Physics Time: {snapshot.PhysicsTimeMs:F2}ms");
Console.WriteLine($"Rigid Bodies: {snapshot.RigidBodyCount}");
Console.WriteLine($"Soft Bodies: {snapshot.SoftBodyCount}");
Console.WriteLine($"Memory: {snapshot.MemoryUsedBytes / 1024 / 1024}MB");
```

### Manual timing:
```csharp
using (_performanceMonitor.MeasureTiming("MyOperation"))
{
    // Code to measure
    await DoSomethingAsync();
}

// Later, get average
var avgTime = _performanceMonitor.GetAverageTiming("MyOperation");
```

### Configuration:
```csharp
// Toggle detailed profiling at runtime
_performanceMonitor.DetailedProfilingEnabled = true;

// Access configuration
var options = _performanceMonitor.Options;
options.FpsWarningThreshold = 25f; // Lower warning threshold
```

---

## ?? Command Logging

### Get execution history:
```csharp
if (_dispatcher is LoggingCommandDispatcher loggingDispatcher)
{
    var history = loggingDispatcher.GetExecutionHistory();
    
    foreach (var log in history.TakeLast(10))
    {
        Console.WriteLine($"[{log.Timestamp:HH:mm:ss}] {log.CommandType}");
        Console.WriteLine($"  Duration: {log.DurationMs}ms");
        Console.WriteLine($"  Success: {log.Success}");
        if (log.ErrorMessage != null)
            Console.WriteLine($"  Error: {log.ErrorMessage}");
    }
}
```

### Get statistics:
```csharp
var stats = loggingDispatcher.GetStats();

Console.WriteLine($"Total Commands: {stats.TotalCommands}");
Console.WriteLine($"Success Rate: {stats.SuccessRate:F1}%");
Console.WriteLine($"Average Duration: {stats.AverageDurationMs:F2}ms");

// Per-command-type stats
foreach (var typeStats in stats.CommandTypeStats)
{
    Console.WriteLine($"\n{typeStats.CommandType}:");
    Console.WriteLine($"  Executions: {typeStats.TotalExecutions}");
    Console.WriteLine($"  Success Rate: {typeStats.SuccessRate:F1}%");
    Console.WriteLine($"  Avg Time: {typeStats.AverageDurationMs:F2}ms");
    Console.WriteLine($"  Min Time: {typeStats.MinDurationMs}ms");
    Console.WriteLine($"  Max Time: {typeStats.MaxDurationMs}ms");
}
```

### Clear history:
```csharp
loggingDispatcher.ClearHistory();
```

---

## ?? Adding New Command Handlers

### 1. Define the command:
```csharp
public record MyCommand(string Param1, int Param2) : ICommand<string>;
```

### 2. Create the handler:
```csharp
public class MyCommandHandler : ICommandHandler<MyCommand, string>
{
    private readonly IPhysicsValidator _validator;
    private readonly IRateLimiter _rateLimiter;
    
    public MyCommandHandler(
        IPhysicsValidator validator,
        IRateLimiter rateLimiter)
    {
        _validator = validator;
        _rateLimiter = rateLimiter;
    }
    
    public async Task<Result<string>> HandleAsync(
        MyCommand command, 
        CancellationToken cancellationToken = default)
    {
        // 1. Check rate limit
        if (!_rateLimiter.TryAcquire("my_operation"))
        {
            return Result<string>.Failure("Rate limit exceeded");
        }
        
        // 2. Validate (if applicable)
        // var validation = _validator.ValidateSomething(...);
        // if (!validation.IsValid) return Result<string>.Failure(...);
        
        // 3. Execute
        try
        {
            // Your logic here
            await DoWorkAsync(command);
            return Result<string>.Success("Success!");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Failed: {ex.Message}");
        }
    }
}
```

### 3. Register in Program.cs:
```csharp
builder.Services.AddScoped<ICommandHandler<MyCommand, string>, MyCommandHandler>();
```

### 4. Use it:
```csharp
var command = new MyCommand("test", 42);
var result = await _dispatcher.DispatchAsync<MyCommand, string>(command);

if (result.IsSuccess)
{
    Console.WriteLine($"Success: {result.Value}");
}
else
{
    Console.WriteLine($"Error: {result.Error}");
}
```

---

## ?? Memory Management Best Practices

### Event Subscriptions:
```csharp
// ? GOOD - Store reference
private Action? _handler;

protected override void OnInitialized()
{
    _handler = HandleEvent;
    _service.OnEvent += _handler;
}

public void Dispose()
{
    if (_handler != null)
    {
        _service.OnEvent -= _handler;
        _handler = null;
    }
}

// ? BAD - No cleanup
protected override void OnInitialized()
{
    _service.OnEvent += HandleEvent; // LEAK!
}
```

### IDisposable/IAsyncDisposable:
```csharp
public class MyComponent : IAsyncDisposable
{
    public async ValueTask DisposeAsync()
    {
        // 1. Stop loops/timers first
        await _loopService.StopAsync();
        
        // 2. Unsubscribe from events
        UnsubscribeFromEvents();
        
        // 3. Dispose services
        await _service1.DisposeAsync();
        await _service2.DisposeAsync();
    }
}
```

---

## ?? Configuration Cheat Sheet

### Performance Monitor:
```csharp
new PerformanceMonitorOptions
{
    DetailedProfilingEnabled = false,      // Toggle profiling
    SampleCount = 60,                      // Samples to average
    FpsWindowMs = 1000,                    // FPS calculation window
    TrackMemory = true,                    // Track GC memory
    TrackGarbageCollection = true,         // Track GC collections
    LogPerformanceWarnings = true,         // Console warnings
    FpsWarningThreshold = 30f,             // Low FPS threshold
    FrameTimeWarningThresholdMs = 33f      // High frame time threshold
}
```

### Rate Limiter:
```csharp
new RateLimiterOptions
{
    MaxRequests = 100,                     // Max requests per window
    Window = TimeSpan.FromMinutes(1),      // Time window
    MaxConcurrent = 10                     // Concurrent operations limit
}
```

### Common Rate Limit Keys:
- `"spawn_rigid"` - Rigid body spawning (100/min)
- `"spawn_soft"` - Soft body spawning (100/min)
- `"apply_force"` - Force application
- `"scene_reset"` - Scene reset operations

---

## ?? Debugging Tips

### Enable detailed logging:
```csharp
// In Program.cs or at runtime
_performanceMonitor.DetailedProfilingEnabled = true;
_performanceMonitor.Options.LogPerformanceWarnings = true;
```

### Check command execution:
```csharp
// View recent commands
var history = loggingDispatcher.GetExecutionHistory();
var recentFailures = history.Where(h => !h.Success).TakeLast(5);
```

### Monitor rate limits:
```csharp
// Check current quota
var remaining = _rateLimiter.GetRemainingQuota("spawn_rigid");
Console.WriteLine($"Remaining spawns: {remaining}");

// Reset if needed (dev only)
_rateLimiter.Reset("spawn_rigid");
```

### Performance issues:
```csharp
var snapshot = _performanceMonitor.GetSnapshot();

if (snapshot.Fps < 30)
    Console.WriteLine("Low FPS detected!");

if (snapshot.PhysicsTimeMs > 16)
    Console.WriteLine("Physics is taking too long!");

if (snapshot.MemoryUsedBytes > 500 * 1024 * 1024) // 500MB
    Console.WriteLine("High memory usage!");
```

---

## ?? Further Reading

- **SOLID Principles**: All services follow SOLID design
- **Command Pattern**: See `Services/Commands/`
- **Result Pattern**: See `Models/Result.cs`
- **Event Aggregator**: See `Services/Events/EventAggregator.cs`
- **Decorator Pattern**: See `LoggingCommandDispatcher.cs`

---

## ?? Common Patterns

### Result Pattern:
```csharp
// Success
return Result<string>.Success("operation-id");

// Failure
return Result<string>.Failure("Error message");

// Using
var result = await SomeOperation();
if (result.IsSuccess)
{
    DoSomething(result.Value);
}
else
{
    LogError(result.Error);
}
```

### Event Publishing:
```csharp
_events.Publish(new ObjectSpawnedEvent(id, name, type));

// Subscribe
_subscription = _events.Subscribe<ObjectSpawnedEvent>(evt =>
{
    Console.WriteLine($"Object {evt.Name} spawned!");
});

// Unsubscribe
_subscription?.Dispose();
```

---

**Need Help?** Check the inline XML documentation - every public method has comprehensive docs!
