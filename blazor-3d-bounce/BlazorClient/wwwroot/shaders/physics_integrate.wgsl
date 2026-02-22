// ============================================================================
// Integration Kernel - Semi-Implicit Euler
// ============================================================================
// This kernel updates velocities and positions using semi-implicit Euler
// integration, which provides better energy conservation than explicit Euler.

struct RigidBody {
    position: vec3<f32>,
    inverseMass: f32,
    rotation: vec4<f32>,
    linearVelocity: vec3<f32>,
    restitution: f32,
    angularVelocity: vec3<f32>,
    friction: f32,
    inverseInertia: vec3<f32>,
    colliderType: u32,
    colliderData: vec4<f32>,
    linearDamping: f32,
    angularDamping: f32,
    flags: u32,
    _padding: f32,
}

struct SimParams {
    gravity: vec3<f32>,
    deltaTime: f32,
    numBodies: u32,
    numContacts: u32,
    solverIterations: u32,
    enableCCD: u32,
    gridCellSize: f32,
    gridDimX: u32,
    gridDimY: u32,
    gridDimZ: u32,
}

// Bindings
@group(0) @binding(0) var<storage, read_write> bodies: array<RigidBody>;
@group(0) @binding(1) var<uniform> params: SimParams;

// External forces buffer (optional - for user-applied forces)
@group(0) @binding(2) var<storage, read> externalForces: array<vec3<f32>>;

// Constants
const FLAG_STATIC: u32 = 1u;
const FLAG_SLEEPING: u32 = 2u;
const EPSILON: f32 = 1e-6;
const MAX_ANGULAR_VELOCITY: f32 = 50.0;  // rad/s cap for stability

// Workgroup size - 64 is optimal for most GPUs
@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    // Bounds check
    if (idx >= params.numBodies) {
        return;
    }
    
    var body = bodies[idx];
    
    // Skip static bodies
    if ((body.flags & FLAG_STATIC) != 0u) {
        return;
    }
    
    // Skip sleeping bodies (but wake them if external force applied)
    if ((body.flags & FLAG_SLEEPING) != 0u) {
        // Check if external force should wake the body
        if (idx < arrayLength(&externalForces)) {
            let extForce = externalForces[idx];
            if (length(extForce) > EPSILON) {
                body.flags = body.flags & ~FLAG_SLEEPING;
            } else {
                bodies[idx] = body;
                return;
            }
        } else {
            bodies[idx] = body;
            return;
        }
    }
    
    let dt = params.deltaTime;
    let invMass = body.inverseMass;
    
    // ========================================
    // Step 1: Apply forces and compute acceleration
    // ========================================
    
    // Gravity (only for dynamic bodies with mass)
    var linearAccel = vec3<f32>(0.0);
    if (invMass > EPSILON) {
        linearAccel = params.gravity;
    }
    
    // External forces (if any)
    if (idx < arrayLength(&externalForces)) {
        let extForce = externalForces[idx];
        linearAccel = linearAccel + extForce * invMass;
    }
    
    // ========================================
    // Step 2: Semi-implicit Euler for linear motion
    // ========================================
    
    // Update velocity first (semi-implicit)
    body.linearVelocity = body.linearVelocity + linearAccel * dt;
    
    // Apply damping (exponential decay for stability)
    let linearDampFactor = exp(-body.linearDamping * dt);
    body.linearVelocity = body.linearVelocity * linearDampFactor;
    
    // Update position using new velocity
    body.position = body.position + body.linearVelocity * dt;
    
    // ========================================
    // Step 3: Semi-implicit Euler for angular motion
    // ========================================
    
    // Angular damping
    let angularDampFactor = exp(-body.angularDamping * dt);
    body.angularVelocity = body.angularVelocity * angularDampFactor;
    
    // Clamp angular velocity for stability
    let angVelMag = length(body.angularVelocity);
    if (angVelMag > MAX_ANGULAR_VELOCITY) {
        body.angularVelocity = body.angularVelocity * (MAX_ANGULAR_VELOCITY / angVelMag);
    }
    
    // Update rotation using quaternion derivative
    // dq/dt = 0.5 * omega * q (where omega is quaternion (wx, wy, wz, 0))
    if (angVelMag > EPSILON) {
        let omega = body.angularVelocity;
        let omegaQuat = vec4<f32>(omega.x, omega.y, omega.z, 0.0);
        
        // Quaternion multiplication: omega * q
        let dq = vec4<f32>(
            omegaQuat.w * body.rotation.x + omegaQuat.x * body.rotation.w + omegaQuat.y * body.rotation.z - omegaQuat.z * body.rotation.y,
            omegaQuat.w * body.rotation.y - omegaQuat.x * body.rotation.z + omegaQuat.y * body.rotation.w + omegaQuat.z * body.rotation.x,
            omegaQuat.w * body.rotation.z + omegaQuat.x * body.rotation.y - omegaQuat.y * body.rotation.x + omegaQuat.z * body.rotation.w,
            omegaQuat.w * body.rotation.w - omegaQuat.x * body.rotation.x - omegaQuat.y * body.rotation.y - omegaQuat.z * body.rotation.z
        );
        
        body.rotation = body.rotation + 0.5 * dq * dt;
        body.rotation = normalize(body.rotation);
    }
    
    // ========================================
    // Step 4: Sleeping check
    // ========================================
    
    let linearSpeed = length(body.linearVelocity);
    let angularSpeed = length(body.angularVelocity);
    let sleepThreshold = 0.01;  // Could be passed via params
    
    // Simple sleep check (in production, use time-based threshold)
    // For now, we just mark bodies that could potentially sleep
    // Actual sleep decision made after collision resolution
    
    // ========================================
    // Write back
    // ========================================
    
    bodies[idx] = body;
}

