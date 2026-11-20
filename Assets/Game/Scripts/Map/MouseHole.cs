using UnityEngine;

namespace MouseGame
{
    [RequireComponent(typeof(BoxCollider))]
    public class MouseHole : MonoBehaviour
    {
        public int index = 1;

        void Reset() => Setup();

        public void Setup()
        {
            var bc = GetComponent<BoxCollider>();
            bc.isTrigger = true;
            bc.center = new Vector3(0f, 0.4f, 0.4f);
            bc.size = new Vector3(1.6f, 0.8f, 1.2f);
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            var bc = GetComponent<BoxCollider>();
            if (bc != null) Gizmos.DrawWireCube(bc.center, bc.size);
        }
    }
}
