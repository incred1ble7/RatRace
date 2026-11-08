using System.Collections.Generic;
using MouseGame.Net;
using Unity.Netcode;
using UnityEngine;

namespace MouseGame
{
    /// <summary>
    /// Networked food on the ground. Static (no NetworkTransform): its position is sent once at spawn.
    /// Bigger piece = more points (tier 1 floor, 2 fridge, 3 table; dropped food keeps its value).
    /// </summary>
    public class FoodItem : NetworkBehaviour
    {
        public static readonly List<FoodItem> All = new List<FoodItem>();

        [SerializeField] Transform visualRoot;
        [SerializeField] float baseVisualHeight = 0.05f;   // cheese mesh height at scale 1

        public readonly NetworkVariable<int> Value = new NetworkVariable<int>(1);
        public readonly NetworkVariable<int> Tier = new NetworkVariable<int>(1);

        /// <summary>Server only: index of the map spawn point it came from (-1 = dropped by a mouse).</summary>
        public int SpawnPointIndex { get; set; } = -1;

        float spinOffset;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            spinOffset = Random.value * 360f;
            Tier.OnValueChanged += (_, __) => ApplyLook();
            ApplyLook();
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        void ApplyLook()
        {
            if (visualRoot == null) return;
            float s = Tier.Value >= 3 ? 2.6f : Tier.Value == 2 ? 2.0f : 1.5f;
            visualRoot.localScale = Vector3.one * s;
            visualRoot.localPosition = new Vector3(0f, baseVisualHeight * s * 0.5f + 0.005f, 0f);
        }

        void Update()
        {
            if (visualRoot != null)
                visualRoot.localRotation = Quaternion.Euler(0f, spinOffset + Time.time * 50f, 0f);
        }

        public static FoodItem FindNearest(Vector3 point, float radius)
        {
            FoodItem best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < All.Count; i++)
            {
                var f = All[i];
                Vector3 d = f.transform.position - point;
                if (Mathf.Abs(d.y) > 0.6f) continue;   // other floor level (table vs floor)
                d.y = 0f;
                float s = d.sqrMagnitude;
                if (s <= bestSqr) { bestSqr = s; best = f; }
            }
            return best;
        }

        public static int ValueOfTier(int tier) => GameConfig.FoodValueByTier[Mathf.Clamp(tier, 1, 3)];
    }
}
