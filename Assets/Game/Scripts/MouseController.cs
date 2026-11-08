using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MouseGame
{
    /// <summary>
    /// Player movement for the mouse: on-screen joystick (mobile) + WASD/arrows (editor),
    /// camera-relative, with gravity. Drives MouseVisual animation and picks up / drops food.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class MouseController : MonoBehaviour
    {
        [SerializeField] MouseVisual visual;
        [SerializeField] VirtualJoystick joystick;
        [SerializeField] Transform cameraTransform;

        [Header("Movement")]
        [SerializeField] float moveSpeed = 1.6f;
        [SerializeField] float acceleration = 10f;
        [SerializeField] float turnSpeed = 720f;
        [SerializeField] float gravity = -20f;
        [Tooltip("Carrying food slows the mouse down a little.")]
        [Range(0.3f, 1f)] [SerializeField] float carrySpeedMultiplier = 0.85f;

        [Header("Food")]
        [SerializeField] float pickupRadius = 0.45f;
        [Tooltip("Where dropped food is placed, in front of the mouse (m).")]
        [SerializeField] float dropDistance = 0.32f;

        CharacterController cc;
        Vector3 spawnPosition;
        Quaternion spawnRotation;
        Vector3 velocity;          // horizontal
        float verticalVelocity;
        FoodPickup carried;

        public bool IsCarrying => carried != null;
        /// <summary>Food the mouse could grab right now (null if none / already carrying).</summary>
        public FoodPickup NearbyFood { get; private set; }
        public event Action<bool> CarryChanged;

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (visual == null) visual = GetComponent<MouseVisual>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        void Update()
        {
            Vector2 input = ReadMoveInput();

            // camera-relative direction on the ground plane
            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (cameraTransform != null)
            {
                fwd = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
            }
            Vector3 wish = fwd * input.y + right * input.x;
            float speed = moveSpeed * (IsCarrying ? carrySpeedMultiplier : 1f);
            velocity = Vector3.MoveTowards(velocity, wish * speed, acceleration * Time.deltaTime);

            if (wish.sqrMagnitude > 0.0001f)
            {
                var target = Quaternion.LookRotation(wish, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
            }

            verticalVelocity = cc.isGrounded ? -1f : verticalVelocity + gravity * Time.deltaTime;
            cc.Move((velocity + Vector3.up * verticalVelocity) * Time.deltaTime);

            if (transform.position.y < -5f) Respawn();

            Vector3 actual = cc.velocity; actual.y = 0f;
            if (visual != null) visual.SetSpeed(actual.magnitude);

            NearbyFood = IsCarrying ? null : FoodPickup.FindNearest(transform.position, pickupRadius);

            if (InteractPressedThisFrame()) Interact();
        }

        /// <summary>Safety net: put the mouse back to its start point (e.g. after falling).</summary>
        public void Respawn()
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            cc.enabled = true;
            velocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        /// <summary>Pick up nearby food, or drop the carried one. Hook to the mobile button.</summary>
        public void Interact()
        {
            if (IsCarrying) Drop();
            else if (NearbyFood != null) PickUp(NearbyFood);
        }

        void PickUp(FoodPickup food)
        {
            if (visual == null) return;
            carried = food;
            food.IsCarried = true;
            visual.PickUp(food.transform);
            NearbyFood = null;
            CarryChanged?.Invoke(true);
        }

        void Drop()
        {
            var food = carried;
            carried = null;
            if (visual != null) visual.Drop();
            if (food != null)
            {
                Vector3 p = transform.position + transform.forward * dropDistance;
                p.y = transform.position.y + food.GroundOffset;
                food.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
                food.IsCarried = false;
            }
            CarryChanged?.Invoke(false);
        }

        Vector2 ReadMoveInput()
        {
            Vector2 v = joystick != null ? joystick.Direction : Vector2.zero;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                Vector2 k = Vector2.zero;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1;
                if (k != Vector2.zero) v = k.normalized;
            }
            var pad = Gamepad.current;
            if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.02f) v = pad.leftStick.ReadValue();
#else
            var k = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (k != Vector2.zero) v = Vector2.ClampMagnitude(k, 1f);
#endif
            return Vector2.ClampMagnitude(v, 1f);
        }

        static bool InteractPressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
#endif
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }
    }
}
