using System.Collections.Generic;
using MouseGame.Net;
using Unity.Netcode;
using UnityEngine;

namespace MouseGame
{
    public enum MatchState : byte { Countdown, Playing, Ended }

    /// <summary>
    /// One match on a map. Spawned by the server after every client loaded the level.
    /// Server: spawns mice, food, runs the timer, scoring, the cat and traps.
    /// Clients: read the synced state; the cat is animated locally from synced timings (cheap on the network).
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        public readonly NetworkVariable<MatchState> State = new NetworkVariable<MatchState>(MatchState.Countdown);
        public readonly NetworkVariable<double> StartTime = new NetworkVariable<double>();
        public readonly NetworkVariable<double> EndTime = new NetworkVariable<double>();
        // cat: which zone is targeted and when the paw lands (server time)
        public readonly NetworkVariable<int> CatZone = new NetworkVariable<int>(-1);
        public readonly NetworkVariable<double> CatStrikeAt = new NetworkVariable<double>();

        [Header("Cat visuals")]
        [SerializeField] Transform catBody;
        [SerializeField] Transform paw;
        [SerializeField] Renderer warningDisc;

        public bool IsPlaying => State.Value == MatchState.Playing;
        public double Now => NetworkManager.ServerTime.Time;
        public float TimeLeft => State.Value == MatchState.Countdown ? (float)(StartTime.Value - Now) : Mathf.Max(0f, (float)(EndTime.Value - Now));
        public KitchenMap Map { get; private set; }
        public MapInfo Info { get; private set; }

        // server state
        readonly Dictionary<ulong, Trap> trapByPlayer = new Dictionary<ulong, Trap>();
        readonly List<(int point, double at)> respawnQueue = new List<(int, double)>();
        readonly HashSet<int> occupiedPoints = new HashSet<int>();
        double nextCatAt;
        bool strikeResolved = true;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Map = KitchenMap.Instance != null ? KitchenMap.Instance : FindAnyObjectByType<KitchenMap>();
            Info = SessionManager.Instance != null ? SessionManager.Instance.CurrentMap : GameConfig.Maps[0];
            if (catBody != null && Map != null && Map.CatSpot != null)
                catBody.SetPositionAndRotation(Map.CatSpot.transform.position, Map.CatSpot.transform.rotation);
            if (warningDisc != null) warningDisc.enabled = false;
            if (paw != null) paw.gameObject.SetActive(false);

            if (IsServer) ServerSetup();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        // ================================================================ server
        void ServerSetup()
        {
            var session = SessionManager.Instance;
            var cm = ConnectionManager.Instance;
            StartTime.Value = Now + GameConfig.CountdownSeconds;
            EndTime.Value = StartTime.Value + Info.MatchSeconds;
            State.Value = MatchState.Countdown;
            nextCatAt = StartTime.Value + Info.CatInterval;

            // players
            for (int i = 0; i < session.Players.Count; i++)
            {
                var p = session.Players[i];
                if (!NetworkManager.ConnectedClients.ContainsKey(p.ClientId)) continue;
                var sp = Map.GetSpawn(p.Slot).transform;
                var go = Instantiate(cm.playerPrefab, sp.position, sp.rotation);
                var mouse = go.GetComponent<NetMouse>();
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId, true);
                mouse.Slot.Value = p.Slot;   // network values are written after spawn
            }

