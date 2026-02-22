// ============================================================================
// Collision Solver - Impulse-Based Response with Friction
// ============================================================================
// Resolves collisions using sequential impulses with warm-starting.
// Implements Coulomb friction model with clamping.
//
// BINDING SCHEME (consistent across all physics shaders):
// Group 0:
//   @binding(0) - bodies: array<RigidBody> (read_write)
//   @binding(1) - params: SimParams (uniform)
//   @binding(2) - contacts: array<Contact> (read_write)
//   @binding(3) - contactCount: u32 (read)

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

struct Contact {
    bodyA: u32,
    bodyB: u32,
    flags: u32,
    _pad0: u32,
    normal: vec3<f32>,
    penetration: f32,
    contactPoint: vec3<f32>,
    normalImpulse: f32,
    tangent1: vec3<f32>,
    tangentImpulse1: f32,
    tangent2: vec3<f32>,
    tangentImpulse2: f32,
}

// Bindings - consistent scheme across all solver entry points
@group(0) @binding(0) var<storage, read_write> bodies: array<RigidBody>;
@group(0) @binding(1) var<uniform> params: SimParams;
@group(0) @binding(2) var<storage, read_write> contacts: array<Contact>;
@group(0) @binding(3) var<storage, read> contactCount: u32;

// Constants
const EPSILON: f32 = 1e-6;
const FLAG_STATIC: u32 = 1u;
const CONTACT_VALID: u32 = 1u;
const GROUND_BODY: u32 = 0xFFFFFFFFu;

// Baumgarte stabilization parameters
const BAUMGARTE_BIAS: f32 = 0.2;
const SLOP: f32 = 0.005;       // Penetration tolerance (5mm)
const MAX_CORRECTION: f32 = 0.2;

// ============================================================================
// Main solver iteration
// ============================================================================
// This kernel should be dispatched multiple times per frame (solverIterations)
// Using Jacobi-style iteration: read from current velocities, accumulate impulses

