using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MouseGame.EditorTools
{
    public class MouseAssetPostprocessor : AssetPostprocessor
    {
        public const string RootFolder = "Assets/Mouse/";
        public const string MouseFbx = RootFolder + "Models/Mouse.fbx";
        public const string CheeseFbx = RootFolder + "Models/Cheese.fbx";
        public const string TextureFolder = RootFolder + "Textures/";

        public const string DefaultSocketPath = "MouseRig/Root/Body/Spine/Food_Socket";

        static readonly HashSet<string> LoopingClips = new HashSet<string> { "Idle", "Run", "Carry" };

        bool IsOurModel =>
            assetPath.StartsWith(RootFolder + "Models/") &&
            assetPath.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessModel()
        {
            if (!IsOurModel) return;
            var mi = (ModelImporter)assetImporter;

            mi.useFileScale = true;
            mi.globalScale = 1f;
            mi.bakeAxisConversion = true;
            mi.preserveHierarchy = true;
            mi.importCameras = false;
            mi.importLights = false;
            mi.importVisibility = false;
            mi.importBlendShapes = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Medium;
            mi.optimizeMeshPolygons = true;
            mi.optimizeMeshVertices = true;
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.generateSecondaryUV = false;
            mi.addCollider = false;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;

            if (assetPath != MouseFbx)
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
                return;
            }

            mi.animationType = ModelImporterAnimationType.Generic;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.skinWeights = ModelImporterSkinWeights.Custom;
            mi.maxBonesPerVertex = 2;
            mi.minBoneWeight = 0.01f;
            mi.optimizeGameObjects = true;
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

            var clips = new List<ModelImporterClipAnimation>();
            foreach (var c in mi.defaultClipAnimations)
            {
                string n = c.takeName;
                int bar = n.LastIndexOf('|');
                if (bar >= 0) n = n.Substring(bar + 1);
                c.name = n;
                bool loop = LoopingClips.Contains(n);
                c.loopTime = loop;
                c.loopPose = false;
                c.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
                clips.Add(c);
            }
            mi.clipAnimations = clips.ToArray();
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TextureFolder)) return;
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
