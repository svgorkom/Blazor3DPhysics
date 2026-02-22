// ============================================================================
// Broad Phase - Spatial Hash Grid
// ============================================================================
// Uses a uniform grid with spatial hashing for O(n) broad phase collision
// detection on GPU. Avoids atomics where possible using counting sort.

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

struct AABB {
    min: vec3<f32>,
    _pad0: f32,
    max: vec3<f32>,
    _pad1: f32,
}

// Candidate collision pair
struct CollisionPair {
    bodyA: u32,
    bodyB: u32,
}

// Grid parameters
const MAX_BODIES_PER_CELL: u32 = 32u;
const HASH_TABLE_SIZE: u32 = 65536u;  // 64K cells

// Bindings
@group(0) @binding(0) var<storage, read> bodies: array<RigidBody>;
@group(0) @binding(1) var<uniform> params: SimParams;
@group(0) @binding(2) var<storage, read_write> cellCounts: array<atomic<u32>>;
@group(0) @binding(3) var<storage, read_write> cellBodies: array<u32>;  // Flat array: [cell0_bodies..., cell1_bodies..., ...]
@group(0) @binding(4) var<storage, read_write> cellOffsets: array<u32>;  // Start index for each cell in cellBodies
@group(0) @binding(5) var<storage, read_write> pairs: array<CollisionPair>;
@group(0) @binding(6) var<storage, read_write> pairCount: atomic<u32>;
@group(0) @binding(7) var<storage, read_write> bodyAABBs: array<AABB>;

// Constants
const FLAG_STATIC: u32 = 1u;
const COLLIDER_SPHERE: u32 = 0u;
const COLLIDER_AABB: u32 = 1u;

// ============================================================================
// Hash function for spatial grid
// ============================================================================
fn hashCell(cellX: i32, cellY: i32, cellZ: i32) -> u32 {
    // Large primes for spatial hashing
    let prime1 = 73856093u;
    let prime2 = 19349663u;
    let prime3 = 83492791u;
    
    // Handle negative coordinates
    let x = u32(cellX + 10000);
    let y = u32(cellY + 10000);
    let z = u32(cellZ + 10000);
    
    return ((x * prime1) ^ (y * prime2) ^ (z * prime3)) % HASH_TABLE_SIZE;
}

// ============================================================================
// Compute AABB for a body
// ============================================================================
fn computeBodyAABB(body: RigidBody) -> AABB {
    var aabb: AABB;
    
    if (body.colliderType == COLLIDER_SPHERE) {
        let radius = body.colliderData.x;
        aabb.min = body.position - vec3<f32>(radius);
        aabb.max = body.position + vec3<f32>(radius);
    } else if (body.colliderType == COLLIDER_AABB) {
        let halfExtents = body.colliderData.xyz;
        // For rotated AABBs, compute world-space bounding box
        let q = body.rotation;
        
        // Rotate each axis and take absolute values
        let rx = abs(quatRotateVec(q, vec3<f32>(halfExtents.x, 0.0, 0.0)));
        let ry = abs(quatRotateVec(q, vec3<f32>(0.0, halfExtents.y, 0.0)));
        let rz = abs(quatRotateVec(q, vec3<f32>(0.0, 0.0, halfExtents.z)));
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

// Helper: Rotate vector by quaternion
fn quatRotateVec(q: vec4<f32>, v: vec3<f32>) -> vec3<f32> {
    let qv = vec3<f32>(q.x, q.y, q.z);
    let uv = cross(qv, v);
    let uuv = cross(qv, uv);
    return v + ((uv * q.w) + uuv) * 2.0;
}

// ============================================================================
// Phase 1: Compute AABBs and count cells
// ============================================================================
@compute @workgroup_size(64)
fn computeAABBs(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= params.numBodies) {
        return;
    }
    
    let body = bodies[idx];
    let aabb = computeBodyAABB(body);
    bodyAABBs[idx] = aabb;
}

// ============================================================================
// Phase 2: Clear cell counts
// ============================================================================
@compute @workgroup_size(256)
fn clearCells(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= HASH_TABLE_SIZE) {
        return;
    }
    
    atomicStore(&cellCounts[idx], 0u);
}

