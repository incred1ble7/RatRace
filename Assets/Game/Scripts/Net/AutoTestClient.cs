using System.IO;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouseGame.Net
{
    /// <summary>
    /// Test bot for multiplayer checks, enabled only with command-line args:
    ///   -autojoin 127.0.0.1   join that host from the main menu
    ///   -autolog  path.txt    write progress to a file
    /// In the lobby it presses Ready and chats; in a match it fetches food and carries it home.
    /// </summary>
    public class AutoTestClient : MonoBehaviour
    {
        string ip, logPath;
        bool joined, readied, chatted;
        string lastScene = "";
        float nextLog;

        public static void InstallIfRequested(GameObject host)
        {
            var args = System.Environment.GetCommandLineArgs();
            string ip = null, log = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-autojoin") ip = args[i + 1];
                if (args[i] == "-autolog") log = args[i + 1];
            }
            if (ip == null) return;
            var t = host.AddComponent<AutoTestClient>();
            t.ip = ip;
            t.logPath = log;
            t.Log("bot started, will join " + ip);
        }

        void Log(string s)
        {
            Debug.Log("[Bot] " + s);
            if (string.IsNullOrEmpty(logPath)) return;
            try { File.AppendAllText(logPath, System.DateTime.Now.ToString("HH:mm:ss") + " " + s + "\n"); } catch { }
        }

        void Update()
        {
            var nm = NetworkManager.Singleton;
            string scene = SceneManager.GetActiveScene().name;
            if (scene != lastScene) { Log("scene -> " + scene); lastScene = scene; }

            if (scene == GameConfig.MenuScene && !joined && Time.time > 1.5f)
            {
                joined = true;
                ConnectionManager.PlayerName = "Bot";
                Log("joining " + ip + " ok=" + ConnectionManager.Instance.Join(ip));
            }

            var s = SessionManager.Instance;
            if (scene == GameConfig.LobbyScene && s != null && s.IsSpawned)
            {
                if (!chatted) { chatted = true; s.SendChat("hello from bot client"); Log("chat sent, players=" + s.Players.Count); }
                if (s.TryGet(nm.LocalClientId, out var me) && !me.Ready) { s.SetReady(true); if (!readied) Log("ready pressed"); readied = true; }
            }

            var mouse = NetMouse.Local;
            var mm = MatchManager.Instance;
            if (mouse != null && mm != null && mm.IsPlaying) Drive(mouse, mm);
            else GameInput.BotMove = null;

            if (Time.time > nextLog && s != null && s.IsSpawned)
            {
                nextLog = Time.time + 3f;
                string line = "phase=" + s.Phase + " players=" + s.Players.Count;
                if (s.TryGet(nm.LocalClientId, out var p)) line += " slot=" + p.Slot + " score=" + p.Score + " delivered=" + p.Delivered + " hits=" + p.CatHits;
                if (mouse != null) line += " pos=" + mouse.transform.position.ToString("F1") + " carried=" + mouse.CarriedCount.Value;
                if (mm != null) line += " match=" + mm.State.Value + " left=" + mm.TimeLeft.ToString("F0") + " food=" + FoodItem.All.Count + " mice=" + NetMouse.All.Count;
                Log(line);
            }
        }

        // ---- very simple bot: floor food only, 2 pieces, then home
        void Drive(NetMouse me, MatchManager mm)
        {
            Vector3 target;
            if (me.CarriedCount.Value >= 2)
            {
                var hole = mm.Map.GetHole(me.Slot.Value);
                target = hole.transform.position + hole.transform.forward * 0.4f;
            }
            else
            {
                FoodItem best = null; float bd = float.MaxValue;
                foreach (var f in FoodItem.All)
                {
                    if (f.transform.position.y > 0.5f) continue;
                    float d = Vector3.Distance(f.transform.position, me.transform.position);
                    if (d < bd) { bd = d; best = f; }
                }
                if (best == null) { GameInput.BotMove = Vector2.zero; return; }
                target = best.transform.position;
                if (bd < GameConfig.PickupRadius * 0.8f) GameInput.PressTake();
            }
            Vector3 dir = target - me.transform.position; dir.y = 0f;
            // camera looks along +Z with no yaw, so world XZ == joystick XY
            GameInput.BotMove = dir.magnitude < 0.05f ? Vector2.zero : new Vector2(dir.x, dir.z).normalized;
        }
    }
}
