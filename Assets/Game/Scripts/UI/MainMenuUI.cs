using System.Linq;
using MouseGame.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MouseGame.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        enum Page { Home, Multiplayer, HowTo }

        Page page = Page.Home;
        Canvas canvas;
        InputField nameField, lobbyField, ipField;
        RectTransform list;
        Text status, browserTitle;
        float connectTimeout;
        string pendingStatus;
        Color pendingStatusColor;

        void Start()
        {
            if (LanDiscovery.Instance != null) LanDiscovery.Instance.ListChanged += RefreshList;

            if (!string.IsNullOrEmpty(ConnectionManager.PendingMenuMessage))
            {
                pendingStatus = L10n.T(ConnectionManager.PendingMenuMessage);
                pendingStatusColor = UIKit.Bad;
                ConnectionManager.PendingMenuMessage = null;
                page = Page.Multiplayer;
            }
            Open(page);
        }

        void OnDestroy()
        {
            if (LanDiscovery.Instance != null)
            {
                LanDiscovery.Instance.ListChanged -= RefreshList;
                LanDiscovery.Instance.StopSearch();
            }
        }

        void Open(Page p)
        {
            page = p;
            Rebuild();
            if (page == Page.Multiplayer) LanDiscovery.Instance?.Search();
            else LanDiscovery.Instance?.StopSearch();
        }

        void Rebuild()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            list = null; status = null; browserTitle = null;
            canvas = UIKit.CreateCanvas("MainMenuCanvas", 0);
            var root = canvas.transform;
            UIKit.Img(root, "Bg", new Color(0.16f, 0.12f, 0.10f, 1f), false).rectTransform.Anchor(0, 0, 1, 1);

            switch (page)
            {
                case Page.Home: BuildHome(root); break;
                case Page.Multiplayer: BuildMultiplayer(root); RefreshList(); break;
                case Page.HowTo: BuildHowTo(root); break;
            }
            if (status != null && pendingStatus != null) { SetStatus(pendingStatus, pendingStatusColor); pendingStatus = null; }
        }

        void BuildHome(Transform root)
        {
            var title = UIKit.Label(root, L10n.T("title"), 110, TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            title.rectTransform.Anchor(0, 0.68f, 1, 0.92f, new Vector2(40, 0), new Vector2(-40, 0));

            var col = UIKit.Rect("Buttons", root);
            col.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(620, 400));
            col.pivot = new Vector2(0.5f, 0.5f);
            var vl = col.gameObject.AddComponent<VerticalLayoutGroup>();
            vl.spacing = 24; vl.childControlHeight = true; vl.childControlWidth = true;
            vl.childForceExpandHeight = false; vl.childForceExpandWidth = true;

            var mp = UIKit.Btn(col, L10n.T("multiplayer"), () => Open(Page.Multiplayer), UIKit.Accent, 52);
            mp.ButtonText().color = new Color(0.2f, 0.12f, 0.05f);
            mp.Height(140);
            UIKit.Btn(col, L10n.T("how_to_play"), () => Open(Page.HowTo), null, 42).Height(110);
            UIKit.Btn(col, L10n.T("quit"), Application.Quit, UIKit.Bad, 36).Height(100);

            var ver = UIKit.Label(root, "v" + Application.version, 24, TextAnchor.LowerRight, UIKit.DimText);
            ver.rectTransform.Anchor(0.5f, 0, 1, 0, new Vector2(0, 16), new Vector2(-24, 56));
        }

        void BuildHowTo(Transform root)
        {
            BackButton(root);
            var title = UIKit.Label(root, L10n.T("how_to_play"), 64, TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            title.rectTransform.Anchor(0, 1, 1, 1, new Vector2(300, -140), new Vector2(-300, -30));
            var panel = UIKit.PanelBox(root, "Rules").rectTransform.Anchor(0.08f, 0.06f, 0.92f, 0.82f);
            var text = UIKit.Label(panel, L10n.T("rules_text"), 36, TextAnchor.UpperLeft);
            text.supportRichText = true;
            text.lineSpacing = 1.15f;
            text.rectTransform.Anchor(0, 0, 1, 1, new Vector2(50, 40), new Vector2(-50, -40));
        }

        void BuildMultiplayer(Transform root)
        {
            BackButton(root);
            var title = UIKit.Label(root, L10n.T("multiplayer"), 64, TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            title.rectTransform.Anchor(0, 1, 1, 1, new Vector2(300, -120), new Vector2(-300, -24));
            var sub = UIKit.Label(root, L10n.F("your_ip", LanDiscovery.LocalIPv4()), 30, TextAnchor.MiddleCenter, UIKit.DimText);
            sub.rectTransform.Anchor(0, 1, 1, 1, new Vector2(40, -170), new Vector2(-40, -120));

            var left = UIKit.PanelBox(root, "Left").rectTransform.Anchor(0.03f, 0.05f, 0.42f, 0.82f);
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

            var right = UIKit.PanelBox(root, "Right").rectTransform.Anchor(0.44f, 0.05f, 0.97f, 0.82f);
            browserTitle = UIKit.Label(right, L10n.T("servers"), 36, TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            browserTitle.rectTransform.Anchor(0, 1, 1, 1, new Vector2(30, -90), new Vector2(-280, -20));
            var refresh = UIKit.Btn(right, L10n.T("refresh"), () => LanDiscovery.Instance?.Search(), null, 32);
            ((RectTransform)refresh.transform).Place(new Vector2(1, 1), new Vector2(-30, -20), new Vector2(230, 70));
            list = UIKit.ScrollList(right, "Servers", out _, 10f);
            ((RectTransform)list.parent.parent).Anchor(0, 0, 1, 1, new Vector2(30, 140), new Vector2(-30, -110));

            status = UIKit.Label(right, "", 30, TextAnchor.MiddleLeft, UIKit.DimText);
            status.rectTransform.Anchor(0, 0, 1, 0, new Vector2(30, 30), new Vector2(-30, 120));
        }

        void BackButton(Transform root)
        {
            var back = UIKit.Btn(root, "< " + L10n.T("back"), () => Open(Page.Home), null, 32);
            ((RectTransform)back.transform).Place(new Vector2(0, 1), new Vector2(30, -30), new Vector2(240, 80));
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
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (page != Page.Home && kb != null && kb.escapeKey.wasPressedThisFrame) Open(Page.Home);
#endif
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
