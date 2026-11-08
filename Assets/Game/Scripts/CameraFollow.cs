using UnityEngine;

namespace MouseGame
{
    /// <summary>Smooth 3/4 top-down follow camera (does not rotate, so joystick directions stay intuitive).</summary>
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new Vector3(0f, 1.5f, -1.7f);
        [SerializeField] float lookHeight = 0.15f;
        [SerializeField] float smoothTime = 0.15f;

        Vector3 vel;

        public Transform Target { get => target; set => target = value; }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref vel, smoothTime);
            transform.rotation = FixedRotation; // constant yaw keeps joystick directions stable
        }

        Quaternion FixedRotation => Quaternion.LookRotation(Vector3.up * lookHeight - offset, Vector3.up);

        /// <summary>Jump to the target instantly (e.g. on scene start).</summary>
        public void Snap()
        {
            if (target == null) return;
            transform.position = target.position + offset;
            transform.rotation = FixedRotation;
        }

        void Start() => Snap();
    }
}
