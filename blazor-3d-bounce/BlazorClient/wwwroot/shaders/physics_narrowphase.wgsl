// ============================================================================
// Narrow Phase - Collision Detection
// ============================================================================
// Performs exact collision tests on candidate pairs from broad phase.
// Generates contact manifolds for collision response.
//
// BINDING SCHEME (consistent across all physics shaders):
// Group 0:
//   @binding(0) - bodies: array<RigidBody> (read or read_write)
//   @binding(1) - params: SimParams (uniform)
//   @binding(2) - pairs: array<CollisionPair> (narrowphase) OR contacts: array<Contact> (solver)
//   @binding(3) - pairCount: u32 (narrowphase) OR contactCount: u32 (solver)
//   @binding(4) - contacts: array<Contact> (narrowphase output)
//   @binding(5) - contactCount: atomic<u32> (narrowphase output)

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

struct CollisionPair {
    bodyA: u32,
    bodyB: u32,
}

// Contact structure for collision response
struct Contact {
    bodyA: u32,
    bodyB: u32,
    flags: u32,                 // bit 0: valid
    _pad0: u32,
    normal: vec3<f32>,          // Points from A to B
    penetration: f32,
    contactPoint: vec3<f32>,
    normalImpulse: f32,         // Accumulated for warm-starting
    tangent1: vec3<f32>,
    tangentImpulse1: f32,
    tangent2: vec3<f32>,
    tangentImpulse2: f32,
}

// Bindings - using consistent scheme across all entry points
@group(0) @binding(0) var<storage, read> bodies: array<RigidBody>;
@group(0) @binding(1) var<uniform> params: SimParams;
@group(0) @binding(2) var<storage, read> pairs: array<CollisionPair>;
@group(0) @binding(3) var<storage, read> pairCount: u32;
@group(0) @binding(4) var<storage, read_write> contacts: array<Contact>;
@group(0) @binding(5) var<storage, read_write> contactCount: atomic<u32>;

// Constants
const COLLIDER_SPHERE: u32 = 0u;
const COLLIDER_AABB: u32 = 1u;
const COLLIDER_CAPSULE: u32 = 2u;
const EPSILON: f32 = 1e-6;
const CONTACT_VALID: u32 = 1u;

// ============================================================================
// Main narrow phase kernel
// ============================================================================
@compute @workgroup_size(64)
fn main(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    // Use params.numContacts as pair count limit (reusing field)
    // This ensures params is always accessed, keeping binding 1 in the layout
    let maxPairs = select(pairCount, params.numContacts, params.numContacts > 0u);
    
    if (idx >= pairCount) {
        return;
    }
    
    let pair = pairs[idx];
    let bodyA = bodies[pair.bodyA];
    let bodyB = bodies[pair.bodyB];
    
    // Dispatch to appropriate collision test based on collider types
    var contact: Contact;
    var hasContact = false;
    
    let typeA = bodyA.colliderType;
    let typeB = bodyB.colliderType;
    
    if (typeA == COLLIDER_SPHERE && typeB == COLLIDER_SPHERE) {
        hasContact = testSphereSphere(bodyA, bodyB, &contact);
    } else if (typeA == COLLIDER_AABB && typeB == COLLIDER_AABB) {
        hasContact = testAABBAABB(bodyA, bodyB, &contact);
    } else if (typeA == COLLIDER_SPHERE && typeB == COLLIDER_AABB) {
        hasContact = testSphereAABB(bodyA, bodyB, &contact);
    } else if (typeA == COLLIDER_AABB && typeB == COLLIDER_SPHERE) {
        hasContact = testSphereAABB(bodyB, bodyA, &contact);
        // Flip normal since we swapped order
        contact.normal = -contact.normal;
        // Swap body indices
        let temp = contact.bodyA;
        contact.bodyA = contact.bodyB;
        contact.bodyB = temp;
    }
    // Add more type combinations as needed (capsule, etc.)
    
    if (hasContact) {
        contact.bodyA = pair.bodyA;
        contact.bodyB = pair.bodyB;
        contact.flags = CONTACT_VALID;
        
        // Compute tangent basis for friction
        let tangents = computeTangents(contact.normal);
        contact.tangent1 = tangents[0];
        contact.tangent2 = tangents[1];
        
        // Initialize accumulated impulses (or use warm-starting from previous frame)
        contact.normalImpulse = 0.0;
        contact.tangentImpulse1 = 0.0;
        contact.tangentImpulse2 = 0.0;
        
        // Add to contact list
        let contactIdx = atomicAdd(&contactCount, 1u);
        if (contactIdx < arrayLength(&contacts)) {
            contacts[contactIdx] = contact;
        }
    }
}

