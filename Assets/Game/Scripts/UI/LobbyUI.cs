using MouseGame.Net;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MouseGame.UI
{
    /// <summary>Lobby: players + ready state, map choice (host), chat, Start (host, only when everyone is ready).</summary>
    public class LobbyUI : MonoBehaviour
    {
        Canvas canvas;
        RectTransform playerList;
        Text header, mapLabel, hint;
        Button readyBtn, startBtn, mapPrev, mapNext;

        void Start()
        {
            L10n.Changed += Rebuild;
            SessionManager.Changed += Refresh;
            Build();
            Refresh();
        }

        void OnDestroy()
        {
            L10n.Changed -= Rebuild;
            SessionManager.Changed -= Refresh;
        }

        void Rebuild()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            Build();
            Refresh();
        }

        static SessionManager S => SessionManager.Instance;
        static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        void Build()
        {
            canvas = UIKit.CreateCanvas("LobbyCanvas", 0);
            var root = canvas.transform;
            UIKit.Img(root, "Bg", new Color(0.16f, 0.12f, 0.10f, 1f), false).rectTransform.Anchor(0, 0, 1, 1);

            header = UIKit.Label(root, "", 60, TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            header.rectTransform.Anchor(0, 1, 1, 1, new Vector2(50, -130), new Vector2(-400, -20));
            var leave = UIKit.Btn(root, L10n.T("leave"), () => ConnectionManager.Instance.Leave(), UIKit.Bad, 32);
            ((RectTransform)leave.transform).Place(new Vector2(1, 1), new Vector2(-40, -35), new Vector2(240, 80));

            // players
            var left = UIKit.PanelBox(root, "Players").rectTransform.Anchor(0.03f, 0.22f, 0.50f, 0.85f);
            var pt = UIKit.Label(left, L10n.T("players"), 36, TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            pt.rectTransform.Anchor(0, 1, 1, 1, new Vector2(30, -80), new Vector2(-30, -16));
            playerList = UIKit.ScrollList(left, "List", out _, 12f);
            ((RectTransform)playerList.parent.parent).Anchor(0, 0, 1, 1, new Vector2(24, 24), new Vector2(-24, -90));

            // map selection
            var mapBox = UIKit.PanelBox(root, "Map").rectTransform.Anchor(0.52f, 0.66f, 0.97f, 0.85f);
            var mt = UIKit.Label(mapBox, L10n.T("map"), 30, TextAnchor.MiddleLeft, UIKit.DimText);
            mt.rectTransform.Anchor(0, 1, 1, 1, new Vector2(30, -60), new Vector2(-30, -10));
            mapPrev = UIKit.Btn(mapBox, "<", () => S?.ChangeMap(-1), null, 50);
            ((RectTransform)mapPrev.transform).Place(new Vector2(0, 0), new Vector2(20, 20), new Vector2(110, 90));
            mapNext = UIKit.Btn(mapBox, ">", () => S?.ChangeMap(1), null, 50);
            ((RectTransform)mapNext.transform).Place(new Vector2(1, 0), new Vector2(-20, 20), new Vector2(110, 90));
            mapLabel = UIKit.Label(mapBox, "", 38, TextAnchor.MiddleCenter, UIKit.TextCol, FontStyle.Bold);
            mapLabel.rectTransform.Anchor(0, 0, 1, 0, new Vector2(140, 20), new Vector2(-140, 110));

            // chat
            var chat = ChatPanel.Create(root);
            ((RectTransform)chat.transform).Anchor(0.52f, 0.22f, 0.97f, 0.64f);

            // bottom: ready + start
            readyBtn = UIKit.Btn(root, "", () =>
            {
                if (S != null && S.TryGet(NetworkManager.Singleton.LocalClientId, out var me)) S.SetReady(!me.Ready);
            }, UIKit.Good, 44);
            ((RectTransform)readyBtn.transform).Anchor(0.03f, 0.04f, 0.33f, 0.18f);

            startBtn = UIKit.Btn(root, L10n.T("start"), () => S?.StartMatch(), UIKit.Accent, 44);
            startBtn.ButtonText().color = new Color(0.2f, 0.12f, 0.05f);
            ((RectTransform)startBtn.transform).Anchor(0.67f, 0.04f, 0.97f, 0.18f);

            hint = UIKit.Label(root, "", 30, TextAnchor.MiddleCenter, UIKit.DimText);
            hint.rectTransform.Anchor(0.34f, 0.04f, 0.66f, 0.18f);
        }

        void Refresh()
        {
            if (canvas == null) return;
            var s = S;
            if (s == null || !s.IsSpawned) return;
            ulong me = NetworkManager.Singleton.LocalClientId;

            header.text = L10n.T("lobby") + ":  " + s.LobbyName.Value;
            mapLabel.text = L10n.T(s.CurrentMap.NameKey);
            mapPrev.gameObject.SetActive(IsHost);
            mapNext.gameObject.SetActive(IsHost);

            UIKit.Clear(playerList);
            bool myReady = false;
            for (int i = 0; i < s.Players.Count; i++)
            {
                var p = s.Players[i];
                if (p.ClientId == me) myReady = p.Ready;
                var row = UIKit.PanelBox(playerList, "P", new Color(0.28f, 0.23f, 0.19f, 1f));
                row.Height(96);
                var dot = UIKit.Img(row.transform, "Colour", GameConfig.SlotColor(p.Slot));
                ((RectTransform)dot.transform).Place(new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(56, 56));
                string tags = "";
                if (p.ClientId == NetworkManager.ServerClientId) tags += "  <size=24><color=#f9c840>[" + L10n.T("host_tag") + "]</color></size>";
                if (p.ClientId == me) tags += "  <size=24><color=#bfb39e>(" + L10n.T("you_tag") + ")</color></size>";
                var name = UIKit.Label(row.transform, p.Name + tags, 36);
                name.supportRichText = true;
                name.rectTransform.Anchor(0, 0, 1, 1, new Vector2(100, 0), new Vector2(-260, 0));
                var st = UIKit.Label(row.transform, p.Ready ? "✔ " + L10n.T("ready") : L10n.T("not_ready"), 32, TextAnchor.MiddleRight,
                    p.Ready ? UIKit.Good : UIKit.DimText, FontStyle.Bold);
                st.rectTransform.Anchor(0, 0, 1, 1, new Vector2(100, 0), new Vector2(-24, 0));
            }

            readyBtn.ButtonText().text = myReady ? L10n.T("cancel_ready") : L10n.T("set_ready");
            readyBtn.image.color = myReady ? UIKit.ButtonCol : UIKit.Good;
            startBtn.gameObject.SetActive(IsHost);
            startBtn.interactable = s.AllReady && s.Phase == SessionPhase.Lobby;
            hint.text = s.AllReady ? (IsHost ? "" : L10n.T("wait_host")) : L10n.T("wait_ready");
        }
    }
}
