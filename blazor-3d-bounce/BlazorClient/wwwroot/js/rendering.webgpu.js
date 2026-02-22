/**
 * WebGPU Detection and Metrics Module
 * Handles WebGPU availability detection, capability reporting, and performance comparison
 * 
 * This module provides WebGPU support detection and benchmarking capabilities
 * alongside the existing WebGL2/Babylon.js renderer.
 */

(function() {
    'use strict';

    /**
     * Renderer backend types
     */
    const RendererBackend = {
        WebGPU: 'WebGPU',
        WebGL2: 'WebGL2',
        WebGL: 'WebGL',
        Unknown: 'Unknown'
    };

    /**
     * WebGPU capabilities and feature detection
     */
    const webGpuCapabilities = {
        isSupported: false,
        adapterName: null,
        vendor: null,
        architecture: null,
        features: [],
        limits: {},
        isFallbackAdapter: false,
        errorMessage: null
    };

    /**
     * Performance metrics tracking
     */
    const performanceMetrics = {
        backend: RendererBackend.Unknown,
        lastFrameTime: 0,
        averageFrameTime: 0,
        frameTimes: [],
        maxFrameTimesSamples: 120,
        gpuTime: 0,
        cpuTime: 0,
        drawCalls: 0,
        triangleCount: 0,
        textureMemory: 0,
        bufferMemory: 0,
        startTime: 0
    };

    /**
     * Detect WebGPU availability and capabilities
     * @returns {Promise<Object>} WebGPU capabilities object
     */
    async function detectWebGPU() {
        console.log('Detecting WebGPU support...');

        // Check if WebGPU API is available
        if (!navigator.gpu) {
            console.log('WebGPU API not available in this browser');
            webGpuCapabilities.isSupported = false;
            webGpuCapabilities.errorMessage = 'WebGPU API not available';
            return webGpuCapabilities;
        }

        try {
            // Request adapter
            const adapter = await navigator.gpu.requestAdapter({
                powerPreference: 'high-performance'
            });

            if (!adapter) {
                console.log('No WebGPU adapter available');
                webGpuCapabilities.isSupported = false;
                webGpuCapabilities.errorMessage = 'No WebGPU adapter found';
                return webGpuCapabilities;
            }

            // Get adapter info
            const adapterInfo = await adapter.requestAdapterInfo();
            
            webGpuCapabilities.isSupported = true;
            webGpuCapabilities.adapterName = adapterInfo.device || 'Unknown Device';
            webGpuCapabilities.vendor = adapterInfo.vendor || 'Unknown Vendor';
            webGpuCapabilities.architecture = adapterInfo.architecture || 'Unknown';
            webGpuCapabilities.isFallbackAdapter = adapter.isFallbackAdapter || false;

            // Get features
            webGpuCapabilities.features = Array.from(adapter.features);

            // Get limits
            const limits = adapter.limits;
            webGpuCapabilities.limits = {
                maxTextureDimension1D: limits.maxTextureDimension1D,
                maxTextureDimension2D: limits.maxTextureDimension2D,
                maxTextureDimension3D: limits.maxTextureDimension3D,
                maxTextureArrayLayers: limits.maxTextureArrayLayers,
                maxBindGroups: limits.maxBindGroups,
                maxBindingsPerBindGroup: limits.maxBindingsPerBindGroup,
                maxDynamicUniformBuffersPerPipelineLayout: limits.maxDynamicUniformBuffersPerPipelineLayout,
                maxDynamicStorageBuffersPerPipelineLayout: limits.maxDynamicStorageBuffersPerPipelineLayout,
                maxSampledTexturesPerShaderStage: limits.maxSampledTexturesPerShaderStage,
                maxSamplersPerShaderStage: limits.maxSamplersPerShaderStage,
                maxStorageBuffersPerShaderStage: limits.maxStorageBuffersPerShaderStage,
                maxStorageTexturesPerShaderStage: limits.maxStorageTexturesPerShaderStage,
                maxUniformBuffersPerShaderStage: limits.maxUniformBuffersPerShaderStage,
                maxUniformBufferBindingSize: limits.maxUniformBufferBindingSize,
                maxStorageBufferBindingSize: limits.maxStorageBufferBindingSize,
                maxVertexBuffers: limits.maxVertexBuffers,
                maxBufferSize: limits.maxBufferSize,
                maxVertexAttributes: limits.maxVertexAttributes,
                maxVertexBufferArrayStride: limits.maxVertexBufferArrayStride,
                maxComputeWorkgroupStorageSize: limits.maxComputeWorkgroupStorageSize,
                maxComputeInvocationsPerWorkgroup: limits.maxComputeInvocationsPerWorkgroup,
                maxComputeWorkgroupSizeX: limits.maxComputeWorkgroupSizeX,
                maxComputeWorkgroupSizeY: limits.maxComputeWorkgroupSizeY,
                maxComputeWorkgroupSizeZ: limits.maxComputeWorkgroupSizeZ,
                maxComputeWorkgroupsPerDimension: limits.maxComputeWorkgroupsPerDimension
            };

            console.log('WebGPU detected:', webGpuCapabilities);
            return webGpuCapabilities;

        } catch (error) {
            console.error('WebGPU detection error:', error);
            webGpuCapabilities.isSupported = false;
            webGpuCapabilities.errorMessage = error.message;
            return webGpuCapabilities;
        }
    }

    /**
     * Detect WebGL2 availability
     * @returns {Object} WebGL2 capabilities
     */
    function detectWebGL2() {
        const canvas = document.createElement('canvas');
        const gl = canvas.getContext('webgl2');
        
        if (!gl) {
            return {
                isSupported: false,
                errorMessage: 'WebGL2 not available'
            };
        }

        const debugInfo = gl.getExtension('WEBGL_debug_renderer_info');
        
        return {
            isSupported: true,
            vendor: debugInfo ? gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL) : 'Unknown',
            renderer: debugInfo ? gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL) : 'Unknown',
            version: gl.getParameter(gl.VERSION),
            shadingLanguageVersion: gl.getParameter(gl.SHADING_LANGUAGE_VERSION),
            maxTextureSize: gl.getParameter(gl.MAX_TEXTURE_SIZE),
            maxCubeMapTextureSize: gl.getParameter(gl.MAX_CUBE_MAP_TEXTURE_SIZE),
            maxRenderbufferSize: gl.getParameter(gl.MAX_RENDERBUFFER_SIZE),
            maxVertexAttributes: gl.getParameter(gl.MAX_VERTEX_ATTRIBS),
            maxTextureImageUnits: gl.getParameter(gl.MAX_TEXTURE_IMAGE_UNITS),
            maxVertexTextureImageUnits: gl.getParameter(gl.MAX_VERTEX_TEXTURE_IMAGE_UNITS),
            maxVaryingVectors: gl.getParameter(gl.MAX_VARYING_VECTORS),
            maxVertexUniformVectors: gl.getParameter(gl.MAX_VERTEX_UNIFORM_VECTORS),
            maxFragmentUniformVectors: gl.getParameter(gl.MAX_FRAGMENT_UNIFORM_VECTORS)
        };
    }

    /**
     * Detect WebGL1 availability (legacy fallback)
     * @returns {Object} WebGL1 capabilities
     */
    function detectWebGL() {
        const canvas = document.createElement('canvas');
        const gl = canvas.getContext('webgl') || canvas.getContext('experimental-webgl');
        
        if (!gl) {
            return {
                isSupported: false,
                errorMessage: 'WebGL not available'
            };
        }

        return {
            isSupported: true,
            version: gl.getParameter(gl.VERSION)
        };
    }

    /**
     * Get the recommended renderer backend based on availability and preference
     * @param {string} preferredBackend - User preferred backend ('WebGPU', 'WebGL2', 'WebGL', 'Auto')
     * @returns {Promise<Object>} Renderer selection result
     */
    async function selectRendererBackend(preferredBackend = 'Auto') {
        const result = {
            selectedBackend: RendererBackend.Unknown,
            webgpu: null,
            webgl2: null,
            webgl: null,
            fallbackReason: null
        };

        // Detect all backends
        result.webgpu = await detectWebGPU();
        result.webgl2 = detectWebGL2();
        result.webgl = detectWebGL();

        // Select based on preference
        if (preferredBackend === 'WebGPU' || preferredBackend === 'Auto') {
            if (result.webgpu.isSupported && !result.webgpu.isFallbackAdapter) {
                result.selectedBackend = RendererBackend.WebGPU;
                console.log('Selected WebGPU renderer');
                return result;
            } else if (preferredBackend === 'WebGPU') {
                result.fallbackReason = result.webgpu.errorMessage || 'WebGPU not available or using fallback adapter';
            }
        }

        if (preferredBackend === 'WebGL2' || preferredBackend === 'Auto' || result.fallbackReason) {
            if (result.webgl2.isSupported) {
                result.selectedBackend = RendererBackend.WebGL2;
                if (result.fallbackReason) {
                    console.log('Falling back to WebGL2:', result.fallbackReason);
                } else {
                    console.log('Selected WebGL2 renderer');
                }
                return result;
            } else if (preferredBackend === 'WebGL2') {
                result.fallbackReason = result.webgl2.errorMessage || 'WebGL2 not available';
            }
        }

        if (result.webgl.isSupported) {
            result.selectedBackend = RendererBackend.WebGL;
            console.log('Falling back to WebGL1:', result.fallbackReason || 'WebGL2 not available');
            return result;
        }

        result.fallbackReason = 'No supported rendering backend available';
        console.error('No rendering backend available');
        return result;
    }

    /**
     * Record a frame time for performance tracking
     * @param {number} frameTimeMs - Frame time in milliseconds
     */
    function recordFrameTime(frameTimeMs) {
        performanceMetrics.lastFrameTime = frameTimeMs;
        performanceMetrics.frameTimes.push(frameTimeMs);
        
        // Keep only recent samples
        if (performanceMetrics.frameTimes.length > performanceMetrics.maxFrameTimesSamples) {
            performanceMetrics.frameTimes.shift();
        }

        // Calculate average
        const sum = performanceMetrics.frameTimes.reduce((a, b) => a + b, 0);
        performanceMetrics.averageFrameTime = sum / performanceMetrics.frameTimes.length;
    }

    /**
     * Update performance metrics
     * @param {Object} metrics - Metrics to update
     */
    function updatePerformanceMetrics(metrics) {
        if (metrics.backend) performanceMetrics.backend = metrics.backend;
        if (metrics.gpuTime !== undefined) performanceMetrics.gpuTime = metrics.gpuTime;
        if (metrics.cpuTime !== undefined) performanceMetrics.cpuTime = metrics.cpuTime;
        if (metrics.drawCalls !== undefined) performanceMetrics.drawCalls = metrics.drawCalls;
        if (metrics.triangleCount !== undefined) performanceMetrics.triangleCount = metrics.triangleCount;
        if (metrics.textureMemory !== undefined) performanceMetrics.textureMemory = metrics.textureMemory;
        if (metrics.bufferMemory !== undefined) performanceMetrics.bufferMemory = metrics.bufferMemory;
    }

    /**
     * Get current performance metrics
     * @returns {Object} Performance metrics
     */
    function getPerformanceMetrics() {
        return {
            ...performanceMetrics,
            fps: performanceMetrics.averageFrameTime > 0 ? 1000 / performanceMetrics.averageFrameTime : 0,
            frameTimeMs: performanceMetrics.averageFrameTime,
            percentile95: calculatePercentile(performanceMetrics.frameTimes, 0.95),
            percentile99: calculatePercentile(performanceMetrics.frameTimes, 0.99),
            minFrameTime: Math.min(...performanceMetrics.frameTimes) || 0,
            maxFrameTime: Math.max(...performanceMetrics.frameTimes) || 0
        };
    }

    /**
     * Calculate percentile from an array of values
     * @param {number[]} arr - Array of values
     * @param {number} p - Percentile (0-1)
     * @returns {number} Percentile value
     */
    function calculatePercentile(arr, p) {
        if (arr.length === 0) return 0;
        const sorted = [...arr].sort((a, b) => a - b);
        const index = Math.ceil(p * sorted.length) - 1;
        return sorted[Math.max(0, index)];
    }

    /**
     * Reset performance metrics
     */
    function resetPerformanceMetrics() {
        performanceMetrics.frameTimes = [];
        performanceMetrics.averageFrameTime = 0;
        performanceMetrics.lastFrameTime = 0;
        performanceMetrics.startTime = performance.now();
    }

    /**
     * Run a simple rendering benchmark to compare backends
     * @param {string} canvasId - Canvas element ID
     * @returns {Promise<Object>} Benchmark results
     */
    async function runBenchmark(canvasId) {
        const results = {
            webgl2: null,
            webgpu: null,
            recommendation: null
        };

        console.log('Running renderer benchmark...');

        // WebGL2 benchmark
        try {
            results.webgl2 = await benchmarkWebGL2(canvasId);
        } catch (e) {
            console.error('WebGL2 benchmark failed:', e);
            results.webgl2 = { error: e.message };
        }

        // WebGPU benchmark (if available)
        if (webGpuCapabilities.isSupported) {
            try {
                results.webgpu = await benchmarkWebGPU(canvasId);
            } catch (e) {
                console.error('WebGPU benchmark failed:', e);
                results.webgpu = { error: e.message };
            }
        }

        // Determine recommendation
        if (results.webgpu && !results.webgpu.error && results.webgl2 && !results.webgl2.error) {
            results.recommendation = results.webgpu.avgFrameTime < results.webgl2.avgFrameTime 
                ? 'WebGPU' 
                : 'WebGL2';
        } else if (results.webgl2 && !results.webgl2.error) {
            results.recommendation = 'WebGL2';
        } else {
            results.recommendation = 'WebGL';
        }

        console.log('Benchmark complete:', results);
        return results;
    }

    /**
     * Simple WebGL2 benchmark
     * @param {string} canvasId - Canvas element ID
     * @returns {Promise<Object>} Benchmark results
     */
    async function benchmarkWebGL2(canvasId) {
        const canvas = document.getElementById(canvasId) || document.createElement('canvas');
        const gl = canvas.getContext('webgl2');
        
        if (!gl) {
            return { error: 'WebGL2 not available' };
        }

        const iterations = 100;
        const times = [];
        
        // Simple triangle rendering benchmark
        const vertexShaderSource = `#version 300 es
            in vec4 a_position;
            void main() {
                gl_Position = a_position;
            }
        `;
        
        const fragmentShaderSource = `#version 300 es
            precision highp float;
            out vec4 outColor;
            void main() {
                outColor = vec4(1.0, 0.5, 0.2, 1.0);
            }
        `;

        function createShader(gl, type, source) {
            const shader = gl.createShader(type);
            gl.shaderSource(shader, source);
            gl.compileShader(shader);
            return shader;
        }

        const vertexShader = createShader(gl, gl.VERTEX_SHADER, vertexShaderSource);
        const fragmentShader = createShader(gl, gl.FRAGMENT_SHADER, fragmentShaderSource);
        
        const program = gl.createProgram();
        gl.attachShader(program, vertexShader);
        gl.attachShader(program, fragmentShader);
        gl.linkProgram(program);
        gl.useProgram(program);

        const positions = new Float32Array([
            0, 0.5,
            -0.5, -0.5,
            0.5, -0.5,
        ]);

        const buffer = gl.createBuffer();
        gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
        gl.bufferData(gl.ARRAY_BUFFER, positions, gl.STATIC_DRAW);

        const positionLocation = gl.getAttribLocation(program, 'a_position');
        gl.enableVertexAttribArray(positionLocation);
        gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);

        // Warm up
        for (let i = 0; i < 10; i++) {
            gl.clear(gl.COLOR_BUFFER_BIT);
            gl.drawArrays(gl.TRIANGLES, 0, 3);
        }
        gl.finish();

        // Benchmark
        for (let i = 0; i < iterations; i++) {
            const start = performance.now();
            gl.clear(gl.COLOR_BUFFER_BIT);
            for (let j = 0; j < 100; j++) {
                gl.drawArrays(gl.TRIANGLES, 0, 3);
            }
            gl.finish();
            times.push(performance.now() - start);
        }

        // Cleanup
        gl.deleteProgram(program);
        gl.deleteShader(vertexShader);
        gl.deleteShader(fragmentShader);
        gl.deleteBuffer(buffer);

        const avgFrameTime = times.reduce((a, b) => a + b, 0) / times.length;
        
        return {
            avgFrameTime,
            minFrameTime: Math.min(...times),
            maxFrameTime: Math.max(...times),
            iterations
        };
    }

    /**
     * Simple WebGPU benchmark
     * @param {string} canvasId - Canvas element ID
     * @returns {Promise<Object>} Benchmark results
     */
    async function benchmarkWebGPU(canvasId) {
        if (!navigator.gpu) {
            return { error: 'WebGPU not available' };
        }

        const adapter = await navigator.gpu.requestAdapter();
        if (!adapter) {
            return { error: 'No WebGPU adapter' };
        }

        const device = await adapter.requestDevice();
        const canvas = document.getElementById(canvasId) || document.createElement('canvas');
        canvas.width = canvas.width || 800;
        canvas.height = canvas.height || 600;
        
        const context = canvas.getContext('webgpu');
        if (!context) {
            return { error: 'Cannot get WebGPU context' };
        }

        const format = navigator.gpu.getPreferredCanvasFormat();
        context.configure({
            device,
            format,
            alphaMode: 'premultiplied'
        });

        const shaderModule = device.createShaderModule({
            code: `
                @vertex
                fn vertexMain(@builtin(vertex_index) vertexIndex : u32) -> @builtin(position) vec4f {
                    var pos = array<vec2f, 3>(
                        vec2f(0.0, 0.5),
                        vec2f(-0.5, -0.5),
                        vec2f(0.5, -0.5)
                    );
                    return vec4f(pos[vertexIndex], 0.0, 1.0);
                }

                @fragment
                fn fragmentMain() -> @location(0) vec4f {
                    return vec4f(1.0, 0.5, 0.2, 1.0);
                }
            `
        });

        const pipeline = device.createRenderPipeline({
            layout: 'auto',
            vertex: {
                module: shaderModule,
                entryPoint: 'vertexMain'
            },
            fragment: {
                module: shaderModule,
                entryPoint: 'fragmentMain',
                targets: [{ format }]
            },
            primitive: {
                topology: 'triangle-list'
            }
        });

        const iterations = 100;
        const times = [];

        // Warm up
        for (let i = 0; i < 10; i++) {
            const commandEncoder = device.createCommandEncoder();
            const renderPass = commandEncoder.beginRenderPass({
                colorAttachments: [{
                    view: context.getCurrentTexture().createView(),
                    clearValue: { r: 0, g: 0, b: 0, a: 1 },
                    loadOp: 'clear',
                    storeOp: 'store'
                }]
            });
            renderPass.setPipeline(pipeline);
            renderPass.draw(3);
            renderPass.end();
            device.queue.submit([commandEncoder.finish()]);
        }
        await device.queue.onSubmittedWorkDone();

        // Benchmark
        for (let i = 0; i < iterations; i++) {
            const start = performance.now();
            const commandEncoder = device.createCommandEncoder();
            for (let j = 0; j < 100; j++) {
                const renderPass = commandEncoder.beginRenderPass({
                    colorAttachments: [{
                        view: context.getCurrentTexture().createView(),
                        clearValue: { r: 0, g: 0, b: 0, a: 1 },
                        loadOp: 'clear',
                        storeOp: 'store'
                    }]
                });
                renderPass.setPipeline(pipeline);
                renderPass.draw(3);
                renderPass.end();
            }
            device.queue.submit([commandEncoder.finish()]);
            await device.queue.onSubmittedWorkDone();
            times.push(performance.now() - start);
        }

        // Cleanup
        device.destroy();

        const avgFrameTime = times.reduce((a, b) => a + b, 0) / times.length;
        
        return {
            avgFrameTime,
            minFrameTime: Math.min(...times),
            maxFrameTime: Math.max(...times),
            iterations
        };
    }

    /**
     * Export WebGPU module to global scope
     */
    window.WebGPUModule = {
        RendererBackend,
        
        /**
         * Detect WebGPU support and capabilities
         */
        detectWebGPU,
        
        /**
         * Detect WebGL2 support and capabilities
         */
        detectWebGL2,
        
        /**
         * Detect WebGL1 support (legacy fallback)
         */
        detectWebGL,
        
        /**
         * Select the best available renderer backend
         */
        selectRendererBackend,
        
        /**
         * Get WebGPU capabilities (after detection)
         */
        getCapabilities: () => ({ ...webGpuCapabilities }),
        
        /**
         * Record frame time for metrics
         */
        recordFrameTime,
        
        /**
         * Update performance metrics
         */
        updatePerformanceMetrics,
        
        /**
         * Get current performance metrics
         */
        getPerformanceMetrics,
        
        /**
         * Reset performance metrics
         */
        resetPerformanceMetrics,
        
        /**
         * Run renderer benchmark
         */
        runBenchmark,
        
        /**
         * Check if WebGPU is supported
         */
        isWebGPUSupported: () => webGpuCapabilities.isSupported,
        
        /**
         * Get all detected renderer capabilities
         */
        getAllCapabilities: async () => ({
            webgpu: await detectWebGPU(),
            webgl2: detectWebGL2(),
            webgl: detectWebGL()
        })
    };

    console.log('WebGPU module loaded');
})();
