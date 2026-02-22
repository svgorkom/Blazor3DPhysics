using BlazorClient.Domain.Models;
using BlazorClient.Application.Services;

// This file contains UI-layer specific types (DTOs) for physics data exchange
// and type aliases for Application layer interfaces.

namespace BlazorClient.Services;

// Re-export Application layer interfaces for convenience
// This allows the Services layer to use these interfaces without full qualification.

/// <summary>
/// Batched transform data for efficient physics-to-rendering synchronization.
/// </summary>
public class RigidTransformBatch
{
    /// <summary>
    /// Flattened transform data array: [px,py,pz,rx,ry,rz,rw, ...] for each body.
    /// </summary>
    public float[] Transforms { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Body IDs corresponding to each transform (7-float stride).
    /// </summary>
    public string[] Ids { get; set; } = Array.Empty<string>();
}
