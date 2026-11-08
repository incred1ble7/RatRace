using UnityEngine;

namespace MouseGame
{
    /// <summary>
    /// Visual-only controller for the mouse prefab.
    /// Gameplay code calls SetSpeed() every frame and PickUp()/Drop() for food.
    /// Movement itself (CharacterController / NavMesh / Rigidbody) stays in your gameplay code.
    /// </summary>
    [DisallowMultipleComponent]
    public class MouseVisual : MonoBehaviour
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int RunSpeedId = Animator.StringToHash("RunSpeed");

        [SerializeField] Animator animator;
        [SerializeField] Transform foodSocket;

        [Header("Run")]
        [Tooltip("Movement speed (m/s) at which the Run clip plays at 1x. Tune to remove foot sliding.")]
        [SerializeField] float referenceRunSpeed = 0.6f;
        [SerializeField] Vector2 runPlaybackClamp = new Vector2(0.6f, 2.5f);

        [Header("Carry")]
        [Tooltip("How fast the paws go up / down when picking up or dropping food (1/sec).")]
        [SerializeField] float carryBlendSpeed = 8f;
        [Tooltip("Extra rotation of the food relative to the mouse.")]
        [SerializeField] Vector3 foodRotationOffset = Vector3.zero;

        int carryLayer = -1;
        float carryWeight;
        Transform carried;

        public Transform FoodSocket => foodSocket;
        public Transform CarriedFood => carried;
        public bool IsCarrying => carried != null;

        void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (foodSocket == null) foodSocket = FindDeep(transform, "Food_Socket");
            if (animator != null) carryLayer = animator.GetLayerIndex("Carry");
        }

        /// <summary>Current horizontal movement speed in m/s. 0 = Idle.</summary>
        public void SetSpeed(float metersPerSecond)
        {
            if (animator == null) return;
            animator.SetFloat(SpeedId, metersPerSecond);
            float k = referenceRunSpeed > 0.001f ? metersPerSecond / referenceRunSpeed : 1f;
            animator.SetFloat(RunSpeedId, Mathf.Clamp(k, runPlaybackClamp.x, runPlaybackClamp.y));
        }

        /// <summary>Attach a food object to the paws. Its pivot is placed at the socket.</summary>
        public void PickUp(Transform food)
        {
            if (food == null || foodSocket == null) return;
            if (carried != null) Drop();

            carried = food;
            if (food.TryGetComponent(out Rigidbody rb))
            {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            food.SetParent(foodSocket, false);
            food.localPosition = Vector3.zero;
            food.rotation = transform.rotation * Quaternion.Euler(foodRotationOffset);
        }

        /// <summary>Detach the carried food (stays in the world where it is). Returns it.</summary>
        public Transform Drop()
        {
            var food = carried;
            carried = null;
            if (food != null)
            {
                food.SetParent(null, true);
                if (food.TryGetComponent(out Rigidbody rb))
                {
                    rb.isKinematic = false;
                    rb.detectCollisions = true;
                }
            }
            return food;
        }

        void Update()
        {
            if (animator == null || carryLayer < 0) return;
            float target = carried != null ? 1f : 0f;
            if (Mathf.Approximately(carryWeight, target)) return;
            carryWeight = Mathf.MoveTowards(carryWeight, target, carryBlendSpeed * Time.deltaTime);
            animator.SetLayerWeight(carryLayer, carryWeight);
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }
#endif
    }
}