// ============================================================================
// Sphere-Sphere collision test
// ============================================================================
fn testSphereSphere(a: RigidBody, b: RigidBody, contact: ptr<function, Contact>) -> bool {
    let radiusA = a.colliderData.x;
    let radiusB = b.colliderData.x;
    let radiusSum = radiusA + radiusB;
    
    let d = b.position - a.position;
    let distSq = dot(d, d);
    
    if (distSq >= radiusSum * radiusSum) {
        return false;  // No collision
    }
    
    let dist = sqrt(distSq);
    
    if (dist < EPSILON) {
        // Centers coincide - use arbitrary normal
        (*contact).normal = vec3<f32>(0.0, 1.0, 0.0);
        (*contact).penetration = radiusSum;
        (*contact).contactPoint = a.position;
    } else {
        (*contact).normal = d / dist;
        (*contact).penetration = radiusSum - dist;
        (*contact).contactPoint = a.position + (*contact).normal * radiusA;
    }
    
    return true;
}

// ============================================================================
// Sphere-Sphere swept test (CCD)
// ============================================================================
fn testSphereSphereCCD(
    posA: vec3<f32>, velA: vec3<f32>, radiusA: f32,
    posB: vec3<f32>, velB: vec3<f32>, radiusB: f32,
    dt: f32
) -> f32 {
    // Returns time of impact, or -1 if no collision
    
    let relVel = velA - velB;
    let relPos = posA - posB;
    let radiusSum = radiusA + radiusB;
    
    // Quadratic coefficients for |relPos + t*relVel|² = radiusSum²
    let a = dot(relVel, relVel);
    let b = 2.0 * dot(relVel, relPos);
    let c = dot(relPos, relPos) - radiusSum * radiusSum;
    
    // Already overlapping?
    if (c < 0.0) {
        return 0.0;
    }
    
    // No relative motion
    if (a < EPSILON) {
        return -1.0;
    }
    
    let discriminant = b * b - 4.0 * a * c;
    if (discriminant < 0.0) {
        return -1.0;  // No real roots - no collision
    }
    
    let sqrtD = sqrt(discriminant);
    let t1 = (-b - sqrtD) / (2.0 * a);
    let t2 = (-b + sqrtD) / (2.0 * a);
    
    // We want the first positive root within [0, dt]
    if (t1 >= 0.0 && t1 <= dt) {
        return t1;
    }
    if (t2 >= 0.0 && t2 <= dt) {
        return t2;
    }
    
    return -1.0;
}

