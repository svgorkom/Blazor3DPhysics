/**
 * GPU Physics Compute Module
 * WebGPU-based physics simulation using compute shaders
 * 
 * This module provides high-performance physics computation on the GPU,
 * scaling to thousands of objects with stable impulse-based collision response.
 * 
 * BINDING SCHEME (consistent across all physics shaders):
 * 
 * Integration Pipeline (Group 0):
 *   @binding(0) - bodies (storage, read_write)
 *   @binding(1) - params (uniform)
 *   @binding(2) - externalForces (storage, read)
 * 
 * Broadphase Pipeline (Group 0):
 *   @binding(0) - bodies (storage, read)
 *   @binding(1) - params (uniform)
 *   @binding(2) - cellCounts (storage, read_write)
 *   @binding(3) - cellBodies (storage, read_write)
 *   @binding(4) - cellOffsets (storage, read_write)
 *   @binding(5) - pairs (storage, read_write)
 *   @binding(6) - pairCount (storage, read_write)
 *   @binding(7) - bodyAABBs (storage, read_write)
 * 
 * Narrowphase Pipeline (Group 0):
 *   @binding(0) - bodies (storage, read)
 *   @binding(1) - params (uniform)
 *   @binding(2) - pairs (storage, read)
 *   @binding(3) - pairCount (storage, read)
 *   @binding(4) - contacts (storage, read_write)
 *   @binding(5) - contactCount (storage, read_write)
 * 
 * Solver Pipeline (Group 0):
 *   @binding(0) - bodies (storage, read_write)
 *   @binding(1) - params (uniform)
 *   @binding(2) - contacts (storage, read_write)
 *   @binding(3) - contactCount (storage, read)
 */

