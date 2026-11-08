using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace MouseGame.Net
{
    /// <summary>A game found on the local network.</summary>
    public struct DiscoveredGame
    {
        public string Address;
        public ushort Port;
        public string Name;
        public int Players;
        public int MaxPlayers;
        public bool Joinable;
        public float SeenAt;
    }

    /// <summary>
    /// Server browser over UDP broadcast (Wi‑Fi / LAN). No accounts, no internet needed.
    ///   host:   answers "RRDISC1" with "RRHOST1|name|port|players|max|joinable"
    ///   client: broadcasts the request and collects answers.
    /// On Android a Wi‑Fi multicast lock is held while searching (otherwise many phones drop broadcasts).
    /// </summary>
    public class LanDiscovery : MonoBehaviour
    {
        const string Request = "RRDISC1";
        const string Reply = "RRHOST1";

        public static LanDiscovery Instance { get; private set; }

        // ---- host side (values updated from the main thread) ----
        volatile string hostName = "";
        volatile int hostPlayers;
        volatile bool hostJoinable = true;
        UdpClient server;
        Thread serverThread;

        // ---- client side ----
        UdpClient client;
        Thread clientThread;
        readonly ConcurrentQueue<DiscoveredGame> incoming = new ConcurrentQueue<DiscoveredGame>();
        readonly Dictionary<string, DiscoveredGame> found = new Dictionary<string, DiscoveredGame>();
        float searchUntil;
        public bool IsSearching => Time.unscaledTime < searchUntil;
        public IEnumerable<DiscoveredGame> Found => found.Values;
        public event Action ListChanged;

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject multicastLock;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void OnDestroy()
        {
            StopHosting();
            StopSearch();
            if (Instance == this) Instance = null;
        }

        void OnApplicationQuit()
        {
            StopHosting();
            StopSearch();
        }

        // ================= host =================
        public void StartHosting(string name)
        {
            StopHosting();
            hostName = Sanitize(name);
            try
            {
                server = new UdpClient(AddressFamily.InterNetwork);
                server.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                server.Client.Bind(new IPEndPoint(IPAddress.Any, GameConfig.DiscoveryPort));
                serverThread = new Thread(ServerLoop) { IsBackground = true, Name = "LanDiscoveryServer" };
                serverThread.Start();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LAN] discovery responder not started: " + e.Message);
            }
        }

        public void UpdateHostInfo(string name, int players, bool joinable)
        {
            hostName = Sanitize(name);
            hostPlayers = players;
            hostJoinable = joinable;
        }

        public void StopHosting()
        {
            try { server?.Close(); } catch { }
            server = null;
            serverThread = null;
        }

        void ServerLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (server != null)
            {
                try
                {
                    var data = server.Receive(ref any);
                    if (Encoding.UTF8.GetString(data) != Request) continue;
                    string msg = string.Join("|", Reply, hostName, GameConfig.GamePort, hostPlayers, GameConfig.MaxPlayers, hostJoinable ? 1 : 0);
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    server.Send(bytes, bytes.Length, any);
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (server == null) break; }
                catch (Exception) { }
            }
        }

        // ================= client =================
        public void Search(float seconds = 2f)
        {
            StopSearch();
            found.Clear();
            ListChanged?.Invoke();
            AcquireMulticastLock();
            try
            {
                client = new UdpClient(AddressFamily.InterNetwork) { EnableBroadcast = true };
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                clientThread = new Thread(ClientLoop) { IsBackground = true, Name = "LanDiscoveryClient" };
                clientThread.Start();

                var req = Encoding.UTF8.GetBytes(Request);
                foreach (var target in BroadcastTargets())
                {
                    try { client.Send(req, req.Length, new IPEndPoint(target, GameConfig.DiscoveryPort)); } catch { }
                }
                searchUntil = Time.unscaledTime + seconds;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LAN] search failed: " + e.Message);
                StopSearch();
            }
        }

        void ClientLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (client != null)
            {
                try
                {
                    var data = client.Receive(ref any);
                    var parts = Encoding.UTF8.GetString(data).Split('|');
                    if (parts.Length < 6 || parts[0] != Reply) continue;
                    incoming.Enqueue(new DiscoveredGame
                    {
                        Address = any.Address.ToString(),
                        Port = ushort.Parse(parts[2]),
                        Name = parts[1],
                        Players = int.Parse(parts[3]),
                        MaxPlayers = int.Parse(parts[4]),
                        Joinable = parts[5] == "1",
                    });
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (client == null) break; }
                catch (Exception) { }
            }
        }

        public void StopSearch()
        {
            try { client?.Close(); } catch { }
            client = null;
            clientThread = null;
            searchUntil = 0f;
            ReleaseMulticastLock();
        }

        void Update()
        {
            bool changed = false;
            while (incoming.TryDequeue(out var g))
            {
                g.SeenAt = Time.unscaledTime;
                // the same host may answer on several interfaces; loopback answers are kept only if nothing better
                string key = g.Name + ":" + g.Port;
                if (found.TryGetValue(key, out var old) && old.Address != "127.0.0.1" && g.Address == "127.0.0.1") continue;
                found[key] = g;
                changed = true;
            }
            if (changed) ListChanged?.Invoke();
            if (client != null && !IsSearching) { StopSearch(); ListChanged?.Invoke(); }
        }

        // ================= helpers =================
        static IEnumerable<IPAddress> BroadcastTargets()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast, IPAddress.Loopback };
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily != AddressFamily.InterNetwork || ua.IPv4Mask == null) continue;
                        var ip = ua.Address.GetAddressBytes();
                        var mask = ua.IPv4Mask.GetAddressBytes();
                        var b = new byte[4];
                        for (int i = 0; i < 4; i++) b[i] = (byte)(ip[i] | ~mask[i]);
                        var bc = new IPAddress(b);
                        if (!list.Contains(bc)) list.Add(bc);
                    }
                }
            }
            catch { /* some platforms do not expose masks; global broadcast still works */ }
            return list;
        }

        /// <summary>Best guess of this device's LAN address (shown in the menu so others can join by IP).</summary>
        public static string LocalIPv4()
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    s.Connect("10.255.255.255", 1);   // no packets are sent for UDP connect
                    return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
                }
            }
            catch { return "127.0.0.1"; }
        }

        static string Sanitize(string s) => string.IsNullOrWhiteSpace(s) ? "Game" : s.Replace("|", "/").Trim();

        void AcquireMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (multicastLock != null) return;
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var wifi = activity.Call<AndroidJavaObject>("getSystemService", "wifi"))
                {
                    multicastLock = wifi.Call<AndroidJavaObject>("createMulticastLock", "RatRaceDiscovery");
                    multicastLock.Call("acquire");
                }
            }
            catch (Exception e) { Debug.LogWarning("[LAN] multicast lock: " + e.Message); }
#endif
        }

        void ReleaseMulticastLock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { multicastLock?.Call("release"); } catch { }
            multicastLock = null;
#endif
        }
    }
}
