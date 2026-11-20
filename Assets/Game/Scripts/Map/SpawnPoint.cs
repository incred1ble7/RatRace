using UnityEngine;

namespace MouseGame
{
    public class SpawnPoint : MonoBehaviour
    {
        public int index = 1;

        void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, 0.3f);
            Gizmos.DrawLine(transform.position + Vector3.up * 0.1f, transform.position + Vector3.up * 0.1f + transform.forward * 0.8f);
        }
    }
}
