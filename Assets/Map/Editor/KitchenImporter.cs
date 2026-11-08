using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MouseGame.EditorTools
{
    /// <summary>
    /// Imports Assets/Map/Models/Kitchen.fbx with mobile-friendly settings and builds Kitchen.prefab:
    ///   Kitchen_Visual  -> one static mesh, one material (1 draw call)
    ///   COL_*           -> BoxColliders only (renderer removed)
    ///   SPAWN/HOLE/FOOD/CATZONE/CAT empties -> gameplay marker components
    /// Menu: Tools/Map/Rebuild Kitchen Prefab. Re-export the FBX from Blender and it rebuilds itself.
    /// </summary>
    public class KitchenImporter : AssetPostprocessor
    {
        const string Root = "Assets/Map/";
        const string Fbx = Root + "Models/Kitchen.fbx";
        const string TexturePath = Root + "Textures/T_Kitchen_Palette.png";
        const string MaterialPath = Root + "Materials/M_Kitchen.mat";
        public const string PrefabPath = Root + "Prefabs/Kitchen.prefab";

        // ---------------- import settings ----------------
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Root + "Models/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.useFileScale = true;
            mi.globalScale = 1f;
            mi.bakeAxisConversion = true;
            mi.preserveHierarchy = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.generateSecondaryUV = true;              // lightmap UVs, in case lighting gets baked
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Root + "Textures/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 128;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (imported.Contains(Fbx))
                EditorApplication.delayCall += () => Build(false);
        }

        // ---------------- prefab build ----------------
        [MenuItem("Tools/Map/Rebuild Kitchen Prefab")]
        static void BuildFromMenu() => Build(true);

        public static void Build(bool verbose)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            if (model == null) { Debug.LogError("[Map] " + Fbx + " not found"); return; }
            EnsureFolder(Root + "Materials");
            EnsureFolder(Root + "Prefabs");
            var mat = CreateMaterial();

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "Kitchen";
            inst.transform.position = Vector3.zero;
            int cols = 0, markers = 0;
            try
            {
                foreach (var t in inst.GetComponentsInChildren<Transform>(true).ToArray())
                {
                    string n = t.name;
                    if (n == "Kitchen_Visual")
                    {
                        var r = t.GetComponent<MeshRenderer>();
                        r.sharedMaterial = mat;
                        r.shadowCastingMode = ShadowCastingMode.On;
                        r.receiveShadows = true;
                        r.lightProbeUsage = LightProbeUsage.Off;
                        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject,
                            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                    }
                    else if (n.StartsWith("COL_"))
                    {
                        // FBX cube is 1x1x1 (scale holds the size) -> unit BoxCollider, no renderer
                        Object.DestroyImmediate(t.GetComponent<MeshRenderer>());
                        Object.DestroyImmediate(t.GetComponent<MeshFilter>());
                        var bc = t.gameObject.AddComponent<BoxCollider>();
                        bc.center = Vector3.zero;
                        bc.size = Vector3.one;
                        GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);
                        t.gameObject.isStatic = true;
                        cols++;
                    }
                    else if (n.StartsWith("SPAWN_")) { t.gameObject.AddComponent<SpawnPoint>().index = Num(n, 1); FlattenScale(t); markers++; }
                    else if (n.StartsWith("HOLE_"))
                    {
                        FlattenScale(t);
                        t.gameObject.AddComponent<BoxCollider>();
                        var h = t.gameObject.AddComponent<MouseHole>();
                        h.index = Num(n, 1);
                        h.Setup();
                        markers++;
                    }
                    else if (n.StartsWith("FOOD_")) { t.gameObject.AddComponent<FoodSpawnPoint>().tier = Mathf.Clamp(Num(n, 1), 1, 3); FlattenScale(t); markers++; }
                    else if (n.StartsWith("CATZONE_"))
                    {
                        var z = t.gameObject.AddComponent<CatZone>();
                        z.index = Num(n, 1);
                        z.radius = t.lossyScale.x;
                        FlattenScale(t);
                        markers++;
                    }
                    else if (n == "CAT") { t.gameObject.AddComponent<CatSpot>(); FlattenScale(t); markers++; }
                }

                var map = inst.AddComponent<KitchenMap>();
                map.Collect();
                PrefabUtility.SaveAsPrefabAsset(inst, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Map] Kitchen prefab built: {cols} box colliders, {markers} markers -> {PrefabPath}");
        }

        static int Num(string name, int part)
        {
            var bits = name.Split('_');
            return bits.Length > part && int.TryParse(bits[part], out int v) ? v : 1;
        }

        /// <summary>Markers: unit scale and an upright rotation (FBX axis conversion leaves empties tilted).
        /// Spawns and holes face the middle of the room along X.</summary>
        static void FlattenScale(Transform t)
        {
            t.localScale = Vector3.one;
            bool facesRoom = t.name.StartsWith("SPAWN_") || t.name.StartsWith("HOLE_");
            if (facesRoom) t.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sign(t.position.x), 0f, 0f), Vector3.up);
            else if (t.name == "CAT") t.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            else t.rotation = Quaternion.identity;
        }

        static Material CreateMaterial()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Simple Lit")
                : Shader.Find("Mobile/Diffuse");
            if (mat == null)
            {
                mat = new Material(shader) { name = "M_Kitchen" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }
            else mat.shader = shader;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            if (mat.HasProperty("_SpecularHighlights")) mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.enableInstancing = false;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void EnsureFolder(string path)
        {
            path = path.TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) return;
            int i = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
    }
}
