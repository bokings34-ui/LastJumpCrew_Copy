using System.IO;
using LastJumpCrew.ParkHanSol.Experiments.MudPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastJumpCrew.ParkHanSol.Experiments.MudPrototype.Editor
{
    [InitializeOnLoad]
    internal static class ParkHanSolCuteGhostMudPrototypeSceneBuilder
    {
        private const string RootFolder = "Assets/02. ParkHanSol_TeamLeader_Build & Multi/03. Prefab/Characters/Experiments/CuteWhiteGhost_MudPrototype";
        private const string ScenePath = RootFolder + "/ParkHanSol_CuteWhiteGhost_MudPrototypeScene.unity";
        private const string ModelPath = RootFolder + "/ParkHanSol_CuteWhiteGhost_MudPrototype_FromGLB.fbx";
        private const string ControllerPath = RootFolder + "/AnimationsFromGLB/ParkHanSol_CuteWhiteGhost_MudPrototype.controller";
        private const string MaterialFolder = RootFolder + "/Materials";
        private const string MeshFolder = RootFolder + "/Meshes";
        private const string MudMaterialPath = MaterialFolder + "/ParkHanSol_CuteGhostMudPrototype_Surface.mat";
        private const string GroundMaterialPath = MaterialFolder + "/ParkHanSol_CuteGhostMudPrototype_Ground.mat";
        private const string FaceMaterialPath = MaterialFolder + "/ParkHanSol_CuteGhostMudPrototype_Face.mat";
        private const string MeshPath = MeshFolder + "/ParkHanSol_CuteGhostMudPrototype_SurfaceMesh.asset";

        static ParkHanSolCuteGhostMudPrototypeSceneBuilder()
        {
            EditorApplication.delayCall += BuildIfMissing;
        }

        private static void BuildIfMissing()
        {
            if (Application.isPlaying || File.Exists(ScenePath))
            {
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("PHS_MUD_PROTOTYPE_SCENE_BUILD_FAILED reason=urp_lit_shader_missing");
                return;
            }

            var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (modelAsset == null)
            {
                Debug.LogError($"PHS_MUD_PROTOTYPE_SCENE_BUILD_FAILED reason=model_missing path={ModelPath}");
                return;
            }

            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);

            var mudMaterial = CreateOrLoadMaterial(MudMaterialPath, shader, new Color(0.88f, 0.96f, 1f, 1f), 0.72f);
            var groundMaterial = CreateOrLoadMaterial(GroundMaterialPath, shader, new Color(0.17f, 0.19f, 0.21f, 1f), 0.35f);
            var faceMaterial = CreateOrLoadMaterial(FaceMaterialPath, shader, new Color(0.02f, 0.025f, 0.03f, 1f), 0.5f);
            var prototypeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            var root = CreateObject("PHS_CuteGhostMudPrototype_Root", prototypeScene);
            BuildCamera(prototypeScene, root.transform);
            BuildLight(prototypeScene, root.transform);
            BuildGround(prototypeScene, root.transform, groundMaterial);
            BuildReferenceModel(prototypeScene, root.transform, modelAsset);
            BuildMudVolume(prototypeScene, root.transform, mudMaterial, faceMaterial);

            if (!EditorSceneManager.SaveScene(prototypeScene, ScenePath))
            {
                Debug.LogError($"PHS_MUD_PROTOTYPE_SCENE_BUILD_FAILED reason=scene_save_failed path={ScenePath}");
                EditorSceneManager.CloseScene(prototypeScene, true);
                return;
            }

            EditorSceneManager.CloseScene(prototypeScene, true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"PHS_MUD_PROTOTYPE_SCENE_BUILD_OK path={ScenePath}");
        }

        private static void BuildCamera(Scene scene, Transform parent)
        {
            var cameraObject = CreateObject("PrototypeCamera", scene, parent);
            cameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.35f, -5.2f), Quaternion.Euler(12f, 0f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 35f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.07f, 0.08f, 1f);
            cameraObject.tag = "MainCamera";
        }

        private static void BuildLight(Scene scene, Transform parent)
        {
            var lightObject = CreateObject("KeyLight", scene, parent);
            lightObject.transform.rotation = Quaternion.Euler(42f, -32f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.2f;
            light.color = new Color(0.93f, 0.98f, 1f, 1f);
        }

        private static void BuildGround(Scene scene, Transform parent, Material groundMaterial)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.name = "PrototypeGround";
            ground.transform.SetParent(parent, false);
            ground.transform.position = new Vector3(0f, -0.98f, 0f);
            ground.transform.localScale = new Vector3(2.8f, 1f, 2.8f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
        }

        private static void BuildReferenceModel(Scene scene, Transform parent, GameObject modelAsset)
        {
            var model = PrefabUtility.InstantiatePrefab(modelAsset, scene) as GameObject;
            if (model == null)
            {
                Debug.LogError("PHS_MUD_PROTOTYPE_SCENE_BUILD_FAILED reason=model_instantiate_failed");
                return;
            }

            model.name = "Reference_CopiedCuteWhiteGhost";
            model.transform.SetParent(parent, false);
            model.transform.position = new Vector3(-1.35f, -0.95f, 0f);
            model.transform.rotation = Quaternion.Euler(0f, 20f, 0f);
            model.transform.localScale = Vector3.one;

            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            var animator = model.GetComponentInChildren<Animator>();
            if (controller != null && animator != null)
            {
                animator.runtimeAnimatorController = controller;
            }
            else
            {
                Debug.LogWarning($"PHS_MUD_PROTOTYPE_CONTROLLER_NOT_ASSIGNED controllerNull={controller == null} animatorNull={animator == null}");
            }
        }

        private static void BuildMudVolume(Scene scene, Transform parent, Material mudMaterial, Material faceMaterial)
        {
            var volumeObject = CreateObject("MudMetaballVolume", scene, parent);
            volumeObject.transform.position = new Vector3(0.85f, 0.2f, 0f);
            volumeObject.AddComponent<MeshFilter>();
            var renderer = volumeObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mudMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var brushes = new[]
            {
                CreateBrush(scene, volumeObject.transform, "MudBrush_BeanBody", new Vector3(0f, 0.08f, 0f), 0.82f, 1.25f),
                CreateBrush(scene, volumeObject.transform, "MudBrush_RoundHead", new Vector3(0f, 0.78f, 0f), 0.58f, 0.95f),
                CreateBrush(scene, volumeObject.transform, "MudBrush_LeftArm", new Vector3(-0.66f, 0.08f, -0.02f), 0.3f, 0.72f),
                CreateBrush(scene, volumeObject.transform, "MudBrush_RightArm", new Vector3(0.66f, 0.08f, -0.02f), 0.3f, 0.72f),
                CreateBrush(scene, volumeObject.transform, "MudBrush_LeftLeg", new Vector3(-0.28f, -0.72f, -0.02f), 0.34f, 0.78f),
                CreateBrush(scene, volumeObject.transform, "MudBrush_RightLeg", new Vector3(0.28f, -0.72f, -0.02f), 0.34f, 0.78f),
                CreateBrush(scene, volumeObject.transform, "MudBrush_BackSquash", new Vector3(0f, 0.08f, 0.42f), 0.45f, 0.55f)
            };

            var volume = volumeObject.AddComponent<MudPrototypeVolume>();
            var serializedVolume = new SerializedObject(volume);
            serializedVolume.FindProperty("brushes").arraySize = brushes.Length;
            for (var i = 0; i < brushes.Length; i++)
            {
                serializedVolume.FindProperty("brushes").GetArrayElementAtIndex(i).objectReferenceValue = brushes[i];
            }

            serializedVolume.FindProperty("boundsSize").vector3Value = new Vector3(2.1f, 2.7f, 1.45f);
            serializedVolume.FindProperty("resolutionX").intValue = 34;
            serializedVolume.FindProperty("resolutionY").intValue = 40;
            serializedVolume.FindProperty("resolutionZ").intValue = 26;
            serializedVolume.FindProperty("isoLevel").floatValue = 0.42f;
            serializedVolume.FindProperty("rebuildOnStart").boolValue = false;
            serializedVolume.ApplyModifiedPropertiesWithoutUndo();

            volume.Regenerate();
            SaveGeneratedMesh(volumeObject);
            BuildFace(scene, volumeObject.transform, faceMaterial);
        }

        private static MudPrototypeSphereBrush CreateBrush(Scene scene, Transform parent, string name, Vector3 localPosition, float radius, float strength)
        {
            var brushObject = CreateObject(name, scene, parent);
            brushObject.transform.localPosition = localPosition;
            var brush = brushObject.AddComponent<MudPrototypeSphereBrush>();
            brush.Radius = radius;
            brush.Strength = strength;
            return brush;
        }

        private static void BuildFace(Scene scene, Transform parent, Material faceMaterial)
        {
            BuildFaceDot(scene, parent, "Face_LeftEye", new Vector3(-0.18f, 0.42f, -0.62f), faceMaterial);
            BuildFaceDot(scene, parent, "Face_RightEye", new Vector3(0.18f, 0.42f, -0.62f), faceMaterial);
        }

        private static void BuildFaceDot(Scene scene, Transform parent, string name, Vector3 localPosition, Material faceMaterial)
        {
            var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            SceneManager.MoveGameObjectToScene(eye, scene);
            eye.name = name;
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = localPosition;
            eye.transform.localScale = new Vector3(0.1f, 0.14f, 0.025f);
            eye.GetComponent<MeshRenderer>().sharedMaterial = faceMaterial;
            Object.DestroyImmediate(eye.GetComponent<SphereCollider>());
        }

        private static void SaveGeneratedMesh(GameObject volumeObject)
        {
            var meshFilter = volumeObject.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                Debug.LogError("PHS_MUD_PROTOTYPE_SCENE_BUILD_FAILED reason=generated_mesh_missing");
                return;
            }

            var meshAsset = Object.Instantiate(meshFilter.sharedMesh);
            meshAsset.name = "ParkHanSol_CuteGhostMudPrototype_SurfaceMesh";
            AssetDatabase.CreateAsset(meshAsset, MeshPath);
            meshFilter.sharedMesh = meshAsset;
        }

        private static Material CreateOrLoadMaterial(string path, Shader shader, Color color, float smoothness)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(shader)
            {
                name = Path.GetFileNameWithoutExtension(path)
            };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Cull", 0f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject CreateObject(string name, Scene scene, Transform parent = null)
        {
            var gameObject = new GameObject(name);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            if (parent != null)
            {
                gameObject.transform.SetParent(parent, false);
            }

            return gameObject;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                Debug.LogError($"PHS_MUD_PROTOTYPE_SCENE_BUILD_FAILED reason=invalid_folder path={path}");
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
