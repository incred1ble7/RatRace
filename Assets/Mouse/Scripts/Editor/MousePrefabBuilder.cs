using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

namespace MouseGame.EditorTools
{
    /// <summary>
    /// Builds Mouse.prefab, Cheese.prefab, M_Mouse material, the Animator Controller and the carry mask.
    /// Menu: Tools/Mouse/Rebuild Prefab. Safe to run again (assets are overwritten).
    /// </summary>
    public static class MousePrefabBuilder
    {
        const string Root = MouseAssetPostprocessor.RootFolder;
        const string MaterialPath = Root + "Materials/M_Mouse.mat";
        const string TexturePath = Root + "Textures/T_Mouse_Palette.png";
        const string ControllerPath = Root + "Animation/AC_Mouse.controller";
        const string MaskPath = Root + "Animation/AM_Mouse_Carry.mask";
        public const string MousePrefabPath = Root + "Prefabs/Mouse.prefab";
        const string CheesePrefabPath = Root + "Prefabs/Cheese.prefab";

        // Bones driven by the Carry layer (everything else keeps playing Idle/Run).
        static readonly string[] CarryBones = { "Head", "Leg_FL", "Leg_FR" };

        [MenuItem("Tools/Mouse/Rebuild Prefab")]
        static void BuildFromMenu() => Build(true);

        public static void Build(bool verbose)
        {
            EnsureFolder(Root + "Materials");
            EnsureFolder(Root + "Animation");
            EnsureFolder(Root + "Prefabs");

            var clips = AssetDatabase.LoadAllAssetsAtPath(MouseAssetPostprocessor.MouseFbx)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview__"))
                .ToDictionary(c => c.name);
            foreach (var n in new[] { "Idle", "Run", "Carry" })
            {
                if (!clips.ContainsKey(n))
                {
                    Debug.LogError($"[Mouse] Clip '{n}' not found in {MouseAssetPostprocessor.MouseFbx}. Found: {string.Join(", ", clips.Keys)}");
                    return;
                }
            }

            if (!EnsureSocketExposed(clips["Run"])) return; // re-import triggered; Build runs again after it

            var material = CreateMaterial();
            var mask = CreateCarryMask(clips["Carry"]);
            var controller = CreateController(clips, mask);
            BuildMousePrefab(material, controller);
            BuildCheesePrefab(material);

            AssetDatabase.SaveAssets();
            if (verbose) Debug.Log($"[Mouse] Prefab built: {MousePrefabPath}");
        }

        // ------------------------------------------------------------------
        // Socket exposure: optimizeGameObjects hides bones, the socket must be exposed by path.
        // ------------------------------------------------------------------
        static bool EnsureSocketExposed(AnimationClip anyClip)
        {
            string spinePath = AnimationUtility.GetCurveBindings(anyClip)
                .Select(b => b.path)
                .FirstOrDefault(p => p == "Spine" || p.EndsWith("/Spine"));
            if (spinePath == null)
            {
                Debug.LogWarning("[Mouse] Could not find Spine bone path in clips; keeping default socket path.");
                return true;
            }
            string socketPath = spinePath + "/Food_Socket";
            var mi = (ModelImporter)AssetImporter.GetAtPath(MouseAssetPostprocessor.MouseFbx);
            if (mi.extraExposedTransformPaths != null && mi.extraExposedTransformPaths.Contains(socketPath))
                return true;

            Debug.Log($"[Mouse] Exposing socket path '{socketPath}' and re-importing.");
            mi.extraExposedTransformPaths = new[] { socketPath };
            mi.SaveAndReimport();
            EditorApplication.delayCall += () => Build(true);
            return false;
        }

        // ------------------------------------------------------------------
        static Material CreateMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = PickShader();
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_Mouse" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.1f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Shader PickShader()
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            string[] candidates = srp
                ? new[] { "Universal Render Pipeline/Simple Lit", "Universal Render Pipeline/Lit", "HDRP/Lit" }
                : new[] { "Mobile/Diffuse", "Legacy Shaders/Diffuse", "Standard" };
            foreach (var n in candidates)
            {
                var s = Shader.Find(n);
                if (s != null) return s;
            }
            return Shader.Find("Standard");
        }

