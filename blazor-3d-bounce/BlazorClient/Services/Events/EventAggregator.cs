// This file is maintained for backwards compatibility
// All types have been moved to BlazorClient.Application.Events and BlazorClient.Infrastructure.Events

global using IEventAggregator = BlazorClient.Application.Events.IEventAggregator;
global using IEvent = BlazorClient.Application.Events.IEvent;
global using ObjectSpawnedEvent = BlazorClient.Application.Events.ObjectSpawnedEvent;
global using ObjectDeletedEvent = BlazorClient.Application.Events.ObjectDeletedEvent;
global using ObjectSelectedEvent = BlazorClient.Application.Events.ObjectSelectedEvent;
global using SimulationPausedEvent = BlazorClient.Application.Events.SimulationPausedEvent;
global using SimulationSettingsChangedEvent = BlazorClient.Application.Events.SimulationSettingsChangedEvent;
global using RenderSettingsChangedEvent = BlazorClient.Application.Events.RenderSettingsChangedEvent;
global using PhysicsSteppedEvent = BlazorClient.Application.Events.PhysicsSteppedEvent;
global using SceneResetEvent = BlazorClient.Application.Events.SceneResetEvent;
global using SceneLoadedEvent = BlazorClient.Application.Events.SceneLoadedEvent;
global using InitializationCompleteEvent = BlazorClient.Application.Events.InitializationCompleteEvent;
global using ErrorOccurredEvent = BlazorClient.Application.Events.ErrorOccurredEvent;
global using ErrorSeverity = BlazorClient.Application.Events.ErrorSeverity;
