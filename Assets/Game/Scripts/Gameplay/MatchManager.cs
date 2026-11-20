using System.Collections.Generic;
using MouseGame.Net;
using Unity.Netcode;
using UnityEngine;

namespace MouseGame
{
    public enum MatchState : byte { Countdown, Playing, Ended }

    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        public readonly NetworkVariable<MatchState> State = new NetworkVariable<MatchState>(MatchState.Countdown);
        public readonly NetworkVariable<double> StartTime = new NetworkVariable<double>();
        public readonly NetworkVariable<double> EndTime = new NetworkVariable<double>();
        public readonly NetworkVariable<Vector3> PawATarget = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<double> PawAAt = new NetworkVariable<double>(-100);
        public readonly NetworkVariable<Vector3> PawBTarget = new NetworkVariable<Vector3>();
        public readonly NetworkVariable<double> PawBAt = new NetworkVariable<double>(-100);

        [Header("Cat visuals")]
        [SerializeField] Transform catBody;
        [SerializeField] Transform paw;
        [SerializeField] Renderer warningDisc;

        public bool IsPlaying => State.Value == MatchState.Playing;
        public double Now => NetworkManager.ServerTime.Time;
        public float TimeLeft => State.Value == MatchState.Countdown ? (float)(StartTime.Value - Now) : Mathf.Max(0f, (float)(EndTime.Value - Now));
        public KitchenMap Map { get; private set; }
        public MapInfo Info { get; private set; }

        readonly Dictionary<ulong, Trap> trapByPlayer = new Dictionary<ulong, Trap>();
        readonly List<(int point, double at)> respawnQueue = new List<(int, double)>();
        readonly HashSet<int> occupiedPoints = new HashSet<int>();
        readonly double[] pawNext = new double[2];
        readonly bool[] pawResolved = { true, true };
        readonly Dictionary<NetMouse, Vector3> lastPos = new Dictionary<NetMouse, Vector3>();
        readonly Dictionary<NetMouse, Vector3> velocity = new Dictionary<NetMouse, Vector3>();
        static readonly RaycastHit[] hitBuf = new RaycastHit[16];
        Transform[] paws;
        Renderer[] discs;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            Map = KitchenMap.Instance != null ? KitchenMap.Instance : FindAnyObjectByType<KitchenMap>();
            Info = SessionManager.Instance != null ? SessionManager.Instance.CurrentMap : GameConfig.Maps[0];
            if (catBody != null && Map != null && Map.CatSpot != null)
                catBody.SetPositionAndRotation(Map.CatSpot.transform.position, Map.CatSpot.transform.rotation);
            if (paw != null && warningDisc != null)
            {
                var paw2 = Instantiate(paw.gameObject, paw.parent).transform;
                paw2.name = "Paw2";
                var disc2 = Instantiate(warningDisc.gameObject, warningDisc.transform.parent).GetComponent<Renderer>();
                paws = new[] { paw, paw2 };
                discs = new[] { warningDisc, disc2 };
                foreach (var d in discs) d.enabled = false;
                foreach (var pw in paws) pw.gameObject.SetActive(false);
            }