@compute @workgroup_size(64)
fn solveVelocities(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    // Access params early to ensure binding is in layout
    let maxContacts = params.numContacts;
    
    if (idx >= contactCount) {
        return;
    }
    
    var contact = contacts[idx];
    
    // Check if contact is valid
    if ((contact.flags & CONTACT_VALID) == 0u) {
        return;
    }
    
    let idxA = contact.bodyA;
    let idxB = contact.bodyB;
    
    var bodyA = bodies[idxA];
    
    // Handle ground contacts (bodyB = GROUND_BODY)
    var bodyB: RigidBody;
    var isGroundContact = false;
    
    if (idxB == GROUND_BODY) {
        isGroundContact = true;
        // Create virtual ground body (static, infinite mass)
        bodyB.position = vec3<f32>(0.0);
        bodyB.linearVelocity = vec3<f32>(0.0);
        bodyB.angularVelocity = vec3<f32>(0.0);
        bodyB.inverseMass = 0.0;
        bodyB.inverseInertia = vec3<f32>(0.0);
        bodyB.restitution = 0.3;
        bodyB.friction = 0.5;
    } else {
        bodyB = bodies[idxB];
    }
    
    let normal = contact.normal;
    let contactPoint = contact.contactPoint;
    
    // Compute relative positions from body centers to contact point
    let rA = contactPoint - bodyA.position;
    let rB = contactPoint - bodyB.position;
    
    // ========================================
    // Normal impulse (restitution)
    // ========================================
    
    // Compute relative velocity at contact point
    let vA = bodyA.linearVelocity + cross(bodyA.angularVelocity, rA);
    let vB = bodyB.linearVelocity + cross(bodyB.angularVelocity, rB);
    let vRel = vA - vB;
    
    // Normal component of relative velocity
    let vn = dot(vRel, normal);
    
    // Only resolve if approaching (vn < 0 means A approaching B)
    if (vn < 0.0) {
        // Compute effective mass for normal direction
        let invMassSum = bodyA.inverseMass + bodyB.inverseMass;
        
        let rAxN = cross(rA, normal);
        let rBxN = cross(rB, normal);
        
        let angularTermA = dot(rAxN, bodyA.inverseInertia * rAxN);
        let angularTermB = dot(rBxN, bodyB.inverseInertia * rBxN);
        
        let effectiveMass = 1.0 / (invMassSum + angularTermA + angularTermB);
        
        // Restitution (coefficient of restitution)
        let e = min(bodyA.restitution, bodyB.restitution);
        
        // Bias velocity for penetration correction (Baumgarte)
        let dt = params.deltaTime;
        let bias = max(0.0, contact.penetration - SLOP) * BAUMGARTE_BIAS / dt;
        
        // Impulse magnitude
        var j = -(1.0 + e) * vn * effectiveMass;
        j = j + bias * effectiveMass;
        
        // Clamp to non-negative (can only push apart, not pull together)
        let oldImpulse = contact.normalImpulse;
        contact.normalImpulse = max(oldImpulse + j, 0.0);
        j = contact.normalImpulse - oldImpulse;
        
        // Apply impulse
        let impulse = j * normal;
        
        bodyA.linearVelocity = bodyA.linearVelocity + impulse * bodyA.inverseMass;
        bodyA.angularVelocity = bodyA.angularVelocity + bodyA.inverseInertia * cross(rA, impulse);
        
        if (!isGroundContact) {
            bodyB.linearVelocity = bodyB.linearVelocity - impulse * bodyB.inverseMass;
            bodyB.angularVelocity = bodyB.angularVelocity - bodyB.inverseInertia * cross(rB, impulse);
        }
    }
    
    // ========================================
    // Friction impulses (tangent directions)
    // ========================================
    
    // Recompute relative velocity after normal impulse
    let vA2 = bodyA.linearVelocity + cross(bodyA.angularVelocity, rA);
    let vB2 = bodyB.linearVelocity + cross(bodyB.angularVelocity, rB);
    let vRel2 = vA2 - vB2;
    
    // Combined friction coefficient
    let mu = sqrt(bodyA.friction * bodyB.friction);
    let maxFriction = mu * contact.normalImpulse;
    
    // Tangent 1
    {
        let tangent = contact.tangent1;
        let vt = dot(vRel2, tangent);
        
        let invMassSum = bodyA.inverseMass + bodyB.inverseMass;
        let rAxT = cross(rA, tangent);
        let rBxT = cross(rB, tangent);
        let angularTermA = dot(rAxT, bodyA.inverseInertia * rAxT);
        let angularTermB = dot(rBxT, bodyB.inverseInertia * rBxT);
        let effectiveMassT = 1.0 / (invMassSum + angularTermA + angularTermB);
        
        var jt = -vt * effectiveMassT;
        
        // Coulomb friction clamp
        let oldImpulseT = contact.tangentImpulse1;
        contact.tangentImpulse1 = clamp(oldImpulseT + jt, -maxFriction, maxFriction);
        jt = contact.tangentImpulse1 - oldImpulseT;
        
        let frictionImpulse = jt * tangent;
        
        bodyA.linearVelocity = bodyA.linearVelocity + frictionImpulse * bodyA.inverseMass;
        bodyA.angularVelocity = bodyA.angularVelocity + bodyA.inverseInertia * cross(rA, frictionImpulse);
        
        if (!isGroundContact) {
            bodyB.linearVelocity = bodyB.linearVelocity - frictionImpulse * bodyB.inverseMass;
            bodyB.angularVelocity = bodyB.angularVelocity - bodyB.inverseInertia * cross(rB, frictionImpulse);
        }
    }
    
    // Tangent 2
    {
        let tangent = contact.tangent2;
        
        // Recompute relative velocity
        let vA3 = bodyA.linearVelocity + cross(bodyA.angularVelocity, rA);
        let vB3 = bodyB.linearVelocity + cross(bodyB.angularVelocity, rB);
        let vRel3 = vA3 - vB3;
        let vt = dot(vRel3, tangent);
        
        let invMassSum = bodyA.inverseMass + bodyB.inverseMass;
        let rAxT = cross(rA, tangent);
        let rBxT = cross(rB, tangent);
        let angularTermA = dot(rAxT, bodyA.inverseInertia * rAxT);
        let angularTermB = dot(rBxT, bodyB.inverseInertia * rBxT);
        let effectiveMassT = 1.0 / (invMassSum + angularTermA + angularTermB);
        
        var jt = -vt * effectiveMassT;
        
        let oldImpulseT = contact.tangentImpulse2;
        contact.tangentImpulse2 = clamp(oldImpulseT + jt, -maxFriction, maxFriction);
        jt = contact.tangentImpulse2 - oldImpulseT;
        
        let frictionImpulse = jt * tangent;
        
        bodyA.linearVelocity = bodyA.linearVelocity + frictionImpulse * bodyA.inverseMass;
        bodyA.angularVelocity = bodyA.angularVelocity + bodyA.inverseInertia * cross(rA, frictionImpulse);
        
        if (!isGroundContact) {
            bodyB.linearVelocity = bodyB.linearVelocity - frictionImpulse * bodyB.inverseMass;
            bodyB.angularVelocity = bodyB.angularVelocity - bodyB.inverseInertia * cross(rB, frictionImpulse);
        }
    }
    
    // ========================================
    // Write back
    // ========================================
    
    bodies[idxA] = bodyA;
    if (!isGroundContact) {
        bodies[idxB] = bodyB;
    }
    contacts[idx] = contact;
}

