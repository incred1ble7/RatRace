using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouseGame.Net
{
    public enum SessionPhase : byte { Lobby, Loading, InGame }

    /// <summary>Everything the lobby and the results screen need to know about one player.</summary>
    public struct PlayerInfo : INetworkSerializable, IEquatable<PlayerInfo>
    {
        public ulong ClientId;
        public FixedString32Bytes Name;
        public int Slot;          // 1..4, also the mouse hole / colour
        public bool Ready;
        // match statistics (reset when a match starts)
        public int Score;
        public int Delivered;
        public int CatHits;
        public int Lost;
        public int TrapsHit;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref ClientId);
            s.SerializeValue(ref Name);
            s.SerializeValue(ref Slot);
            s.SerializeValue(ref Ready);
            s.SerializeValue(ref Score);
            s.SerializeValue(ref Delivered);
            s.SerializeValue(ref CatHits);
            s.SerializeValue(ref Lost);
            s.SerializeValue(ref TrapsHit);
        }

        public bool Equals(PlayerInfo o) =>
            ClientId == o.ClientId && Name.Equals(o.Name) && Slot == o.Slot && Ready == o.Ready &&
            Score == o.Score && Delivered == o.Delivered && CatHits == o.CatHits && Lost == o.Lost && TrapsHit == o.TrapsHit;
    }

    /// <summary>
    /// Persistent (DontDestroyOnLoad) network object created by the host.
    /// Owns the lobby state, ready system, map choice, chat and moves everybody between scenes.
    /// </summary>
    public class SessionManager : NetworkBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public NetworkList<PlayerInfo> Players;
        public readonly NetworkVariable<int> MapIndex = new NetworkVariable<int>(0);
        public readonly NetworkVariable<SessionPhase> PhaseVar = new NetworkVariable<SessionPhase>(SessionPhase.Lobby);
        public readonly NetworkVariable<FixedString64Bytes> LobbyName = new NetworkVariable<FixedString64Bytes>();

        public SessionPhase Phase => PhaseVar.Value;
        public int PlayerCount => Players.Count;
        public MapInfo CurrentMap => GameConfig.Maps[Mathf.Clamp(MapIndex.Value, 0, GameConfig.Maps.Length - 1)];

        public static event Action Changed;                         // any lobby data changed
        public static event Action<string, string, Color> ChatReceived;  // name, text, colour
        public static readonly List<(string name, string text, Color color)> ChatLog = new List<(string, string, Color)>();

        void Awake()
        {
            Players = new NetworkList<PlayerInfo>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ChatLog.Clear();
            Players.OnListChanged += _ => Changed?.Invoke();
            MapIndex.OnValueChanged += (_, __) => Changed?.Invoke();
            PhaseVar.OnValueChanged += (_, __) => Changed?.Invoke();

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
                NetworkManager.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
                foreach (var id in NetworkManager.ConnectedClientsIds) OnClientConnected(id);
            }
            Changed?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
                if (NetworkManager.SceneManager != null) NetworkManager.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
            }
            if (Instance == this) Instance = null;
        }

        public void InitHost(string lobbyName) => LobbyName.Value = new FixedString64Bytes(lobbyName.Length > 30 ? lobbyName.Substring(0, 30) : lobbyName);

        // ============================== queries ==============================
        public bool TryGet(ulong clientId, out PlayerInfo info)
        {
            for (int i = 0; i < Players.Count; i++)
                if (Players[i].ClientId == clientId) { info = Players[i]; return true; }
            info = default;
            return false;
        }

        public int IndexOf(ulong clientId)
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].ClientId == clientId) return i;
            return -1;
        }

        public bool AllReady
        {
            get
            {
                if (Players.Count == 0) return false;
                for (int i = 0; i < Players.Count; i++) if (!Players[i].Ready) return false;
                return true;
            }
        }

        public string NameOf(ulong clientId) => TryGet(clientId, out var p) ? p.Name.ToString() : "?";

        // ============================== server: connections ==============================
        void OnClientConnected(ulong clientId)
        {
            if (IndexOf(clientId) >= 0) return;
            string name = ConnectionManager.PendingNames.TryGetValue(clientId, out var n) ? n : "Mouse";
            ConnectionManager.PendingNames.Remove(clientId);
            name = UniqueName(name);
            Players.Add(new PlayerInfo { ClientId = clientId, Name = new FixedString32Bytes(name), Slot = FreeSlot() });
            SystemChat("joined", name);
            UpdateDiscovery();
        }

        void OnClientDisconnected(ulong clientId)
        {
            int i = IndexOf(clientId);
            if (i < 0) return;
            string name = Players[i].Name.ToString();
            Players.RemoveAt(i);
            SystemChat("left", name);
            UpdateDiscovery();
        }

        string UniqueName(string name)
        {
            string result = name;
            int k = 2;
            while (true)
            {
                bool clash = false;
                for (int i = 0; i < Players.Count; i++) if (Players[i].Name.ToString() == result) clash = true;
                if (!clash) return result;
                result = name + k++;
            }
        }

        int FreeSlot()
        {
            for (int s = 1; s <= GameConfig.MaxPlayers; s++)
            {
                bool used = false;
                for (int i = 0; i < Players.Count; i++) if (Players[i].Slot == s) used = true;
                if (!used) return s;
            }
            return 1;
        }

        void UpdateDiscovery() => LanDiscovery.Instance?.UpdateHostInfo(LobbyName.Value.ToString(), Players.Count, Phase == SessionPhase.Lobby && Players.Count < GameConfig.MaxPlayers);

        // ============================== lobby actions ==============================
        public void SetReady(bool ready) => SetReadyRpc(ready);

        [Rpc(SendTo.Server)]
        void SetReadyRpc(bool ready, RpcParams p = default)
        {
            int i = IndexOf(p.Receive.SenderClientId);
            if (i < 0 || Phase != SessionPhase.Lobby) return;
            var info = Players[i];
            info.Ready = ready;
            Players[i] = info;
        }

        public void ChangeMap(int delta)
        {
            if (!IsServer || Phase != SessionPhase.Lobby) return;
            int n = GameConfig.Maps.Length;
            MapIndex.Value = ((MapIndex.Value + delta) % n + n) % n;
        }

        /// <summary>Host only: everyone ready -> load the chosen map for all players.</summary>
        public bool StartMatch()
        {
            if (!IsServer || Phase != SessionPhase.Lobby || !AllReady) return false;
            LoadMap(MapIndex.Value);
            return true;
        }

        void LoadMap(int index)
        {
            MapIndex.Value = index;
            ResetStats();
            PhaseVar.Value = SessionPhase.Loading;
            UpdateDiscovery();
            NetworkManager.SceneManager.LoadScene(GameConfig.Maps[index].Scene, LoadSceneMode.Single);
        }

        // ============================== after a match ==============================
        public void RestartMatch() { if (IsServer && Phase == SessionPhase.InGame) LoadMap(MapIndex.Value); }

        public bool HasNextLevel => MapIndex.Value + 1 < GameConfig.Maps.Length;

        public void NextLevel() { if (IsServer && Phase == SessionPhase.InGame && HasNextLevel) LoadMap(MapIndex.Value + 1); }

        public void BackToLobby()
        {
            if (!IsServer) return;
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Ready = false;
                Players[i] = p;
            }
            PhaseVar.Value = SessionPhase.Loading;
            NetworkManager.SceneManager.LoadScene(GameConfig.LobbyScene, LoadSceneMode.Single);
        }

        void OnSceneLoaded(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == GameConfig.LobbyScene) PhaseVar.Value = SessionPhase.Lobby;
            else if (GameConfig.MapIndexOfScene(sceneName) >= 0)
            {
                PhaseVar.Value = SessionPhase.InGame;
                MatchSpawner.SpawnMatch(this);   // server spawns the match manager + players
            }
            UpdateDiscovery();
        }

        void ResetStats()
        {
            for (int i = 0; i < Players.Count; i++)
            {
                var p = Players[i];
                p.Score = p.Delivered = p.CatHits = p.Lost = p.TrapsHit = 0;
                Players[i] = p;
            }
        }

        /// <summary>Server: modify one player's stats.</summary>
        public void Modify(ulong clientId, Func<PlayerInfo, PlayerInfo> change)
        {
            int i = IndexOf(clientId);
            if (i >= 0) Players[i] = change(Players[i]);
        }

        // ============================== chat ==============================
        public void SendChat(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            text = text.Trim();
            if (text.Length > 120) text = text.Substring(0, 120);
            SendChatRpc(text);
        }

        [Rpc(SendTo.Server)]
        void SendChatRpc(string text, RpcParams p = default)
        {
            if (!TryGet(p.Receive.SenderClientId, out var info)) return;
            ChatRpc(info.Name.ToString(), text, info.Slot);
        }

        /// <summary>System message (join / leave): key is localised on each client.</summary>
        void SystemChat(string key, string arg) => SystemChatRpc(key, arg);

        [Rpc(SendTo.ClientsAndHost)]
        void ChatRpc(string name, string text, int slot) => AddChat(name, text, GameConfig.SlotColor(slot));

        [Rpc(SendTo.ClientsAndHost)]
        void SystemChatRpc(string key, string arg) => AddChat("", UI.L10n.F(key, arg), new Color(0.7f, 0.7f, 0.7f));

        static void AddChat(string name, string text, Color c)
        {
            ChatLog.Add((name, text, c));
            if (ChatLog.Count > 40) ChatLog.RemoveAt(0);
            ChatReceived?.Invoke(name, text, c);
        }
    }
}
