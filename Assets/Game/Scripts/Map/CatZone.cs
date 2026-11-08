using UnityEngine;

namespace MouseGame
{
    /// <summary>Area the cat slaps with its paw. Gameplay reads radius; visual ring is baked into the map.</summary>
    public class CatZone : MonoBehaviour
    {
        public int index = 1;
        public float radius = 1.1f;

        public bool Contains(Vector3 worldPos)
        {
            Vector3 d = worldPos - transform.position;
            if (Mathf.Abs(d.y) > 1f) return false;     // different floor level (e.g. under the table)
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            const int seg = 24;
            Vector3 prev = transform.position + new Vector3(radius, 0.02f, 0f);
            for (int i = 1; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a) * radius, 0.02f, Mathf.Sin(a) * radius);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }
        }
    }
}
