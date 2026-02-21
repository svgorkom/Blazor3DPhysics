/**
 * Physics Interop Module
 * Handles batched communication between Blazor and JS physics/rendering modules
 */

// Performance tracking
let frameCount = 0;
let lastFpsUpdate = performance.now();
let currentFps = 0;
let physicsTimeMs = 0;
let renderTimeMs = 0;

/**
 * Main interop object for Blazor communication
 */
window.PhysicsInterop = {
    /**
     * Initialize all modules
     * @param {string} canvasId - Canvas element ID
     */
    initialize: async function(canvasId) {
        console.log('Initializing Physics Interop...');
        
        // Modules will be initialized by their respective services
        // This is a coordination point
        
        // Start FPS counter
        this.startFpsCounter();
        
        console.log('Physics Interop initialized');
    },

    /**
     * Start FPS counter
     */
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
     * Update rigid body transforms from physics to rendering
     * @param {Float32Array} transforms - Packed transforms [px,py,pz,rx,ry,rz,rw,...]
     * @param {string[]} ids - Body IDs
     */
    updateRigidTransforms: function(transforms, ids) {
        if (!transforms || !ids || ids.length === 0) return;

        const stride = 7; // position (3) + quaternion (4)
        
        for (let i = 0; i < ids.length; i++) {
            const offset = i * stride;
            const id = ids[i];
            
            const position = [
                transforms[offset],
                transforms[offset + 1],
                transforms[offset + 2]
            ];
            const rotation = [
                transforms[offset + 3],
                transforms[offset + 4],
                transforms[offset + 5],
                transforms[offset + 6]
            ];

            // Update rendering
            if (window.RenderingModule) {
                window.RenderingModule.updateMeshTransform(id, position, rotation, null);
            }
        }
    },

    /**
     * Update soft body vertices from physics to rendering
     * @param {string} id - Soft body ID
     * @param {Float32Array} vertices - Vertex positions
     * @param {Float32Array} normals - Vertex normals (optional)
     */
    updateSoftBodyVertices: function(id, vertices, normals) {
        if (!id || !vertices) return;

        if (window.RenderingModule) {
            window.RenderingModule.updateSoftMeshVertices(id, vertices, normals);
        }
    },

    /**
     * Apply a complete frame update (batched)
     * @param {Object} frameData - Frame data with transforms and vertices
     */
    applyFrame: function(frameData) {
        const startTime = performance.now();

        // Update rigid bodies
        if (frameData.rigidTransforms && frameData.rigidIds) {
            this.updateRigidTransforms(frameData.rigidTransforms, frameData.rigidIds);
        }

        // Update soft bodies
        if (frameData.softBodies) {
            for (const [id, data] of Object.entries(frameData.softBodies)) {
                this.updateSoftBodyVertices(id, data.vertices, data.normals);
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
            const id = await window.RenderingModule.loadModel(url);
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
            const scene = JSON.parse(json);
            // Process scene data
            console.log('Importing scene:', scene);
            return true;
        } catch (e) {
            console.error('Failed to import scene:', e);
            return false;
        }
    }
};

// Keyboard shortcuts
document.addEventListener('keydown', (e) => {
    // Space - Play/Pause (handled in Blazor)
    // R - Reset (handled in Blazor)
    // S - Step (handled in Blazor)
    // G - Toggle grid
    if (e.key === 'g' || e.key === 'G') {
        // Toggle grid visibility
        // This would need to communicate back to Blazor
    }
});

console.log('Physics Interop module loaded');
