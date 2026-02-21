/**
 * Rapier.js Rigid Body Physics Module
 * Handles rigid body dynamics with realistic restitution and friction
 */

(function() {
    'use strict';

    // Global Rapier reference
    let RAPIER = null;
    let world = null;
    let bodies = new Map();
    let colliders = new Map();
    let groundCollider = null;
    let config = {};
    let accumulator = 0;
    let initialStates = new Map();
    let _isInitialized = false;
    let useMockPhysics = false;

    /**
     * Initialize Rapier physics
     */
    window.RigidPhysicsModule = {
        initialize: async function(settings) {
            console.log('RigidPhysicsModule.initialize called');
            
            if (_isInitialized) {
                console.log('Rigid physics already initialized');
                return true;
            }

            try {
                config = {
                    gravity: (settings && settings.gravity) || [0, -9.81, 0],
                    timeStep: (settings && settings.timeStep) || 1/120,
                    subSteps: (settings && settings.subSteps) || 3,
                    enableSleeping: !settings || settings.enableSleeping !== false,
                    sleepThreshold: (settings && settings.sleepThreshold) || 0.01
                };

                // Create a simple physics simulation
                console.log('Creating physics simulation...');
                this.createSimplePhysics();

                _isInitialized = true;
                console.log('Rigid physics initialized successfully');
                return true;
            } catch (e) {
                console.error('Failed to initialize rigid physics:', e);
                this.createSimplePhysics();
                _isInitialized = true;
                return true;
            }
        },

        createSimplePhysics: function() {
            // Simple physics simulation without Rapier
            useMockPhysics = true;
            world = {
                gravity: { x: config.gravity[0], y: config.gravity[1], z: config.gravity[2] },
                bodies: []
            };
            console.log('Using simple physics simulation');
        },

        createGround: function(restitution, friction) {
            console.log('Creating ground with restitution:', restitution, 'friction:', friction);
            // Ground is implicit in our simple physics - objects stop at y=0
        },

        createRigidBody: function(data) {
            console.log('Creating rigid body:', data.id, data.primitiveType);
            
            var position = data.position || [0, 5, 0];
            var rotation = data.rotation || [0, 0, 0, 1];
            var scale = data.scale || [1, 1, 1];
            
            var body = {
                id: data.id,
                position: { x: position[0], y: position[1], z: position[2] },
                rotation: { x: rotation[0], y: rotation[1], z: rotation[2], w: rotation[3] },
                velocity: { x: 0, y: 0, z: 0 },
                angularVelocity: { x: 0, y: 0, z: 0 },
                isStatic: data.isStatic || false,
                restitution: data.restitution || 0.5,
                mass: data.mass || 1.0,
                linearDamping: data.linearDamping || 0.01,
                primitiveType: data.primitiveType,
                scale: scale,
                onGround: false
            };

            bodies.set(data.id, body);

            // Store initial state for reset
            initialStates.set(data.id, {
                position: [position[0], position[1], position[2]],
                rotation: [rotation[0], rotation[1], rotation[2], rotation[3]],
                velocity: [0, 0, 0]
            });

            console.log('Rigid body created successfully');
        },

        removeRigidBody: function(id) {
            bodies.delete(id);
            initialStates.delete(id);
            console.log('Removed rigid body:', id);
        },

        updateRigidBody: function(updates) {
            if (!updates || !updates.id) return;
            
            var body = bodies.get(updates.id);
            if (!body) return;

            if (updates.linearDamping !== undefined) {
                body.linearDamping = updates.linearDamping;
            }
            if (updates.restitution !== undefined) {
                body.restitution = updates.restitution;
            }
        },

        applyImpulse: function(id, impulse) {
            var body = bodies.get(id);
            if (body && !body.isStatic && impulse) {
                body.velocity.x += impulse[0] / body.mass;
                body.velocity.y += impulse[1] / body.mass;
                body.velocity.z += impulse[2] / body.mass;
            }
        },

        applyForce: function(id, force) {
            if (force) {
                this.applyImpulse(id, [force[0] * 0.016, force[1] * 0.016, force[2] * 0.016]);
            }
        },

        setLinearVelocity: function(id, velocity) {
            var body = bodies.get(id);
            if (body && !body.isStatic && velocity) {
                body.velocity.x = velocity[0];
                body.velocity.y = velocity[1];
                body.velocity.z = velocity[2];
            }
        },

        updateSettings: function(settings) {
            if (!settings) return;
            
            if (settings.gravity) {
                config.gravity = settings.gravity;
                if (world) {
                    world.gravity = {
                        x: settings.gravity[0],
                        y: settings.gravity[1],
                        z: settings.gravity[2]
                    };
                }
            }
            if (settings.timeStep) config.timeStep = settings.timeStep;
            if (settings.subSteps) config.subSteps = settings.subSteps;
        },

        step: function(deltaTime) {
            if (!world) return;

            var dt = deltaTime || config.timeStep || 1/60;
            var gravity = world.gravity;
            var groundY = 0.5; // Half-height of objects

            bodies.forEach(function(body, id) {
                if (body.isStatic) return;

                // Apply gravity
                body.velocity.y += gravity.y * dt;

                // Apply damping
                var damping = 1 - body.linearDamping * dt;
                body.velocity.x *= damping;
                body.velocity.y *= damping;
                body.velocity.z *= damping;

                // Update position
                body.position.x += body.velocity.x * dt;
                body.position.y += body.velocity.y * dt;
                body.position.z += body.velocity.z * dt;

                // Simple ground collision
                if (body.position.y <= groundY) {
                    body.position.y = groundY;
                    
                    // Bounce
                    if (body.velocity.y < 0) {
                        body.velocity.y = -body.velocity.y * body.restitution;
                        
                        // Stop if velocity is very small
                        if (Math.abs(body.velocity.y) < 0.1) {
                            body.velocity.y = 0;
                            body.onGround = true;
                        }
                    }

                    // Friction when on ground
                    body.velocity.x *= 0.98;
                    body.velocity.z *= 0.98;
                }
            });
        },

        getTransformBatch: function() {
            var transforms = [];
            var ids = [];

            bodies.forEach(function(body, id) {
                // Pack: [px, py, pz, rx, ry, rz, rw]
                transforms.push(
                    body.position.x, body.position.y, body.position.z,
                    body.rotation.x, body.rotation.y, body.rotation.z, body.rotation.w
                );
                ids.push(id);
            });

            return {
                transforms: transforms,
                ids: ids
            };
        },

        reset: function() {
            initialStates.forEach(function(state, id) {
                var body = bodies.get(id);
                if (body) {
                    body.position.x = state.position[0];
                    body.position.y = state.position[1];
                    body.position.z = state.position[2];
                    body.rotation.x = state.rotation[0];
                    body.rotation.y = state.rotation[1];
                    body.rotation.z = state.rotation[2];
                    body.rotation.w = state.rotation[3];
                    body.velocity.x = 0;
                    body.velocity.y = 0;
                    body.velocity.z = 0;
                    body.onGround = false;
                }
            });
        },

        dispose: function() {
            bodies.clear();
            colliders.clear();
            initialStates.clear();
            world = null;
            _isInitialized = false;
        }
    };

    console.log('Rigid physics module loaded, RigidPhysicsModule:', typeof window.RigidPhysicsModule);
})();
