using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MouseGame.EditorTools
{
    /// <summary>
    /// Mobile-friendly import settings for everything in Assets/Mouse.
    /// Applied automatically on import; after the first import the prefab is built once.
    /// </summary>
    public class MouseAssetPostprocessor : AssetPostprocessor
    {
        public const string RootFolder = "Assets/Mouse/";
        public const string MouseFbx = RootFolder + "Models/Mouse.fbx";
        public const string CheeseFbx = RootFolder + "Models/Cheese.fbx";
        public const string TextureFolder = RootFolder + "Textures/";

        // Path of the food socket inside the model (relative to the model root).
        // MousePrefabBuilder verifies it against the real hierarchy and fixes it if needed.
        public const string DefaultSocketPath = "MouseRig/Root/Body/Spine/Food_Socket";

        static readonly HashSet<string> LoopingClips = new HashSet<string> { "Idle", "Run", "Carry" };

        bool IsOurModel =>
            assetPath.StartsWith(RootFolder + "Models/") &&
            assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsOurModel) return;
            var mi = (ModelImporter)assetImporter;

            // ---- shared mesh settings ----------------------------------------
            mi.useFileScale = true;
            mi.globalScale = 1f;
            mi.bakeAxisConversion = true;           // no -90° X rotation on the root
            mi.preserveHierarchy = true;            // deterministic paths (MouseRig/...)
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.isReadable = false;                  // no CPU copy of the mesh -> less RAM
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.importNormals = ModelImporterNormals.Import;   // flat low-poly normals from Blender
            mi.importTangents = ModelImporterTangents.None;   // no normal maps -> no tangents
            mi.generateSecondaryUV = false;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None; // material comes from M_Mouse

            if (assetPath != MouseFbx)
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                return;
            }

            // ---- rig / animation (mouse only) --------------------------------
            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.skinWeights = ModelImporterSkinWeights.Custom;
            mi.maxBonesPerVertex = 2;               // matches SkinQuality.Bone2 on the renderer
            mi.minBoneWeight = 0.01f;
            mi.optimizeGameObjects = true;          // bones are not GameObjects at runtime
            if (mi.extraExposedTransformPaths == null || mi.extraExposedTransformPaths.Length == 0)
                mi.extraExposedTransformPaths = new[] { DefaultSocketPath };

            mi.importAnimation = true;
            mi.resampleCurves = true;
            mi.animationCompression = ModelImporterAnimationCompression.Optimal;
            mi.animationRotationError = 0.5f;
            mi.animationPositionError = 0.5f;
            mi.animationScaleError = 0.5f;
        }

        void OnPreprocessAnimation()
        {
            if (assetPath != MouseFbx) return;
            var mi = (ModelImporter)assetImporter;

            // Blender names takes "MouseRig|Idle" -> rename to "Idle", set looping.
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var c in mi.defaultClipAnimations)
            {
                string n = c.takeName;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                c.name = n;
                bool loop = LoopingClips.Contains(n);
                c.loopTime = loop;
                c.loopPose = false;               // clips are authored seamless already
                c.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                clips.Add(c);
            }
            mi.clipAnimations = clips.ToArray();
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureFolder)) return;
            var ti = (TextureImporter)assetImporter;
            // 64x8 colour palette: tiny, exact colours, no mips.
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.None;
            ti.mipmapEnabled = false;
            ti.filterMode = FilterMode.Point;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.isReadable = false;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // 1.5 KB, compression would smear colours
            ti.maxTextureSize = 64;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            foreach (var p in imported)
            {
                if (p == MouseFbx && !System.IO.File.Exists(MousePrefabBuilder.MousePrefabPath))
                {
                    EditorApplication.delayCall += () => MousePrefabBuilder.Build(false);
                    return;
                }
            }
        }
    }
}
