/**
 * Babylon.js Rendering Module
 * Handles 3D scene rendering, camera, lights, and mesh management
 * 
 * OCP Compliance: Mesh and material creation uses registry pattern
 * WebGPU Support: Automatic detection with fallback to WebGL2
 */

(function() {
    'use strict';

    // Global state
    let engine = null;
    let scene = null;
    let camera = null;
    let ground = null;
    let gridHelper = null;
    let axesHelper = null;
    let highlightLayer = null;
    let shadowGenerator = null;
    let meshes = new Map();
    let softMeshes = new Map();
    let softMeshData = new Map();
    let settings = {};
    
    // Renderer backend state
    let activeBackend = 'Unknown';
    let backendCapabilities = null;
    let rendererInfo = {
        backend: 'Unknown',
        vendor: null,
        renderer: null,
        version: null,
        isWebGPU: false,
        isFallback: false,
        fallbackReason: null
    };

    // Performance tracking
    let lastFrameTime = 0;
    let frameCount = 0;
    let fpsUpdateInterval = null;

    /**
     * Mesh creator registry (OCP - extensible without modification)
     */
    const meshCreators = {
        sphere: function(id, scn, options) {
            return BABYLON.MeshBuilder.CreateSphere(id, {
                diameter: options.diameter || 1,
                segments: options.segments || 32
            }, scn);
        },
        box: function(id, scn, options) {
            return BABYLON.MeshBuilder.CreateBox(id, {
                size: options.size || 1
            }, scn);
        },
        capsule: function(id, scn, options) {
            return BABYLON.MeshBuilder.CreateCapsule(id, {
                radius: options.radius || 0.5,
                height: options.height || 2,
                tessellation: options.tessellation || 32
            }, scn);
        },
        cylinder: function(id, scn, options) {
            return BABYLON.MeshBuilder.CreateCylinder(id, {
                diameter: options.diameter || 1,
                height: options.height || 1,
                tessellation: options.tessellation || 32
            }, scn);
        },
        cone: function(id, scn, options) {
            return BABYLON.MeshBuilder.CreateCylinder(id, {
                diameterTop: 0,
                diameterBottom: options.diameterBottom || 1,
                height: options.height || 1,
                tessellation: options.tessellation || 32
            }, scn);
        }
    };

    /**
     * Material creator registry (OCP - extensible without modification)
     */
    const materialCreators = {
        rubber: function(id, scn) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.8, 0.2, 0.2);
            material.metallic = 0.0;
            material.roughness = 0.7;
            return material;
        },
        wood: function(id, scn) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.6, 0.4, 0.2);
            material.metallic = 0.0;
            material.roughness = 0.8;
            return material;
        },
        steel: function(id, scn) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.7, 0.7, 0.75);
            material.metallic = 0.9;
            material.roughness = 0.3;
            return material;
        },
        ice: function(id, scn) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.7, 0.9, 1.0);
            material.metallic = 0.0;
            material.roughness = 0.1;
            material.alpha = 0.8;
            return material;
        },
        default: function(id, scn) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.5, 0.5, 0.6);
            material.metallic = 0.2;
            material.roughness = 0.5;
            return material;
        }
    };

    /**
     * Soft mesh creator registry (OCP - extensible without modification)
     */
    const softMeshCreators = {
        cloth: function(id, scn, data) {
            const width = data.width || 2;
            const height = data.height || 2;
            const resX = data.resolutionX || 20;
            const resY = data.resolutionY || 20;
            
            const mesh = BABYLON.MeshBuilder.CreateGround(id, {
                width: width,
                height: height,
                subdivisions: Math.max(resX, resY),
                updatable: true
            }, scn);
            
            mesh.rotation.x = -Math.PI / 2;
            return mesh;
        },
        
        volumetric: function(id, scn, data) {
            const radius = data.radius || 0.5;
            
            const mesh = BABYLON.MeshBuilder.CreateSphere(id, {
                diameter: radius * 2,
                segments: data.resolutionX || 12,
                updatable: true
            }, scn);
            
            return mesh;
        }
    };

    /**
     * Soft material creator registry
     */
    const softMaterialCreators = {
        cloth: function(id, scn, data) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.2, 0.5, 0.8);
            material.metallic = 0.0;
            material.roughness = 0.8;
            material.backFaceCulling = false;
            material.twoSidedLighting = true;
            return material;
        },
        
        volumetric: function(id, scn, data) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.8, 0.3, 0.3);
            material.metallic = 0.0;
            material.roughness = 0.4;
            material.alpha = 0.9;
            return material;
        },
        
        default: function(id, scn, data) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scn);
            material.albedoColor = new BABYLON.Color3(0.2, 0.6, 0.8);
            material.metallic = 0.0;
            material.roughness = 0.6;
            material.backFaceCulling = false;
            return material;
        }
    };

    /**
     * Determine the best rendering backend to use
     */
    async function selectRenderingBackend(preferredBackend) {
        preferredBackend = preferredBackend || 'Auto';
        console.log('Selecting rendering backend, preference:', preferredBackend);
        
        if (window.WebGPUModule) {
            const result = await window.WebGPUModule.selectRendererBackend(preferredBackend);
            backendCapabilities = result;
            return result;
        }
        
        const result = {
            selectedBackend: 'WebGL2',
            webgpu: { isSupported: false },
            webgl2: { isSupported: true },
            fallbackReason: 'WebGPU module not loaded'
        };
        
        if (navigator.gpu && (preferredBackend === 'WebGPU' || preferredBackend === 'Auto')) {
            try {
                const adapter = await navigator.gpu.requestAdapter();
                if (adapter && !adapter.isFallbackAdapter) {
                    result.selectedBackend = 'WebGPU';
                    result.webgpu.isSupported = true;
                    result.fallbackReason = null;
                }
            } catch (e) {
                console.log('WebGPU not available:', e.message);
            }
        }
        
        backendCapabilities = result;
        return result;
    }

    /**
     * Create Babylon.js engine with the appropriate backend
     */
    async function createEngineForBackend(canvas, backend, engineOptions) {
        console.log('Creating engine for backend:', backend);
        
        const baseOptions = {
            preserveDrawingBuffer: true,
            stencil: true,
            antialias: true,
            ...engineOptions
        };
        
        if (backend === 'WebGPU') {
            try {
                if (BABYLON.WebGPUEngine) {
                    console.log('Creating WebGPU engine...');
                    const webgpuEngine = new BABYLON.WebGPUEngine(canvas, baseOptions);
                    await webgpuEngine.initAsync();
                    
                    rendererInfo.backend = 'WebGPU';
                    rendererInfo.isWebGPU = true;
                    rendererInfo.isFallback = false;
                    
                    if (webgpuEngine._adapter) {
                        try {
                            if (webgpuEngine._adapter.info) {
                                rendererInfo.vendor = webgpuEngine._adapter.info.vendor || 'Unknown';
                                rendererInfo.renderer = webgpuEngine._adapter.info.device || 'WebGPU Device';
                            } else if (typeof webgpuEngine._adapter.requestAdapterInfo === 'function') {
                                const adapterInfo = await webgpuEngine._adapter.requestAdapterInfo();
                                rendererInfo.vendor = adapterInfo.vendor || 'Unknown';
                                rendererInfo.renderer = adapterInfo.device || 'WebGPU Device';
                            } else {
                                rendererInfo.vendor = 'Unknown';
                                rendererInfo.renderer = 'WebGPU Device';
                            }
                        } catch (adapterErr) {
                            console.warn('Could not get adapter info:', adapterErr);
                            rendererInfo.vendor = 'Unknown';
                            rendererInfo.renderer = 'WebGPU Device';
                        }
                    }
                    
                    console.log('WebGPU engine created successfully');
                    activeBackend = 'WebGPU';
                    return webgpuEngine;
                } else {
                    console.warn('BABYLON.WebGPUEngine not available, falling back to WebGL2');
                    rendererInfo.fallbackReason = 'BABYLON.WebGPUEngine not loaded';
                }
            } catch (e) {
                console.error('WebGPU engine creation failed:', e);
                rendererInfo.fallbackReason = e.message;
            }
        }
        
        console.log('Creating WebGL engine...');
        const webglEngine = new BABYLON.Engine(canvas, true, baseOptions);
        
        const gl = webglEngine._gl;
        if (gl instanceof WebGL2RenderingContext) {
            rendererInfo.backend = 'WebGL2';
            rendererInfo.version = gl.getParameter(gl.VERSION);
        } else {
            rendererInfo.backend = 'WebGL';
            rendererInfo.version = gl ? gl.getParameter(gl.VERSION) : 'Unknown';
        }
        
        rendererInfo.isWebGPU = false;
        rendererInfo.isFallback = backend === 'WebGPU';
        
        const debugInfo = gl ? gl.getExtension('WEBGL_debug_renderer_info') : null;
        if (debugInfo) {
            rendererInfo.vendor = gl.getParameter(debugInfo.UNMASKED_VENDOR_WEBGL);
            rendererInfo.renderer = gl.getParameter(debugInfo.UNMASKED_RENDERER_WEBGL);
        }
        
        activeBackend = rendererInfo.backend;
        console.log('WebGL engine created:', rendererInfo.backend);
        return webglEngine;
    }

    function startPerformanceTracking() {
        lastFrameTime = performance.now();
        frameCount = 0;
        
        if (window.WebGPUModule) {
            window.WebGPUModule.resetPerformanceMetrics();
        }
    }

    function trackFrame() {
        const now = performance.now();
        const frameTime = now - lastFrameTime;
        lastFrameTime = now;
        frameCount++;
        
        if (window.WebGPUModule) {
            window.WebGPUModule.recordFrameTime(frameTime);
            window.WebGPUModule.updatePerformanceMetrics({
                backend: activeBackend,
                drawCalls: scene ? scene._activeMeshes.length : 0,
                triangleCount: scene ? scene._totalVertices / 3 : 0
            });
        }
    }

    /**
     * Initialize the Babylon.js rendering engine - MAIN MODULE
     */
    window.RenderingModule = {
        isInitialized: function() {
            return engine !== null && scene !== null;
        },

        initialize: async function(canvasId, renderSettings) {
            console.log('RenderingModule.initialize called with canvasId:', canvasId);
            
            const canvas = document.getElementById(canvasId);
            if (!canvas) {
                console.error('Canvas not found:', canvasId);
                return false;
            }

            if (typeof BABYLON === 'undefined') {
                console.error('Babylon.js not loaded');
                return false;
            }

            settings = renderSettings || {};

            try {
                let retries = 0;
                while ((canvas.clientWidth === 0 || canvas.clientHeight === 0) && retries < 10) {
                    console.log('Canvas has no dimensions, waiting for layout... (attempt ' + (retries + 1) + ')');
                    await new Promise(resolve => setTimeout(resolve, 100));
                    retries++;
                }

                canvas.width = canvas.clientWidth || window.innerWidth || 800;
                canvas.height = canvas.clientHeight || window.innerHeight || 600;
                
                console.log('Canvas dimensions:', canvas.width, 'x', canvas.height);

                const preferredBackend = settings.preferredBackend || 'Auto';
                const backendSelection = await selectRenderingBackend(preferredBackend);
                console.log('Backend selection:', backendSelection);

                engine = await createEngineForBackend(canvas, backendSelection.selectedBackend, {
                    preserveDrawingBuffer: true,
                    stencil: true,
                    antialias: true
                });
                
                if (!engine) {
                    console.error('Failed to create engine');
                    return false;
                }

                scene = new BABYLON.Scene(engine);
                
                if (!scene) {
                    console.error('Failed to create scene');
                    return false;
                }
                
                scene.clearColor = new BABYLON.Color4(0.1, 0.1, 0.15, 1);

                camera = new BABYLON.ArcRotateCamera(
                    "camera",
                    -Math.PI / 4,
                    Math.PI / 3,
                    20,
                    new BABYLON.Vector3(0, 2, 0),
                    scene
                );
                camera.attachControl(canvas, true);
                camera.minZ = 0.1;
                camera.maxZ = 1000;
                camera.wheelPrecision = 20;
                camera.lowerRadiusLimit = 2;
                camera.upperRadiusLimit = 100;

                const hemiLight = new BABYLON.HemisphericLight(
                    "hemiLight",
                    new BABYLON.Vector3(0, 1, 0),
                    scene
                );
                hemiLight.intensity = 0.4;

                const dirLight = new BABYLON.DirectionalLight(
                    "dirLight",
                    new BABYLON.Vector3(-1, -2, -1),
                    scene
                );
                dirLight.position = new BABYLON.Vector3(10, 20, 10);
                dirLight.intensity = 0.8;

                if (settings.enableShadows !== false) {
                    shadowGenerator = new BABYLON.ShadowGenerator(
                        settings.shadowMapSize || 2048,
                        dirLight
                    );
                    shadowGenerator.useBlurExponentialShadowMap = true;
                    shadowGenerator.blurKernel = 32;
                }

                highlightLayer = new BABYLON.HighlightLayer("highlight", scene);

                this.createGround();

                if (settings.showGrid !== false) {
                    this.createGrid();
                }
                if (settings.showAxes !== false) {
                    this.createAxes();
                }

                if (settings.enableFXAA !== false) {
                    new BABYLON.FxaaPostProcess("fxaa", 1.0, camera);
                }

                startPerformanceTracking();

                engine.runRenderLoop(function() {
                    trackFrame();
                    if (scene) {
                        scene.render();
                    }
                });

                window.addEventListener('resize', function() {
                    if (engine) {
                        engine.resize();
                    }
                });

                engine.resize();

                console.log('Rendering module initialized successfully');
                console.log('Active backend:', activeBackend);
                console.log('Renderer info:', rendererInfo);
                console.log('Scene has', scene.meshes.length, 'meshes');
                return true;
            } catch (e) {
                console.error('Failed to initialize rendering:', e);
                if (scene) {
                    scene.dispose();
                    scene = null;
                }
                if (engine) {
                    engine.dispose();
                    engine = null;
                }
                return false;
            }
        },

        createGround: function() {
            ground = BABYLON.MeshBuilder.CreateGround("ground", {
                width: 50,
                height: 50,
                subdivisions: 50
            }, scene);

            const groundMaterial = new BABYLON.PBRMaterial("groundMat", scene);
            groundMaterial.albedoColor = new BABYLON.Color3(0.15, 0.15, 0.2);
            groundMaterial.metallic = 0.1;
            groundMaterial.roughness = 0.8;
            ground.material = groundMaterial;
            ground.receiveShadows = true;
        },

        createGrid: function() {
            gridHelper = BABYLON.MeshBuilder.CreateLineSystem("grid", {
                lines: this.generateGridLines(20, 1)
            }, scene);
            gridHelper.color = new BABYLON.Color3(0.3, 0.3, 0.35);
            gridHelper.position.y = 0.01;
        },

        generateGridLines: function(size, step) {
            const lines = [];
            const half = size / 2;

            for (let i = -half; i <= half; i += step) {
                lines.push([
                    new BABYLON.Vector3(i, 0, -half),
                    new BABYLON.Vector3(i, 0, half)
                ]);
                lines.push([
                    new BABYLON.Vector3(-half, 0, i),
                    new BABYLON.Vector3(half, 0, i)
                ]);
            }

            return lines;
        },

        createAxes: function() {
            const axisLength = 3;
            
            const xAxis = BABYLON.MeshBuilder.CreateLines("xAxis", {
                points: [BABYLON.Vector3.Zero(), new BABYLON.Vector3(axisLength, 0, 0)]
            }, scene);
            xAxis.color = new BABYLON.Color3(1, 0.2, 0.2);

            const yAxis = BABYLON.MeshBuilder.CreateLines("yAxis", {
                points: [BABYLON.Vector3.Zero(), new BABYLON.Vector3(0, axisLength, 0)]
            }, scene);
            yAxis.color = new BABYLON.Color3(0.2, 1, 0.2);

            const zAxis = BABYLON.MeshBuilder.CreateLines("zAxis", {
                points: [BABYLON.Vector3.Zero(), new BABYLON.Vector3(0, 0, axisLength)]
            }, scene);
            zAxis.color = new BABYLON.Color3(0.2, 0.2, 1);

            axesHelper = { x: xAxis, y: yAxis, z: zAxis };
        },

        createRigidMesh: function(data) {
            console.log('Creating rigid mesh:', data.id, data.primitiveType);
            
            if (!scene) {
                console.error('Cannot create mesh: scene not initialized. Call initialize() first.');
                throw new Error('Scene not initialized. Call RenderingModule.initialize() before creating meshes.');
            }
            
            const creator = meshCreators[data.primitiveType] || meshCreators.sphere;
            const mesh = creator(data.id, scene, data.meshOptions || {});

            const materialCreator = materialCreators[data.materialPreset] || materialCreators.default;
            mesh.material = materialCreator(data.id, scene);

            if (data.position) {
                mesh.position = new BABYLON.Vector3(
                    data.position[0],
                    data.position[1],
                    data.position[2]
                );
            }
            
            if (data.rotation) {
                mesh.rotationQuaternion = new BABYLON.Quaternion(
                    data.rotation[0],
                    data.rotation[1],
                    data.rotation[2],
                    data.rotation[3]
                );
            }

            if (data.scale) {
                mesh.scaling = new BABYLON.Vector3(
                    data.scale[0],
                    data.scale[1],
                    data.scale[2]
                );
            }

            if (shadowGenerator) {
                shadowGenerator.addShadowCaster(mesh);
                mesh.receiveShadows = true;
            }

            meshes.set(data.id, mesh);
            console.log('Rigid mesh created successfully');
        },

        createSoftMesh: function(data) {
            console.log('Creating soft mesh:', data.id, data.type);
            
            if (!scene) {
                console.error('Cannot create soft mesh: scene not initialized. Call initialize() first.');
                throw new Error('Scene not initialized. Call RenderingModule.initialize() before creating meshes.');
            }
            
            const type = (data.type || 'cloth').toLowerCase();
            
            const meshCreator = softMeshCreators[type] || softMeshCreators.cloth;
            const mesh = meshCreator(data.id, scene, data);

            if (data.position) {
                mesh.position = new BABYLON.Vector3(
                    data.position[0],
                    data.position[1],
                    data.position[2]
                );
            }

            const materialCreator = softMaterialCreators[type] || softMaterialCreators.default;
            mesh.material = materialCreator(data.id, scene, data);

            if (shadowGenerator) {
                shadowGenerator.addShadowCaster(mesh);
                mesh.receiveShadows = true;
            }

            softMeshes.set(data.id, mesh);
            softMeshData.set(data.id, {
                type: type,
                originalData: data,
                vertexCount: mesh.getTotalVertices()
            });

            console.log('Soft mesh created:', data.id, 'vertices:', mesh.getTotalVertices());
        },

        updateMeshTransform: function(id, position, rotation, scale) {
            const mesh = meshes.get(id);
            if (!mesh) return;

            if (position) {
                mesh.position.x = position[0];
                mesh.position.y = position[1];
                mesh.position.z = position[2];
            }

            if (rotation) {
                if (!mesh.rotationQuaternion) {
                    mesh.rotationQuaternion = new BABYLON.Quaternion();
                }
                mesh.rotationQuaternion.x = rotation[0];
                mesh.rotationQuaternion.y = rotation[1];
                mesh.rotationQuaternion.z = rotation[2];
                mesh.rotationQuaternion.w = rotation[3];
            }

            if (scale) {
                mesh.scaling.x = scale[0];
                mesh.scaling.y = scale[1];
                mesh.scaling.z = scale[2];
            }
        },

        updateSoftMeshVertices: function(id, vertices, normals) {
            const mesh = softMeshes.get(id);
            if (!mesh) {
                console.warn('Soft mesh not found:', id);
                return;
            }

            const meshData = softMeshData.get(id);
            if (!meshData) return;

            try {
                const currentPositions = mesh.getVerticesData(BABYLON.VertexBuffer.PositionKind);
                if (!currentPositions) return;

                const physicsVertexCount = vertices.length / 3;
                const meshVertexCount = currentPositions.length / 3;

                if (physicsVertexCount === meshVertexCount) {
                    mesh.updateVerticesData(BABYLON.VertexBuffer.PositionKind, new Float32Array(vertices));
                } else {
                    const updateCount = Math.min(physicsVertexCount, meshVertexCount);
                    const newPositions = new Float32Array(currentPositions.length);
                    
                    for (let i = 0; i < updateCount * 3; i++) {
                        newPositions[i] = vertices[i];
                    }
                    for (let i = updateCount * 3; i < currentPositions.length; i++) {
                        newPositions[i] = currentPositions[i];
                    }
                    
                    mesh.updateVerticesData(BABYLON.VertexBuffer.PositionKind, newPositions);
                }

                if (normals && normals.length > 0) {
                    mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, new Float32Array(normals));
                } else {
                    const indices = mesh.getIndices();
                    const positions = mesh.getVerticesData(BABYLON.VertexBuffer.PositionKind);
                    const computedNormals = [];
                    BABYLON.VertexData.ComputeNormals(positions, indices, computedNormals);
                    mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, new Float32Array(computedNormals));
                }
            } catch (e) {
                console.error('Error updating soft mesh vertices:', id, e);
            }
        },

        removeMesh: function(id) {
            let mesh = meshes.get(id);
            if (mesh) {
                mesh.dispose();
                meshes.delete(id);
                return;
            }

            mesh = softMeshes.get(id);
            if (mesh) {
                mesh.dispose();
                softMeshes.delete(id);
                softMeshData.delete(id);
            }
        },

        loadModel: async function(path) {
            try {
                const result = await BABYLON.SceneLoader.ImportMeshAsync("", "", path, scene);
                const id = "model_" + Date.now();
                
                result.meshes.forEach(function(mesh, index) {
                    if (index === 0) {
                        meshes.set(id, mesh);
                    }
                    if (shadowGenerator) {
                        shadowGenerator.addShadowCaster(mesh);
                        mesh.receiveShadows = true;
                    }
                });

                return id;
            } catch (e) {
                console.error('Failed to load model:', e);
                return null;
            }
        },

        setSelection: function(id) {
            highlightLayer.removeAllMeshes();

            if (!id) return;

            let mesh = meshes.get(id) || softMeshes.get(id);
            if (mesh) {
                highlightLayer.addMesh(mesh, BABYLON.Color3.Teal());
            }
        },

        updateSettings: function(renderSettings) {
            settings = renderSettings || {};

            if (gridHelper) {
                gridHelper.isVisible = settings.showGrid !== false;
            }

            if (axesHelper) {
                axesHelper.x.isVisible = settings.showAxes !== false;
                axesHelper.y.isVisible = settings.showAxes !== false;
                axesHelper.z.isVisible = settings.showAxes !== false;
            }

            meshes.forEach(function(mesh) {
                if (mesh.material) {
                    mesh.material.wireframe = settings.showWireframe === true;
                }
            });
            softMeshes.forEach(function(mesh) {
                if (mesh.material) {
                    mesh.material.wireframe = settings.showWireframe === true;
                }
            });
        },

        resize: function() {
            if (engine) {
                engine.resize();
            }
        },

        getRendererInfo: function() {
            return { ...rendererInfo };
        },

        getActiveBackend: function() {
            return activeBackend;
        },

        getPerformanceMetrics: function() {
            if (window.WebGPUModule) {
                return window.WebGPUModule.getPerformanceMetrics();
            }
            
            return {
                backend: activeBackend,
                fps: engine ? engine.getFps() : 0,
                frameTimeMs: engine ? 1000 / engine.getFps() : 0
            };
        },

        detectBackends: async function() {
            if (window.WebGPUModule) {
                return await window.WebGPUModule.getAllCapabilities();
            }
            
            const webgpuSupported = !!navigator.gpu;
            const canvas = document.createElement('canvas');
            const webgl2Supported = !!canvas.getContext('webgl2');
            const webglSupported = !!canvas.getContext('webgl');
            
            return {
                webgpu: { isSupported: webgpuSupported },
                webgl2: { isSupported: webgl2Supported },
                webgl: { isSupported: webglSupported }
            };
        },

        runBenchmark: async function(canvasId) {
            if (window.WebGPUModule) {
                return await window.WebGPUModule.runBenchmark(canvasId);
            }
            return { error: 'WebGPU module not loaded' };
        },

        dispose: function() {
            if (fpsUpdateInterval) {
                clearInterval(fpsUpdateInterval);
            }
            if (engine) {
                engine.dispose();
                engine = null;
            }
            scene = null;
            meshes.clear();
            softMeshes.clear();
            softMeshData.clear();
            activeBackend = 'Unknown';
        }
    };

    console.log('RenderingModule loaded successfully');
})();