// ============================================================================
// Phase 3: Count bodies per cell (atomic increment)
// ============================================================================
@compute @workgroup_size(64)
fn countBodiesPerCell(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= params.numBodies) {
        return;
    }
    
    let aabb = bodyAABBs[idx];
    let cellSize = params.gridCellSize;
    
    // Get cell range for this AABB
    let minCell = vec3<i32>(floor(aabb.min / cellSize));
    let maxCell = vec3<i32>(floor(aabb.max / cellSize));
    
    // Increment count for each overlapping cell
    for (var z = minCell.z; z <= maxCell.z; z = z + 1) {
        for (var y = minCell.y; y <= maxCell.y; y = y + 1) {
            for (var x = minCell.x; x <= maxCell.x; x = x + 1) {
                let cellHash = hashCell(x, y, z);
                atomicAdd(&cellCounts[cellHash], 1u);
            }
        }
    }
}

// ============================================================================
// Phase 4: Prefix sum on cell counts (parallel scan)
// ============================================================================
// Note: This is a simplified single-pass scan. For large grids, use
// Blelloch or Hillis-Steele algorithm with multiple passes.

var<workgroup> sharedData: array<u32, 256>;

@compute @workgroup_size(256)
fn prefixSumLocal(
    @builtin(global_invocation_id) globalId: vec3<u32>,
    @builtin(local_invocation_id) localId: vec3<u32>,
    @builtin(workgroup_id) workgroupId: vec3<u32>
) {
    let tid = localId.x;
    let gid = globalId.x;
    
    // Load data into shared memory
    if (gid < HASH_TABLE_SIZE) {
        sharedData[tid] = atomicLoad(&cellCounts[gid]);
    } else {
        sharedData[tid] = 0u;
    }
    
    workgroupBarrier();
    
    // Up-sweep (reduce) phase
    var offset = 1u;
    for (var d = 128u; d > 0u; d = d >> 1u) {
        if (tid < d) {
            let ai = offset * (2u * tid + 1u) - 1u;
            let bi = offset * (2u * tid + 2u) - 1u;
            if (bi < 256u) {
                sharedData[bi] = sharedData[bi] + sharedData[ai];
            }
        }
        offset = offset * 2u;
        workgroupBarrier();
    }
    
    // Clear last element
    if (tid == 0u) {
        sharedData[255] = 0u;
    }
    workgroupBarrier();
    
    // Down-sweep phase
    for (var d = 1u; d < 256u; d = d * 2u) {
        offset = offset >> 1u;
        if (tid < d) {
            let ai = offset * (2u * tid + 1u) - 1u;
            let bi = offset * (2u * tid + 2u) - 1u;
            if (bi < 256u) {
                let temp = sharedData[ai];
                sharedData[ai] = sharedData[bi];
                sharedData[bi] = sharedData[bi] + temp;
            }
        }
        workgroupBarrier();
    }
    
    // Write back exclusive prefix sum
    if (gid < HASH_TABLE_SIZE) {
        cellOffsets[gid] = sharedData[tid];
    }
}

// ============================================================================
// Phase 5: Insert bodies into cells
// ============================================================================
@compute @workgroup_size(64)
fn insertBodiesIntoCells(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= params.numBodies) {
        return;
    }
    
    let aabb = bodyAABBs[idx];
    let cellSize = params.gridCellSize;
    
    let minCell = vec3<i32>(floor(aabb.min / cellSize));
    let maxCell = vec3<i32>(floor(aabb.max / cellSize));
    
    for (var z = minCell.z; z <= maxCell.z; z = z + 1) {
        for (var y = minCell.y; y <= maxCell.y; y = y + 1) {
            for (var x = minCell.x; x <= maxCell.x; x = x + 1) {
                let cellHash = hashCell(x, y, z);
                let offset = cellOffsets[cellHash];
                let localIdx = atomicAdd(&cellCounts[cellHash], 1u);
                
                // Bounds check
                let writeIdx = offset + localIdx;
                if (writeIdx < arrayLength(&cellBodies) && localIdx < MAX_BODIES_PER_CELL) {
                    cellBodies[writeIdx] = idx;
                }
            }
        }
    }
}