// ============================================================================
// AABB-AABB collision test
// ============================================================================
fn testAABBAABB(a: RigidBody, b: RigidBody, contact: ptr<function, Contact>) -> bool {
    let halfExtentsA = a.colliderData.xyz;
    let halfExtentsB = b.colliderData.xyz;
    
    // Transform half-extents to world space (considering rotation)
    // For axis-aligned test, we compute world-space AABB bounds
    let worldHalfA = transformHalfExtents(halfExtentsA, a.rotation);
    let worldHalfB = transformHalfExtents(halfExtentsB, b.rotation);
    
    let minA = a.position - worldHalfA;
    let maxA = a.position + worldHalfA;
    let minB = b.position - worldHalfB;
    let maxB = b.position + worldHalfB;
    
    // Check for overlap on each axis
    if (maxA.x < minB.x || maxB.x < minA.x) { return false; }
    if (maxA.y < minB.y || maxB.y < minA.y) { return false; }
    if (maxA.z < minB.z || maxB.z < minA.z) { return false; }
    
    // Find minimum penetration axis (separating axis with smallest overlap)
    var minPenetration: f32 = 1e10;
    var normal = vec3<f32>(0.0);
    
    // X axis
    let penX1 = maxA.x - minB.x;
    let penX2 = maxB.x - minA.x;
    let penX = min(penX1, penX2);
    if (penX < minPenetration) {
        minPenetration = penX;
        normal = select(vec3<f32>(-1.0, 0.0, 0.0), vec3<f32>(1.0, 0.0, 0.0), penX1 < penX2);
    }
    
    // Y axis
    let penY1 = maxA.y - minB.y;
    let penY2 = maxB.y - minA.y;
    let penY = min(penY1, penY2);
    if (penY < minPenetration) {
        minPenetration = penY;
        normal = select(vec3<f32>(0.0, -1.0, 0.0), vec3<f32>(0.0, 1.0, 0.0), penY1 < penY2);
    }
    
    // Z axis
    let penZ1 = maxA.z - minB.z;
    let penZ2 = maxB.z - minA.z;
    let penZ = min(penZ1, penZ2);
    if (penZ < minPenetration) {
        minPenetration = penZ;
        normal = select(vec3<f32>(0.0, 0.0, -1.0), vec3<f32>(0.0, 0.0, 1.0), penZ1 < penZ2);
    }
    
    // Contact point: midpoint of overlap region
    let overlapMin = max(minA, minB);
    let overlapMax = min(maxA, maxB);
    (*contact).contactPoint = (overlapMin + overlapMax) * 0.5;
    (*contact).normal = normal;
    (*contact).penetration = minPenetration;
    
    return true;
}

// Helper: Transform half-extents considering rotation (compute world AABB)
fn transformHalfExtents(halfExtents: vec3<f32>, q: vec4<f32>) -> vec3<f32> {
    // For a rotated box, compute the world-aligned bounding box
    let rx = abs(quatRotate(q, vec3<f32>(halfExtents.x, 0.0, 0.0)));
    let ry = abs(quatRotate(q, vec3<f32>(0.0, halfExtents.y, 0.0)));
    let rz = abs(quatRotate(q, vec3<f32>(0.0, 0.0, halfExtents.z)));
    return rx + ry + rz;
}

// ============================================================================
// Sphere-AABB collision test
// ============================================================================
fn testSphereAABB(sphere: RigidBody, box: RigidBody, contact: ptr<function, Contact>) -> bool {
    let radius = sphere.colliderData.x;
    let halfExtents = box.colliderData.xyz;
    
    // Transform sphere center to box local space
    let localSpherePos = transformToLocal(sphere.position, box.position, box.rotation);
    
    // Find closest point on AABB to sphere center
    let closest = clamp(localSpherePos, -halfExtents, halfExtents);
    
    let diff = localSpherePos - closest;
    let distSq = dot(diff, diff);
    
    if (distSq >= radius * radius) {
        return false;  // No collision
    }
    
    // Transform results back to world space
    let dist = sqrt(distSq);
    var localNormal: vec3<f32>;
    
    if (dist < EPSILON) {
        // Sphere center is inside the box
        // Find closest face
        let distToFaces = halfExtents - abs(localSpherePos);
        
        if (distToFaces.x < distToFaces.y && distToFaces.x < distToFaces.z) {
            localNormal = vec3<f32>(sign(localSpherePos.x), 0.0, 0.0);
            (*contact).penetration = radius + distToFaces.x;
        } else if (distToFaces.y < distToFaces.z) {
            localNormal = vec3<f32>(0.0, sign(localSpherePos.y), 0.0);
            (*contact).penetration = radius + distToFaces.y;
        } else {
            localNormal = vec3<f32>(0.0, 0.0, sign(localSpherePos.z));
            (*contact).penetration = radius + distToFaces.z;
        }
    } else {
        localNormal = diff / dist;
        (*contact).penetration = radius - dist;
    }
    
    // Transform normal to world space
    (*contact).normal = quatRotate(box.rotation, localNormal);
    
    // Contact point in world space (on sphere surface)
    (*contact).contactPoint = sphere.position - (*contact).normal * radius;
    
    return true;
}