// ============================================================================
// Variant: Integration with Sub-stepping (for CCD)
// ============================================================================
// This kernel handles variable timesteps and CCD requirements

@compute @workgroup_size(64)
fn integrateWithCCD(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= params.numBodies) {
        return;
    }
    
    var body = bodies[idx];
    
    if ((body.flags & FLAG_STATIC) != 0u) {
        return;
    }
    
    let dt = params.deltaTime;
    let speed = length(body.linearVelocity);
    
    // Determine if CCD is needed based on velocity
    // Object travels more than its radius in one timestep
    let radius = body.colliderData.x;  // Assuming sphere or using max extent
    let travelDistance = speed * dt;
    
    var numSubSteps = 1u;
    if (travelDistance > radius * 0.5 && params.enableCCD != 0u) {
        // Need sub-stepping
        numSubSteps = min(u32(ceil(travelDistance / (radius * 0.5))), 8u);
    }
    
    let subDt = dt / f32(numSubSteps);
    
    // Integrate with sub-steps
    for (var step = 0u; step < numSubSteps; step = step + 1u) {
        // Simplified integration per substep
        // In full implementation, collision detection runs between substeps
        
        if (body.inverseMass > EPSILON) {
            body.linearVelocity = body.linearVelocity + params.gravity * subDt;
        }
        
        let linearDampFactor = exp(-body.linearDamping * subDt);
        body.linearVelocity = body.linearVelocity * linearDampFactor;
        
        body.position = body.position + body.linearVelocity * subDt;
        
        // Angular update
        let angularDampFactor = exp(-body.angularDamping * subDt);
        body.angularVelocity = body.angularVelocity * angularDampFactor;
        
        let angVelMag = length(body.angularVelocity);
        if (angVelMag > EPSILON) {
            let omega = body.angularVelocity;
            let omegaQuat = vec4<f32>(omega.x, omega.y, omega.z, 0.0);
            
            let dq = vec4<f32>(
                omegaQuat.w * body.rotation.x + omegaQuat.x * body.rotation.w + omegaQuat.y * body.rotation.z - omegaQuat.z * body.rotation.y,
                omegaQuat.w * body.rotation.y - omegaQuat.x * body.rotation.z + omegaQuat.y * body.rotation.w + omegaQuat.z * body.rotation.x,
                omegaQuat.w * body.rotation.z + omegaQuat.x * body.rotation.y - omegaQuat.y * body.rotation.x + omegaQuat.z * body.rotation.w,
                omegaQuat.w * body.rotation.w - omegaQuat.x * body.rotation.x - omegaQuat.y * body.rotation.y - omegaQuat.z * body.rotation.z
            );
            
            body.rotation = body.rotation + 0.5 * dq * subDt;
            body.rotation = normalize(body.rotation);
        }
    }
    
    bodies[idx] = body;
}