        // ------------------------------------------------------------------
        static AvatarMask CreateCarryMask(AnimationClip carry)
        {
            // All bone paths come from the clip itself, so the mask always matches the hierarchy.
            var paths = new SortedSet<string>();
            foreach (var b in AnimationUtility.GetCurveBindings(carry))
            {
                string p = b.path;
                while (!string.IsNullOrEmpty(p))
                {
                    paths.Add(p);
                    int i = p.LastIndexOf('/');
                    p = i < 0 ? "" : p.Substring(0, i);
                }
            }

            AssetDatabase.DeleteAsset(MaskPath);
            var mask = new AvatarMask { name = "AM_Mouse_Carry" };
            // Humanoid body parts are irrelevant for a Generic rig; disable them.
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

            var list = paths.ToList();
            mask.transformCount = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                string bone = list[i].Substring(list[i].LastIndexOf('/') + 1);
                mask.SetTransformPath(i, list[i]);
                mask.SetTransformActive(i, CarryBones.Contains(bone));
            }
            AssetDatabase.CreateAsset(mask, MaskPath);
            return mask;
        }

        // ------------------------------------------------------------------
        static AnimatorController CreateController(Dictionary<string, AnimationClip> clips, AvatarMask mask)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter
            {
                name = "RunSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f
            });

            // Base layer: Idle <-> Run by Speed
            var sm = ac.layers[0].stateMachine;
            var idle = sm.AddState("Idle", new Vector3(300, 0));
            idle.motion = clips["Idle"];
            var run = sm.AddState("Run", new Vector3(300, 100));
            run.motion = clips["Run"];
            run.speedParameter = "RunSpeed";
            run.speedParameterActive = true;
            sm.defaultState = idle;

            var toRun = idle.AddTransition(run);
            toRun.hasExitTime = false;
            toRun.duration = 0.1f;
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.05f, "Speed");

            var toIdle = run.AddTransition(idle);
            toIdle.hasExitTime = false;
            toIdle.duration = 0.15f;
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");

            // Carry layer: override paws + head, weight driven by MouseVisual (0..1)
            var carrySm = new AnimatorStateMachine { name = "Carry", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(carrySm, ac);
            var carry = carrySm.AddState("Carry", new Vector3(300, 0));
            carry.motion = clips["Carry"];
            carry.writeDefaultValues = false;
            carrySm.defaultState = carry;

            ac.AddLayer(new AnimatorControllerLayer
            {
                name = "Carry",
                stateMachine = carrySm,
                avatarMask = mask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                defaultWeight = 0f,
            });

            EditorUtility.SetDirty(ac);
            return ac;
        }

        // ------------------------------------------------------------------
        static void BuildMousePrefab(Material mat, AnimatorController controller)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MouseAssetPostprocessor.MouseFbx);
            var root = new GameObject("Mouse");
            try
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model, root.transform);
                inst.name = "Model";
                // The exported mesh faces -Z; turn it so the prefab root faces +Z (Unity forward).
                inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                var animator = inst.GetComponent<Animator>();
                if (animator == null) animator = inst.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms; // no skinning when off-screen
                animator.updateMode = AnimatorUpdateMode.Normal;

                foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.sharedMaterial = mat;
                    smr.quality = SkinQuality.Bone2;
                    smr.updateWhenOffscreen = false;
                    smr.skinnedMotionVectors = false;
                    smr.lightProbeUsage = LightProbeUsage.Off;
                    smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    smr.allowOcclusionWhenDynamic = true;
                }

                var visual = root.AddComponent<MouseVisual>();
                var so = new SerializedObject(visual);
                so.FindProperty("animator").objectReferenceValue = animator;
                var socket = FindDeep(inst.transform, "Food_Socket");
                if (socket == null)
                    Debug.LogWarning("[Mouse] Food_Socket not found on the model – carrying will not work.");
                so.FindProperty("foodSocket").objectReferenceValue = socket;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, MousePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        static void BuildCheesePrefab(Material mat)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MouseAssetPostprocessor.CheeseFbx);
            if (model == null) return;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                inst.name = "Cheese";
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterial = mat;
                    r.lightProbeUsage = LightProbeUsage.Off;
                    r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                PrefabUtility.SaveAsPrefabAsset(inst, CheesePrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        // ------------------------------------------------------------------
        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            string parent = path.Substring(0, i);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(i + 1));
        }
    }
}
