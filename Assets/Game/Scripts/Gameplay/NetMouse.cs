using System.Collections.Generic;
using MouseGame.Net;
using Unity.Netcode;
using UnityEngine;

namespace MouseGame
{
    /// <summary>
    /// Networked player mouse.
    ///  * Movement is owner-authoritative (smooth on phones); NetworkTransform (Owner mode) syncs it.
    ///  * Everything that scores or hurts is decided by the server: carried food, stun, slow, delivery.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class NetMouse : NetworkBehaviour
    {
        public static readonly List<NetMouse> All = new List<NetMouse>();
        public static NetMouse Local { get; private set; }

        [SerializeField] MouseVisual visual;
        [SerializeField] GameObject carryVisualPrefab;   // non-networked cheese model
        [SerializeField] Material[] ringMaterials;        // one per slot colour (assets, so shaders are kept in builds)

        public readonly NetworkVariable<int> Slot = new NetworkVariable<int>(1);
        public readonly NetworkVariable<int> CarriedCount = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> CarriedValue = new NetworkVariable<int>(0);
        public readonly NetworkVariable<double> StunUntil = new NetworkVariable<double>(0);
        public readonly NetworkVariable<double> SlowUntil = new NetworkVariable<double>(0);
        public readonly NetworkVariable<bool> InputEnabled = new NetworkVariable<bool>(false);

        CharacterController cc;
        Transform cam;
        Vector3 velocity;
        float verticalVelocity;
        float dashUntil, dashReadyAt, trapReadyAt;
        Vector3 spawnPos;
        Quaternion spawnRot;

        // visuals
        Vector3 lastPos;
        float shownSpeed;
        Transform carried;
        Transform model;
        Renderer ring;

        public float DashCooldown01 => Mathf.Clamp01((dashReadyAt - Time.time) / GameConfig.DashCooldown);
        public float TrapCooldown01 => Mathf.Clamp01((trapReadyAt - Time.time) / GameConfig.TrapCooldown);
        public double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0;
        public bool IsStunned => Now < StunUntil.Value;
        public bool IsSlowed => Now < SlowUntil.Value;
        public string PlayerName => SessionManager.Instance != null ? SessionManager.Instance.NameOf(OwnerClientId) : "?";
        public FoodItem NearbyFood { get; private set; }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            if (visual == null) visual = GetComponent<MouseVisual>();
            model = transform.Find("Model");
        }

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            if (IsOwner)
            {
                Local = this;
                var follow = Camera.main != null ? Camera.main.GetComponent<CameraFollow>() : null;
                if (follow != null) { follow.Target = transform; follow.Snap(); }
                cam = Camera.main != null ? Camera.main.transform : null;
            }
            else
            {
                cc.enabled = false;   // remote mice are moved by NetworkTransform only
            }
            spawnPos = transform.position;
            spawnRot = transform.rotation;
            lastPos = transform.position;
            CarriedCount.OnValueChanged += (_, n) => RefreshCarryVisual(n);
            Slot.OnValueChanged += (_, s) => ColourRing(s);
            BuildRing();
            ColourRing(Slot.Value);
            RefreshCarryVisual(CarriedCount.Value);
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }

        // ================================================================ update
        void Update()
        {
            if (!IsSpawned) return;
            if (IsOwner) OwnerUpdate();
            UpdateVisuals();
        }

        void OwnerUpdate()
        {
            bool canAct = InputEnabled.Value && !IsStunned;
            Vector2 input = canAct ? GameInput.Move : Vector2.zero;

            Vector3 fwd = Vector3.forward, right = Vector3.right;
            if (cam != null)
            {
                fwd = Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized;
            }
            Vector3 wish = fwd * input.y + right * input.x;

            float speed = GameConfig.MoveSpeed * SpeedFactor();
            if (Time.time < dashUntil) speed *= GameConfig.DashSpeedMultiplier;
            velocity = Vector3.MoveTowards(velocity, wish * speed, 12f * Time.deltaTime);
            if (Time.time < dashUntil && wish.sqrMagnitude < 0.01f)
                velocity = transform.forward * GameConfig.MoveSpeed * GameConfig.DashSpeedMultiplier;   // dash even from standstill

            if (wish.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(wish), 720f * Time.deltaTime);

            verticalVelocity = cc.isGrounded ? -1f : verticalVelocity - 20f * Time.deltaTime;
            cc.Move((velocity + Vector3.up * verticalVelocity) * Time.deltaTime);
            if (transform.position.y < -5f) Respawn();

            NearbyFood = CarriedCount.Value < GameConfig.MaxCarry ? FoodItem.FindNearest(transform.position, GameConfig.PickupRadius) : null;

            if (!canAct) return;
            if (GameInput.Take && NearbyFood != null) TakeRpc(NearbyFood.NetworkObject);
            if (GameInput.Dash && Time.time >= dashReadyAt)
            {
                dashUntil = Time.time + GameConfig.DashDuration;
                dashReadyAt = Time.time + GameConfig.DashCooldown;
            }
            if (GameInput.Trap && Time.time >= trapReadyAt)
            {
                trapReadyAt = Time.time + GameConfig.TrapCooldown;
                PlaceTrapRpc(transform.position - transform.forward * 0.35f);
            }
        }

        float SpeedFactor()
        {
            float f = Mathf.Max(GameConfig.CarryMinSpeedFactor, 1f - GameConfig.CarrySlowPerItem * CarriedCount.Value);
            if (IsSlowed) f *= GameConfig.TrapSlowFactor;
            return f;
        }

        void Respawn()
        {
            cc.enabled = false;
            transform.SetPositionAndRotation(spawnPos, spawnRot);
            cc.enabled = true;
            velocity = Vector3.zero;
            verticalVelocity = 0f;
        }

        /// <summary>Server -> owner: teleport (match start / restart).</summary>
        [Rpc(SendTo.Owner)]
        public void TeleportRpc(Vector3 pos, Quaternion rot)
        {
            spawnPos = pos;
            spawnRot = rot;
            Respawn();
        }

        // ================================================================ server requests
        [Rpc(SendTo.Server)]
        void TakeRpc(NetworkObjectReference foodRef)
        {
            if (!InputEnabled.Value || IsStunned || CarriedCount.Value >= GameConfig.MaxCarry) return;
            if (!foodRef.TryGet(out var no) || !no.TryGetComponent(out FoodItem food) || !food.IsSpawned) return;
            if (Vector3.Distance(food.transform.position, transform.position) > GameConfig.PickupRadius + 0.6f) return; // lag tolerance
            CarriedCount.Value += 1;
            CarriedValue.Value += food.Value.Value;
            MatchManager.Instance?.OnFoodTaken(food);
        }

        [Rpc(SendTo.Server)]
        void PlaceTrapRpc(Vector3 pos)
        {
            if (!InputEnabled.Value) return;
            MatchManager.Instance?.PlaceTrap(this, pos);
        }

        /// <summary>Server: clear carried food and return (count, value).</summary>
        public (int count, int value) ServerTakeAllFood()
        {
            var r = (CarriedCount.Value, CarriedValue.Value);
            CarriedCount.Value = 0;
            CarriedValue.Value = 0;
            return r;
        }

        // ================================================================ feedback to the owner
        [Rpc(SendTo.Owner)]
        public void NotifyRpc(string key, int arg) => GameHUD.Instance?.Toast(UI.L10n.F(key, arg), key == "delivered");

        // ================================================================ visuals
        void UpdateVisuals()
        {
            Vector3 p = transform.position;
            Vector3 d = p - lastPos; d.y = 0f;
            lastPos = p;
            float inst = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
            shownSpeed = Mathf.Lerp(shownSpeed, inst, 1f - Mathf.Exp(-15f * Time.deltaTime));
            if (visual != null) visual.SetSpeed(shownSpeed < 0.05f ? 0f : shownSpeed);

            if (model != null)
            {
                // squashed while stunned by the cat
                Vector3 target = IsStunned ? new Vector3(1.25f, 0.45f, 1.25f) : Vector3.one;
                model.localScale = Vector3.Lerp(model.localScale, target, 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
        }

        void RefreshCarryVisual(int count)
        {
            if (visual == null) return;
            if (count <= 0)
            {
                if (carried != null) { visual.Drop(); Destroy(carried.gameObject); carried = null; }
                return;
            }
            if (carried == null && carryVisualPrefab != null)
            {
                carried = Instantiate(carryVisualPrefab).transform;
                foreach (var c in carried.GetComponentsInChildren<Collider>()) Destroy(c);
                visual.PickUp(carried);
            }
            if (carried != null) carried.localScale = Vector3.one * Mathf.Min(1.3f + 0.25f * count, 3.2f);
        }

        void BuildRing()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "SlotRing";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.01f, 0.02f);
            go.transform.localScale = new Vector3(0.38f, 0.004f, 0.5f);
            ring = go.GetComponent<Renderer>();
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
        }

        void ColourRing(int slot)
        {
            if (ring == null || ringMaterials == null || ringMaterials.Length == 0) return;
            ring.sharedMaterial = ringMaterials[Mathf.Clamp(slot - 1, 0, ringMaterials.Length - 1)];
        }
    }
}
