// Global using directives for backwards compatibility with domain types
// All domain types are now in BlazorClient.Domain.Models and BlazorClient.Domain.Common

// Domain Models
global using SceneObject = BlazorClient.Domain.Models.SceneObject;
global using RigidBody = BlazorClient.Domain.Models.RigidBody;
global using SoftBody = BlazorClient.Domain.Models.SoftBody;
global using SimulationSettings = BlazorClient.Domain.Models.SimulationSettings;
global using RenderSettings = BlazorClient.Domain.Models.RenderSettings;
global using PerformanceStats = BlazorClient.Domain.Models.PerformanceStats;
global using ScenePreset = BlazorClient.Domain.Models.ScenePreset;
global using RendererInfo = BlazorClient.Domain.Models.RendererInfo;
global using RendererBackend = BlazorClient.Domain.Models.RendererBackend;

// Rendering Types
global using RendererCapabilities = BlazorClient.Domain.Models.RendererCapabilities;
global using BackendCapability = BlazorClient.Domain.Models.BackendCapability;
global using RendererPerformanceMetrics = BlazorClient.Domain.Models.RendererPerformanceMetrics;
global using BenchmarkResults = BlazorClient.Domain.Models.BenchmarkResults;
global using BenchmarkResult = BlazorClient.Domain.Models.BenchmarkResult;

// Physics Types
global using Vector3 = BlazorClient.Domain.Models.Vector3;
global using Quaternion = BlazorClient.Domain.Models.Quaternion;
global using TransformData = BlazorClient.Domain.Models.TransformData;
global using MaterialPreset = BlazorClient.Domain.Models.MaterialPreset;
global using RigidPrimitiveType = BlazorClient.Domain.Models.RigidPrimitiveType;
global using SoftBodyType = BlazorClient.Domain.Models.SoftBodyType;
global using SoftBodyPreset = BlazorClient.Domain.Models.SoftBodyPreset;
global using PhysicsMaterial = BlazorClient.Domain.Models.PhysicsMaterial;
global using SoftBodyMaterial = BlazorClient.Domain.Models.SoftBodyMaterial;

// Events
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
