using UnityEngine;

namespace MouseGame
{
    /// <summary>Where food can appear. Tier: 1 floor, 2 fridge, 3 table (premium, under the cat's paw).</summary>
    public class FoodSpawnPoint : MonoBehaviour
    {
        [Range(1, 3)] public int tier = 1;

        void OnDrawGizmos()
        {
            Gizmos.color = tier == 3 ? new Color(1f, 0.5f, 0f) : tier == 2 ? Color.cyan : Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.1f, Vector3.one * 0.2f);
        }
    }
}
