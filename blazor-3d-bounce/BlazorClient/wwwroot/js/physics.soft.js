/**
 * Ammo.js Soft Body Physics Module
 * Handles cloth, rope, and volumetric soft body dynamics
 */

// Ammo.js state
let Ammo = null;
let softWorld = null;
let softBodies = new Map();
let softBodyHelpers = null;
let config = {};
let initialStates = new Map();
let isAvailable = false;

// Collision configuration for soft bodies
let collisionConfiguration = null;
let dispatcher = null;
let broadphase = null;
let solver = null;
let softBodySolver = null;

/**
 * Initialize Ammo.js soft body physics
 */
window.SoftPhysicsModule = {
    initialize: async function(settings) {
        try {
            // Check if Ammo is already loaded
            if (typeof window.Ammo === 'function') {
                Ammo = await window.Ammo();
            } else if (typeof window.Ammo === 'object') {
                Ammo = window.Ammo;
            } else {
                console.warn('Ammo.js not available - soft body physics disabled');
                return false;
            }

            config = {
                gravity: settings.gravity || [0, -9.81, 0],
                timeStep: settings.timeStep || 1/120,
                subSteps: settings.subSteps || 3
            };

            // Create soft body world
            this.createWorld();

            isAvailable = true;
            console.log('Ammo.js soft body physics initialized');
            return true;
        } catch (e) {
            console.error('Failed to initialize Ammo.js:', e);
            isAvailable = false;
            return false;
        }
    },

    createWorld: function() {
        // Soft body collision configuration
        collisionConfiguration = new Ammo.btSoftBodyRigidBodyCollisionConfiguration();
        dispatcher = new Ammo.btCollisionDispatcher(collisionConfiguration);
        broadphase = new Ammo.btDbvtBroadphase();
        solver = new Ammo.btSequentialImpulseConstraintSolver();
        softBodySolver = new Ammo.btDefaultSoftBodySolver();

        // Create soft body world
        softWorld = new Ammo.btSoftRigidDynamicsWorld(
            dispatcher,
            broadphase,
            solver,
            collisionConfiguration,
            softBodySolver
        );

        // Set gravity
        softWorld.setGravity(new Ammo.btVector3(
            config.gravity[0],
            config.gravity[1],
            config.gravity[2]
        ));

        // Get soft body world info
        softBodyHelpers = new Ammo.btSoftBodyHelpers();
    },

    createCloth: function(data) {
        if (!isAvailable || !softWorld) return;

        const worldInfo = softWorld.getWorldInfo();
        
        // Cloth corners
        const pos = data.position;
        const w = data.width / 2;
        const h = data.height / 2;

        const corner00 = new Ammo.btVector3(pos[0] - w, pos[1], pos[2] - h);
        const corner01 = new Ammo.btVector3(pos[0] - w, pos[1], pos[2] + h);
        const corner10 = new Ammo.btVector3(pos[0] + w, pos[1], pos[2] - h);
        const corner11 = new Ammo.btVector3(pos[0] + w, pos[1], pos[2] + h);

        // Create cloth patch
        const cloth = softBodyHelpers.CreatePatch(
            worldInfo,
            corner00,
            corner10,
            corner01,
            corner11,
            data.resolutionX,
            data.resolutionY,
            0, // Fixed corners bitmask (none initially)
            true // Generate diagonal links
        );

        // Configure soft body
        const sbConfig = cloth.get_m_cfg();
        
        // Velocity solver iterations
        sbConfig.set_viterations(data.iterations || 10);
        // Position solver iterations
        sbConfig.set_piterations(data.iterations || 10);
        // Collision flags
        sbConfig.set_collisions(
            Ammo.btSoftBody.fCollision.SDF_RS | 
            Ammo.btSoftBody.fCollision.CL_SS
        );

        // Set material properties
        const mat = cloth.get_m_materials().at(0);
        mat.set_m_kLST(data.structuralStiffness || 0.9); // Linear stiffness
        mat.set_m_kAST(data.shearStiffness || 0.9);      // Angular stiffness
        mat.set_m_kVST(data.bendingStiffness || 0.5);    // Volume stiffness

        // Set damping
        sbConfig.set_kDP(data.damping || 0.05);

        // Set total mass
        cloth.setTotalMass(data.mass || 1.0, false);

        // Self collision
        if (data.selfCollision) {
            sbConfig.set_collisions(
                sbConfig.get_collisions() | 
                Ammo.btSoftBody.fCollision.CL_SELF
            );
        }

        // Pin vertices
        if (data.pinnedVertices && data.pinnedVertices.length > 0) {
            const nodes = cloth.get_m_nodes();
            for (const idx of data.pinnedVertices) {
                if (idx < nodes.size()) {
                    cloth.setMass(idx, 0); // Mass 0 = pinned
                }
            }
        }

        // Add to world
        softWorld.addSoftBody(cloth, 1, -1);

        // Store
        softBodies.set(data.id, {
            body: cloth,
            type: 'cloth',
            vertexCount: data.resolutionX * data.resolutionY,
            initialData: { ...data }
        });

        // Store initial state
        initialStates.set(data.id, this.getVertexPositions(cloth));
    },

    createRope: function(data) {
        if (!isAvailable || !softWorld) return;

        const worldInfo = softWorld.getWorldInfo();
        const pos = data.position;

        // Rope start and end points
        const from = new Ammo.btVector3(pos[0], pos[1], pos[2]);
        const to = new Ammo.btVector3(pos[0], pos[1] - data.length, pos[2]);

        // Create rope
        const rope = softBodyHelpers.CreateRope(
            worldInfo,
            from,
            to,
            data.segments || 20,
            0 // Fixed ends bitmask
        );

        // Configure
        const sbConfig = rope.get_m_cfg();
        sbConfig.set_viterations(data.iterations || 15);
        sbConfig.set_piterations(data.iterations || 15);

        // Material
        const mat = rope.get_m_materials().at(0);
        mat.set_m_kLST(data.structuralStiffness || 0.95);
        mat.set_m_kAST(data.bendingStiffness || 0.1);

        // Damping
        sbConfig.set_kDP(data.damping || 0.1);

        // Mass
        rope.setTotalMass(data.mass || 0.5, false);

        // Pin top vertex
        if (data.pinnedVertices && data.pinnedVertices.length > 0) {
            for (const idx of data.pinnedVertices) {
                rope.setMass(idx, 0);
            }
        }

        // Add to world
        softWorld.addSoftBody(rope, 1, -1);

        softBodies.set(data.id, {
            body: rope,
            type: 'rope',
            vertexCount: data.segments + 1,
            initialData: { ...data }
        });

        initialStates.set(data.id, this.getVertexPositions(rope));
    },

    createVolumetric: function(data) {
        if (!isAvailable || !softWorld) return;

        const worldInfo = softWorld.getWorldInfo();
        const pos = data.position;

        // Create ellipsoid approximation
        const center = new Ammo.btVector3(pos[0], pos[1], pos[2]);
        const radius = new Ammo.btVector3(
            data.width / 2 || data.radius || 0.5,
            data.height / 2 || data.radius || 0.5,
            data.depth / 2 || data.radius || 0.5
        );

        // Create from triangle mesh or ellipsoid
        const softBody = softBodyHelpers.CreateEllipsoid(
            worldInfo,
            center,
            radius,
            Math.min(data.resolutionX || 10, 20) * 10
        );

        // Configure
        const sbConfig = softBody.get_m_cfg();
        sbConfig.set_viterations(data.iterations || 12);
        sbConfig.set_piterations(data.iterations || 12);

        // Material
        const mat = softBody.get_m_materials().at(0);
        mat.set_m_kLST(data.structuralStiffness || 0.5);
        mat.set_m_kAST(data.shearStiffness || 0.4);
        mat.set_m_kVST(data.volumeConservation || 0.95);

        // Pressure for volume preservation
        sbConfig.set_kPR(data.pressure || 50);
        sbConfig.set_kVC(data.volumeConservation || 1.0);

        // Damping
        sbConfig.set_kDP(data.damping || 0.1);

        // Mass
        softBody.setTotalMass(data.mass || 2.0, false);

        // Add to world
        softWorld.addSoftBody(softBody, 1, -1);

        softBodies.set(data.id, {
            body: softBody,
            type: 'volumetric',
            vertexCount: softBody.get_m_nodes().size(),
            initialData: { ...data }
        });

        initialStates.set(data.id, this.getVertexPositions(softBody));
    },

    getVertexPositions: function(softBody) {
        const nodes = softBody.get_m_nodes();
        const numNodes = nodes.size();
        const positions = new Float32Array(numNodes * 3);

        for (let i = 0; i < numNodes; i++) {
            const node = nodes.at(i);
            const pos = node.get_m_x();
            positions[i * 3] = pos.x();
            positions[i * 3 + 1] = pos.y();
            positions[i * 3 + 2] = pos.z();
        }

        return positions;
    },

    removeSoftBody: function(id) {
        if (!isAvailable || !softWorld) return;

        const entry = softBodies.get(id);
        if (entry) {
            softWorld.removeSoftBody(entry.body);
            Ammo.destroy(entry.body);
            softBodies.delete(id);
            initialStates.delete(id);
        }
    },

    updateSoftBody: function(updates) {
        if (!isAvailable) return;

        const entry = softBodies.get(updates.id);
        if (!entry) return;

        const body = entry.body;
        const sbConfig = body.get_m_cfg();
        const mat = body.get_m_materials().at(0);

        // Update stiffness
        if (updates.structuralStiffness !== undefined) {
            mat.set_m_kLST(updates.structuralStiffness);
        }
        if (updates.shearStiffness !== undefined) {
            mat.set_m_kAST(updates.shearStiffness);
        }
        if (updates.bendingStiffness !== undefined) {
            mat.set_m_kVST(updates.bendingStiffness);
        }

        // Update damping
        if (updates.damping !== undefined) {
            sbConfig.set_kDP(updates.damping);
        }

        // Update pressure (volumetric only)
        if (updates.pressure !== undefined) {
            sbConfig.set_kPR(updates.pressure);
        }
        if (updates.volumeConservation !== undefined) {
            sbConfig.set_kVC(updates.volumeConservation);
        }

        // Update iterations
        if (updates.iterations !== undefined) {
            sbConfig.set_viterations(updates.iterations);
            sbConfig.set_piterations(updates.iterations);
        }

        // Update self-collision
        if (updates.selfCollision !== undefined) {
            let collisions = sbConfig.get_collisions();
            if (updates.selfCollision) {
                collisions |= Ammo.btSoftBody.fCollision.CL_SELF;
            } else {
                collisions &= ~Ammo.btSoftBody.fCollision.CL_SELF;
            }
            sbConfig.set_collisions(collisions);
        }
    },

    pinVertex: function(id, vertexIndex, worldPosition) {
        if (!isAvailable) return;

        const entry = softBodies.get(id);
        if (!entry) return;

        const body = entry.body;
        body.setMass(vertexIndex, 0);

        // Optionally move node to position
        if (worldPosition) {
            const nodes = body.get_m_nodes();
            if (vertexIndex < nodes.size()) {
                const node = nodes.at(vertexIndex);
                node.get_m_x().setValue(
                    worldPosition[0],
                    worldPosition[1],
                    worldPosition[2]
                );
            }
        }
    },

    unpinVertex: function(id, vertexIndex) {
        if (!isAvailable) return;

        const entry = softBodies.get(id);
        if (!entry) return;

        // Restore mass (use average mass)
        const body = entry.body;
        const totalMass = entry.initialData.mass || 1.0;
        const avgMass = totalMass / entry.vertexCount;
        body.setMass(vertexIndex, avgMass);
    },

    updateSettings: function(settings) {
        if (!isAvailable || !softWorld) return;

        config = { ...config, ...settings };

        // Update gravity
        if (settings.gravity) {
            softWorld.setGravity(new Ammo.btVector3(
                settings.gravity[0],
                settings.gravity[1],
                settings.gravity[2]
            ));
        }
    },

    step: function(deltaTime) {
        if (!isAvailable || !softWorld) return;

        const dt = config.timeStep || 1/120;
        const substeps = config.subSteps || 3;

        softWorld.stepSimulation(dt, substeps, dt / substeps);
    },

    getDeformedVertices: function(id) {
        if (!isAvailable) return { vertices: [], normals: null };

        const entry = softBodies.get(id);
        if (!entry) return { vertices: [], normals: null };

        const body = entry.body;
        const nodes = body.get_m_nodes();
        const numNodes = nodes.size();

        const vertices = new Float32Array(numNodes * 3);
        const normals = new Float32Array(numNodes * 3);

        for (let i = 0; i < numNodes; i++) {
            const node = nodes.at(i);
            const pos = node.get_m_x();
            const norm = node.get_m_n();

            vertices[i * 3] = pos.x();
            vertices[i * 3 + 1] = pos.y();
            vertices[i * 3 + 2] = pos.z();

            normals[i * 3] = norm.x();
            normals[i * 3 + 1] = norm.y();
            normals[i * 3 + 2] = norm.z();
        }

        return {
            vertices: Array.from(vertices),
            normals: Array.from(normals),
            vertexCount: numNodes
        };
    },

    getAllDeformedVertices: function() {
        if (!isAvailable) return {};

        const result = {};
        softBodies.forEach((entry, id) => {
            result[id] = this.getDeformedVertices(id);
        });
        return result;
    },

    reset: function() {
        if (!isAvailable) return;

        initialStates.forEach((positions, id) => {
            const entry = softBodies.get(id);
            if (!entry) return;

            const body = entry.body;
            const nodes = body.get_m_nodes();
            const numNodes = nodes.size();

            for (let i = 0; i < numNodes && i * 3 + 2 < positions.length; i++) {
                const node = nodes.at(i);
                node.get_m_x().setValue(
                    positions[i * 3],
                    positions[i * 3 + 1],
                    positions[i * 3 + 2]
                );
                // Reset velocity
                node.get_m_v().setValue(0, 0, 0);
            }
        });
    },

    dispose: function() {
        if (!isAvailable) return;

        softBodies.forEach((entry) => {
            if (softWorld) {
                softWorld.removeSoftBody(entry.body);
            }
            Ammo.destroy(entry.body);
        });

        softBodies.clear();
        initialStates.clear();

        if (softWorld) {
            Ammo.destroy(softWorld);
            softWorld = null;
        }
        if (solver) Ammo.destroy(solver);
        if (softBodySolver) Ammo.destroy(softBodySolver);
        if (broadphase) Ammo.destroy(broadphase);
        if (dispatcher) Ammo.destroy(dispatcher);
        if (collisionConfiguration) Ammo.destroy(collisionConfiguration);

        isAvailable = false;
    }
};

export default window.SoftPhysicsModule;