// ============================================================================
// Utility functions
// ============================================================================

// Transform point to local space of body
fn transformToLocal(worldPoint: vec3<f32>, bodyPos: vec3<f32>, bodyRot: vec4<f32>) -> vec3<f32> {
    let relative = worldPoint - bodyPos;
    // Rotate by inverse quaternion (conjugate for unit quaternion)
    let invRot = vec4<f32>(-bodyRot.x, -bodyRot.y, -bodyRot.z, bodyRot.w);
    return quatRotate(invRot, relative);
}

// Rotate vector by quaternion
fn quatRotate(q: vec4<f32>, v: vec3<f32>) -> vec3<f32> {
    let qv = vec3<f32>(q.x, q.y, q.z);
    let uv = cross(qv, v);
    let uuv = cross(qv, uv);
    return v + ((uv * q.w) + uuv) * 2.0;
}

// Compute orthonormal tangent basis from normal
fn computeTangents(normal: vec3<f32>) -> array<vec3<f32>, 2> {
    var tangent1: vec3<f32>;
    
    // Choose reference vector that's not parallel to normal
    if (abs(normal.x) < 0.9) {
        tangent1 = cross(normal, vec3<f32>(1.0, 0.0, 0.0));
    } else {
        tangent1 = cross(normal, vec3<f32>(0.0, 1.0, 0.0));
    }
    tangent1 = normalize(tangent1);
    let tangent2 = cross(normal, tangent1);
    
    return array<vec3<f32>, 2>(tangent1, tangent2);
}

// ============================================================================
// Ground plane collision (special case for efficiency)
// ============================================================================
@compute @workgroup_size(64)
fn testGroundCollisions(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= params.numBodies) {
        return;
    }
    
    let body = bodies[idx];
    
    // Skip static bodies
    if ((body.flags & 1u) != 0u) {
        return;
    }
    
    var contact: Contact;
    var hasContact = false;
    
    // Ground at y = 0
    let groundY = 0.0;
    
    if (body.colliderType == COLLIDER_SPHERE) {
        let radius = body.colliderData.x;
        let bottomY = body.position.y - radius;
        
        if (bottomY < groundY) {
            contact.normal = vec3<f32>(0.0, 1.0, 0.0);
            contact.penetration = groundY - bottomY;
            contact.contactPoint = vec3<f32>(body.position.x, groundY, body.position.z);
            hasContact = true;
        }
    } else if (body.colliderType == COLLIDER_AABB) {
        let halfExtents = body.colliderData.xyz;
        let worldHalf = transformHalfExtents(halfExtents, body.rotation);
        let bottomY = body.position.y - worldHalf.y;
        
        if (bottomY < groundY) {
            contact.normal = vec3<f32>(0.0, 1.0, 0.0);
            contact.penetration = groundY - bottomY;
            contact.contactPoint = vec3<f32>(body.position.x, groundY, body.position.z);
            hasContact = true;
        }
    }
    
    if (hasContact) {
        contact.bodyA = idx;
        contact.bodyB = 0xFFFFFFFFu;  // Special value for ground
        contact.flags = CONTACT_VALID;
        
        let tangents = computeTangents(contact.normal);
        contact.tangent1 = tangents[0];
        contact.tangent2 = tangents[1];
        contact.normalImpulse = 0.0;
        contact.tangentImpulse1 = 0.0;
        contact.tangentImpulse2 = 0.0;
        
        let contactIdx = atomicAdd(&contactCount, 1u);
        if (contactIdx < arrayLength(&contacts)) {
            contacts[contactIdx] = contact;
        }
    }
}
