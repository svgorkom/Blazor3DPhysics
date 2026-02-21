/**
 * Rapier.js Rigid Body Physics Module
 * Handles rigid body dynamics with realistic restitution and friction
 */

// Import Rapier when loaded
let RAPIER = null;
let world = null;
let bodies = new Map();
let colliders = new Map();
let groundCollider = null;
let config = {};
let accumulator = 0;
let initialStates = new Map();

/**
 * Initialize Rapier physics
 */
window.RigidPhysicsModule = {
    initialize: async function(settings) {
        try {
            // Initialize Rapier WASM
            RAPIER = await import('https://cdn.jsdelivr.net/npm/@dimforge/rapier3d-compat@0.12.0/+esm');
            await RAPIER.init();

            config = {
                gravity: settings.gravity || [0, -9.81, 0],
                timeStep: settings.timeStep || 1/120,
                subSteps: settings.subSteps || 3,
                enableSleeping: settings.enableSleeping !== false,
                sleepThreshold: settings.sleepThreshold || 0.01
            };

            // Create physics world
            world = new RAPIER.World({
                x: config.gravity[0],
                y: config.gravity[1],
                z: config.gravity[2]
            });

            console.log('Rapier physics initialized');
            return true;
        } catch (e) {
            console.error('Failed to initialize Rapier:', e);
            
            // Fallback: create a mock physics world for basic functionality
            this.createMockPhysics();
            return true;
        }
    },

    createMockPhysics: function() {
        // Simple mock physics for fallback
        world = {
            step: function() {},
            gravity: { x: 0, y: -9.81, z: 0 },
            createRigidBody: function() { return {}; },
            createCollider: function() { return {}; },
            removeRigidBody: function() {}
        };
        console.warn('Using mock physics - Rapier not available');
    },

    createGround: function(restitution, friction) {
        if (!world || !RAPIER) return;

        // Create ground collider (half-space or large box)
        const groundBodyDesc = RAPIER.RigidBodyDesc.fixed()
            .setTranslation(0, 0, 0);
        const groundBody = world.createRigidBody(groundBodyDesc);

        const groundColliderDesc = RAPIER.ColliderDesc.cuboid(25, 0.1, 25)
            .setRestitution(restitution || 0.3)
            .setFriction(friction || 0.5);
        groundCollider = world.createCollider(groundColliderDesc, groundBody);
    },

    createRigidBody: function(data) {
        if (!world || !RAPIER) return;

        // Create rigid body description
        let bodyDesc;
        if (data.isStatic) {
            bodyDesc = RAPIER.RigidBodyDesc.fixed();
        } else {
            bodyDesc = RAPIER.RigidBodyDesc.dynamic()
                .setLinearDamping(data.linearDamping || 0.01)
                .setAngularDamping(data.angularDamping || 0.01);

            if (data.enableCCD) {
                bodyDesc.setCcdEnabled(true);
            }
        }

        // Set position
        bodyDesc.setTranslation(
            data.position[0],
            data.position[1],
            data.position[2]
        );

        // Set rotation
        if (data.rotation) {
            bodyDesc.setRotation({
                x: data.rotation[0],
                y: data.rotation[1],
                z: data.rotation[2],
                w: data.rotation[3]
            });
        }

        // Create body
        const body = world.createRigidBody(bodyDesc);

        // Create collider based on primitive type
        let colliderDesc;
        const scale = data.scale || [1, 1, 1];

        switch (data.primitiveType) {
            case 'sphere':
                colliderDesc = RAPIER.ColliderDesc.ball(0.5 * scale[0]);
                break;
            case 'box':
                colliderDesc = RAPIER.ColliderDesc.cuboid(
                    0.5 * scale[0],
                    0.5 * scale[1],
                    0.5 * scale[2]
                );
                break;
            case 'capsule':
                colliderDesc = RAPIER.ColliderDesc.capsule(
                    0.5 * scale[1],
                    0.25 * scale[0]
                );
                break;
            case 'cylinder':
                colliderDesc = RAPIER.ColliderDesc.cylinder(
                    0.5 * scale[1],
                    0.5 * scale[0]
                );
                break;
            case 'cone':
                colliderDesc = RAPIER.ColliderDesc.cone(
                    0.5 * scale[1],
                    0.5 * scale[0]
                );
                break;
            default:
                colliderDesc = RAPIER.ColliderDesc.ball(0.5);
        }

        // Set material properties
        colliderDesc.setRestitution(data.restitution || 0.5);
        colliderDesc.setFriction(data.staticFriction || 0.5);
        
        // Set density for mass
        if (data.density) {
            colliderDesc.setDensity(data.density / 1000); // Convert to appropriate units
        } else if (data.mass) {
            // Estimate density from mass (assuming unit size)
            colliderDesc.setDensity(data.mass);
        }

        const collider = world.createCollider(colliderDesc, body);

        // Apply initial velocities
        if (data.linearVelocity && !data.isStatic) {
            body.setLinvel({
                x: data.linearVelocity[0],
                y: data.linearVelocity[1],
                z: data.linearVelocity[2]
            }, true);
        }

        if (data.angularVelocity && !data.isStatic) {
            body.setAngvel({
                x: data.angularVelocity[0],
                y: data.angularVelocity[1],
                z: data.angularVelocity[2]
            }, true);
        }

        // Store body and collider
        bodies.set(data.id, body);
        colliders.set(data.id, collider);

        // Store initial state for reset
        initialStates.set(data.id, {
            position: [...data.position],
            rotation: data.rotation ? [...data.rotation] : [0, 0, 0, 1],
            linearVelocity: data.linearVelocity ? [...data.linearVelocity] : [0, 0, 0],
            angularVelocity: data.angularVelocity ? [...data.angularVelocity] : [0, 0, 0]
        });
    },

    removeRigidBody: function(id) {
        if (!world) return;

        const body = bodies.get(id);
        if (body) {
            world.removeRigidBody(body);
            bodies.delete(id);
            colliders.delete(id);
            initialStates.delete(id);
        }
    },

    updateRigidBody: function(updates) {
        if (!world || !RAPIER) return;

        const body = bodies.get(updates.id);
        const collider = colliders.get(updates.id);
        if (!body || !collider) return;

        // Update damping
        if (updates.linearDamping !== undefined) {
            body.setLinearDamping(updates.linearDamping);
        }
        if (updates.angularDamping !== undefined) {
            body.setAngularDamping(updates.angularDamping);
        }

        // Update CCD
        if (updates.enableCCD !== undefined) {
            body.enableCcd(updates.enableCCD);
        }

        // Update material properties
        if (updates.restitution !== undefined) {
            collider.setRestitution(updates.restitution);
        }
        if (updates.staticFriction !== undefined) {
            collider.setFriction(updates.staticFriction);
        }
    },

    applyImpulse: function(id, impulse) {
        const body = bodies.get(id);
        if (body && body.isValid()) {
            body.applyImpulse({ x: impulse[0], y: impulse[1], z: impulse[2] }, true);
        }
    },

    applyForce: function(id, force) {
        const body = bodies.get(id);
        if (body && body.isValid()) {
            body.addForce({ x: force[0], y: force[1], z: force[2] }, true);
        }
    },

    setLinearVelocity: function(id, velocity) {
        const body = bodies.get(id);
        if (body && body.isValid()) {
            body.setLinvel({ x: velocity[0], y: velocity[1], z: velocity[2] }, true);
        }
    },

    updateSettings: function(settings) {
        if (!world) return;

        config = { ...config, ...settings };

        // Update gravity
        if (settings.gravity) {
            world.gravity = {
                x: settings.gravity[0],
                y: settings.gravity[1],
                z: settings.gravity[2]
            };
        }
    },

    step: function(deltaTime) {
        if (!world) return;

        // Fixed timestep with substeps
        const dt = config.timeStep || 1/120;
        const substeps = config.subSteps || 3;

        for (let i = 0; i < substeps; i++) {
            world.step();
        }
    },

    getTransformBatch: function() {
        const transforms = [];
        const ids = [];

        bodies.forEach((body, id) => {
            if (!body.isValid()) return;

            const pos = body.translation();
            const rot = body.rotation();

            // Pack: [px, py, pz, rx, ry, rz, rw]
            transforms.push(
                pos.x, pos.y, pos.z,
                rot.x, rot.y, rot.z, rot.w
            );
            ids.push(id);
        });

        return {
            transforms: transforms,
            ids: ids
        };
    },

    reset: function() {
        if (!world || !RAPIER) return;

        initialStates.forEach((state, id) => {
            const body = bodies.get(id);
            if (body && body.isValid()) {
                // Reset position and rotation
                body.setTranslation({
                    x: state.position[0],
                    y: state.position[1],
                    z: state.position[2]
                }, true);

                body.setRotation({
                    x: state.rotation[0],
                    y: state.rotation[1],
                    z: state.rotation[2],
                    w: state.rotation[3]
                }, true);

                // Reset velocities
                body.setLinvel({
                    x: state.linearVelocity[0],
                    y: state.linearVelocity[1],
                    z: state.linearVelocity[2]
                }, true);

                body.setAngvel({
                    x: state.angularVelocity[0],
                    y: state.angularVelocity[1],
                    z: state.angularVelocity[2]
                }, true);

                // Wake up body
                body.wakeUp();
            }
        });
    },

    dispose: function() {
        if (world) {
            bodies.clear();
            colliders.clear();
            initialStates.clear();
            world.free();
            world = null;
        }
    }
};

export default window.RigidPhysicsModule;
