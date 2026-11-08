using System.Linq;
using MouseGame.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MouseGame.UI
{
    /// <summary>Main menu: name, Host, Join by IP, LAN server browser, language, quit.</summary>
    public class MainMenuUI : MonoBehaviour
    {
        Canvas canvas;
        InputField nameField, lobbyField, ipField;
        RectTransform list;
        Text status, browserTitle;
        float connectTimeout;

        void Start()
        {
            L10n.Changed += Rebuild;
            Build();
            if (!string.IsNullOrEmpty(ConnectionManager.PendingMenuMessage))
            {
                SetStatus(L10n.T(ConnectionManager.PendingMenuMessage), UIKit.Bad);
                ConnectionManager.PendingMenuMessage = null;
            }
            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.ListChanged += RefreshList;
                LanDiscovery.Instance.Search();
            }
        }

        void OnDestroy()
        {
            L10n.Changed -= Rebuild;
            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.ListChanged -= RefreshList;
                LanDiscovery.Instance.StopSearch();
            }
        }

        void Rebuild()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            Build();
            RefreshList();
        }

        void Build()
        {
            canvas = UIKit.CreateCanvas("MainMenuCanvas", 0);
            var root = canvas.transform;
            var bg = UIKit.Img(root, "Bg", new Color(0.16f, 0.12f, 0.10f, 1f), false);
            bg.rectTransform.Anchor(0, 0, 1, 1);

            var title = UIKit.Label(root, L10n.T("title"), 84, TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            title.rectTransform.Anchor(0, 1, 1, 1, new Vector2(40, -150), new Vector2(-40, -30));
            var sub = UIKit.Label(root, L10n.T("subtitle") + "   •   " + L10n.F("your_ip", LanDiscovery.LocalIPv4()), 32, TextAnchor.MiddleCenter, UIKit.DimText);
            sub.rectTransform.Anchor(0, 1, 1, 1, new Vector2(40, -200), new Vector2(-40, -150));

            // ---------------- left: player + host ----------------
            var left = UIKit.PanelBox(root, "Left").rectTransform.Anchor(0.03f, 0.05f, 0.42f, 0.78f);
            float y = -30f;
            Caption(left, L10n.T("your_name"), ref y);
            nameField = UIKit.Field(left, L10n.T("your_name"), ConnectionManager.PlayerName, 38, 16);
            Place(nameField.transform, ref y, 90);
            nameField.onEndEdit.AddListener(v => ConnectionManager.PlayerName = v);

            Caption(left, L10n.T("lobby_name"), ref y);
            lobbyField = UIKit.Field(left, ConnectionManager.PlayerName + "'s game", ConnectionManager.LobbyName, 38, 30);
            Place(lobbyField.transform, ref y, 90);

            y -= 10;
            var host = UIKit.Btn(left, L10n.T("host"), OnHost, UIKit.Accent, 44);
            host.ButtonText().color = new Color(0.2f, 0.12f, 0.05f);
            Place(host.transform, ref y, 120);

            y -= 10;
            Caption(left, L10n.T("join_ip"), ref y);
            ipField = UIKit.Field(left, "192.168.0.10", PlayerPrefs.GetString("last_ip", ""), 38, 40);
            ipField.contentType = InputField.ContentType.Standard;
            var ipRt = Place(ipField.transform, ref y, 90);
            ipRt.offsetMax = new Vector2(-260, ipRt.offsetMax.y);
            var joinIp = UIKit.Btn(left, L10n.T("join"), () => OnJoin(ipField.text, GameConfig.GamePort), UIKit.Good, 38);
            var jr = (RectTransform)joinIp.transform;
            jr.anchorMin = new Vector2(1, 1); jr.anchorMax = new Vector2(1, 1); jr.pivot = new Vector2(1, 1);
            jr.sizeDelta = new Vector2(230, 90); jr.anchoredPosition = new Vector2(-30, ipRt.anchoredPosition.y);

            // ---------------- right: server browser ----------------
            var right = UIKit.PanelBox(root, "Right").rectTransform.Anchor(0.44f, 0.05f, 0.97f, 0.78f);
            browserTitle = UIKit.Label(right, L10n.T("servers"), 36, TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            browserTitle.rectTransform.Anchor(0, 1, 1, 1, new Vector2(30, -90), new Vector2(-280, -20));
            var refresh = UIKit.Btn(right, L10n.T("refresh"), () => LanDiscovery.Instance?.Search(), null, 32);
            ((RectTransform)refresh.transform).Place(new Vector2(1, 1), new Vector2(-30, -20), new Vector2(230, 70));
            list = UIKit.ScrollList(right, "Servers", out _, 10f);
            ((RectTransform)list.parent.parent).Anchor(0, 0, 1, 1, new Vector2(30, 140), new Vector2(-30, -110));

            // ---------------- bottom bar ----------------
            status = UIKit.Label(right, "", 30, TextAnchor.MiddleLeft, UIKit.DimText);
            status.rectTransform.Anchor(0, 0, 1, 0, new Vector2(30, 30), new Vector2(-30, 120));

            var lang = UIKit.Btn(root, L10n.T("language"), L10n.Next, null, 28);
            ((RectTransform)lang.transform).Place(new Vector2(0, 1), new Vector2(30, -30), new Vector2(330, 70));
            var quit = UIKit.Btn(root, L10n.T("quit"), Application.Quit, UIKit.Bad, 28);
            ((RectTransform)quit.transform).Place(new Vector2(1, 1), new Vector2(-30, -30), new Vector2(200, 70));
        }

        static void Caption(RectTransform parent, string text, ref float y)
        {
            var t = UIKit.Label(parent, text, 30, TextAnchor.LowerLeft, UIKit.DimText);
            t.rectTransform.Anchor(0, 1, 1, 1, new Vector2(34, y - 44), new Vector2(-30, y));
            y -= 50;
        }

        static RectTransform Place(Transform t, ref float y, float h)
        {
            var rt = (RectTransform)t;
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(30, y - h); rt.offsetMax = new Vector2(-30, y);
            y -= h + 14;
            return rt;
        }

        void RefreshList()
        {
            if (list == null) return;
            UIKit.Clear(list);
            var lan = LanDiscovery.Instance;
            var games = lan != null ? lan.Found.OrderBy(g => g.Name).ToList() : new System.Collections.Generic.List<DiscoveredGame>();
            if (browserTitle != null)
                browserTitle.text = L10n.T("servers") + (lan != null && lan.IsSearching ? "  —  " + L10n.T("searching") : "");
            if (games.Count == 0)
            {
                var t = UIKit.Label(list, lan != null && lan.IsSearching ? L10n.T("searching") : L10n.T("no_servers"), 30, TextAnchor.MiddleCenter, UIKit.DimText);
                t.Height(120);
                return;
            }
            foreach (var g in games)
            {
                var row = UIKit.PanelBox(list, "Game", new Color(0.28f, 0.23f, 0.19f, 1f));
                row.Height(110);
                var info = UIKit.Label(row.transform, $"<b>{g.Name}</b>\n<size=26><color=#bfb39e>{g.Address}  •  {L10n.F("players_n", g.Players, g.MaxPlayers)}{(g.Joinable ? "" : "  •  " + L10n.T("in_game"))}</color></size>", 34);
                info.supportRichText = true;
                info.rectTransform.Anchor(0, 0, 1, 1, new Vector2(24, 6), new Vector2(-260, -6));
                var game = g;
                var b = UIKit.Btn(row.transform, L10n.T("join"), () => OnJoin(game.Address, game.Port), UIKit.Good, 34);
                ((RectTransform)b.transform).Place(new Vector2(1, 0.5f), new Vector2(-16, 0), new Vector2(220, 80));
                b.interactable = g.Joinable;
            }
        }

        void SaveFields()
        {
            ConnectionManager.PlayerName = nameField.text;
            ConnectionManager.LobbyName = lobbyField.text;
        }

        void OnHost()
        {
            SaveFields();
            LanDiscovery.Instance?.StopSearch();
            SetStatus(L10n.T("connecting"), UIKit.DimText);
            if (!ConnectionManager.Instance.Host()) SetStatus(L10n.T("conn_failed"), UIKit.Bad);
        }

        void OnJoin(string address, ushort port)
        {
            SaveFields();
            if (string.IsNullOrWhiteSpace(address)) address = "127.0.0.1";
            PlayerPrefs.SetString("last_ip", address);
            LanDiscovery.Instance?.StopSearch();
            SetStatus(L10n.T("connecting") + " " + address, UIKit.DimText);
            connectTimeout = Time.unscaledTime + 10f;
            if (!ConnectionManager.Instance.Join(address, port)) SetStatus(L10n.T("conn_failed"), UIKit.Bad);
        }

        void Update()
        {
            // a failed connection attempt: NGO shuts down by itself; show the error once
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (connectTimeout > 0f && nm != null && !nm.IsListening && !nm.ShutdownInProgress)
            {
                connectTimeout = 0f;
                string reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "conn_failed" : nm.DisconnectReason;
                SetStatus(L10n.T(reason), UIKit.Bad);
            }
            else if (connectTimeout > 0f && Time.unscaledTime > connectTimeout && nm != null && !nm.IsConnectedClient)
            {
                connectTimeout = 0f;
                nm.Shutdown();
                SetStatus(L10n.T("conn_failed"), UIKit.Bad);
            }
        }

        void SetStatus(string text, Color c)
        {
            if (status == null) return;
            status.text = text;
            status.color = c;
        }
    }
}