// ============================================================================
// Position correction (Baumgarte stabilization)
// ============================================================================
// Separate pass for position correction to avoid energy injection

@compute @workgroup_size(64)
fn solvePositions(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    // Access params early to ensure binding is in layout
    let dt = params.deltaTime;
    
    if (idx >= contactCount) {
        return;
    }
    
    let contact = contacts[idx];
    
    if ((contact.flags & CONTACT_VALID) == 0u) {
        return;
    }
    
    // Only correct if penetration exceeds slop
    if (contact.penetration <= SLOP) {
        return;
    }
    
    let idxA = contact.bodyA;
    let idxB = contact.bodyB;
    
    var bodyA = bodies[idxA];
    let isGroundContact = (idxB == GROUND_BODY);
    
    var invMassB = 0.0;
    if (!isGroundContact) {
        invMassB = bodies[idxB].inverseMass;
    }
    
    let invMassSum = bodyA.inverseMass + invMassB;
    
    if (invMassSum < EPSILON) {
        return;  // Both static
    }
    
    // Compute correction
    let correction = (contact.penetration - SLOP) * BAUMGARTE_BIAS;
    let correctionClamped = min(correction, MAX_CORRECTION);
    
    let correctionVec = contact.normal * (correctionClamped / invMassSum);
    
    // Apply position correction
    bodyA.position = bodyA.position + correctionVec * bodyA.inverseMass;
    bodies[idxA] = bodyA;
    
    if (!isGroundContact) {
        var bodyB = bodies[idxB];
        bodyB.position = bodyB.position - correctionVec * invMassB;
        bodies[idxB] = bodyB;
    }
}

// ============================================================================
// Warm-starting: Apply accumulated impulses from previous frame
// ============================================================================
// Call at the beginning of solver iterations to improve convergence

@compute @workgroup_size(64)
fn warmStart(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    // Access params early to ensure binding is in layout
    let iterations = params.solverIterations;
    
    if (idx >= contactCount) {
        return;
    }
    
    let contact = contacts[idx];
    
    if ((contact.flags & CONTACT_VALID) == 0u) {
        return;
    }
    
    // Skip if no accumulated impulse
    if (abs(contact.normalImpulse) < EPSILON && 
        abs(contact.tangentImpulse1) < EPSILON && 
        abs(contact.tangentImpulse2) < EPSILON) {
        return;
    }
    
    let idxA = contact.bodyA;
    let idxB = contact.bodyB;
    
    var bodyA = bodies[idxA];
    let isGroundContact = (idxB == GROUND_BODY);
    
    let rA = contact.contactPoint - bodyA.position;
    
    // Total impulse
    let impulse = contact.normalImpulse * contact.normal +
                  contact.tangentImpulse1 * contact.tangent1 +
                  contact.tangentImpulse2 * contact.tangent2;
    
    // Apply to body A
    bodyA.linearVelocity = bodyA.linearVelocity + impulse * bodyA.inverseMass;
    bodyA.angularVelocity = bodyA.angularVelocity + bodyA.inverseInertia * cross(rA, impulse);
    bodies[idxA] = bodyA;
    
    // Apply to body B
    if (!isGroundContact) {
        var bodyB = bodies[idxB];
        let rB = contact.contactPoint - bodyB.position;
        
        bodyB.linearVelocity = bodyB.linearVelocity - impulse * bodyB.inverseMass;
        bodyB.angularVelocity = bodyB.angularVelocity - bodyB.inverseInertia * cross(rB, impulse);
        bodies[idxB] = bodyB;
    }
}

// ============================================================================
// Clear contacts for new frame
// ============================================================================
@compute @workgroup_size(256)
fn clearContacts(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    // Access params early to ensure binding is in layout
    let numContacts = params.numContacts;
    
    if (idx >= arrayLength(&contacts)) {
        return;
    }
    
    contacts[idx].flags = 0u;
    contacts[idx].normalImpulse = 0.0;
    contacts[idx].tangentImpulse1 = 0.0;
    contacts[idx].tangentImpulse2 = 0.0;
}