            // food on every spawn point
            for (int i = 0; i < Map.FoodPoints.Length; i++) SpawnFoodAtPoint(i);
        }

        void Update()
        {
            UpdateCatVisuals();
            if (!IsServer || !IsSpawned) return;

            switch (State.Value)
            {
                case MatchState.Countdown:
                    if (Now >= StartTime.Value)
                    {
                        State.Value = MatchState.Playing;
                        SetInput(true);
                    }
                    break;

                case MatchState.Playing:
                    ServerDeliveries();
                    ServerRespawnFood();
                    ServerCat();
                    if (Now >= EndTime.Value) EndMatch();
                    break;
            }
        }

        void SetInput(bool on)
        {
            foreach (var m in NetMouse.All) m.InputEnabled.Value = on;
        }

        void EndMatch()
        {
            State.Value = MatchState.Ended;
            CatZone.Value = -1;
            SetInput(false);
        }

        // ---- delivery: carried food counts only inside the player's own hole
        void ServerDeliveries()
        {
            var session = SessionManager.Instance;
            foreach (var m in NetMouse.All)
            {
                if (m.CarriedCount.Value <= 0) continue;
                var hole = Map.GetHole(m.Slot.Value);
                if (hole == null) continue;
                Vector3 target = hole.transform.position + hole.transform.forward * 0.4f;
                Vector3 d = m.transform.position - target;
                if (Mathf.Abs(d.y) > 0.6f) continue;
                d.y = 0f;
                if (d.magnitude > GameConfig.DeliverRadius) continue;

                var (count, value) = m.ServerTakeAllFood();
                session.Modify(m.OwnerClientId, p => { p.Score += value; p.Delivered += count; return p; });
                m.NotifyRpc("delivered", value);
            }
        }

        // ---- food
        void SpawnFoodAtPoint(int index)
        {
            var fp = Map.FoodPoints[index];
            var food = SpawnFood(fp.transform.position, fp.tier, FoodItem.ValueOfTier(fp.tier));
            food.SpawnPointIndex = index;
            occupiedPoints.Add(index);
        }

        FoodItem SpawnFood(Vector3 pos, int tier, int value)
        {
            var go = Instantiate(ConnectionManager.Instance.foodPrefab, pos, Quaternion.identity);
            var f = go.GetComponent<FoodItem>();
            go.GetComponent<NetworkObject>().Spawn(true);
            f.Tier.Value = tier;
            f.Value.Value = value;
            return f;
        }

        public void OnFoodTaken(FoodItem food)
        {
            if (food.SpawnPointIndex >= 0)
            {
                occupiedPoints.Remove(food.SpawnPointIndex);
                respawnQueue.Add((food.SpawnPointIndex, Now + Random.Range(6f, 11f)));
            }
            food.NetworkObject.Despawn(true);
        }

        void ServerRespawnFood()
        {
            for (int i = respawnQueue.Count - 1; i >= 0; i--)
            {
                if (Now < respawnQueue[i].at) continue;
                int point = respawnQueue[i].point;
                respawnQueue.RemoveAt(i);
                if (!occupiedPoints.Contains(point)) SpawnFoodAtPoint(point);
            }
        }

        // ---- traps
        public void PlaceTrap(NetMouse owner, Vector3 pos)
        {
            if (!IsPlaying) return;
            if (trapByPlayer.TryGetValue(owner.OwnerClientId, out var old) && old != null && old.IsSpawned)
                old.NetworkObject.Despawn(true);      // only one trap per player
            var go = Instantiate(ConnectionManager.Instance.trapPrefab, pos, Quaternion.Euler(0f, owner.transform.eulerAngles.y, 0f));
            var t = go.GetComponent<Trap>();
            go.GetComponent<NetworkObject>().Spawn(true);
            t.OwnerPlayer.Value = owner.OwnerClientId;
            t.OwnerSlot.Value = owner.Slot.Value;
            t.ArmedAt.Value = Now + GameConfig.TrapArmDelay;
            trapByPlayer[owner.OwnerClientId] = t;
        }

        public void OnTrapTriggered(Trap trap, NetMouse victim)
        {
            victim.SlowUntil.Value = Now + GameConfig.TrapSlowSeconds;
            victim.NotifyRpc("trapped", 0);
            SessionManager.Instance.Modify(trap.OwnerPlayer.Value, p => { p.TrapsHit += 1; return p; });
            trapByPlayer.Remove(trap.OwnerPlayer.Value);
            trap.NetworkObject.Despawn(true);
        }

        // ---- the cat: warn -> strike on one marked zone; mice inside are stunned and drop half their food
        void ServerCat()
        {
            if (Map.CatZones.Length == 0) return;
            if (strikeResolved && Now >= nextCatAt - Info.CatWarning)
            {
                CatZone.Value = Random.Range(0, Map.CatZones.Length);
                CatStrikeAt.Value = nextCatAt;
                strikeResolved = false;
            }
            if (!strikeResolved && Now >= CatStrikeAt.Value)
            {
                strikeResolved = true;
                var zone = Map.CatZones[CatZone.Value];
                foreach (var m in NetMouse.All)
                {
                    if (m.IsStunned || !zone.Contains(m.transform.position)) continue;
                    HitByCat(m, zone.transform.position.y);
                }
                nextCatAt = Now + Info.CatInterval * Random.Range(0.8f, 1.2f);
            }
            if (strikeResolved && CatZone.Value >= 0 && Now > CatStrikeAt.Value + 0.8) CatZone.Value = -1;
        }

        void HitByCat(NetMouse m, float groundY)
        {
            m.StunUntil.Value = Now + GameConfig.CatStunSeconds;
            int count = m.CarriedCount.Value, value = m.CarriedValue.Value;
            int lose = Mathf.CeilToInt(count / 2f);
            if (lose > 0)
            {
                int lostValue = Mathf.Max(lose, Mathf.RoundToInt(value * (float)lose / count));
                m.CarriedCount.Value = count - lose;
                m.CarriedValue.Value = Mathf.Max(0, value - lostValue);
                // scatter the dropped food around the mouse (other players can grab it)
                for (int i = 0; i < lose; i++)
                {
                    int v = lostValue / lose + (i < lostValue % lose ? 1 : 0);
                    float a = (i / (float)lose) * Mathf.PI * 2f + Random.value;
                    Vector3 p = m.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Random.Range(0.6f, 1.0f);
                    p.y = groundY;
                    SpawnFood(p, v >= 4 ? 3 : v >= 2 ? 2 : 1, Mathf.Max(1, v));
                }
            }
            SessionManager.Instance.Modify(m.OwnerClientId, p => { p.CatHits += 1; p.Lost += lose; return p; });
            m.NotifyRpc("cat_hit", lose);
        }

        // ================================================================ cat visuals (every client)
        void UpdateCatVisuals()
        {
            if (Map == null || paw == null) return;
            int zi = CatZone.Value;
            if (zi < 0 || zi >= Map.CatZones.Length)
            {
                if (warningDisc != null) warningDisc.enabled = false;
                paw.gameObject.SetActive(false);
                return;
            }
            var zone = Map.CatZones[zi];
            Vector3 c = zone.transform.position;
            float t = (float)(CatStrikeAt.Value - Now);          // >0 before the strike

            if (warningDisc != null)
            {
                warningDisc.enabled = t > 0f && Mathf.Repeat(Time.time * (t < 0.6f ? 10f : 5f), 1f) < 0.6f;
                warningDisc.transform.position = c + Vector3.up * 0.02f;
                warningDisc.transform.localScale = new Vector3(zone.radius * 2f, 0.01f, zone.radius * 2f);
            }

            // paw hovers high, slams down at strike time, then lifts
            float h;
            if (t > 0.12f) h = Mathf.Lerp(2.2f, 3.4f, Mathf.Clamp01((t - 0.12f) / 1.0f));
            else if (t > 0f) h = Mathf.Lerp(0f, 2.2f, t / 0.12f);
            else h = Mathf.Lerp(0f, 3.5f, Mathf.Clamp01((-t - 0.25f) / 0.5f));
            paw.gameObject.SetActive(true);
            paw.position = c + Vector3.up * h;
        }
    }

    /// <summary>Server helper called when a level finished loading for everybody.</summary>
    public static class MatchSpawner
    {
        public static void SpawnMatch(SessionManager session)
        {
            var cm = ConnectionManager.Instance;
            if (cm == null || cm.matchPrefab == null) { Debug.LogError("[Match] match prefab missing"); return; }
            var go = Object.Instantiate(cm.matchPrefab);
            go.GetComponent<NetworkObject>().Spawn(true);
        }
    }
}
