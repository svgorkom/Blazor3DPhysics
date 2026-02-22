/**
 * Soft Body Physics Module (Highly Optimized)
 * Provides cloth and volumetric soft body simulation
 * Uses Position Based Dynamics aligned with rigid body simulation
 * 
 * Performance optimizations:
 * - Typed arrays for all vertex data
 * - Zero allocations in simulation loop
 * - Batched vertex output
 * - Reduced constraint iterations with adaptive quality
 * - Skip normal calculation unless requested
 * - Lower default resolution
 */

(function() {
    'use strict';

    // Body storage - use object instead of Map for faster iteration
    const softBodies = {};
    const initialStates = {};
    let bodyCount = 0;
    let bodyIds = [];
    
    // Module state
    let configGravityX = 0;
    let configGravityY = -9.81;
    let configGravityZ = 0;
    let configTimeStep = 1/60;
    let _isAvailable = false;
    let _isInitialized = false;

    // Shared output buffers - reused across frames
    let outputBuffer = null;
    let outputBufferSize = 0;

    // Physics constants
    const GROUND_Y = 0.05;
    const GROUND_RESTITUTION = 0.3;
    const GROUND_FRICTION = 0.95;
    const MIN_VELOCITY_SQ = 0.0025; // 0.05^2
    const EPSILON = 0.0001;
    const EPSILON_SQ = EPSILON * EPSILON;

    // Adaptive quality - reduce iterations when at rest
    const REST_THRESHOLD_SQ = 0.01;

    /**
     * Create optimized body structure
     */
    function createBody(type, vertexCount, constraintCount, resX, resY) {
        return {
            type: type,
            vertexCount: vertexCount,
            positions: new Float32Array(vertexCount * 3),
            velocities: new Float32Array(vertexCount * 3),
            inverseMasses: new Float32Array(vertexCount),
            // Constraints in Structure of Arrays for cache efficiency
            cI1: new Uint16Array(constraintCount),
            cI2: new Uint16Array(constraintCount),
            cRest: new Float32Array(constraintCount),
            cStiff: new Float32Array(constraintCount),
            cCount: 0,
            damping: 0.03,
            iterations: 4, // Reduced default
            maxIterations: 6,
            resX: resX | 0,
            resY: resY | 0,
            isAtRest: false,
            kineticEnergy: 1.0
        };
    }

    /**
     * Create cloth - lower default resolution for performance
     */
    function createClothBody(data) {
        const pos = data.position || [0, 0, 0];
        const width = data.width || 2.0;
        const height = data.height || 2.0;
        // Lower default resolution for better performance
        const resX = Math.min(data.resolutionX || 10, 15);
        const resY = Math.min(data.resolutionY || 10, 15);
        const pinnedSet = new Set(data.pinnedVertices || []);

        const vertexCount = (resX + 1) * (resY + 1);
        const stride = resX + 1;
        
        // Estimate constraints: struct + shear + bend
        const maxConstraints = resX * (resY + 1) + (resX + 1) * resY + 
                              resX * resY * 2 + 
                              (resX - 1) * (resY + 1) + (resX + 1) * (resY - 1);

        const body = createBody('cloth', vertexCount, maxConstraints, resX, resY);
        body.damping = data.damping || 0.03;
        body.iterations = Math.min(data.iterations || 4, 6);
        body.maxIterations = body.iterations + 2;

        const halfW = width / 2;
        const halfH = height / 2;
        const stepX = width / resX;
        const stepY = height / resY;
        const diagLen = Math.sqrt(stepX * stepX + stepY * stepY);

        const sStiff = data.structuralStiffness || 0.85;
        const shStiff = data.shearStiffness || 0.4;
        const bStiff = data.bendingStiffness || 0.2;

        // Initialize vertices
        const positions = body.positions;
        const velocities = body.velocities;
        const inverseMasses = body.inverseMasses;
        
        for (let y = 0, vIdx = 0; y <= resY; y++) {
            for (let x = 0; x <= resX; x++, vIdx++) {
                const i3 = vIdx * 3;
                positions[i3] = pos[0] - halfW + x * stepX;
                positions[i3 + 1] = pos[1] + halfH - y * stepY;
                positions[i3 + 2] = pos[2];
                velocities[i3] = velocities[i3 + 1] = velocities[i3 + 2] = 0;
                inverseMasses[vIdx] = pinnedSet.has(vIdx) ? 0 : 1.0;
            }
        }

        // Build constraints directly into arrays
        const cI1 = body.cI1, cI2 = body.cI2, cRest = body.cRest, cStiff = body.cStiff;
        let c = 0;

        for (let y = 0; y <= resY; y++) {
            for (let x = 0; x <= resX; x++) {
                const idx = y * stride + x;

                // Structural horizontal
                if (x < resX) {
                    cI1[c] = idx; cI2[c] = idx + 1; cRest[c] = stepX; cStiff[c++] = sStiff;
                }
                // Structural vertical
                if (y < resY) {
                    cI1[c] = idx; cI2[c] = idx + stride; cRest[c] = stepY; cStiff[c++] = sStiff;
                }
                // Shear diagonals
                if (x < resX && y < resY) {
                    cI1[c] = idx; cI2[c] = idx + stride + 1; cRest[c] = diagLen; cStiff[c++] = shStiff;
                }
                if (x > 0 && y < resY) {
                    cI1[c] = idx; cI2[c] = idx + stride - 1; cRest[c] = diagLen; cStiff[c++] = shStiff;
                }
                // Bending (skip one) - only add half for performance
                if (x < resX - 1 && (x + y) % 2 === 0) {
                    cI1[c] = idx; cI2[c] = idx + 2; cRest[c] = stepX * 2; cStiff[c++] = bStiff;
                }
                if (y < resY - 1 && (x + y) % 2 === 0) {
                    cI1[c] = idx; cI2[c] = idx + stride * 2; cRest[c] = stepY * 2; cStiff[c++] = bStiff;
                }
            }
        }
        body.cCount = c;
        return body;
    }

    /**
     * Create volumetric - simplified for performance
     */
    function createVolumetricBody(data) {
        const pos = data.position || [0, 0, 0];
        const radius = data.radius || 0.5;
        const resolution = Math.min(data.resolutionX || 6, 8);
        const pinnedSet = new Set(data.pinnedVertices || []);

        const rings = resolution;
        const segments = resolution * 2;
        const vertexCount = 1 + rings * segments;
        
        const radialCount = vertexCount - 1;
        const horizCount = rings * segments;
        const vertCount = (rings - 1) * segments;
        const constraintCount = radialCount + horizCount + vertCount;

        const body = createBody('volumetric', vertexCount, constraintCount, 0, 0);
        body.damping = data.damping || 0.05;
        body.iterations = Math.min(data.iterations || 4, 6);

        const pStiff = Math.min(0.8, (data.pressure || 50) / 100);
        const sStiff = data.structuralStiffness || 0.7;

        const positions = body.positions;
        const velocities = body.velocities;
        const inverseMasses = body.inverseMasses;

        // Center
        positions[0] = pos[0]; positions[1] = pos[1]; positions[2] = pos[2];
        velocities[0] = velocities[1] = velocities[2] = 0;
        inverseMasses[0] = 1.0;

        // Surface
        let vIdx = 1;
        for (let ring = 1; ring <= rings; ring++) {
            const phi = (Math.PI * ring) / (rings + 1);
            const y = pos[1] + radius * Math.cos(phi);
            const rRad = radius * Math.sin(phi);

            for (let seg = 0; seg < segments; seg++) {
                const theta = (2 * Math.PI * seg) / segments;
                const i3 = vIdx * 3;
                positions[i3] = pos[0] + rRad * Math.cos(theta);
                positions[i3 + 1] = y;
                positions[i3 + 2] = pos[2] + rRad * Math.sin(theta);
                velocities[i3] = velocities[i3 + 1] = velocities[i3 + 2] = 0;
                inverseMasses[vIdx] = pinnedSet.has(vIdx) ? 0 : 1.0;
                vIdx++;
            }
        }

        // Constraints
        const cI1 = body.cI1, cI2 = body.cI2, cRest = body.cRest, cStiff = body.cStiff;
        let c = 0;

        // Radial
        for (let i = 1; i < vertexCount; i++) {
            const i3 = i * 3;
            const dx = positions[i3] - positions[0];
            const dy = positions[i3 + 1] - positions[1];
            const dz = positions[i3 + 2] - positions[2];
            cI1[c] = 0; cI2[c] = i;
            cRest[c] = Math.sqrt(dx*dx + dy*dy + dz*dz);
            cStiff[c++] = pStiff;
        }

        // Surface
        for (let ring = 1; ring <= rings; ring++) {
            const rs = 1 + (ring - 1) * segments;
            for (let seg = 0; seg < segments; seg++) {
                const i1 = rs + seg, i2 = rs + ((seg + 1) % segments);
                const i1_3 = i1 * 3, i2_3 = i2 * 3;
                const dx = positions[i2_3] - positions[i1_3];
                const dy = positions[i2_3+1] - positions[i1_3+1];
                const dz = positions[i2_3+2] - positions[i1_3+2];
                cI1[c] = i1; cI2[c] = i2;
                cRest[c] = Math.sqrt(dx*dx + dy*dy + dz*dz);
                cStiff[c++] = sStiff;
            }
            if (ring < rings) {
                const nrs = rs + segments;
                for (let seg = 0; seg < segments; seg++) {
                    const i1 = rs + seg, i2 = nrs + seg;
                    const i1_3 = i1 * 3, i2_3 = i2 * 3;
                    const dx = positions[i2_3] - positions[i1_3];
                    const dy = positions[i2_3+1] - positions[i1_3+1];
                    const dz = positions[i2_3+2] - positions[i1_3+2];
                    cI1[c] = i1; cI2[c] = i2;
                    cRest[c] = Math.sqrt(dx*dx + dy*dy + dz*dz);
                    cStiff[c++] = sStiff;
                }
            }
        }
        body.cCount = c;
        return body;
    }

    /**
     * Ultra-optimized constraint solver
     */
    function solveConstraints(pos, invMass, cI1, cI2, cRest, cStiff, cCount, iterations) {
        for (let iter = 0; iter < iterations; iter++) {
            for (let c = 0; c < cCount; c++) {
                const i1 = cI1[c], i2 = cI2[c];
                const im1 = invMass[i1], im2 = invMass[i2];
                const totalIM = im1 + im2;
                if (totalIM < EPSILON) continue;

                const i1_3 = i1 * 3, i2_3 = i2 * 3;
                const dx = pos[i2_3] - pos[i1_3];
                const dy = pos[i2_3 + 1] - pos[i1_3 + 1];
                const dz = pos[i2_3 + 2] - pos[i1_3 + 2];

                const distSq = dx * dx + dy * dy + dz * dz;
                if (distSq < EPSILON_SQ) continue;

                const dist = Math.sqrt(distSq);
                const diff = (dist - cRest[c]) * cStiff[c] / dist;

                if (im1 > 0) {
                    const f = im1 / totalIM * diff;
                    pos[i1_3] += dx * f;
                    pos[i1_3 + 1] += dy * f;
                    pos[i1_3 + 2] += dz * f;
                }
                if (im2 > 0) {
                    const f = im2 / totalIM * diff;
                    pos[i2_3] -= dx * f;
                    pos[i2_3 + 1] -= dy * f;
                    pos[i2_3 + 2] -= dz * f;
                }
            }
        }
    }

    /**
     * Step single body - fully inlined
     */
    function stepBody(body, dt) {
        const pos = body.positions;
        const vel = body.velocities;
        const invMass = body.inverseMasses;
        const n = body.vertexCount;
        const damp = 1 - body.damping;

        let totalKE = 0;

        // Integration + ground collision
        for (let i = 0; i < n; i++) {
            if (invMass[i] === 0) continue;

            const i3 = i * 3;
            
            // Gravity
            vel[i3] += configGravityX * dt;
            vel[i3 + 1] += configGravityY * dt;
            vel[i3 + 2] += configGravityZ * dt;

            // Damping
            vel[i3] *= damp;
            vel[i3 + 1] *= damp;
            vel[i3 + 2] *= damp;

            // Position
            pos[i3] += vel[i3] * dt;
            pos[i3 + 1] += vel[i3 + 1] * dt;
            pos[i3 + 2] += vel[i3 + 2] * dt;

            // Ground
            if (pos[i3 + 1] < GROUND_Y) {
                pos[i3 + 1] = GROUND_Y;
                if (vel[i3 + 1] < 0) {
                    vel[i3 + 1] *= -GROUND_RESTITUTION;
                    if (vel[i3 + 1] * vel[i3 + 1] < MIN_VELOCITY_SQ) vel[i3 + 1] = 0;
                }
                vel[i3] *= GROUND_FRICTION;
                vel[i3 + 2] *= GROUND_FRICTION;
            }

            // Track kinetic energy for adaptive quality
            totalKE += vel[i3]*vel[i3] + vel[i3+1]*vel[i3+1] + vel[i3+2]*vel[i3+2];
        }

        // Adaptive iterations based on motion
        body.kineticEnergy = totalKE / n;
        const iters = body.kineticEnergy < REST_THRESHOLD_SQ ? 
            Math.max(2, body.iterations - 2) : body.iterations;

        // Constraints
        solveConstraints(pos, invMass, body.cI1, body.cI2, body.cRest, body.cStiff, body.cCount, iters);

        // Re-enforce ground
        for (let i = 0; i < n; i++) {
            if (invMass[i] === 0) continue;
            const i3_1 = i * 3 + 1;
            if (pos[i3_1] < GROUND_Y) pos[i3_1] = GROUND_Y;
        }
    }

    /**
     * Public API
     */
    window.SoftPhysicsModule = {
        initialize: function(settings) {
            if (_isInitialized) return _isAvailable;

            if (settings && settings.gravity) {
                configGravityX = settings.gravity[0];
                configGravityY = settings.gravity[1];
                configGravityZ = settings.gravity[2];
            }
            configTimeStep = (settings && settings.timeStep) || 1/60;

            _isAvailable = true;
            _isInitialized = true;
            return _isAvailable;
        },

        createCloth: function(data) {
            if (!_isAvailable || !data || !data.id) return;
            const body = createClothBody(data);
            softBodies[data.id] = body;
            initialStates[data.id] = {
                pos: new Float32Array(body.positions),
                vel: new Float32Array(body.velocities)
            };
            bodyIds = Object.keys(softBodies);
            bodyCount = bodyIds.length;
        },

        createVolumetric: function(data) {
            if (!_isAvailable || !data || !data.id) return;
            const body = createVolumetricBody(data);
            softBodies[data.id] = body;
            initialStates[data.id] = {
                pos: new Float32Array(body.positions),
                vel: new Float32Array(body.velocities)
            };
            bodyIds = Object.keys(softBodies);
            bodyCount = bodyIds.length;
        },

        removeSoftBody: function(id) {
            delete softBodies[id];
            delete initialStates[id];
            bodyIds = Object.keys(softBodies);
            bodyCount = bodyIds.length;
        },

        updateSoftBody: function(updates) {
            if (!updates || !updates.id) return;
            const body = softBodies[updates.id];
            if (!body) return;
            if (updates.damping !== undefined) body.damping = updates.damping;
            if (updates.iterations !== undefined) body.iterations = Math.min(updates.iterations, 8);
        },

        pinVertex: function(id, idx, worldPos) {
            const body = softBodies[id];
            if (!body || idx >= body.vertexCount) return;
            body.inverseMasses[idx] = 0;
            if (worldPos) {
                const i3 = idx * 3;
                body.positions[i3] = worldPos[0];
                body.positions[i3 + 1] = worldPos[1];
                body.positions[i3 + 2] = worldPos[2];
            }
        },

        unpinVertex: function(id, idx) {
            const body = softBodies[id];
            if (!body || idx >= body.vertexCount) return;
            body.inverseMasses[idx] = 1.0;
        },

        updateSettings: function(settings) {
            if (!settings) return;
            if (settings.gravity) {
                configGravityX = settings.gravity[0];
                configGravityY = settings.gravity[1];
                configGravityZ = settings.gravity[2];
            }
            if (settings.timeStep !== undefined) configTimeStep = settings.timeStep;
        },

        step: function(deltaTime) {
            if (!_isAvailable || bodyCount === 0) return;
            const dt = deltaTime || configTimeStep;
            for (let i = 0; i < bodyCount; i++) {
                stepBody(softBodies[bodyIds[i]], dt);
            }
        },

        // Return positions directly - no Array.from conversion
        getDeformedVertices: function(id) {
            const body = softBodies[id];
            if (!body) return null;
            // Return the typed array directly - let caller handle conversion if needed
            return body.positions;
        },

        // Batched version for all bodies - single call
        getAllPositions: function() {
            if (bodyCount === 0) return null;
            const result = {};
            for (let i = 0; i < bodyCount; i++) {
                const id = bodyIds[i];
                result[id] = softBodies[id].positions;
            }
            return result;
        },

        // Legacy compatibility - with Array conversion
        getAllDeformedVertices: function() {
            if bodyCount === 0) return {};
            const result = {};
            for (let i = 0; i < bodyCount; i++) {
                const id = bodyIds[i];
                const body = softBodies[id];
                // Only convert to array when absolutely necessary for C# interop
                result[id] = { 
                    vertices: Array.from(body.positions), 
                    normals: null 
                };
            }
            return result;
        },

        reset: function() {
            for (let i = 0; i < bodyCount; i++) {
                const id = bodyIds[i];
                const body = softBodies[id];
                const state = initialStates[id];
                if (body && state) {
                    body.positions.set(state.pos);
                    body.velocities.set(state.vel);
                    body.kineticEnergy = 1.0;
                }
            }
        },

        dispose: function() {
            for (const id in softBodies) delete softBodies[id];
            for (const id in initialStates) delete initialStates[id];
            bodyIds = [];
            bodyCount = 0;
            outputBuffer = null;
            _isAvailable = false;
            _isInitialized = false;
        },

        isAvailable: function() { return _isAvailable; },
        getSoftBodyCount: function() { return bodyCount; },
        getSoftBodyIds: function() { return bodyIds.slice(); }
    };
})();
