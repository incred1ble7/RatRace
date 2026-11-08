using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MouseGame.EditorTools
{
    /// <summary>
    /// Bakes smoothed normals into the tangent channel of our low-poly models (kitchen, mouse, cheese).
    /// The toon outline pass pushes vertices along them, so hard-edged meshes get a closed outline
    /// instead of cracks. Tangents are skinned too, so it also works on the animated mouse.
    /// </summary>
    public class ToonOutlineNormals : AssetPostprocessor
    {
        static readonly string[] Folders = { "Assets/Map/Models/", "Assets/Mouse/Models/" };

        public override int GetPostprocessOrder() => 100;   // after the other importers

        void OnPostprocessModel(GameObject root)
        {
            bool ours = false;
            foreach (var f in Folders) if (assetPath.StartsWith(f)) ours = true;
            if (!ours) return;

            var meshes = new HashSet<Mesh>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true)) if (mf.sharedMesh != null) meshes.Add(mf.sharedMesh);
            foreach (var sm in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (sm.sharedMesh != null) meshes.Add(sm.sharedMesh);
            foreach (var m in meshes) Bake(m);
        }

        public static void Bake(Mesh mesh)
        {
            var verts = mesh.vertices;
            var normals = mesh.normals;
            if (verts.Length == 0 || normals.Length != verts.Length) return;

            // average the normals of all vertices sharing a position (1 mm grid)
            var sum = new Dictionary<Vector3Int, Vector3>();
            var keys = new Vector3Int[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var k = Vector3Int.RoundToInt(verts[i] * 1000f);
                keys[i] = k;
                sum.TryGetValue(k, out var s);
                sum[k] = s + normals[i];
            }
            var tangents = new Vector4[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var n = sum[keys[i]];
                n = n.sqrMagnitude > 1e-8f ? n.normalized : normals[i];
                tangents[i] = new Vector4(n.x, n.y, n.z, 1f);
            }
            mesh.tangents = tangents;
        }
    }
}
