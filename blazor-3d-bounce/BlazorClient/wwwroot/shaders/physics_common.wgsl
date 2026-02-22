// ============================================================================
// Physics Engine Common Types and Constants
// ============================================================================

// Collider types
const COLLIDER_SPHERE: u32 = 0u;
const COLLIDER_AABB: u32 = 1u;
const COLLIDER_CAPSULE: u32 = 2u;

// Body flags
const FLAG_STATIC: u32 = 1u;
const FLAG_SLEEPING: u32 = 2u;
const FLAG_CCD_ENABLED: u32 = 4u;

// Numerical constants
const EPSILON: f32 = 1e-6;
const PI: f32 = 3.14159265359;

// Physics parameters (can be overridden via uniform)
const BAUMGARTE_BIAS: f32 = 0.2;
const SLOP_THRESHOLD: f32 = 0.005;
const MAX_CORRECTION: f32 = 0.2;
const VELOCITY_SLEEP_THRESHOLD: f32 = 0.01;

// Rigid body structure - 112 bytes, 16-byte aligned
struct RigidBody {
    position: vec3<f32>,        // 12 bytes
    inverseMass: f32,           // 4 bytes
    rotation: vec4<f32>,        // 16 bytes
    linearVelocity: vec3<f32>,  // 12 bytes
    restitution: f32,           // 4 bytes
    angularVelocity: vec3<f32>, // 12 bytes
    friction: f32,              // 4 bytes
    inverseInertia: vec3<f32>,  // 12 bytes
    colliderType: u32,          // 4 bytes
    colliderData: vec4<f32>,    // 16 bytes (radius for sphere, half-extents for AABB)
    linearDamping: f32,         // 4 bytes
    angularDamping: f32,        // 4 bytes
    flags: u32,                 // 4 bytes
    _padding: f32,              // 4 bytes (alignment)
}

// Contact structure - 64 bytes
struct Contact {
    bodyA: u32,
    bodyB: u32,
    flags: u32,                 // valid, etc.
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

// Simulation parameters
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

// AABB for broad phase
struct AABB {
    min: vec3<f32>,
    max: vec3<f32>,
}

// ============================================================================
// Utility Functions
// ============================================================================

// Quaternion multiplication
fn quatMul(a: vec4<f32>, b: vec4<f32>) -> vec4<f32> {
    return vec4<f32>(
        a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
        a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
        a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w,
        a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z
    );
}

// Rotate vector by quaternion
fn quatRotate(q: vec4<f32>, v: vec3<f32>) -> vec3<f32> {
    let qv = vec3<f32>(q.x, q.y, q.z);
    let uv = cross(qv, v);
    let uuv = cross(qv, uv);
    return v + ((uv * q.w) + uuv) * 2.0;
}

// Quaternion from angular velocity (for integration)
fn quatFromAngularVelocity(omega: vec3<f32>) -> vec4<f32> {
    return vec4<f32>(omega.x, omega.y, omega.z, 0.0);
}

// Safe normalize
fn safeNormalize(v: vec3<f32>) -> vec3<f32> {
    let len = length(v);
    if (len < EPSILON) {
        return vec3<f32>(0.0, 1.0, 0.0);
    }
    return v / len;
}

// Compute AABB from body
fn computeAABB(body: RigidBody) -> AABB {
    var aabb: AABB;
    
    if (body.colliderType == COLLIDER_SPHERE) {
        let radius = body.colliderData.x;
        aabb.min = body.position - vec3<f32>(radius);
        aabb.max = body.position + vec3<f32>(radius);
    } else if (body.colliderType == COLLIDER_AABB) {
        // Half-extents stored in colliderData.xyz
        let halfExtents = body.colliderData.xyz;
        // Rotate half-extents (for rotated AABBs, compute world-aligned bounds)
        let rx = abs(quatRotate(body.rotation, vec3<f32>(halfExtents.x, 0.0, 0.0)));
        let ry = abs(quatRotate(body.rotation, vec3<f32>(0.0, halfExtents.y, 0.0)));
        let rz = abs(quatRotate(body.rotation, vec3<f32>(0.0, 0.0, halfExtents.z)));
        let worldHalfExtents = rx + ry + rz;
        aabb.min = body.position - worldHalfExtents;
        aabb.max = body.position + worldHalfExtents;
    } else {
        // Capsule or default
        let radius = body.colliderData.x;
        let halfHeight = body.colliderData.y;
        let extent = max(radius, halfHeight + radius);
        aabb.min = body.position - vec3<f32>(extent);
        aabb.max = body.position + vec3<f32>(extent);
    }
    
    return aabb;
}

// Check AABB overlap
fn aabbOverlap(a: AABB, b: AABB) -> bool {
    return all(a.min <= b.max) && all(b.min <= a.max);
}

// Compute orthonormal basis from normal
fn computeTangents(normal: vec3<f32>) -> array<vec3<f32>, 2> {
    var tangent1: vec3<f32>;
    
    if (abs(normal.x) < 0.9) {
        tangent1 = cross(normal, vec3<f32>(1.0, 0.0, 0.0));
    } else {
        tangent1 = cross(normal, vec3<f32>(0.0, 1.0, 0.0));
    }
    tangent1 = normalize(tangent1);
    let tangent2 = cross(normal, tangent1);
    
    return array<vec3<f32>, 2>(tangent1, tangent2);
}
