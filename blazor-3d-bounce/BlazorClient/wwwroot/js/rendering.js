/**
 * Babylon.js Rendering Module
 * Handles 3D scene rendering, camera, lights, and mesh management
 */

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
let settings = {};

/**
 * Initialize the Babylon.js rendering engine
 * @param {string} canvasId - Canvas element ID
 * @param {Object} renderSettings - Render settings object
 */
window.RenderingModule = {
    initialize: async function(canvasId, renderSettings) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) {
            console.error('Canvas not found:', canvasId);
            return;
        }

        settings = renderSettings || {};

        // Create engine
        engine = new BABYLON.Engine(canvas, true, {
            preserveDrawingBuffer: true,
            stencil: true,
            antialias: true
        });

        // Create scene
        scene = new BABYLON.Scene(engine);
        scene.clearColor = new BABYLON.Color4(0.1, 0.1, 0.15, 1);

        // Setup camera
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

        // Setup lights
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

        // Shadow generator
        if (settings.enableShadows !== false) {
            shadowGenerator = new BABYLON.ShadowGenerator(
                settings.shadowMapSize || 2048,
                dirLight
            );
            shadowGenerator.useBlurExponentialShadowMap = true;
            shadowGenerator.blurKernel = 32;
        }

        // Highlight layer for selection
        highlightLayer = new BABYLON.HighlightLayer("highlight", scene);

        // Create ground
        this.createGround();

        // Create helpers
        if (settings.showGrid !== false) {
            this.createGrid();
        }
        if (settings.showAxes !== false) {
            this.createAxes();
        }

        // Load environment
        if (settings.hdriPath) {
            try {
                const hdrTexture = BABYLON.CubeTexture.CreateFromPrefilteredData(
                    settings.hdriPath,
                    scene
                );
                scene.environmentTexture = hdrTexture;
                scene.createDefaultSkybox(hdrTexture, true, 1000, 0.3);
            } catch (e) {
                console.warn('Could not load HDRI environment:', e);
            }
        }

        // Post processing
        if (settings.enableFXAA !== false) {
            const fxaaPostProcess = new BABYLON.FxaaPostProcess("fxaa", 1.0, camera);
        }

        // Start render loop
        engine.runRenderLoop(() => {
            scene.render();
        });

        // Handle resize
        window.addEventListener('resize', () => {
            engine.resize();
        });

        console.log('Rendering module initialized');
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
        
        // X axis - red
        const xAxis = BABYLON.MeshBuilder.CreateLines("xAxis", {
            points: [BABYLON.Vector3.Zero(), new BABYLON.Vector3(axisLength, 0, 0)]
        }, scene);
        xAxis.color = new BABYLON.Color3(1, 0.2, 0.2);

        // Y axis - green
        const yAxis = BABYLON.MeshBuilder.CreateLines("yAxis", {
            points: [BABYLON.Vector3.Zero(), new BABYLON.Vector3(0, axisLength, 0)]
        }, scene);
        yAxis.color = new BABYLON.Color3(0.2, 1, 0.2);

        // Z axis - blue
        const zAxis = BABYLON.MeshBuilder.CreateLines("zAxis", {
            points: [BABYLON.Vector3.Zero(), new BABYLON.Vector3(0, 0, axisLength)]
        }, scene);
        zAxis.color = new BABYLON.Color3(0.2, 0.2, 1);

        axesHelper = { x: xAxis, y: yAxis, z: zAxis };
    },

    createRigidMesh: function(data) {
        let mesh;

        switch (data.primitiveType) {
            case 'sphere':
                mesh = BABYLON.MeshBuilder.CreateSphere(data.id, {
                    diameter: 1,
                    segments: 32
                }, scene);
                break;
            case 'box':
                mesh = BABYLON.MeshBuilder.CreateBox(data.id, {
                    size: 1
                }, scene);
                break;
            case 'capsule':
                mesh = BABYLON.MeshBuilder.CreateCapsule(data.id, {
                    radius: 0.5,
                    height: 2,
                    tessellation: 32
                }, scene);
                break;
            case 'cylinder':
                mesh = BABYLON.MeshBuilder.CreateCylinder(data.id, {
                    diameter: 1,
                    height: 1,
                    tessellation: 32
                }, scene);
                break;
            case 'cone':
                mesh = BABYLON.MeshBuilder.CreateCylinder(data.id, {
                    diameterTop: 0,
                    diameterBottom: 1,
                    height: 1,
                    tessellation: 32
                }, scene);
                break;
            default:
                mesh = BABYLON.MeshBuilder.CreateSphere(data.id, {
                    diameter: 1
                }, scene);
        }

        // Apply material
        const material = this.createMaterial(data.id, data.materialPreset);
        mesh.material = material;

        // Apply transform
        mesh.position = new BABYLON.Vector3(
            data.position[0],
            data.position[1],
            data.position[2]
        );
        
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

        // Shadows
        if (shadowGenerator) {
            shadowGenerator.addShadowCaster(mesh);
            mesh.receiveShadows = true;
        }

        meshes.set(data.id, mesh);
    },

    createMaterial: function(id, preset) {
        const material = new BABYLON.PBRMaterial(id + "_mat", scene);
        
        switch (preset) {
            case 'rubber':
                material.albedoColor = new BABYLON.Color3(0.8, 0.2, 0.2);
                material.metallic = 0.0;
                material.roughness = 0.7;
                break;
            case 'wood':
                material.albedoColor = new BABYLON.Color3(0.6, 0.4, 0.2);
                material.metallic = 0.0;
                material.roughness = 0.8;
                break;
            case 'steel':
                material.albedoColor = new BABYLON.Color3(0.7, 0.7, 0.75);
                material.metallic = 0.9;
                material.roughness = 0.3;
                break;
            case 'ice':
                material.albedoColor = new BABYLON.Color3(0.7, 0.9, 1.0);
                material.metallic = 0.0;
                material.roughness = 0.1;
                material.alpha = 0.8;
                break;
            default:
                material.albedoColor = new BABYLON.Color3(0.5, 0.5, 0.6);
                material.metallic = 0.2;
                material.roughness = 0.5;
        }

        return material;
    },

    createSoftMesh: function(data) {
        let mesh;
        let vertexData;

        switch (data.type) {
            case 'cloth':
                mesh = BABYLON.MeshBuilder.CreateGround(data.id, {
                    width: data.width,
                    height: data.height,
                    subdivisions: Math.max(data.resolutionX, data.resolutionY),
                    updatable: true
                }, scene);
                break;

            case 'rope':
                // Create tube for rope
                const path = [];
                const segmentLength = data.length / data.segments;
                for (let i = 0; i <= data.segments; i++) {
                    path.push(new BABYLON.Vector3(0, -i * segmentLength, 0));
                }
                mesh = BABYLON.MeshBuilder.CreateTube(data.id, {
                    path: path,
                    radius: 0.02,
                    tessellation: 8,
                    updatable: true
                }, scene);
                break;

            case 'volumetric':
                mesh = BABYLON.MeshBuilder.CreateSphere(data.id, {
                    diameter: data.radius * 2,
                    segments: 16,
                    updatable: true
                }, scene);
                break;

            default:
                mesh = BABYLON.MeshBuilder.CreateGround(data.id, {
                    width: 2,
                    height: 2,
                    subdivisions: 20,
                    updatable: true
                }, scene);
        }

        // Apply position
        mesh.position = new BABYLON.Vector3(
            data.position[0],
            data.position[1],
            data.position[2]
        );

        // Create soft body material
        const material = new BABYLON.PBRMaterial(data.id + "_mat", scene);
        material.albedoColor = new BABYLON.Color3(0.2, 0.6, 0.8);
        material.metallic = 0.0;
        material.roughness = 0.6;
        material.backFaceCulling = false;
        mesh.material = material;

        // Enable shadows
        if (shadowGenerator) {
            shadowGenerator.addShadowCaster(mesh);
            mesh.receiveShadows = true;
        }

        softMeshes.set(data.id, mesh);
    },

    updateMeshTransform: function(id, position, rotation, scale) {
        const mesh = meshes.get(id);
        if (!mesh) return;

        mesh.position.x = position[0];
        mesh.position.y = position[1];
        mesh.position.z = position[2];

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
        if (!mesh) return;

        // Update vertex positions
        mesh.updateVerticesData(BABYLON.VertexBuffer.PositionKind, vertices);

        // Update normals if provided, otherwise recompute
        if (normals) {
            mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, normals);
        } else {
            // Recompute normals
            const indices = mesh.getIndices();
            const computedNormals = [];
            BABYLON.VertexData.ComputeNormals(vertices, indices, computedNormals);
            mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, computedNormals);
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
        }
    },

    loadModel: async function(path) {
        try {
            const result = await BABYLON.SceneLoader.ImportMeshAsync("", "", path, scene);
            const id = "model_" + Date.now();
            
            result.meshes.forEach((mesh, index) => {
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
        // Clear previous highlights
        highlightLayer.removeAllMeshes();

        if (!id) return;

        let mesh = meshes.get(id) || softMeshes.get(id);
        if (mesh) {
            highlightLayer.addMesh(mesh, BABYLON.Color3.Teal());
        }
    },

    updateSettings: function(renderSettings) {
        settings = renderSettings;

        // Toggle grid
        if (gridHelper) {
            gridHelper.isVisible = settings.showGrid !== false;
        }

        // Toggle axes
        if (axesHelper) {
            axesHelper.x.isVisible = settings.showAxes !== false;
            axesHelper.y.isVisible = settings.showAxes !== false;
            axesHelper.z.isVisible = settings.showAxes !== false;
        }

        // Toggle wireframe
        meshes.forEach(mesh => {
            if (mesh.material) {
                mesh.material.wireframe = settings.showWireframe === true;
            }
        });
        softMeshes.forEach(mesh => {
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

    dispose: function() {
        if (engine) {
            engine.dispose();
        }
        meshes.clear();
        softMeshes.clear();
    }
};

export default window.RenderingModule;
