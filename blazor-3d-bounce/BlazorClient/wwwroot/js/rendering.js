/**
 * Babylon.js Rendering Module
 * Handles 3D scene rendering, camera, lights, and mesh management
 * 
 * OCP Compliance: Mesh and material creation uses registry pattern
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
    let softMeshData = new Map(); // Store original mesh data for rebuilding
    let settings = {};

    /**
     * Mesh creator registry (OCP - extensible without modification)
     */
    const meshCreators = {
        sphere: function(id, scene, options) {
            return BABYLON.MeshBuilder.CreateSphere(id, {
                diameter: options.diameter || 1,
                segments: options.segments || 32
            }, scene);
        },
        box: function(id, scene, options) {
            return BABYLON.MeshBuilder.CreateBox(id, {
                size: options.size || 1
            }, scene);
        },
        capsule: function(id, scene, options) {
            return BABYLON.MeshBuilder.CreateCapsule(id, {
                radius: options.radius || 0.5,
                height: options.height || 2,
                tessellation: options.tessellation || 32
            }, scene);
        },
        cylinder: function(id, scene, options) {
            return BABYLON.MeshBuilder.CreateCylinder(id, {
                diameter: options.diameter || 1,
                height: options.height || 1,
                tessellation: options.tessellation || 32
            }, scene);
        },
        cone: function(id, scene, options) {
            return BABYLON.MeshBuilder.CreateCylinder(id, {
                diameterTop: 0,
                diameterBottom: options.diameterBottom || 1,
                height: options.height || 1,
                tessellation: options.tessellation || 32
            }, scene);
        }
    };

    /**
     * Material creator registry (OCP - extensible without modification)
     */
    const materialCreators = {
        rubber: function(id, scene) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.8, 0.2, 0.2);
            material.metallic = 0.0;
            material.roughness = 0.7;
            return material;
        },
        wood: function(id, scene) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.6, 0.4, 0.2);
            material.metallic = 0.0;
            material.roughness = 0.8;
            return material;
        },
        steel: function(id, scene) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.7, 0.7, 0.75);
            material.metallic = 0.9;
            material.roughness = 0.3;
            return material;
        },
        ice: function(id, scene) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.7, 0.9, 1.0);
            material.metallic = 0.0;
            material.roughness = 0.1;
            material.alpha = 0.8;
            return material;
        },
        default: function(id, scene) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
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
        cloth: function(id, scene, data) {
            const width = data.width || 2;
            const height = data.height || 2;
            const resX = data.resolutionX || 20;
            const resY = data.resolutionY || 20;
            
            // Create a ground mesh that matches Ammo.js cloth vertex layout
            const mesh = BABYLON.MeshBuilder.CreateGround(id, {
                width: width,
                height: height,
                subdivisions: Math.max(resX, resY),
                updatable: true
            }, scene);
            
            // Rotate to match Ammo.js orientation (vertical cloth)
            mesh.rotation.x = -Math.PI / 2;
            
            return mesh;
        },
        
        rope: function(id, scene, data) {
            const length = data.length || 5;
            const segments = data.segments || 20;
            const segmentLength = length / segments;
            
            // Create initial path for rope
            const path = [];
            for (let i = 0; i <= segments; i++) {
                path.push(new BABYLON.Vector3(0, -i * segmentLength, 0));
            }
            
            const mesh = BABYLON.MeshBuilder.CreateTube(id, {
                path: path,
                radius: data.radius || 0.02,
                tessellation: 8,
                updatable: true,
                sideOrientation: BABYLON.Mesh.DOUBLESIDE
            }, scene);
            
            return mesh;
        },
        
        volumetric: function(id, scene, data) {
            const radius = data.radius || 0.5;
            
            // Create sphere with enough segments for deformation
            const mesh = BABYLON.MeshBuilder.CreateSphere(id, {
                diameter: radius * 2,
                segments: data.resolutionX || 12,
                updatable: true
            }, scene);
            
            return mesh;
        }
    };

    /**
     * Soft material creator registry
     */
    const softMaterialCreators = {
        cloth: function(id, scene, data) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.2, 0.5, 0.8);
            material.metallic = 0.0;
            material.roughness = 0.8;
            material.backFaceCulling = false;
            material.twoSidedLighting = true;
            return material;
        },
        
        rope: function(id, scene, data) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.6, 0.4, 0.2);
            material.metallic = 0.0;
            material.roughness = 0.9;
            return material;
        },
        
        volumetric: function(id, scene, data) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.8, 0.3, 0.3);
            material.metallic = 0.0;
            material.roughness = 0.4;
            material.alpha = 0.9;
            return material;
        },
        
        default: function(id, scene, data) {
            const material = new BABYLON.PBRMaterial(id + "_mat", scene);
            material.albedoColor = new BABYLON.Color3(0.2, 0.6, 0.8);
            material.metallic = 0.0;
            material.roughness = 0.6;
            material.backFaceCulling = false;
            return material;
        }
    };

    /**
     * Initialize the Babylon.js rendering engine
     */
    window.RenderingModule = {
        /**
         * Register a custom mesh creator (OCP - extend without modification)
         * @param {string} type - The mesh type name
         * @param {function} creator - The creator function(id, scene, options)
         */
        registerMeshCreator: function(type, creator) {
            meshCreators[type.toLowerCase()] = creator;
        },

        /**
         * Register a custom material creator (OCP - extend without modification)
         * @param {string} preset - The material preset name
         * @param {function} creator - The creator function(id, scene)
         */
        registerMaterialCreator: function(preset, creator) {
            materialCreators[preset.toLowerCase()] = creator;
        },

        /**
         * Register a custom soft mesh creator (OCP - extend without modification)
         */
        registerSoftMeshCreator: function(type, creator) {
            softMeshCreators[type.toLowerCase()] = creator;
        },

        /**
         * Register a custom soft material creator (OCP - extend without modification)
         */
        registerSoftMaterialCreator: function(type, creator) {
            softMaterialCreators[type.toLowerCase()] = creator;
        },

        initialize: async function(canvasId, renderSettings) {
            console.log('RenderingModule.initialize called with canvasId:', canvasId);
            
            const canvas = document.getElementById(canvasId);
            if (!canvas) {
                console.error('Canvas not found:', canvasId);
                return false;
            }

            // Check if BABYLON is available
            if (typeof BABYLON === 'undefined') {
                console.error('Babylon.js not loaded');
                return false;
            }

            settings = renderSettings || {};

            try {
                // Ensure canvas has dimensions - wait for layout if needed
                if (canvas.clientWidth === 0 || canvas.clientHeight === 0) {
                    console.log('Canvas has no dimensions, waiting for layout...');
                    await new Promise(resolve => setTimeout(resolve, 100));
                }

                // Force canvas to fill its container
                canvas.width = canvas.clientWidth || window.innerWidth;
                canvas.height = canvas.clientHeight || window.innerHeight;
                
                console.log('Canvas dimensions:', canvas.width, 'x', canvas.height);

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

                // Post processing
                if (settings.enableFXAA !== false) {
                    new BABYLON.FxaaPostProcess("fxaa", 1.0, camera);
                }

                // Start render loop
                engine.runRenderLoop(function() {
                    scene.render();
                });

                // Handle resize
                window.addEventListener('resize', function() {
                    engine.resize();
                });

                // Ensure initial resize is called
                engine.resize();

                console.log('Rendering module initialized successfully');
                console.log('Scene has', scene.meshes.length, 'meshes');
                return true;
            } catch (e) {
                console.error('Failed to initialize rendering:', e);
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

        /**
         * Create a rigid mesh using the registry pattern (OCP compliant)
         */
        createRigidMesh: function(data) {
            console.log('Creating rigid mesh:', data.id, data.primitiveType);
            
            // Use registry to get creator (OCP - no switch statement)
            const creator = meshCreators[data.primitiveType] || meshCreators.sphere;
            const mesh = creator(data.id, scene, data.meshOptions || {});

            // Use registry for material (OCP - no switch statement)
            const materialCreator = materialCreators[data.materialPreset] || materialCreators.default;
            mesh.material = materialCreator(data.id, scene);

            // Apply transform
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

            // Shadows
            if (shadowGenerator) {
                shadowGenerator.addShadowCaster(mesh);
                mesh.receiveShadows = true;
            }

            meshes.set(data.id, mesh);
            console.log('Rigid mesh created successfully');
        },

        /**
         * Create material using registry (OCP compliant)
         * @deprecated Use materialCreators registry directly
         */
        createMaterial: function(id, preset) {
            const creator = materialCreators[preset] || materialCreators.default;
            return creator(id, scene);
        },

        createSoftMesh: function(data) {
            console.log('Creating soft mesh:', data.id, data.type);
            
            const type = (data.type || 'cloth').toLowerCase();
            
            // Use registry to create mesh (OCP compliant)
            const meshCreator = softMeshCreators[type] || softMeshCreators.cloth;
            const mesh = meshCreator(data.id, scene, data);

            // Apply position
            if (data.position) {
                mesh.position = new BABYLON.Vector3(
                    data.position[0],
                    data.position[1],
                    data.position[2]
                );
            }

            // Use registry for material (OCP compliant)
            const materialCreator = softMaterialCreators[type] || softMaterialCreators.default;
            mesh.material = materialCreator(data.id, scene, data);

            // Enable shadows
            if (shadowGenerator) {
                shadowGenerator.addShadowCaster(mesh);
                mesh.receiveShadows = true;
            }

            // Store mesh and its data
            softMeshes.set(data.id, mesh);
            softMeshData.set(data.id, {
                type: type,
                originalData: data,
                vertexCount: mesh.getTotalVertices()
            });

            console.log('Soft mesh created:', data.id, 'vertices:', mesh.getTotalVertices());
        },

        /**
         * Update mesh transform
         */
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

        /**
         * Update soft mesh vertices from physics simulation
         */
        updateSoftMeshVertices: function(id, vertices, normals) {
            const mesh = softMeshes.get(id);
            if (!mesh) {
                console.warn('Soft mesh not found:', id);
                return;
            }

            const meshData = softMeshData.get(id);
            if (!meshData) return;

            try {
                // For cloth, we need to handle the vertex mapping
                if (meshData.type === 'cloth') {
                    this._updateClothVertices(mesh, vertices, normals);
                } else if (meshData.type === 'rope') {
                    this._updateRopeVertices(mesh, vertices, meshData.originalData);
                } else if (meshData.type === 'volumetric') {
                    this._updateVolumetricVertices(mesh, vertices, normals);
                } else {
                    // Generic update
                    mesh.updateVerticesData(BABYLON.VertexBuffer.PositionKind, new Float32Array(vertices));
                    if (normals && normals.length > 0) {
                        mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, new Float32Array(normals));
                    }
                }
            } catch (e) {
                console.error('Error updating soft mesh vertices:', id, e);
            }
        },

        /**
         * Update cloth mesh vertices
         */
        _updateClothVertices: function(mesh, vertices, normals) {
            const currentPositions = mesh.getVerticesData(BABYLON.VertexBuffer.PositionKind);
            if (!currentPositions) return;

            // Map Ammo.js vertices to Babylon.js mesh vertices
            // Ammo.js cloth has (resX+1) * (resY+1) vertices
            // Babylon.js ground has similar structure but may have different ordering
            
            const physicsVertexCount = vertices.length / 3;
            const meshVertexCount = currentPositions.length / 3;

            if (physicsVertexCount === meshVertexCount) {
                // Direct mapping
                mesh.updateVerticesData(BABYLON.VertexBuffer.PositionKind, new Float32Array(vertices));
            } else {
                // Need to interpolate or map vertices
                // For now, update what we can
                const updateCount = Math.min(physicsVertexCount, meshVertexCount);
                const newPositions = new Float32Array(currentPositions.length);
                
                for (let i = 0; i < updateCount * 3; i++) {
                    newPositions[i] = vertices[i];
                }
                // Keep remaining vertices unchanged
                for (let i = updateCount * 3; i < currentPositions.length; i++) {
                    newPositions[i] = currentPositions[i];
                }
                
                mesh.updateVerticesData(BABYLON.VertexBuffer.PositionKind, newPositions);
            }

            // Update normals
            if (normals && normals.length > 0) {
                mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, new Float32Array(normals));
            } else {
                // Recompute normals
                const indices = mesh.getIndices();
                const positions = mesh.getVerticesData(BABYLON.VertexBuffer.PositionKind);
                const computedNormals = [];
                BABYLON.VertexData.ComputeNormals(positions, indices, computedNormals);
                mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, new Float32Array(computedNormals));
            }
        },

        /**
         * Update rope mesh by rebuilding the tube path
         */
        _updateRopeVertices: function(mesh, vertices, originalData) {
            // Rope needs to be rebuilt with new path
            const vertexCount = vertices.length / 3;
            const path = [];

            for (let i = 0; i < vertexCount; i++) {
                path.push(new BABYLON.Vector3(
                    vertices[i * 3],
                    vertices[i * 3 + 1],
                    vertices[i * 3 + 2]
                ));
            }

            // Update tube with new path
            if (path.length >= 2) {
                mesh = BABYLON.MeshBuilder.CreateTube(null, {
                    path: path,
                    radius: originalData.radius || 0.02,
                    tessellation: 8,
                    instance: mesh
                });
            }
        },

        /**
         * Update volumetric mesh vertices
         */
        _updateVolumetricVertices: function(mesh, vertices, normals) {
            const currentPositions = mesh.getVerticesData(BABYLON.VertexBuffer.PositionKind);
            if (!currentPositions) return;

            const physicsVertexCount = vertices.length / 3;
            const meshVertexCount = currentPositions.length / 3;

            // Volumetric bodies may have different vertex counts
            // Use center of mass offset for simple translation
            if (physicsVertexCount !== meshVertexCount) {
                // Calculate center of physics body
                let cx = 0, cy = 0, cz = 0;
                for (let i = 0; i < physicsVertexCount; i++) {
                    cx += vertices[i * 3];
                    cy += vertices[i * 3 + 1];
                    cz += vertices[i * 3 + 2];
                }
                cx /= physicsVertexCount;
                cy /= physicsVertexCount;
                cz /= physicsVertexCount;

                // Move mesh to center
                mesh.position.set(cx, cy, cz);
                
                // Scale based on bounding radius
                let maxDist = 0;
                for (let i = 0; i < physicsVertexCount; i++) {
                    const dx = vertices[i * 3] - cx;
                    const dy = vertices[i * 3 + 1] - cy;
                    const dz = vertices[i * 3 + 2] - cz;
                    maxDist = Math.max(maxDist, Math.sqrt(dx*dx + dy*dy + dz*dz));
                }
                
                const originalRadius = mesh.getBoundingInfo().boundingSphere.radius;
                if (originalRadius > 0 && maxDist > 0) {
                    const scale = maxDist / originalRadius;
                    mesh.scaling.setAll(scale);
                }
            } else {
                // Direct vertex update
                mesh.updateVerticesData(BABYLON.VertexBuffer.PositionKind, new Float32Array(vertices));
                
                if (normals && normals.length > 0) {
                    mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, new Float32Array(normals));
                } else {
                    const indices = mesh.getIndices();
                    const computedNormals = [];
                    BABYLON.VertexData.ComputeNormals(vertices, indices, computedNormals);
                    mesh.updateVerticesData(BABYLON.VertexBuffer.NormalKind, new Float32Array(computedNormals));
                }
            }
        },

        /**
         * Batch update multiple soft meshes (performance optimization)
         */
        updateAllSoftMeshVertices: function(vertexDataMap) {
            for (const id in vertexDataMap) {
                const data = vertexDataMap[id];
                if (data && data.vertices && data.vertices.length > 0) {
                    this.updateSoftMeshVertices(id, data.vertices, data.normals);
                }
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

        dispose: function() {
            if (engine) {
                engine.dispose();
            }
            meshes.clear();
            softMeshes.clear();
            softMeshData.clear();
        }
    };

    console.log('Rendering module loaded, RenderingModule:', typeof window.RenderingModule);
})();
