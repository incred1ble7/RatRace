using System.Collections.Generic;
using UnityEngine;

namespace MouseGame
{
    /// <summary>Marks an object (cheese etc.) that the mouse can pick up.</summary>
    public class FoodPickup : MonoBehaviour
    {
        static readonly List<FoodPickup> all = new List<FoodPickup>();
        public static IReadOnlyList<FoodPickup> All => all;

        [Tooltip("Idle spin/bob while lying on the ground, so it is easy to notice.")]
        [SerializeField] bool animateOnGround = true;
        [SerializeField] float spinSpeed = 60f;

        public bool IsCarried { get; set; }

        /// <summary>Height of the pivot above the lowest point of the mesh (to rest on the ground).</summary>
        public float GroundOffset { get; private set; }

        void Awake()
        {
            var r = GetComponentInChildren<Renderer>();
            GroundOffset = r != null ? transform.position.y - r.bounds.min.y : 0f;
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        void Update()
        {
            if (!animateOnGround || IsCarried) return;
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }

        /// <summary>Nearest free food within radius of a point, or null.</summary>
        public static FoodPickup FindNearest(Vector3 point, float radius)
        {
            FoodPickup best = null;
            float bestSqr = radius * radius;
            for (int i = 0; i < all.Count; i++)
            {
                var f = all[i];
                if (f.IsCarried) continue;
                Vector3 d = f.transform.position - point;
                d.y = 0f;
                float s = d.sqrMagnitude;
                if (s <= bestSqr) { bestSqr = s; best = f; }
            }
            return best;
        }
    }
}
