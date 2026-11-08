using MouseGame.Net;
using Unity.Netcode;
using UnityEngine;

namespace MouseGame
{
    /// <summary>
    /// Mousetrap thrown by a player. One per player (placing a new one removes the old).
    /// Server: the first *other* mouse that steps on it is slowed, then the trap disappears.
    /// </summary>
    public class Trap : NetworkBehaviour
    {
        public readonly NetworkVariable<ulong> OwnerPlayer = new NetworkVariable<ulong>();
        public readonly NetworkVariable<int> OwnerSlot = new NetworkVariable<int>(1);
        public readonly NetworkVariable<double> ArmedAt = new NetworkVariable<double>(double.MaxValue);

        [SerializeField] Transform bar;          // snapping bar (visual)
        [SerializeField] Renderer tint;          // coloured marker
        [SerializeField] Material[] slotMaterials;

        bool fired;

        public override void OnNetworkSpawn()
        {
            OwnerSlot.OnValueChanged += (_, s) => Colour(s);
            Colour(OwnerSlot.Value);
        }

        void Colour(int slot)
        {
            if (tint != null && slotMaterials != null && slotMaterials.Length > 0)
                tint.sharedMaterial = slotMaterials[Mathf.Clamp(slot - 1, 0, slotMaterials.Length - 1)];
        }

        void Update()
        {
            if (bar != null)
            {
                // armed: bar open (back); not yet armed: bar wobbles slightly
                bool armed = NetworkManager != null && NetworkManager.ServerTime.Time >= ArmedAt.Value;
                float ang = armed ? -70f : -70f + Mathf.Sin(Time.time * 20f) * 6f;
                bar.localRotation = Quaternion.Euler(ang, 0f, 0f);
            }

            if (!IsServer || fired || !IsSpawned) return;
            if (NetworkManager.ServerTime.Time < ArmedAt.Value) return;
            var mm = MatchManager.Instance;
            if (mm == null || !mm.IsPlaying) return;

            foreach (var m in NetMouse.All)
            {
                if (m.OwnerClientId == OwnerPlayer.Value) continue;
                Vector3 d = m.transform.position - transform.position;
                if (Mathf.Abs(d.y) > 0.5f) continue;
                d.y = 0f;
                if (d.sqrMagnitude > GameConfig.TrapRadius * GameConfig.TrapRadius) continue;
                fired = true;
                mm.OnTrapTriggered(this, m);
                break;
            }
        }
    }
}
