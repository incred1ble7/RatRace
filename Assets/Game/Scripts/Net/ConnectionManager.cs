using System;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouseGame.Net
{
    /// <summary>
    /// Lives on the NetworkManager object (Boot scene, never destroyed).
    /// Host / Join / Leave, connection approval (name, max players, no joining mid-match),
    /// registers network prefabs and spawns the persistent SessionManager.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        [Header("Network prefabs")]
        [SerializeField] GameObject sessionPrefab;
        public GameObject matchPrefab;
        public GameObject playerPrefab;
        public GameObject foodPrefab;
        public GameObject trapPrefab;

        NetworkManager nm;
        public static string PlayerName
        {
            get
            {
                if (!PlayerPrefs.HasKey("player_name")) PlayerPrefs.SetString("player_name", "Mouse" + UnityEngine.Random.Range(10, 99));
                return PlayerPrefs.GetString("player_name");
            }
            set => PlayerPrefs.SetString("player_name", string.IsNullOrWhiteSpace(value) ? "Mouse" : value.Trim());
        }
        public static string LobbyName
        {
            get => PlayerPrefs.GetString("lobby_name", "");
            set => PlayerPrefs.SetString("lobby_name", value ?? "");
        }

        /// <summary>Message to show in the main menu after returning there (disconnect reason etc.).</summary>
        public static string PendingMenuMessage;

        public event Action<string> ConnectFailed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            nm = GetComponent<NetworkManager>();
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            foreach (var p in new[] { sessionPrefab, matchPrefab, playerPrefab, foodPrefab, trapPrefab })
                if (p != null) nm.AddNetworkPrefab(p);

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = Approve;
            nm.OnClientDisconnectCallback += OnClientDisconnect;
            nm.OnServerStarted += OnServerStarted;
            if (GetComponent<LanDiscovery>() == null) gameObject.AddComponent<LanDiscovery>();
            AutoTestClient.InstallIfRequested(gameObject);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            if (nm != null)
            {
                nm.OnClientDisconnectCallback -= OnClientDisconnect;
                nm.OnServerStarted -= OnServerStarted;
            }
            Instance = null;
        }

        UnityTransport Transport => nm.GetComponent<UnityTransport>();

        // ---------------------------------------------------------------
        public bool Host()
        {
            Transport.SetConnectionData("0.0.0.0", GameConfig.GamePort, "0.0.0.0");
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(PlayerName);
            if (!nm.StartHost())
            {
                ConnectFailed?.Invoke("conn_failed");
                return false;
            }
            return true;
        }

        public bool Join(string address, ushort port = GameConfig.GamePort)
        {
            if (string.IsNullOrWhiteSpace(address)) address = "127.0.0.1";
            Transport.SetConnectionData(address.Trim(), port);
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(PlayerName);
            if (!nm.StartClient())
            {
                ConnectFailed?.Invoke("conn_failed");
                return false;
            }
            return true;
        }

        /// <summary>Leave the session (host closes it for everyone) and go to the main menu.</summary>
        public void Leave(string reasonKey = null)
        {
            // already in the menu (failed connect): the menu shows the reason itself
            PendingMenuMessage = SceneManager.GetActiveScene().name == GameConfig.MenuScene ? null : reasonKey;
            LanDiscovery.Instance?.StopHosting();
            if (nm.IsListening) nm.Shutdown();
            if (SceneManager.GetActiveScene().name != GameConfig.MenuScene)
                SceneManager.LoadScene(GameConfig.MenuScene);
        }

        // ---------------------------------------------------------------
        void OnServerStarted()
        {
            var session = Instantiate(sessionPrefab);
            session.GetComponent<NetworkObject>().Spawn(false);
            string lobby = string.IsNullOrWhiteSpace(LobbyName) ? PlayerName + "'s game" : LobbyName;
            session.GetComponent<SessionManager>().InitHost(lobby);
            LanDiscovery.Instance?.StartHosting(lobby);
            nm.SceneManager.LoadScene(GameConfig.LobbyScene, LoadSceneMode.Single);
        }

        void Approve(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res)
        {
            res.CreatePlayerObject = false;
            var session = SessionManager.Instance;
            bool isHostItself = req.ClientNetworkId == NetworkManager.ServerClientId;
            if (!isHostItself && session != null)
            {
                if (session.PlayerCount >= GameConfig.MaxPlayers) { res.Approved = false; res.Reason = "full"; return; }
                if (session.Phase != SessionPhase.Lobby) { res.Approved = false; res.Reason = "started"; return; }
            }
            res.Approved = true;
            string name = req.Payload != null && req.Payload.Length > 0 ? Encoding.UTF8.GetString(req.Payload) : "Mouse";
            if (name.Length > 16) name = name.Substring(0, 16);
            PendingNames[req.ClientNetworkId] = name;
        }

        /// <summary>Names from approval, picked up by SessionManager when the client is connected.</summary>
        public static readonly System.Collections.Generic.Dictionary<ulong, string> PendingNames = new System.Collections.Generic.Dictionary<ulong, string>();

        void OnClientDisconnect(ulong clientId)
        {
            // this callback on a client means *we* lost the connection
            if (nm.IsServer) return;
            if (clientId != nm.LocalClientId && clientId != NetworkManager.ServerClientId) return;
            string reason = nm.DisconnectReason;
            string key = string.IsNullOrEmpty(reason) ? (SessionManager.Instance != null ? "host_left" : "conn_failed") : reason;
            Leave(key);
        }
    }
}