            if (IsServer) ServerSetup();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }

        void ServerSetup()
        {
            var session = SessionManager.Instance;
            var cm = ConnectionManager.Instance;
            StartTime.Value = Now + GameConfig.CountdownSeconds;
            EndTime.Value = StartTime.Value + Info.MatchSeconds;
            State.Value = MatchState.Countdown;
            pawNext[0] = StartTime.Value + Info.CatInterval * 0.6f;
            pawNext[1] = StartTime.Value + Info.CatInterval * 1.1f;

            for (int i = 0; i < session.Players.Count; i++)
            {
                var p = session.Players[i];
                if (!NetworkManager.ConnectedClients.ContainsKey(p.ClientId)) continue;
                var sp = Map.GetSpawn(p.Slot).transform;
                var go = Instantiate(cm.playerPrefab, sp.position, sp.rotation);
                var mouse = go.GetComponent<NetMouse>();
                go.GetComponent<NetworkObject>().SpawnAsPlayerObject(p.ClientId, true);
                mouse.Slot.Value = p.Slot;
            }

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
            PawAAt.Value = -100;
            PawBAt.Value = -100;
            SetInput(false);
        }

        void ServerDeliveries()
        {
            var session = SessionManager.Instance;
            foreach (var m in NetMouse.All)
            {
                if (m.CarriedCount.Value <= 0) continue;
                var hole = Map.GetHole(m.Slot.Value);
                if (hole == null) continue;
                Vector3 target = hole.transform.position + hole.transform.forward * 0.3f;
                Vector3 d = m.transform.position - target;
                if (Mathf.Abs(d.y) > 0.6f) continue;
                d.y = 0f;
                if (d.magnitude > GameConfig.DeliverRadius) continue;

                var (count, value) = m.ServerTakeAllFood();
                session.Modify(m.OwnerClientId, p => { p.Score += value; p.Delivered += count; return p; });
                m.NotifyRpc("delivered", value);
            }
        }

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

        public void PlaceTrap(NetMouse owner, Vector3 pos)
        {
            if (!IsPlaying) return;
            if (trapByPlayer.TryGetValue(owner.OwnerClientId, out var old) && old != null && old.IsSpawned)
                old.NetworkObject.Despawn(true);
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

        double PawAt(int i) => i == 0 ? PawAAt.Value : PawBAt.Value;
        Vector3 PawTarget(int i) => i == 0 ? PawATarget.Value : PawBTarget.Value;

        void SetPaw(int i, Vector3 target, double at)
        {
            if (i == 0) { PawATarget.Value = target; PawAAt.Value = at; }
            else { PawBTarget.Value = target; PawBAt.Value = at; }
            pawResolved[i] = false;
        }

        void TrackVelocities()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.001f);
            foreach (var m in NetMouse.All)
            {
                Vector3 p = m.transform.position;
                Vector3 v = Vector3.zero;
                if (lastPos.TryGetValue(m, out var last)) v = (p - last) / dt;
                v.y = 0f;
                if (v.sqrMagnitude > 64f) v = Vector3.zero;
                velocity.TryGetValue(m, out var old);
                velocity[m] = Vector3.Lerp(old, v, 0.25f);
                lastPos[m] = p;
            }
        }

        void ServerCat()
        {
            TrackVelocities();
            float progress = Mathf.Clamp01((float)((Now - StartTime.Value) / Info.MatchSeconds));
            float intervalMul = Mathf.Lerp(1f, GameConfig.CatEndIntervalFactor, progress);
            float warning = Mathf.Max(GameConfig.CatMinWarning, Info.CatWarning * Mathf.Lerp(1f, GameConfig.CatEndWarningFactor, progress));

            for (int i = 0; i < 2; i++)
            {
                if (pawResolved[i] && Now >= pawNext[i] - warning && Now > PawAt(i) + 1.0)
                    SchedulePaw(i, warning);

                if (!pawResolved[i] && Now >= PawAt(i))
                {
                    pawResolved[i] = true;
                    ResolveStrike(PawTarget(i));
                    pawNext[i] = Now + Mathf.Max(1.2f + warning, Info.CatInterval * intervalMul * Random.Range(0.75f, 1.25f));
                }
            }
        }

        void ResolveStrike(Vector3 c)
        {
            float r = GameConfig.CatPawRadius;
            foreach (var m in NetMouse.All)
            {
                if (m.IsStunned || Now < m.StunUntil.Value + GameConfig.CatGraceSeconds) continue;
                Vector3 d = m.transform.position - c;
                if (Mathf.Abs(d.y) > 0.8f) continue;
                d.y = 0f;
                if (d.sqrMagnitude > r * r || IsCovered(m.transform.position)) continue;
                HitByCat(m, c.y);
            }
        }

        void SchedulePaw(int i, float warning)
        {
            double at = Now + warning;
            var target = Random.value < GameConfig.CatAimChance ? PickTarget(i) : null;
            if (target == null)
            {
                SetPaw(i, RandomStrikePoint(), at);
                return;
            }

            float lead = Random.Range(0.2f, 1.0f);
            SetPaw(i, Predict(target, lead * warning), at);

            int j = 1 - i;
            if (pawResolved[j] && Now > PawAt(j) + 1.0 && Random.value < Info.ComboChance)
            {
                float lead2 = lead < 0.6f ? lead + 0.9f : 0f;
                SetPaw(j, Predict(target, lead2 * warning + 0.3f), at + 0.3);
            }
        }

        Vector3 Predict(NetMouse m, float seconds)
        {
            velocity.TryGetValue(m, out var v);
            Vector3 p = m.transform.position + v * seconds;
            Vector2 j = Random.insideUnitCircle * 0.3f;
            p.x += j.x; p.z += j.y;
            return OnSurface(p, m.transform.position);
        }

        Vector3 RandomStrikePoint()
        {
            Vector3 p;
            if (Map.FoodPoints.Length > 0 && Random.value < 0.6f)
                p = Map.FoodPoints[Random.Range(0, Map.FoodPoints.Length)].transform.position;
            else if (Map.CatZones.Length > 0 && Random.value < 0.5f)
                p = Map.CatZones[Random.Range(0, Map.CatZones.Length)].transform.position;
            else
                p = new Vector3(Random.Range(-9f, 9f), 0f, Random.Range(-7.5f, 7.5f));
            Vector2 j = Random.insideUnitCircle * 0.6f;
            p.x += j.x; p.z += j.y;
            return OnSurface(p, p);
        }

        Vector3 OnSurface(Vector3 p, Vector3 fallback)
        {
            p.x = Mathf.Clamp(p.x, -10.4f, 10.4f);
            p.z = Mathf.Clamp(p.z, -8.6f, 8.6f);
            if (Surface(p, out float y) && y < 4f) { p.y = y; return p; }
            return fallback;
        }

        NetMouse PickTarget(int paw)
        {
            NetMouse best = null;
            float total = 0f;
            foreach (var m in NetMouse.All)
            {
                if (m.IsStunned || Now < m.StunUntil.Value + GameConfig.CatGraceSeconds) continue;
                Vector3 pos = m.transform.position;
                if (NearOwnHole(m) || IsCovered(pos)) continue;
                float w = 1f + m.CarriedValue.Value * 0.5f;
                if ((paw == 0 && pos.x <= 1f) || (paw == 1 && pos.x >= -1f)) w *= 2f;
                total += w;
                if (Random.value * total < w) best = m;
            }
            return best;
        }

        bool NearOwnHole(NetMouse m)
        {
            var hole = Map.GetHole(m.Slot.Value);
            if (hole == null) return false;
            Vector3 d = m.transform.position - (hole.transform.position + hole.transform.forward * 0.3f);
            d.y = 0f;
            return d.sqrMagnitude < GameConfig.CatSafeHoleRadius * GameConfig.CatSafeHoleRadius;
        }

        bool IsCovered(Vector3 pos) => Surface(pos, out float y) && y > pos.y + 0.4f;

        static bool Surface(Vector3 p, out float y)
        {
            y = 0f;
            int n = Physics.RaycastNonAlloc(new Vector3(p.x, 12f, p.z), Vector3.down, hitBuf, 20f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool ok = false;
            for (int k = 0; k < n; k++)
            {
                var c = hitBuf[k].collider;
                if (c.GetComponentInParent<NetMouse>() != null || c.GetComponentInParent<FoodItem>() != null
                    || c.GetComponentInParent<Trap>() != null || c.GetComponentInParent<MatchManager>() != null) continue;
                if (hitBuf[k].distance < best) { best = hitBuf[k].distance; y = hitBuf[k].point.y; ok = true; }
            }
            return ok;
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

        void UpdateCatVisuals()
        {
            if (paws == null) return;
            for (int i = 0; i < 2; i++) UpdatePaw(i);
        }

        void UpdatePaw(int i)
        {
            var pw = paws[i];
            var disc = discs[i];
            float t = (float)(PawAt(i) - Now);
            if (State.Value == MatchState.Ended || t > 3f || t < -1.0f)
            {
                disc.enabled = false;
                pw.gameObject.SetActive(false);
                return;
            }
            Vector3 c = PawTarget(i);
            float r = GameConfig.CatPawRadius;

            disc.enabled = t > 0f && Mathf.Repeat(Time.time * (t < 0.4f ? 12f : 6f), 1f) < 0.65f;
            disc.transform.position = c + Vector3.up * 0.02f;
            disc.transform.localScale = new Vector3(r * 2f, 0.01f, r * 2f);

            float h;
            if (t > 0.15f) h = Mathf.Lerp(4.2f, 5.6f, Mathf.Clamp01((t - 0.15f) / 0.8f));
            else if (t > 0f) h = Mathf.Lerp(0f, 4.2f, t / 0.15f);
            else h = Mathf.Lerp(0f, 5.6f, Mathf.Clamp01((-t - 0.25f) / 0.5f));
            pw.gameObject.SetActive(true);
            Vector3 toCat = catBody != null ? catBody.position - c : Vector3.forward;
            toCat.y = 0f;
            Vector3 dir = toCat.sqrMagnitude > 0.01f ? toCat.normalized : Vector3.forward;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.01f && Vector3.Dot(dir, fwd.normalized) < 0.3f) dir = fwd.normalized;
            }
            pw.position = c + Vector3.up * h + dir * (Mathf.Clamp01(h / 4.2f) * 1.6f);
            pw.rotation = Quaternion.LookRotation(dir);
        }
    }

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
