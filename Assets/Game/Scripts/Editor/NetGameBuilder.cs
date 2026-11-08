using System.Collections.Generic;
using MouseGame.Net;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MouseGame.EditorTools
{
    /// <summary>
    /// Tools/Net/Build Everything: network prefabs, materials, Boot / MainMenu / Lobby / level scenes, Build Settings.
    /// Safe to run again (overwrites what it made).
    /// </summary>
    public static class NetGameBuilder
    {
        const string Root = "Assets/Game/";
        const string PrefabDir = Root + "Prefabs/Net/";
        const string MatDir = Root + "Materials/";
        const string SceneDir = "Assets/Scenes/";

        [MenuItem("Tools/Net/Build Everything")]
        public static void BuildAll()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Ensure(PrefabDir); Ensure(MatDir); Ensure(SceneDir);

            var ringMats = new Material[4];
            for (int i = 0; i < 4; i++) ringMats[i] = Mat("Ring_" + (i + 1), GameConfig.SlotColors[i], unlit: true);

            var session = BuildSession();
            var player = BuildPlayer(ringMats);
            var food = BuildFood();
            var trap = BuildTrap(ringMats);
            var match = BuildMatch();

            BuildBootScene(session, match, player, food, trap);
            BuildMenuScene(GameConfig.MenuScene, typeof(UI.MainMenuUI));
            BuildMenuScene(GameConfig.LobbyScene, typeof(UI.LobbyUI));
            BuildLevel(GameConfig.Maps[0].Scene, night: false);
            BuildLevel(GameConfig.Maps[1].Scene, night: true);

            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(SceneDir + GameConfig.BootScene + ".unity", true),
                new EditorBuildSettingsScene(SceneDir + GameConfig.MenuScene + ".unity", true),
                new EditorBuildSettingsScene(SceneDir + GameConfig.LobbyScene + ".unity", true),
            };
            foreach (var m in GameConfig.Maps) scenes.Add(new EditorBuildSettingsScene(SceneDir + m.Scene + ".unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneDir + GameConfig.BootScene + ".unity");
            Debug.Log("[Net] Everything built. Press Play in the Boot scene.");
        }

        // ------------------------------------------------------------ prefabs
        static GameObject BuildSession()
        {
            var go = new GameObject("Session");
            go.AddComponent<NetworkObject>();
            go.AddComponent<SessionManager>();
            return Save(go, "Session");
        }

        static GameObject BuildPlayer(Material[] ringMats)
        {
            var mouse = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mouse/Prefabs/Mouse.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(mouse);
            go.name = "Player";
            var no = go.AddComponent<NetworkObject>();
            no.DontDestroyWithOwner = false;
            var nt = go.AddComponent<NetworkTransform>();
            nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            nt.SyncPositionX = nt.SyncPositionY = nt.SyncPositionZ = true;
            nt.SyncRotAngleX = false; nt.SyncRotAngleY = true; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            nt.UseHalfFloatPrecision = true;   // less bandwidth on mobile
            var cc = go.AddComponent<CharacterController>();
            cc.height = 0.24f; cc.radius = 0.11f; cc.center = new Vector3(0f, 0.13f, 0f);
            cc.stepOffset = 0.05f; cc.skinWidth = 0.01f; cc.minMoveDistance = 0f; cc.slopeLimit = 45f;
            var nm = go.AddComponent<NetMouse>();
            var so = new SerializedObject(nm);
            so.FindProperty("visual").objectReferenceValue = go.GetComponent<MouseVisual>();
            so.FindProperty("carryVisualPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mouse/Prefabs/Cheese.prefab");
            var arr = so.FindProperty("ringMaterials");
            arr.arraySize = ringMats.Length;
            for (int i = 0; i < ringMats.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = ringMats[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            var vso = new SerializedObject(go.GetComponent<MouseVisual>());
            vso.FindProperty("referenceRunSpeed").floatValue = 0.9f;
            vso.ApplyModifiedPropertiesWithoutUndo();
            return Save(go, "Player");
        }

        static GameObject BuildFood()
        {
            var go = new GameObject("Food");
            go.AddComponent<NetworkObject>();
            var f = go.AddComponent<FoodItem>();
            var cheese = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mouse/Prefabs/Cheese.prefab"), go.transform);
            cheese.name = "Visual";
            var so = new SerializedObject(f);
            so.FindProperty("visualRoot").objectReferenceValue = cheese.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(go, "Food");
        }

        static GameObject BuildTrap(Material[] ringMats)
        {
            var wood = Mat("M_TrapWood", new Color(0.78f, 0.58f, 0.36f));
            var metal = Mat("M_Metal", new Color(0.72f, 0.74f, 0.78f));
            var go = new GameObject("Trap");
            go.AddComponent<NetworkObject>();
            var t = go.AddComponent<Trap>();
            Box(go.transform, "Base", new Vector3(0f, 0.015f, 0f), new Vector3(0.34f, 0.03f, 0.22f), wood);
            var tint = Box(go.transform, "Tint", new Vector3(0f, 0.032f, 0.07f), new Vector3(0.06f, 0.006f, 0.06f), ringMats[0]);
            var pivot = new GameObject("BarPivot").transform;
            pivot.SetParent(go.transform, false);
            pivot.localPosition = new Vector3(0f, 0.035f, -0.02f);
            Box(pivot, "Bar", new Vector3(0f, 0f, 0.09f), new Vector3(0.28f, 0.012f, 0.012f), metal);
            Box(pivot, "BarL", new Vector3(-0.135f, 0f, 0.045f), new Vector3(0.012f, 0.012f, 0.09f), metal);
            Box(pivot, "BarR", new Vector3(0.135f, 0f, 0.045f), new Vector3(0.012f, 0.012f, 0.09f), metal);
            go.transform.localScale = Vector3.one * 1.6f;
            var so = new SerializedObject(t);
            so.FindProperty("bar").objectReferenceValue = pivot;
            so.FindProperty("tint").objectReferenceValue = tint.GetComponent<Renderer>();
            var arr = so.FindProperty("slotMaterials");
            arr.arraySize = ringMats.Length;
            for (int i = 0; i < ringMats.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = ringMats[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(go, "Trap");
        }

        static GameObject BuildMatch()
        {
            var fur = Mat("M_CatFur", new Color(0.93f, 0.60f, 0.30f));
            var dark = Mat("M_CatDark", new Color(0.55f, 0.33f, 0.16f));
            var light = Mat("M_CatLight", new Color(0.98f, 0.92f, 0.82f));
            var pink = Mat("M_CatPink", new Color(0.95f, 0.62f, 0.68f));
            var eye = Mat("M_CatEye", new Color(0.25f, 0.75f, 0.30f));
            var warn = Mat("M_Warning", new Color(1f, 0.15f, 0.1f, 0.45f), unlit: true, transparent: true);

            var go = new GameObject("Match");
            go.AddComponent<NetworkObject>();
            var mm = go.AddComponent<MatchManager>();

            // a big sitting cat behind the table (decor, no colliders)
            var cat = new GameObject("CatBody").transform;
            cat.SetParent(go.transform, false);
            Prim(PrimitiveType.Capsule, cat, "Body", new Vector3(0, 2.0f, 0.3f), new Vector3(2.4f, 2.2f, 2.0f), fur);
            Prim(PrimitiveType.Sphere, cat, "Chest", new Vector3(0, 2.3f, -0.45f), new Vector3(1.5f, 2.0f, 1.0f), light);
            Prim(PrimitiveType.Sphere, cat, "Head", new Vector3(0, 4.7f, -0.2f), new Vector3(2.0f, 1.8f, 1.8f), fur);
            Prim(PrimitiveType.Sphere, cat, "Muzzle", new Vector3(0, 4.45f, -1.0f), new Vector3(0.9f, 0.6f, 0.5f), light);
            Prim(PrimitiveType.Sphere, cat, "Nose", new Vector3(0, 4.62f, -1.22f), new Vector3(0.18f, 0.12f, 0.12f), pink);
            Prim(PrimitiveType.Sphere, cat, "EyeL", new Vector3(-0.45f, 4.95f, -0.98f), new Vector3(0.3f, 0.36f, 0.15f), eye);
            Prim(PrimitiveType.Sphere, cat, "EyeR", new Vector3(0.45f, 4.95f, -0.98f), new Vector3(0.3f, 0.36f, 0.15f), eye);
            var earL = Prim(PrimitiveType.Cube, cat, "EarL", new Vector3(-0.62f, 5.65f, -0.1f), new Vector3(0.55f, 0.75f, 0.2f), dark);
            earL.transform.localRotation = Quaternion.Euler(0, 0, 20);
            var earR = Prim(PrimitiveType.Cube, cat, "EarR", new Vector3(0.62f, 5.65f, -0.1f), new Vector3(0.55f, 0.75f, 0.2f), dark);
            earR.transform.localRotation = Quaternion.Euler(0, 0, -20);
            var tail = Prim(PrimitiveType.Capsule, cat, "Tail", new Vector3(1.5f, 0.5f, 0.9f), new Vector3(0.45f, 1.6f, 0.45f), dark);
            tail.transform.localRotation = Quaternion.Euler(0, 0, 70);

            // the striking paw (pivot = bottom of the pad)
            var paw = new GameObject("Paw").transform;
            paw.SetParent(go.transform, false);
            Prim(PrimitiveType.Sphere, paw, "Pad", new Vector3(0, 0.45f, 0), new Vector3(1.5f, 0.9f, 1.6f), fur);
            Prim(PrimitiveType.Capsule, paw, "Arm", new Vector3(0, 2.6f, 0.25f), new Vector3(1.0f, 2.2f, 1.0f), fur);
            for (int i = -1; i <= 1; i++)
                Prim(PrimitiveType.Sphere, paw, "Toe", new Vector3(i * 0.45f, 0.18f, -0.55f), new Vector3(0.38f, 0.3f, 0.38f), pink);

            var disc = Prim(PrimitiveType.Cylinder, go.transform, "Warning", Vector3.zero, new Vector3(2.2f, 0.01f, 2.2f), warn);
            var dr = disc.GetComponent<Renderer>();
            dr.shadowCastingMode = ShadowCastingMode.Off;
            dr.receiveShadows = false;

            var so = new SerializedObject(mm);
            so.FindProperty("catBody").objectReferenceValue = cat;
            so.FindProperty("paw").objectReferenceValue = paw;
            so.FindProperty("warningDisc").objectReferenceValue = dr;
            so.ApplyModifiedPropertiesWithoutUndo();
            return Save(go, "Match");
        }

        // ------------------------------------------------------------ scenes
        static void BuildBootScene(GameObject session, GameObject match, GameObject player, GameObject food, GameObject trap)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("NetworkManager");
            var nm = go.AddComponent<NetworkManager>();
            var utp = go.AddComponent<UnityTransport>();
            nm.NetworkConfig = new NetworkConfig { NetworkTransport = utp, ConnectionApproval = true, EnableSceneManagement = true, TickRate = 30 };
            go.AddComponent<LanDiscovery>();
            var cm = go.AddComponent<ConnectionManager>();
            var so = new SerializedObject(cm);
            so.FindProperty("sessionPrefab").objectReferenceValue = session;
            so.FindProperty("matchPrefab").objectReferenceValue = match;
            so.FindProperty("playerPrefab").objectReferenceValue = player;
            so.FindProperty("foodPrefab").objectReferenceValue = food;
            so.FindProperty("trapPrefab").objectReferenceValue = trap;
            so.ApplyModifiedPropertiesWithoutUndo();

            var boot = new GameObject("Bootstrap").AddComponent<Bootstrap>();
            var bso = new SerializedObject(boot);
            bso.FindProperty("isBootScene").boolValue = true;
            bso.ApplyModifiedPropertiesWithoutUndo();
            var cam = new GameObject("Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.12f, 0.10f);
            EditorSceneManager.SaveScene(scene, SceneDir + GameConfig.BootScene + ".unity");
        }

        static void BuildMenuScene(string name, System.Type ui)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.12f, 0.10f);
            cam.cullingMask = 0;   // UI only, nothing 3D to draw
            var go = new GameObject("UI");
            go.AddComponent<Bootstrap>();
            go.AddComponent(ui);
            EditorSceneManager.SaveScene(scene, SceneDir + name + ".unity");
        }

        static void BuildLevel(string name, bool night)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Bootstrap").AddComponent<Bootstrap>();

            var kitchen = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map/Prefabs/Kitchen.prefab"));
            kitchen.name = "Kitchen";

            var lightGo = new GameObject("Sun");
            var sun = lightGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = night ? 0.5f : 0.75f;
            sun.intensity = night ? 0.35f : 1.15f;
            sun.color = night ? new Color(0.55f, 0.65f, 1f) : new Color(1f, 0.96f, 0.88f);
            lightGo.transform.rotation = Quaternion.Euler(night ? 60f : 50f, night ? 200f : 330f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = night ? new Color(0.13f, 0.15f, 0.25f) : new Color(0.55f, 0.52f, 0.50f);
            RenderSettings.fog = false;

            if (night)
            {
                // the open fridge glows into the dark kitchen (one point light, per-pixel on mobile)
                var fl = new GameObject("FridgeLight").AddComponent<Light>();
                fl.type = LightType.Point;
                fl.range = 12f;
                fl.intensity = 6f;
                fl.color = new Color(0.85f, 0.95f, 1f);
                fl.shadows = LightShadows.None;
                fl.transform.position = new Vector3(0f, 3f, 5.5f);
            }

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = night ? new Color(0.04f, 0.05f, 0.09f) : new Color(0.55f, 0.75f, 0.9f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            camGo.AddComponent<AudioListener>();
            var follow = camGo.AddComponent<CameraFollow>();
            camGo.transform.position = new Vector3(0, 12, -16);
            camGo.transform.LookAt(new Vector3(0, 1, 0));

            new GameObject("HUD").AddComponent<GameHUD>();
            EditorSceneManager.SaveScene(scene, SceneDir + name + ".unity");
        }

        // ------------------------------------------------------------ helpers
        static GameObject Save(GameObject go, string name)
        {
            var path = PrefabDir + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(t);
            g.name = name;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return g;
        }

        static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
        {
            var g = Prim(PrimitiveType.Cube, parent, name, pos, size, m);
            g.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return g;
        }

        static Material Mat(string name, Color c, bool unlit = false, bool transparent = false)
        {
            string path = MatDir + name + ".mat";
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Simple Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else m.shader = shader;
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            if (transparent)
            {
                m.SetFloat("_Surface", 1f);          // Transparent
                m.SetFloat("_Blend", 0f);            // Alpha
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Ensure(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            Ensure(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