// ============================================================================
// Phase 6: Generate collision pairs from grid
// ============================================================================
@compute @workgroup_size(64)
fn generatePairs(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= params.numBodies) {
        return;
    }
    
    let bodyA = bodies[idx];
    let aabbA = bodyAABBs[idx];
    let cellSize = params.gridCellSize;
    
    // Skip static-static pairs (both static = no collision response needed)
    let isStaticA = (bodyA.flags & FLAG_STATIC) != 0u;
    
    let minCell = vec3<i32>(floor(aabbA.min / cellSize));
    let maxCell = vec3<i32>(floor(aabbA.max / cellSize));
    
    // Check all cells this body overlaps
    for (var z = minCell.z; z <= maxCell.z; z = z + 1) {
        for (var y = minCell.y; y <= maxCell.y; y = y + 1) {
            for (var x = minCell.x; x <= maxCell.x; x = x + 1) {
                let cellHash = hashCell(x, y, z);
                let offset = cellOffsets[cellHash];
                let count = atomicLoad(&cellCounts[cellHash]) - offset;  // Actual count after insertion
                
                // Check all bodies in this cell
                for (var i = 0u; i < min(count, MAX_BODIES_PER_CELL); i = i + 1u) {
                    let otherIdx = cellBodies[offset + i];
                    
                    // Skip self and ensure we only create each pair once (A < B)
                    if (otherIdx <= idx) {
                        continue;
                    }
                    
                    let bodyB = bodies[otherIdx];
                    let isStaticB = (bodyB.flags & FLAG_STATIC) != 0u;
                    
                    // Skip static-static pairs
                    if (isStaticA && isStaticB) {
                        continue;
                    }
                    
                    // AABB overlap test
                    let aabbB = bodyAABBs[otherIdx];
                    if (aabbOverlapTest(aabbA, aabbB)) {
                        // Add to pair list
                        let pairIdx = atomicAdd(&pairCount, 1u);
                        if (pairIdx < arrayLength(&pairs)) {
                            pairs[pairIdx] = CollisionPair(idx, otherIdx);
                        }
                    }
                }
            }
        }
    }
}

// AABB overlap test
fn aabbOverlapTest(a: AABB, b: AABB) -> bool {
    return all(a.min <= b.max) && all(b.min <= a.max);
}

// ============================================================================
// Alternative: Simple O(n²) broad phase for small object counts
// ============================================================================
// Use when numBodies < 256 to avoid grid overhead

@compute @workgroup_size(64)
fn bruteForceePairs(@builtin(global_invocation_id) globalId: vec3<u32>) {
    let idx = globalId.x;
    
    if (idx >= params.numBodies) {
        return;
    }
    
    let bodyA = bodies[idx];
    let aabbA = bodyAABBs[idx];
    let isStaticA = (bodyA.flags & FLAG_STATIC) != 0u;
    
    // Only check bodies with higher index to avoid duplicates
    for (var j = idx + 1u; j < params.numBodies; j = j + 1u) {
        let bodyB = bodies[j];
        let isStaticB = (bodyB.flags & FLAG_STATIC) != 0u;
        
        // Skip static-static
        if (isStaticA && isStaticB) {
            continue;
        }
        
        let aabbB = bodyAABBs[j];
        if (aabbOverlapTest(aabbA, aabbB)) {
            let pairIdx = atomicAdd(&pairCount, 1u);
            if (pairIdx < arrayLength(&pairs)) {
                pairs[pairIdx] = CollisionPair(idx, j);
            }
        }
    }
}