(function() {
    'use strict';

    // ========================================
    // Constants
    // ========================================
    
    const COLLIDER_SPHERE = 0;
    const COLLIDER_AABB = 1;
    const COLLIDER_CAPSULE = 2;
    
    const FLAG_STATIC = 1;
    const FLAG_SLEEPING = 2;
    const FLAG_CCD_ENABLED = 4;
    
    // Buffer sizes
    const MAX_BODIES = 16384;
    const MAX_CONTACTS = 65536;
    const MAX_PAIRS = 65536;
    const HASH_TABLE_SIZE = 65536;
    const MAX_BODIES_PER_CELL = 32;
    
    // Struct sizes (bytes)
    const RIGID_BODY_SIZE = 112;  // Must match WGSL struct
    const CONTACT_SIZE = 80;       // Updated to match WGSL Contact struct (64 bytes + padding)
    const PAIR_SIZE = 8;
    const AABB_SIZE = 32;
    
    // Workgroup sizes
    const WORKGROUP_SIZE = 64;
    const LARGE_WORKGROUP_SIZE = 256;

    // ========================================
    // Module State
    // ========================================
    
    let device = null;
    let queue = null;
    let initialized = false;
    
    // Buffers
    let bodiesBuffer = null;
    let bodiesStagingBuffer = null;
    let contactsBuffer = null;
    let pairsBuffer = null;
    let aabbsBuffer = null;
    let cellCountsBuffer = null;
    let cellBodiesBuffer = null;
    let cellOffsetsBuffer = null;
    let pairCountBuffer = null;
    let contactCountBuffer = null;
    let paramsBuffer = null;
    let externalForcesBuffer = null;
    let readbackBuffer = null;
    
    // Pipelines
    let integratePipeline = null;
    let computeAABBsPipeline = null;
    let clearCellsPipeline = null;
    let countBodiesPipeline = null;
    let prefixSumPipeline = null;
    let insertBodiesPipeline = null;
    let generatePairsPipeline = null;
    let narrowPhasePipeline = null;
    let groundCollisionPipeline = null;
    let warmStartPipeline = null;
    let solveVelocitiesPipeline = null;
    let solvePositionsPipeline = null;
    let clearContactsPipeline = null;
    
    // Simulation state
    let simParams = {
        gravity: [0, -9.81, 0],
        deltaTime: 1/120,
        numBodies: 0,
        numContacts: 0,
        solverIterations: 8,
        enableCCD: 0,
        gridCellSize: 2.0,
        gridDimX: 64,
        gridDimY: 64,
        gridDimZ: 64
    };
    
    // Body data (CPU mirror for initialization)
    let bodyData = new Map();
    let bodyIndexMap = new Map();  // id -> buffer index
    let nextBodyIndex = 0;
    
    // Performance metrics
    let metrics = {
        lastStepTime: 0,
        broadphaseTime: 0,
        narrowphaseTime: 0,
        solverTime: 0,
        contactCount: 0,
        pairCount: 0
    };

    // ========================================
    // Shader Sources
    // ========================================
    
    // Shaders are loaded from external files
    let shaderSources = {};
    
    async function loadShaders() {
        const shaderFiles = [
            'physics_common.wgsl',
            'physics_integrate.wgsl',
            'physics_broadphase.wgsl',
            'physics_narrowphase.wgsl',
            'physics_solver.wgsl'
        ];
        
        for (const file of shaderFiles) {
            try {
                const response = await fetch(`shaders/${file}`);
                if (response.ok) {
                    shaderSources[file] = await response.text();
                } else {
                    console.warn(`Failed to load shader: ${file}`);
                }
            } catch (e) {
                console.error(`Error loading shader ${file}:`, e);
            }
        }
    }

    // ========================================
    // Initialization
    // ========================================
    
    async function initialize(settings) {
        console.log('GPUPhysicsModule.initialize called');
        
        if (initialized) {
            console.log('GPU physics already initialized');
            return true;
        }
        
        // Check WebGPU support
        if (!navigator.gpu) {
            console.warn('WebGPU not supported');
            return false;
        }
        
        try {
            // Request adapter and device
            const adapter = await navigator.gpu.requestAdapter({
                powerPreference: 'high-performance'
            });
            
            if (!adapter) {
                console.warn('No WebGPU adapter available');
                return false;
            }
            
            // Request device with optional limits - fall back to defaults if not available
            try {
                device = await adapter.requestDevice({
                    requiredLimits: {
                        maxStorageBufferBindingSize: Math.min(256 * 1024 * 1024, adapter.limits.maxStorageBufferBindingSize),
                        maxBufferSize: Math.min(256 * 1024 * 1024, adapter.limits.maxBufferSize)
                    }
                });
            } catch (limitsError) {
                console.warn('Could not request device with custom limits, using defaults:', limitsError);
                device = await adapter.requestDevice();
            }
            
            if (!device) {
                console.warn('Failed to get WebGPU device');
                return false;
            }
            
            // Add error handler for device lost
            device.lost.then((info) => {
                console.error('WebGPU device lost:', info.message);
                initialized = false;
            });
            
            // Add error handler for uncaptured errors
            device.onuncapturederror = (event) => {
                console.error('WebGPU uncaptured error:', event.error.message);
            };
            
            queue = device.queue;
            
            // Apply settings
            if (settings) {
                if (settings.gravity) simParams.gravity = settings.gravity;
                if (settings.timeStep) simParams.deltaTime = settings.timeStep;
                if (settings.subSteps) simParams.solverIterations = settings.subSteps * 4;
                if (settings.gridCellSize) simParams.gridCellSize = settings.gridCellSize;
            }
            
            // Load shaders
            await loadShaders();
            
            // Check if we have the required shaders
            if (!shaderSources['physics_integrate.wgsl']) {
                console.warn('Physics shaders not loaded - GPU physics unavailable');
                return false;
            }
            
            // Create buffers
            createBuffers();
            
            // Create pipelines
            await createPipelines();
            
            initialized = true;
            console.log('GPU physics initialized successfully');
            return true;
            
        } catch (e) {
            console.error('Failed to initialize GPU physics:', e);
            return false;
        }
    }
    
    function createBuffers() {
        // Bodies buffer (read/write storage)
        bodiesBuffer = device.createBuffer({
            size: MAX_BODIES * RIGID_BODY_SIZE,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST | GPUBufferUsage.COPY_SRC,
            label: 'Bodies Buffer'
        });
        
        // Staging buffer for CPU upload
        bodiesStagingBuffer = device.createBuffer({
            size: MAX_BODIES * RIGID_BODY_SIZE,
            usage: GPUBufferUsage.MAP_WRITE | GPUBufferUsage.COPY_SRC,
            label: 'Bodies Staging Buffer'
        });
        
        // Contacts buffer
        contactsBuffer = device.createBuffer({
            size: MAX_CONTACTS * CONTACT_SIZE,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_SRC,
            label: 'Contacts Buffer'
        });
        
        // Collision pairs buffer
        pairsBuffer = device.createBuffer({
            size: MAX_PAIRS * PAIR_SIZE,
            usage: GPUBufferUsage.STORAGE,
            label: 'Pairs Buffer'
        });
        
        // AABBs buffer
        aabbsBuffer = device.createBuffer({
            size: MAX_BODIES * AABB_SIZE,
            usage: GPUBufferUsage.STORAGE,
            label: 'AABBs Buffer'
        });
        
        // Spatial hash grid buffers
        cellCountsBuffer = device.createBuffer({
            size: HASH_TABLE_SIZE * 4,
            usage: GPUBufferUsage.STORAGE,
            label: 'Cell Counts Buffer'
        });
        
        cellBodiesBuffer = device.createBuffer({
            size: HASH_TABLE_SIZE * MAX_BODIES_PER_CELL * 4,
            usage: GPUBufferUsage.STORAGE,
            label: 'Cell Bodies Buffer'
        });
        
        cellOffsetsBuffer = device.createBuffer({
            size: HASH_TABLE_SIZE * 4,
            usage: GPUBufferUsage.STORAGE,
            label: 'Cell Offsets Buffer'
        });
        
        // Atomic counters
        pairCountBuffer = device.createBuffer({
            size: 4,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST | GPUBufferUsage.COPY_SRC,
            label: 'Pair Count Buffer'
        });
        
        contactCountBuffer = device.createBuffer({
            size: 4,
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST | GPUBufferUsage.COPY_SRC,
            label: 'Contact Count Buffer'
        });
        
        // Simulation parameters (uniform)
        paramsBuffer = device.createBuffer({
            size: 64,  // Padded to 16-byte alignment
            usage: GPUBufferUsage.UNIFORM | GPUBufferUsage.COPY_DST,
            label: 'Params Buffer'
        });
        
        // External forces (optional)
        externalForcesBuffer = device.createBuffer({
            size: MAX_BODIES * 16,  // vec3 + padding per body
            usage: GPUBufferUsage.STORAGE | GPUBufferUsage.COPY_DST,
            label: 'External Forces Buffer'
        });
        
        // Readback buffer for getting results back to CPU
        readbackBuffer = device.createBuffer({
            size: MAX_BODIES * RIGID_BODY_SIZE,
            usage: GPUBufferUsage.MAP_READ | GPUBufferUsage.COPY_DST,
            label: 'Readback Buffer'
        });
    }
    
    async function createPipelines() {
        // Helper to create shader module with error checking
        async function createShaderWithLogging(code, label) {
            const module = device.createShaderModule({ code, label });
            
            // Wait for and log any compilation errors
            try {
                const info = await module.getCompilationInfo();
                for (const message of info.messages) {
                    const prefix = `${label} (line ${message.lineNum}:${message.linePos})`;
                    if (message.type === 'error') {
                        console.error(`${prefix}: ${message.message}`);
                    } else if (message.type === 'warning') {
                        console.warn(`${prefix}: ${message.message}`);
                    }
                }
            } catch (e) {
                console.warn(`Could not get compilation info for ${label}:`, e);
            }
            
            return module;
        }
        
        // Integration pipeline
        if (shaderSources['physics_integrate.wgsl']) {
            try {
                const module = await createShaderWithLogging(
                    shaderSources['physics_integrate.wgsl'],
                    'Integrate Shader'
                );
                
                integratePipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'main' },
                    label: 'Integrate Pipeline'
                });
            } catch (e) {
                console.error('Failed to create integrate pipeline:', e);
            }
        }
        
        // Broadphase pipelines
        if (shaderSources['physics_broadphase.wgsl']) {
            try {
                const module = await createShaderWithLogging(
                    shaderSources['physics_broadphase.wgsl'],
                    'Broadphase Shader'
                );
                
                computeAABBsPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'computeAABBs' },
                    label: 'Compute AABBs Pipeline'
                });
                
                clearCellsPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'clearCells' },
                    label: 'Clear Cells Pipeline'
                });
                
                countBodiesPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'countBodiesPerCell' },
                    label: 'Count Bodies Pipeline'
                });
                
                generatePairsPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'generatePairs' },
                    label: 'Generate Pairs Pipeline'
                });
            } catch (e) {
                console.error('Failed to create broadphase pipelines:', e);
            }
        }
        
        // Narrowphase pipeline
        if (shaderSources['physics_narrowphase.wgsl']) {
            try {
                const module = await createShaderWithLogging(
                    shaderSources['physics_narrowphase.wgsl'],
                    'Narrowphase Shader'
                );
                
                narrowPhasePipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'main' },
                    label: 'Narrowphase Pipeline'
                });
                
                groundCollisionPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'testGroundCollisions' },
                    label: 'Ground Collision Pipeline'
                });
            } catch (e) {
                console.error('Failed to create narrowphase pipelines:', e);
            }
        }
        
        // Solver pipelines
        if (shaderSources['physics_solver.wgsl']) {
            try {
                const module = await createShaderWithLogging(
                    shaderSources['physics_solver.wgsl'],
                    'Solver Shader'
                );
                
                warmStartPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'warmStart' },
                    label: 'Warm Start Pipeline'
                });
                
                solveVelocitiesPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'solveVelocities' },
                    label: 'Solve Velocities Pipeline'
                });
                
                solvePositionsPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'solvePositions' },
                    label: 'Solve Positions Pipeline'
                });
                
                clearContactsPipeline = device.createComputePipeline({
                    layout: 'auto',
                    compute: { module, entryPoint: 'clearContacts' },
                    label: 'Clear Contacts Pipeline'
                });
                
                console.log('All solver pipelines created successfully');
            } catch (e) {
                console.error('Failed to create solver pipelines:', e);
            }
        }
    }

    // ========================================
    // Helper: Safe Bind Group Creation
    // ========================================
    
    function createBindGroupSafe(pipeline, entries, label) {
        try {
            return device.createBindGroup({
                layout: pipeline.getBindGroupLayout(0),
                entries: entries,
                label: label
            });
        } catch (e) {
            console.error(`Failed to create bind group '${label}':`, e.message);
            console.error('Entries:', entries.map(e => `binding ${e.binding}`).join(', '));
            return null;
        }
    }

    // ========================================
    // Body Management
    // ========================================
    
    function createRigidBody(data = {}) {
        if (!initialized) {
            console.warn('GPU physics not initialized');
            return;
        }
        
        const id = data.id;
        const index = nextBodyIndex++;
        bodyIndexMap.set(id, index);
        
        // Convert to GPU format
        const body = {
            position: data.position || [0, 5, 0],
            inverseMass: data.isStatic ? 0 : (1.0 / (data.mass || 1.0)),
            rotation: data.rotation || [0, 0, 0, 1],
            linearVelocity: data.linearVelocity || [0, 0, 0],
            restitution: data.restitution || 0.5,
            angularVelocity: data.angularVelocity || [0, 0, 0],
            friction: data.staticFriction || 0.5,
            inverseInertia: computeInverseInertia(data),
            colliderType: getColliderType(data.primitiveType),
            colliderData: getColliderData(data),
            linearDamping: data.linearDamping || 0.01,
            angularDamping: data.angularDamping || 0.01,
            flags: (data.isStatic ? FLAG_STATIC : 0) | (data.enableCCD ? FLAG_CCD_ENABLED : 0)
        };
        
        bodyData.set(id, body);
        simParams.numBodies = bodyData.size;
        
        // Upload to GPU
        uploadBody(index, body);
    }
    
    function removeRigidBody(id) {
        const index = bodyIndexMap.get(id);
        if (index === undefined) return;
        
        // Mark as invalid (infinite mass, zero velocity)
        const body = bodyData.get(id);
        if (body) {
            body.inverseMass = 0;
            body.linearVelocity = [0, 0, 0];
            body.flags = FLAG_STATIC | FLAG_SLEEPING;
            uploadBody(index, body);
        }
        
        bodyData.delete(id);
        bodyIndexMap.delete(id);
        
        // Note: We don't compact the buffer - indices are stable
        // This could be optimized with a free list
    }
    
    function uploadBody(index, body) {
        const data = new Float32Array(RIGID_BODY_SIZE / 4);
        let offset = 0;
        
        // Position (vec3) + inverseMass (f32)
        data[offset++] = body.position[0];
        data[offset++] = body.position[1];
        data[offset++] = body.position[2];
        data[offset++] = body.inverseMass;
        
        // Rotation (vec4)
        data[offset++] = body.rotation[0];
        data[offset++] = body.rotation[1];
        data[offset++] = body.rotation[2];
        data[offset++] = body.rotation[3];
        
        // Linear velocity (vec3) + restitution
        data[offset++] = body.linearVelocity[0];
        data[offset++] = body.linearVelocity[1];
        data[offset++] = body.linearVelocity[2];
        data[offset++] = body.restitution;
        
        // Angular velocity (vec3) + friction
        data[offset++] = body.angularVelocity[0];
        data[offset++] = body.angularVelocity[1];
        data[offset++] = body.angularVelocity[2];
        data[offset++] = body.friction;
        
        // Inverse inertia (vec3) + collider type
        data[offset++] = body.inverseInertia[0];
        data[offset++] = body.inverseInertia[1];
        data[offset++] = body.inverseInertia[2];
        new Uint32Array(data.buffer, offset * 4, 1)[0] = body.colliderType;
        offset++;
        
        // Collider data (vec4)
        data[offset++] = body.colliderData[0];
        data[offset++] = body.colliderData[1];
        data[offset++] = body.colliderData[2];
        data[offset++] = body.colliderData[3];
        
        // Damping + flags + padding
        data[offset++] = body.linearDamping;
        data[offset++] = body.angularDamping;
        new Uint32Array(data.buffer, offset * 4, 1)[0] = body.flags;
        offset++;
        data[offset++] = 0;  // padding
        
        // Write to GPU
        queue.writeBuffer(bodiesBuffer, index * RIGID_BODY_SIZE, data);
    }
    
    function getColliderType(primitiveType) {
        switch (primitiveType?.toLowerCase()) {
            case 'sphere': return COLLIDER_SPHERE;
            case 'box': return COLLIDER_AABB;
            case 'capsule': return COLLIDER_CAPSULE;
            default: return COLLIDER_SPHERE;
        }
    }
    
    function getColliderData(data) {
        const scale = data.scale || [1, 1, 1];
        switch (data.primitiveType?.toLowerCase()) {
            case 'sphere':
                return [scale[0] * 0.5, 0, 0, 0];  // radius
            case 'box':
                return [scale[0] * 0.5, scale[1] * 0.5, scale[2] * 0.5, 0];  // half-extents
            case 'capsule':
                return [scale[0] * 0.5, scale[1] * 0.5, 0, 0];  // radius, half-height
            default:
                return [scale[0] * 0.5, 0, 0, 0];
        }
    }
    
    function computeInverseInertia(data) {
        const mass = data.isStatic ? 0 : (data.mass || 1.0);
        if (mass === 0) return [0, 0, 0];
        
        const scale = data.scale || [1, 1, 1];
        
        switch (data.primitiveType?.toLowerCase()) {
            case 'sphere': {
                const r = scale[0] * 0.5;
                const I = 0.4 * mass * r * r;
                const invI = 1.0 / I;
                return [invI, invI, invI];
            }
            case 'box': {
                const w = scale[0], h = scale[1], d = scale[2];
                const Ix = (mass / 12.0) * (h*h + d*d);
                const Iy = (mass / 12.0) * (w*w + d*d);
                const Iz = (mass / 12.0) * (w*w + h*h);
                return [1.0/Ix, 1.0/Iy, 1.0/Iz];
            }
            default:
                return [1.0/mass, 1.0/mass, 1.0/mass];
        }
    }

    // ========================================
    // Simulation Step
    // ========================================
    
    async function step(deltaTime) {
        if (!initialized || simParams.numBodies === 0) {
            return;
        }
        
        const startTime = performance.now();
        
        simParams.deltaTime = deltaTime || simParams.deltaTime;
        
        // Upload simulation parameters
        uploadParams();
        
        // Reset counters
        queue.writeBuffer(pairCountBuffer, 0, new Uint32Array([0]));
        queue.writeBuffer(contactCountBuffer, 0, new Uint32Array([0]));
        
        // Create command encoder
        const commandEncoder = device.createCommandEncoder({ label: 'Physics Step' });
        
        // --- Integration Pass ---
        // The integrate shader declares: bodies(0), params(1), externalForces(2)
        if (integratePipeline) {
            const pass = commandEncoder.beginComputePass({ label: 'Integration' });
            pass.setPipeline(integratePipeline);
            
            const bindGroup = createBindGroupSafe(integratePipeline, [
                { binding: 0, resource: { buffer: bodiesBuffer } },
                { binding: 1, resource: { buffer: paramsBuffer } },
                { binding: 2, resource: { buffer: externalForcesBuffer } }
            ], 'Integrate Bind Group');
            
            if (bindGroup) {
                pass.setBindGroup(0, bindGroup);
                pass.dispatchWorkgroups(Math.ceil(simParams.numBodies / WORKGROUP_SIZE));
            }
            pass.end();
        }
        
        // --- Broad Phase ---
        const broadphaseStart = performance.now();
        
        // computeAABBs shader uses: bodies(0), params(1), bodyAABBs(7)
        // Note: Shader declares binding 7 for bodyAABBs
        if (computeAABBsPipeline) {
            const pass = commandEncoder.beginComputePass({ label: 'Compute AABBs' });
            pass.setPipeline(computeAABBsPipeline);
            
            const bindGroup = createBindGroupSafe(computeAABBsPipeline, [
                { binding: 0, resource: { buffer: bodiesBuffer } },
                { binding: 1, resource: { buffer: paramsBuffer } },
                { binding: 7, resource: { buffer: aabbsBuffer } }
            ], 'Compute AABBs Bind Group');
            
            if (bindGroup) {
                pass.setBindGroup(0, bindGroup);
                pass.dispatchWorkgroups(Math.ceil(simParams.numBodies / WORKGROUP_SIZE));
            }
            pass.end();
        }
        
        // clearCells shader uses only: cellCounts(2)
        if (clearCellsPipeline) {
            const pass = commandEncoder.beginComputePass({ label: 'Clear Cells' });
            pass.setPipeline(clearCellsPipeline);
            
            const bindGroup = createBindGroupSafe(clearCellsPipeline, [
                { binding: 2, resource: { buffer: cellCountsBuffer } }
            ], 'Clear Cells Bind Group');
            
            if (bindGroup) {
                pass.setBindGroup(0, bindGroup);
                pass.dispatchWorkgroups(Math.ceil(HASH_TABLE_SIZE / LARGE_WORKGROUP_SIZE));
            }
            pass.end();
        }
        
        // countBodiesPerCell shader uses: params(1), cellCounts(2), bodyAABBs(7)
        // Note: params is accessed for params.numBodies and params.gridCellSize
        if (countBodiesPipeline) {
            const pass = commandEncoder.beginComputePass({ label: 'Count Bodies' });
            pass.setPipeline(countBodiesPipeline);
            
            const bindGroup = createBindGroupSafe(countBodiesPipeline, [
                { binding: 1, resource: { buffer: paramsBuffer } },
                { binding: 2, resource: { buffer: cellCountsBuffer } },
                { binding: 7, resource: { buffer: aabbsBuffer } }
            ], 'Count Bodies Bind Group');
            
            if (bindGroup) {
                pass.setBindGroup(0, bindGroup);
                pass.dispatchWorkgroups(Math.ceil(simParams.numBodies / WORKGROUP_SIZE));
            }
            pass.end();
        }
        
        // generatePairs shader uses all broadphase bindings:
        // bodies(0), params(1), cellCounts(2), cellBodies(3), cellOffsets(4), pairs(5), pairCount(6), bodyAABBs(7)
        if (generatePairsPipeline) {
            const pass = commandEncoder.beginComputePass({ label: 'Generate Pairs' });
            pass.setPipeline(generatePairsPipeline);
            
            const bindGroup = createBindGroupSafe(generatePairsPipeline, [
                { binding: 0, resource: { buffer: bodiesBuffer } },
                { binding: 1, resource: { buffer: paramsBuffer } },
                { binding: 2, resource: { buffer: cellCountsBuffer } },
                { binding: 3, resource: { buffer: cellBodiesBuffer } },
                { binding: 4, resource: { buffer: cellOffsetsBuffer } },
                { binding: 5, resource: { buffer: pairsBuffer } },
                { binding: 6, resource: { buffer: pairCountBuffer } },
                { binding: 7, resource: { buffer: aabbsBuffer } }
            ], 'Generate Pairs Bind Group');
            
            if (bindGroup) {
                pass.setBindGroup(0, bindGroup);
                pass.dispatchWorkgroups(Math.ceil(simParams.numBodies / WORKGROUP_SIZE));
            }
            pass.end();
        }
        
        metrics.broadphaseTime = performance.now() - broadphaseStart;
        
        // --- Ground Collisions ---
        // testGroundCollisions shader uses: bodies(0), params(1), contacts(4), contactCount(5)
        // Note: This entry point uses params.numBodies, so binding 1 IS needed
        if (groundCollisionPipeline) {
            const pass = commandEncoder.beginComputePass({ label: 'Ground Collisions' });
            pass.setPipeline(groundCollisionPipeline);
            
            const bindGroup = createBindGroupSafe(groundCollisionPipeline, [
                { binding: 0, resource: { buffer: bodiesBuffer } },
                { binding: 1, resource: { buffer: paramsBuffer } },
                { binding: 4, resource: { buffer: contactsBuffer } },
                { binding: 5, resource: { buffer: contactCountBuffer } }
            ], 'Ground Collisions Bind Group');
            
            if (bindGroup) {
                pass.setBindGroup(0, bindGroup);
                pass.dispatchWorkgroups(Math.ceil(simParams.numBodies / WORKGROUP_SIZE));
            }
            pass.end();
        }
        
        // --- Narrow Phase ---
        // main narrowphase shader uses: bodies(0), pairs(2), pairCount(3), contacts(4), contactCount(5)
        // Note: params(1) is declared but NOT used in this entry point
        const narrowphaseStart = performance.now();
        
        if (narrowPhasePipeline) {
            const pass = commandEncoder.beginComputePass({ label: 'Narrow Phase' });
            pass.setPipeline(narrowPhasePipeline);
            
            const bindGroup = createBindGroupSafe(narrowPhasePipeline, [
                { binding: 0, resource: { buffer: bodiesBuffer } },
                { binding: 1, resource: { buffer: paramsBuffer } },
                { binding: 2, resource: { buffer: pairsBuffer } },
                { binding: 3, resource: { buffer: pairCountBuffer } },
                { binding: 4, resource: { buffer: contactsBuffer } },
                { binding: 5, resource: { buffer: contactCountBuffer } }
            ], 'Narrow Phase Bind Group');
            
            if (bindGroup) {
                pass.setBindGroup(0, bindGroup);
                pass.dispatchWorkgroups(Math.ceil(MAX_PAIRS / WORKGROUP_SIZE));
            }
            pass.end();
        }
        
        metrics.narrowphaseTime = performance.now() - narrowphaseStart;
        
        // --- Constraint Solver ---
        // All solver entry points now use: bodies(0), params(1), contacts(2), contactCount(3)
        // We added dummy params reads to all entry points to ensure consistent layouts
        const solverStart = performance.now();
        
        if (solveVelocitiesPipeline && solvePositionsPipeline) {
            // Solver iterations
            for (let i = 0; i < simParams.solverIterations; i++) {
                const pass = commandEncoder.beginComputePass({ label: `Solve Velocities ${i}` });
                pass.setPipeline(solveVelocitiesPipeline);
                
                const bindGroup = createBindGroupSafe(solveVelocitiesPipeline, [
                    { binding: 0, resource: { buffer: bodiesBuffer } },
                    { binding: 1, resource: { buffer: paramsBuffer } },
                    { binding: 2, resource: { buffer: contactsBuffer } },
                    { binding: 3, resource: { buffer: contactCountBuffer } }
                ], 'Solve Velocities Bind Group');
                
                if (bindGroup) {
                    pass.setBindGroup(0, bindGroup);
                    pass.dispatchWorkgroups(Math.ceil(MAX_CONTACTS / WORKGROUP_SIZE));
                }
                pass.end();
            }
            
            // Position correction - now includes params since shader was updated
            {
                const pass = commandEncoder.beginComputePass({ label: 'Solve Positions' });
                pass.setPipeline(solvePositionsPipeline);
                
                const bindGroup = createBindGroupSafe(solvePositionsPipeline, [
                    { binding: 0, resource: { buffer: bodiesBuffer } },
                    { binding: 1, resource: { buffer: paramsBuffer } },
                    { binding: 2, resource: { buffer: contactsBuffer } },
                    { binding: 3, resource: { buffer: contactCountBuffer } }
                ], 'Solve Positions Bind Group');
                
                if (bindGroup) {
                    pass.setBindGroup(0, bindGroup);
                    pass.dispatchWorkgroups(Math.ceil(MAX_CONTACTS / WORKGROUP_SIZE));
                }
                pass.end();
            }
        }
        
        metrics.solverTime = performance.now() - solverStart;
        
        // Submit commands
        const commandBuffer = commandEncoder.finish();
        queue.submit([commandBuffer]);
        
        // Wait for completion
        await queue.onSubmittedWorkDone();
        
        metrics.lastStepTime = performance.now() - startTime;
    }
    
    function uploadParams() {
        const data = new ArrayBuffer(64);
        const floatView = new Float32Array(data);
        const uintView = new Uint32Array(data);
        
        // gravity (vec3) + deltaTime
        floatView[0] = simParams.gravity[0];
        floatView[1] = simParams.gravity[1];
        floatView[2] = simParams.gravity[2];
        floatView[3] = simParams.deltaTime;
        
        // numBodies, numContacts, solverIterations, enableCCD
        uintView[4] = simParams.numBodies;
        uintView[5] = simParams.numContacts;
        uintView[6] = simParams.solverIterations;
        uintView[7] = simParams.enableCCD;
        
        // gridCellSize, gridDimX, gridDimY, gridDimZ
        floatView[8] = simParams.gridCellSize;
        uintView[9] = simParams.gridDimX;
        uintView[10] = simParams.gridDimY;
        uintView[11] = simParams.gridDimZ;
        
        queue.writeBuffer(paramsBuffer, 0, data);
    }

    // ========================================
    // Data Retrieval
    // ========================================
    
    async function getTransformBatch() {
        if (!initialized || simParams.numBodies === 0) {
            return { transforms: [], ids: [] };
        }
        
        // Copy bodies buffer to readback buffer
        const commandEncoder = device.createCommandEncoder();
        commandEncoder.copyBufferToBuffer(
            bodiesBuffer, 0,
            readbackBuffer, 0,
            simParams.numBodies * RIGID_BODY_SIZE
        );
        
        queue.submit([commandEncoder.finish()]);
        
        // Map and read
        await readbackBuffer.mapAsync(GPUMapMode.READ);
        const data = new Float32Array(readbackBuffer.getMappedRange().slice(0));
        readbackBuffer.unmap();
        
        // Extract transforms
        const transforms = [];
        const ids = [];
        
        for (const [id, index] of bodyIndexMap) {
            const offset = index * (RIGID_BODY_SIZE / 4);
            
            // Position (indices 0-2)
            const px = data[offset + 0];
            const py = data[offset + 1];
            const pz = data[offset + 2];
            
            // Rotation (indices 4-7)
            const rx = data[offset + 4];
            const ry = data[offset + 5];
            const rz = data[offset + 6];
            const rw = data[offset + 7];
            
            transforms.push(px, py, pz, rx, ry, rz, rw);
            ids.push(id);
        }
        
        return { transforms, ids };
    }
    
    function getMetrics() {
        return { 
            totalStepTimeMs: metrics.lastStepTime,
            broadPhaseTimeMs: metrics.broadphaseTime,
            narrowPhaseTimeMs: metrics.narrowphaseTime,
            solverTimeMs: metrics.solverTime,
            contactCount: metrics.contactCount,
            pairCount: metrics.pairCount,
            bodyCount: simParams.numBodies,
            isGpuActive: initialized,
            // Legacy format for compatibility
            lastStepTime: metrics.lastStepTime,
            broadphaseTime: metrics.broadphaseTime,
            narrowphaseTime: metrics.narrowphaseTime,
            solverTime: metrics.solverTime
        };
    }

    // ========================================
    // Utility Functions
    // ========================================
    
    function updateSettings(settings) {
        if (settings.gravity) simParams.gravity = settings.gravity;
        if (settings.timeStep) simParams.deltaTime = settings.timeStep;
        if (settings.subSteps) simParams.solverIterations = settings.subSteps * 4;
    }
    
    function applyImpulse(id, impulse) {
        const index = bodyIndexMap.get(id);
        if (index === undefined) return;
        
        const body = bodyData.get(id);
        if (body && body.inverseMass > 0) {
            body.linearVelocity[0] += impulse[0] * body.inverseMass;
            body.linearVelocity[1] += impulse[1] * body.inverseMass;
            body.linearVelocity[2] += impulse[2] * body.inverseMass;
            uploadBody(index, body);
        }
    }
    
    function reset() {
        bodyData.clear();
        bodyIndexMap.clear();
        nextBodyIndex = 0;
        simParams.numBodies = 0;
    }
    
    function dispose() {
        if (device) {
            bodiesBuffer?.destroy();
            bodiesStagingBuffer?.destroy();
            contactsBuffer?.destroy();
            pairsBuffer?.destroy();
            aabbsBuffer?.destroy();
            cellCountsBuffer?.destroy();
            cellBodiesBuffer?.destroy();
            cellOffsetsBuffer?.destroy();
            pairCountBuffer?.destroy();
            contactCountBuffer?.destroy();
            paramsBuffer?.destroy();
            externalForcesBuffer?.destroy();
            readbackBuffer?.destroy();
            
            device.destroy();
            device = null;
        }
        
        initialized = false;
        bodyData.clear();
        bodyIndexMap.clear();
    }
    
    function isAvailable() {
        return initialized;
    }

    // ========================================
    // Export Module
    // ========================================
    
    window.GPUPhysicsModule = {
        initialize,
        createRigidBody,
        removeRigidBody,
        step,
        getTransformBatch,
        getMetrics,
        updateSettings,
        applyImpulse,
        reset,
        dispose,
        isAvailable,
        
        // Debug accessors
        getSimParams: () => ({ ...simParams }),
        getBodyCount: () => simParams.numBodies
    };
    
    console.log('GPU Physics module loaded');
})();
