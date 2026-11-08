using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace MouseGame.EditorTools
{
    /// <summary>Menu: Tools/Mouse/Build Game Scene — creates Assets/Scenes/Game.unity with mouse, cheese and mobile UI.</summary>
    public static class GameSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string GroundMatPath = "Assets/Game/Materials/M_Ground.mat";

        [MenuItem("Tools/Mouse/Build Game Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---------- light & camera ----------
            var cam = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.75f, 0.9f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;

            // ---------- ground ----------
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f); // 20 x 20 m
            ground.GetComponent<Renderer>().sharedMaterial = GroundMaterial();
            ground.isStatic = true;

            // invisible walls so the mouse cannot run off the edge
            var walls = new GameObject("Walls").transform;
            for (int w = 0; w < 4; w++)
            {
                var wall = new GameObject("Wall_" + w);
                wall.transform.SetParent(walls, false);
                var bc = wall.AddComponent<BoxCollider>();
                bool alongX = w < 2;
                float sign = (w % 2 == 0) ? 1f : -1f;
                bc.size = alongX ? new Vector3(20.4f, 2f, 0.2f) : new Vector3(0.2f, 2f, 20.4f);
                wall.transform.position = alongX ? new Vector3(0, 1f, 10.1f * sign) : new Vector3(10.1f * sign, 1f, 0);
                wall.isStatic = true;
            }

            // ---------- mouse ----------
            var mousePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mouse/Prefabs/Mouse.prefab");
            var mouse = (GameObject)PrefabUtility.InstantiatePrefab(mousePrefab);
            mouse.name = "Mouse";
            mouse.transform.position = Vector3.zero;
            var cc = mouse.AddComponent<CharacterController>();
            cc.height = 0.24f; cc.radius = 0.11f; cc.center = new Vector3(0f, 0.13f, 0f);
            cc.stepOffset = 0.05f; cc.skinWidth = 0.01f; cc.minMoveDistance = 0f; cc.slopeLimit = 45f;
            var controller = mouse.AddComponent<MouseController>();
            var visual = mouse.GetComponent<MouseVisual>();
            var vso = new SerializedObject(visual);
            vso.FindProperty("referenceRunSpeed").floatValue = 0.7f;
            vso.ApplyModifiedPropertiesWithoutUndo();

            // ---------- cheese ----------
            var cheesePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Mouse/Prefabs/Cheese.prefab");
            var cheeseRoot = new GameObject("Cheese").transform;
            Vector3[] spots = { new Vector3(0.8f, 0, 1.2f), new Vector3(-1.5f, 0, 2.2f), new Vector3(2.2f, 0, -0.8f),
                                new Vector3(-2.4f, 0, -1.6f), new Vector3(0.3f, 0, 3.4f) };
            for (int i = 0; i < spots.Length; i++)
            {
                var c = (GameObject)PrefabUtility.InstantiatePrefab(cheesePrefab, cheeseRoot);
                c.name = "Cheese_" + (i + 1);
                c.transform.localScale = Vector3.one * 1.6f;
                c.transform.SetPositionAndRotation(spots[i], Quaternion.Euler(0, i * 47f, 0));
                var cr = c.GetComponentInChildren<Renderer>();
                if (cr != null) c.transform.position += Vector3.up * (c.transform.position.y - cr.bounds.min.y);
                c.AddComponent<FoodPickup>();
            }

            // ---------- camera follow ----------
            var follow = cam.gameObject.AddComponent<CameraFollow>();
            var fso = new SerializedObject(follow);
            fso.FindProperty("target").objectReferenceValue = mouse.transform;
            fso.ApplyModifiedPropertiesWithoutUndo();
            follow.Snap();

            // ---------- UI ----------
            var canvasGo = new GameObject("MobileUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif

            var circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // joystick touch zone = left half of the screen
            var zone = NewUI("JoystickZone", canvasGo.transform);
            zone.anchorMin = new Vector2(0, 0); zone.anchorMax = new Vector2(0.5f, 1f);
            zone.offsetMin = zone.offsetMax = Vector2.zero;
            var zoneImg = zone.gameObject.AddComponent<Image>();
            zoneImg.color = new Color(1, 1, 1, 0f);

            var bg = NewUI("Background", zone);
            bg.anchorMin = bg.anchorMax = new Vector2(0, 0);
            bg.sizeDelta = new Vector2(260, 260);
            bg.anchoredPosition = new Vector2(260, 240);
            var bgImg = bg.gameObject.AddComponent<Image>();
            bgImg.sprite = circle; bgImg.color = new Color(1, 1, 1, 0.25f); bgImg.raycastTarget = false;

            var handle = NewUI("Handle", bg);
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(120, 120);
            var hImg = handle.gameObject.AddComponent<Image>();
            hImg.sprite = circle; hImg.color = new Color(1, 1, 1, 0.8f); hImg.raycastTarget = false;

            // the zone anchors are stretched, so background local position is relative to zone center:
            // convert by putting background anchored to bottom-left of the zone (done above).
            var joy = zone.gameObject.AddComponent<VirtualJoystick>();
            var jso = new SerializedObject(joy);
            jso.FindProperty("background").objectReferenceValue = bg;
            jso.FindProperty("handle").objectReferenceValue = handle;
            jso.ApplyModifiedPropertiesWithoutUndo();

            // action button (bottom-right)
            var btnRt = NewUI("ActionButton", canvasGo.transform);
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(1, 0);
            btnRt.sizeDelta = new Vector2(240, 240);
            btnRt.anchoredPosition = new Vector2(-230, 230);
            var btnImg = btnRt.gameObject.AddComponent<Image>();
            btnImg.sprite = circle; btnImg.color = new Color(1f, 0.82f, 0.25f, 0.95f);
            var btn = btnRt.gameObject.AddComponent<Button>();
            btn.targetGraphic = btnImg;

            var labelRt = NewUI("Label", btnRt);
            labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;
            var label = labelRt.gameObject.AddComponent<Text>();
            label.font = font; label.fontSize = 38; label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter; label.color = new Color(0.25f, 0.15f, 0.05f);
            label.text = "Взять"; label.raycastTarget = false;

            var hud = canvasGo.AddComponent<MobileHUD>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("player").objectReferenceValue = controller;
            hso.FindProperty("actionButton").objectReferenceValue = btn;
            hso.FindProperty("actionLabel").objectReferenceValue = label;
            hso.ApplyModifiedPropertiesWithoutUndo();

            // ---------- wire controller ----------
            var cso = new SerializedObject(controller);
            cso.FindProperty("visual").objectReferenceValue = visual;
            cso.FindProperty("joystick").objectReferenceValue = joy;
            cso.FindProperty("cameraTransform").objectReferenceValue = cam.transform;
            cso.ApplyModifiedPropertiesWithoutUndo();

            // ---------- save ----------
            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("[Mouse] Game scene built: " + ScenePath);
        }

        static RectTransform NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static Material GroundMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(GroundMatPath);
            if (mat != null) return mat;
            EnsureFolder("Assets/Game/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Mobile/Diffuse") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = "M_Ground" };
            var col = new Color(0.47f, 0.62f, 0.33f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", col);
            AssetDatabase.CreateAsset(mat, GroundMatPath);
            return mat;
        }

        static void AddToBuildSettings(string path)
        {
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            list.Add(new EditorBuildSettingsScene(path, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != path) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            string parent = path.Substring(0, i);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(i + 1));
        }
    }
}
