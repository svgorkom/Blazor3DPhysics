/**
 * Physics Interop Module
 * Handles batched communication between Blazor and JS physics/rendering modules
 * 
 * DIP Compliance: Dependencies are now injected rather than directly referenced
 */

(function() {
    'use strict';

    // Suppress browser extension errors (common with React DevTools, etc.)
    // These errors are not from our application
    window.addEventListener('error', function(event) {
        if (event.message && event.message.includes('runtime.lastError')) {
            event.preventDefault();
            return true;
        }
    });

    // Also handle unhandled promise rejections from extensions
    window.addEventListener('unhandledrejection', function(event) {
        if (event.reason && event.reason.message && 
            event.reason.message.includes('runtime.lastError')) {
            event.preventDefault();
            return true;
        }
    });

    // Performance tracking
    var frameCount = 0;
    var lastFpsUpdate = performance.now();
    var currentFps = 0;
    var physicsTimeMs = 0;
    var renderTimeMs = 0;

    /**
     * Main interop object for Blazor communication
     * DIP: Dependencies are injected, not hard-coded
     */
    window.PhysicsInterop = {
        // Injected dependencies (DIP compliance)
        _renderingModule: null,
        _rigidPhysicsModule: null,
        _softPhysicsModule: null,

        /**
         * Set the rendering module dependency
         * @param {object} module - The rendering module
         */
        setRenderingModule: function(module) {
            this._renderingModule = module;
        },

        /**
         * Set the rigid physics module dependency
         * @param {object} module - The rigid physics module
         */
        setRigidPhysicsModule: function(module) {
            this._rigidPhysicsModule = module;
        },

        /**
         * Set the soft physics module dependency
         * @param {object} module - The soft physics module
         */
        setSoftPhysicsModule: function(module) {
            this._softPhysicsModule = module;
        },

        /**
         * Get the rendering module (with fallback for backward compatibility)
         */
        getRenderingModule: function() {
            return this._renderingModule || window.RenderingModule;
        },

        /**
         * Initialize all modules
         * @param {string} canvasId - Canvas element ID
         */
        initialize: async function(canvasId) {
            console.log('PhysicsInterop.initialize called with canvasId:', canvasId);
            
            // Auto-inject default modules if not set (backward compatibility)
            if (!this._renderingModule && window.RenderingModule) {
                this._renderingModule = window.RenderingModule;
            }
            if (!this._rigidPhysicsModule && window.RigidPhysicsModule) {
                this._rigidPhysicsModule = window.RigidPhysicsModule;
            }
            if (!this._softPhysicsModule && window.SoftPhysicsModule) {
                this._softPhysicsModule = window.SoftPhysicsModule;
            }
            
            // Start FPS counter
            this.startFpsCounter();
            
            console.log('Physics Interop initialized');
            return true;
        },

        /**
         * Start FPS counter
         */
        startFpsCounter: function() {
            var self = this;
            var updateFps = function() {
                frameCount++;
                var now = performance.now();
                var elapsed = now - lastFpsUpdate;
                
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
         * Update rigid body transforms from physics to rendering
         * @param {Array} transforms - Packed transforms [px,py,pz,rx,ry,rz,rw,...]
         * @param {Array} ids - Body IDs
         */
        updateRigidTransforms: function(transforms, ids) {
            if (!transforms || !ids || ids.length === 0) return;

            var renderingModule = this.getRenderingModule();
            if (!renderingModule) return;

            var stride = 7; // position (3) + quaternion (4)
            
            for (var i = 0; i < ids.length; i++) {
                var offset = i * stride;
                var id = ids[i];
                
                var position = [
                    transforms[offset],
                    transforms[offset + 1],
                    transforms[offset + 2]
                ];
                var rotation = [
                    transforms[offset + 3],
                    transforms[offset + 4],
                    transforms[offset + 5],
                    transforms[offset + 6]
                ];

                // Update rendering using injected module
                renderingModule.updateMeshTransform(id, position, rotation, null);
            }
        },

        /**
         * Update soft body vertices from physics to rendering
         * @param {string} id - Soft body ID
         * @param {Array} vertices - Vertex positions
         * @param {Array} normals - Vertex normals (optional)
         */
        updateSoftBodyVertices: function(id, vertices, normals) {
            if (!id || !vertices) return;

            var renderingModule = this.getRenderingModule();
            if (renderingModule) {
                renderingModule.updateSoftMeshVertices(id, vertices, normals);
            }
        },

        /**
         * Apply a complete frame update (batched)
         * @param {Object} frameData - Frame data with transforms and vertices
         */
        applyFrame: function(frameData) {
            if (!frameData) return;
            
            var startTime = performance.now();

            // Update rigid bodies
            if (frameData.rigidTransforms && frameData.rigidIds) {
                this.updateRigidTransforms(frameData.rigidTransforms, frameData.rigidIds);
            }

            // Update soft bodies
            if (frameData.softBodies) {
                for (var id in frameData.softBodies) {
                    if (frameData.softBodies.hasOwnProperty(id)) {
                        var data = frameData.softBodies[id];
                        this.updateSoftBodyVertices(id, data.vertices, data.normals);
                    }
                }
            }

            renderTimeMs = performance.now() - startTime;
        },

        /**
         * Get current performance statistics
         */
        getPerformanceStats: function() {
            return {
                fps: currentFps,
                frameTimeMs: 1000 / (currentFps || 60),
                physicsTimeMs: physicsTimeMs,
                renderTimeMs: renderTimeMs
            };
        },

        /**
         * Set physics time for stats
         * @param {number} ms - Physics step time in milliseconds
         */
        setPhysicsTime: function(ms) {
            physicsTimeMs = ms;
        },

        /**
         * Handle file drop for model import
         * @param {DataTransfer} dataTransfer - Drop event data transfer
         */
        handleFileDrop: async function(dataTransfer) {
            if (!dataTransfer.files || dataTransfer.files.length === 0) return null;

            const file = dataTransfer.files[0];
            const validExtensions = ['.gltf', '.glb'];
            const ext = file.name.toLowerCase().substring(file.name.lastIndexOf('.'));

            if (!validExtensions.includes(ext)) {
                console.warn('Invalid file type:', ext);
                return null;
            }

            // Create object URL for loading
            const url = URL.createObjectURL(file);
            
            try {
                var renderingModule = this.getRenderingModule();
                if (!renderingModule) return null;
                
                const id = await renderingModule.loadModel(url);
                return { id, name: file.name };
            } finally {
                URL.revokeObjectURL(url);
            }
        },

        /**
         * Export current scene to JSON
         */
        exportScene: function() {
            // This would gather data from both physics modules
            // Implementation depends on scene state management
            return JSON.stringify({
                version: '1.0',
                exportedAt: new Date().toISOString()
            });
        },

        /**
         * Import scene from JSON
         * @param {string} json - Scene JSON string
         */
        importScene: async function(json) {
            try {
                var scene = JSON.parse(json);
                console.log('Importing scene:', scene);
                return true;
            } catch (e) {
                console.error('Failed to import scene:', e);
                return false;
            }
        }
    };

    /**
     * Download file utility function for scene export
     * Called from C# via IJSRuntime
     */
    window.downloadFile = function(filename, content, mimeType) {
        const blob = new Blob([content], { type: mimeType || 'application/octet-stream' });
        const url = URL.createObjectURL(blob);
        
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        link.style.display = 'none';
        
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        URL.revokeObjectURL(url);
    };

    /**
     * Read file content utility function for scene import
     */
    window.readFileContent = function(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error);
            reader.readAsText(file);
        });
    };

    // Keyboard shortcuts
    document.addEventListener('keydown', function(e) {
        // Space - Play/Pause (handled in Blazor)
        // R - Reset (handled in Blazor)
        // S - Step (handled in Blazor)
        // G - Toggle grid
        if (e.key === 'g' || e.key === 'G') {
            // Toggle grid visibility
            // This would need to communicate back to Blazor
        }
    });

    console.log('Physics Interop module loaded, PhysicsInterop:', typeof window.PhysicsInterop);
})();
