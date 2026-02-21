/**
 * Physics Interop Module
 * Handles batched communication between Blazor and JS physics/rendering modules
 * Optimized for minimal per-frame overhead
 */

(function() {
    'use strict';

    // Suppress browser extension errors
    window.addEventListener('error', function(event) {
        if (event.message && event.message.includes('runtime.lastError')) {
            event.preventDefault();
            return true;
        }
    });

    window.addEventListener('unhandledrejection', function(event) {
        if (event.reason && event.reason.message && 
            event.reason.message.includes('runtime.lastError')) {
            event.preventDefault();
            return true;
        }
    });

    // Performance tracking
    let frameCount = 0;
    let lastFpsUpdate = performance.now();
    let currentFps = 0;
    let physicsTimeMs = 0;
    let renderTimeMs = 0;

    // Cache rendering module reference
    let _renderingModule = null;

    /**
     * Get rendering module with caching
     */
    function getRenderingModule() {
        if (!_renderingModule) {
            _renderingModule = window.RenderingModule;
        }
        return _renderingModule;
    }

    /**
     * Main interop object for Blazor communication
     */
    window.PhysicsInterop = {
        initialize: function(canvasId) {
            _renderingModule = window.RenderingModule;
            this.startFpsCounter();
            return true;
        },

        startFpsCounter: function() {
            const updateFps = () => {
                frameCount++;
                const now = performance.now();
                const elapsed = now - lastFpsUpdate;
                
                if (elapsed >= 1000) {
                    currentFps = (frameCount * 1000) / elapsed;
                    frameCount = 0;
                    lastFpsUpdate = now;
                }
                
                requestAnimationFrame(updateFps);
            };
            requestAnimationFrame(updateFps);
        },

        /**
         * Update rigid body transforms - optimized
         */
        updateRigidTransforms: function(transforms, ids) {
            if (!transforms || !ids || ids.length === 0) return;

            const renderingModule = getRenderingModule();
            if (!renderingModule) return;

            const len = ids.length;
            for (let i = 0; i < len; i++) {
                const offset = i * 7;
                renderingModule.updateMeshTransform(
                    ids[i],
                    [transforms[offset], transforms[offset + 1], transforms[offset + 2]],
                    [transforms[offset + 3], transforms[offset + 4], transforms[offset + 5], transforms[offset + 6]],
                    null
                );
            }
        },

        /**
         * Update single soft body vertices
         */
        updateSoftBodyVertices: function(id, vertices, normals) {
            if (!id || !vertices || vertices.length === 0) return;

            const renderingModule = getRenderingModule();
            if (renderingModule && renderingModule.updateSoftMeshVertices) {
                renderingModule.updateSoftMeshVertices(id, vertices, normals);
            }
        },

        /**
         * Batch update all soft bodies in single call - optimized
         */
        updateAllSoftBodies: function(vertexDataMap) {
            if (!vertexDataMap) return;

            const renderingModule = getRenderingModule();
            if (!renderingModule || !renderingModule.updateSoftMeshVertices) return;

            for (const id in vertexDataMap) {
                const data = vertexDataMap[id];
                if (data && data.vertices && data.vertices.length > 0) {
                    renderingModule.updateSoftMeshVertices(id, data.vertices, data.normals);
                }
            }
        },

        /**
         * Apply a complete frame update (batched)
         */
        applyFrame: function(frameData) {
            if (!frameData) return;
            
            const startTime = performance.now();

            if (frameData.rigidTransforms && frameData.rigidIds) {
                this.updateRigidTransforms(frameData.rigidTransforms, frameData.rigidIds);
            }

            if (frameData.softBodies) {
                this.updateAllSoftBodies(frameData.softBodies);
            }

            renderTimeMs = performance.now() - startTime;
        },

        getPerformanceStats: function() {
            return {
                fps: currentFps,
                frameTimeMs: 1000 / (currentFps || 60),
                physicsTimeMs: physicsTimeMs,
                renderTimeMs: renderTimeMs
            };
        },

        setPhysicsTime: function(ms) {
            physicsTimeMs = ms;
        }
    };
})();
