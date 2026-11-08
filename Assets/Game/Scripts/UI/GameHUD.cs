using System.Collections.Generic;
using System.Linq;
using MouseGame.Net;
using MouseGame.UI;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace MouseGame
{
    /// <summary>
    /// Match HUD for phones: timer, scores, carried food, joystick + Take/Dash/Trap buttons,
    /// name tags, toasts, chat, pause menu and the end-of-match results with Restart / Next level / Lobby.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        Canvas canvas;
        Text timer, scores, carry, toast, bigText, hint;
        Button takeBtn, dashBtn, trapBtn;
        Image dashFill, trapFill;
        RectTransform tagsRoot;
        readonly Dictionary<NetMouse, Text> tags = new Dictionary<NetMouse, Text>();
        GameObject chatBox, menuBox, resultsBox;
        float toastUntil;
        bool resultsShown;
        int lastScoresHash;

        void Awake() => Instance = this;

        void Start()
        {
            L10n.Changed += Rebuild;
            SessionManager.Changed += OnSessionChanged;
            Build();
        }

        void OnDestroy()
        {
            L10n.Changed -= Rebuild;
            SessionManager.Changed -= OnSessionChanged;
            if (Instance == this) Instance = null;
            GameInput.Joystick = null;
        }

        void Rebuild()
        {
            if (canvas != null) Destroy(canvas.gameObject);
            tags.Clear();
            resultsShown = false;
            Build();
        }

        static SessionManager S => SessionManager.Instance;
        static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // ================================================================ build
        void Build()
        {
            canvas = UIKit.CreateCanvas("HUD", 10);
            var root = canvas.transform;

            tagsRoot = UIKit.Rect("NameTags", root).Anchor(0, 0, 1, 1);

            // timer (top centre)
            var tb = UIKit.PanelBox(root, "TimerBox", UIKit.Bg);
            ((RectTransform)tb.transform).Place(new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(300, 100));
            timer = UIKit.Label(tb.transform, "0:00", 60, TextAnchor.MiddleCenter, UIKit.TextCol, FontStyle.Bold);
            timer.rectTransform.Anchor(0, 0, 1, 1);

            // scores (top left)
            var sb = UIKit.PanelBox(root, "Scores", UIKit.Bg);
            ((RectTransform)sb.transform).Place(new Vector2(0, 1), new Vector2(20, -20), new Vector2(430, 230));
            scores = UIKit.Label(sb.transform, "", 34);
            scores.supportRichText = true;
            scores.alignment = TextAnchor.UpperLeft;
            scores.rectTransform.Anchor(0, 0, 1, 1, new Vector2(22, 14), new Vector2(-18, -14));

            // top right: chat + menu
            var chatToggle = UIKit.Btn(root, L10n.T("chat"), ToggleChat, null, 30);
            ((RectTransform)chatToggle.transform).Place(new Vector2(1, 1), new Vector2(-200, -20), new Vector2(180, 90));
            var menuBtn = UIKit.Btn(root, "II", () => menuBox.SetActive(!menuBox.activeSelf), null, 40);
            ((RectTransform)menuBtn.transform).Place(new Vector2(1, 1), new Vector2(-20, -20), new Vector2(160, 90));

            var cp = ChatPanel.Create(root);
            ((RectTransform)cp.transform).Place(new Vector2(1, 1), new Vector2(-20, -125), new Vector2(720, 520));
            chatBox = cp.gameObject;
            chatBox.SetActive(false);

            // carried food (above the action buttons)
            carry = UIKit.Label(root, "", 36, TextAnchor.MiddleRight, UIKit.Accent, FontStyle.Bold);
            carry.rectTransform.Place(new Vector2(1, 0), new Vector2(-40, 520), new Vector2(700, 60));

            // touch controls
            bool touch = GameInput.IsTouchDevice;
            var zone = UIKit.Img(root, "JoystickZone", new Color(1, 1, 1, 0), false);
            zone.rectTransform.Anchor(0, 0, 0.45f, 0.75f);
            var jbg = UIKit.Img(zone.transform, "Background", new Color(1, 1, 1, 0.18f), true, false);
            jbg.rectTransform.anchorMin = jbg.rectTransform.anchorMax = Vector2.zero;
            jbg.rectTransform.sizeDelta = new Vector2(300, 300);
            jbg.rectTransform.anchoredPosition = new Vector2(280, 260);
            jbg.type = Image.Type.Simple;
            var handle = UIKit.Img(jbg.transform, "Handle", new Color(1, 1, 1, 0.75f), true, false);
            handle.type = Image.Type.Simple;
            handle.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(130, 130));
            handle.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var joy = zone.gameObject.AddComponent<VirtualJoystick>();
            joy.Setup(jbg.rectTransform, handle.rectTransform, 125f);
            GameInput.Joystick = joy;
            zone.gameObject.SetActive(touch);

            takeBtn = RoundButton(root, L10n.T("take"), new Vector2(-60, 60), 260, UIKit.Accent, GameInput.PressTake, out _);
            takeBtn.ButtonText().color = new Color(0.2f, 0.12f, 0.05f);
            dashBtn = RoundButton(root, L10n.T("dash"), new Vector2(-340, 40), 190, new Color(0.40f, 0.65f, 0.95f), GameInput.PressDash, out dashFill);
            trapBtn = RoundButton(root, L10n.T("trap"), new Vector2(-120, 340), 190, new Color(0.80f, 0.45f, 0.35f), GameInput.PressTrap, out trapFill);

            hint = UIKit.Label(root, touch ? "" : L10n.T("hint_pc"), 26, TextAnchor.LowerCenter, UIKit.DimText);
            hint.rectTransform.Anchor(0.2f, 0, 0.8f, 0, new Vector2(0, 10), new Vector2(0, 60));

            // centre texts
            bigText = UIKit.Label(root, "", 150, TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            bigText.rectTransform.Anchor(0, 0.4f, 1, 0.75f);
            var sh = bigText.gameObject.AddComponent<Outline>(); sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(4, -4);
            toast = UIKit.Label(root, "", 52, TextAnchor.MiddleCenter, UIKit.TextCol, FontStyle.Bold);
            toast.rectTransform.Anchor(0.15f, 0.62f, 0.85f, 0.72f);
            var ts = toast.gameObject.AddComponent<Outline>(); ts.effectColor = new Color(0, 0, 0, 0.7f); ts.effectDistance = new Vector2(3, -3);

            BuildMenu(root);
            resultsBox = null;
        }

        Button RoundButton(Transform root, string label, Vector2 pos, float size, Color c, UnityEngine.Events.UnityAction a, out Image fill)
        {
            var b = UIKit.Btn(root, label, null, c, (int)(size * 0.16f));
            var rt = (RectTransform)b.transform;
            rt.Place(new Vector2(1, 0), pos, new Vector2(size, size));
            ((Image)b.targetGraphic).type = Image.Type.Simple;   // full circle
            // fire on press (not release) — feels better for action buttons
            var trig = b.gameObject.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            var e = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            e.callback.AddListener(_ => { if (b.interactable) a(); });
            trig.triggers.Add(e);
            fill = UIKit.Img(b.transform, "Cooldown", new Color(0, 0, 0, 0.55f), false, false);
            fill.sprite = UIKit.Round;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            fill.fillAmount = 0f;
            fill.rectTransform.Anchor(0, 0, 1, 1);
            b.ButtonText().transform.SetAsLastSibling();
            return b;
        }

        void BuildMenu(Transform root)
        {
            var box = UIKit.Img(root, "Menu", new Color(0, 0, 0, 0.6f), false);
            box.rectTransform.Anchor(0, 0, 1, 1);
            var panel = UIKit.PanelBox(box.transform, "Panel");
            ((RectTransform)panel.transform).Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620, 420));
            var t = UIKit.Label(panel.transform, L10n.T("menu"), 50, TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            t.rectTransform.Anchor(0, 1, 1, 1, new Vector2(0, -110), new Vector2(0, -20));
            var resume = UIKit.Btn(panel.transform, L10n.T("resume"), () => menuBox.SetActive(false), UIKit.Good, 40);
            ((RectTransform)resume.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(460, 100));
            var leave = UIKit.Btn(panel.transform, L10n.T("leave"), () => ConnectionManager.Instance.Leave(), UIKit.Bad, 40);
            ((RectTransform)leave.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0, -120), new Vector2(460, 100));
            menuBox = box.gameObject;
            menuBox.SetActive(false);
        }

        void ToggleChat()
        {
            chatBox.SetActive(!chatBox.activeSelf);
            if (chatBox.activeSelf) chatBox.GetComponent<ChatPanel>().Focus();
        }

        public void Toast(string text, bool good)
        {
            if (toast == null) return;
            toast.text = text;
            toast.color = good ? UIKit.Good : UIKit.Bad;
            toastUntil = Time.time + 2f;
        }

        void OnSessionChanged() => lastScoresHash = 0;

        // ================================================================ update
        void Update()
        {
            if (canvas == null) return;
            var mm = MatchManager.Instance;
            var me = NetMouse.Local;

#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.enterKey.wasPressedThisFrame && !GameInput.Blocked && !chatBox.activeSelf) ToggleChat();
            if (kb != null && kb.escapeKey.wasPressedThisFrame) menuBox.SetActive(!menuBox.activeSelf);
#endif
            toast.enabled = Time.time < toastUntil;

            if (mm != null && mm.IsSpawned)
            {
                float left = mm.TimeLeft;
                if (mm.State.Value == MatchState.Countdown)
                {
                    bigText.text = Mathf.CeilToInt(left).ToString();
                    timer.text = Clock(mm.Info.MatchSeconds);
                }
                else
                {
                    float sincePlay = (float)(mm.Now - mm.StartTime.Value);
                    bigText.text = mm.State.Value == MatchState.Playing && sincePlay < 1f ? L10n.T("go") : "";
                    timer.text = Clock(left);
                    timer.color = left < 15f && mm.IsPlaying ? UIKit.Bad : UIKit.TextCol;
                }
                if (mm.State.Value == MatchState.Ended && !resultsShown) ShowResults();
                if (mm.State.Value != MatchState.Ended && resultsShown) HideResults();
            }

            UpdateScores();

            if (me != null)
            {
                int n = me.CarriedCount.Value;
                carry.text = n > 0 ? (n >= GameConfig.MaxCarry ? L10n.T("full_paws") : L10n.F("carry", n, me.CarriedValue.Value)) : "";
                takeBtn.interactable = me.NearbyFood != null && !me.IsStunned;
                ((Image)takeBtn.targetGraphic).color = takeBtn.interactable ? UIKit.Accent : new Color(0.6f, 0.55f, 0.45f, 0.6f);
                dashFill.fillAmount = me.DashCooldown01;
                trapFill.fillAmount = me.TrapCooldown01;
            }

            UpdateNameTags();
        }

        void UpdateScores()
        {
            var s = S;
            if (s == null) return;
            int hash = 17;
            for (int i = 0; i < s.Players.Count; i++) hash = hash * 31 + s.Players[i].Score + (int)s.Players[i].ClientId * 7;
            if (hash == lastScoresHash) return;
            lastScoresHash = hash;
            var sb = new System.Text.StringBuilder();
            foreach (var p in Sorted(s))
                sb.Append("<color=#").Append(UIKit.Hex(GameConfig.SlotColor(p.Slot))).Append(">■</color> ")
                  .Append(p.Name.ToString()).Append("  <b>").Append(p.Score).Append("</b>\n");
            scores.text = sb.ToString();
        }

        static List<PlayerInfo> Sorted(SessionManager s)
        {
            var list = new List<PlayerInfo>();
            for (int i = 0; i < s.Players.Count; i++) list.Add(s.Players[i]);
            return list.OrderByDescending(p => p.Score).ThenBy(p => p.Slot).ToList();
        }

        void UpdateNameTags()
        {
            var cam = Camera.main;
            if (cam == null) return;
            foreach (var m in NetMouse.All)
            {
                if (!tags.TryGetValue(m, out var t) || t == null)
                {
                    t = UIKit.Label(tagsRoot, "", 28, TextAnchor.MiddleCenter, GameConfig.SlotColor(m.Slot.Value), FontStyle.Bold);
                    t.rectTransform.sizeDelta = new Vector2(320, 44);
                    t.rectTransform.anchorMin = t.rectTransform.anchorMax = Vector2.zero;
                    var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.8f); o.effectDistance = new Vector2(2, -2);
                    tags[m] = t;
                }
                Vector3 sp = cam.WorldToScreenPoint(m.transform.position + Vector3.up * 0.55f);
                bool visible = sp.z > 0f;
                t.enabled = visible;
                if (!visible) continue;
                float scale = canvas.scaleFactor;
                t.rectTransform.anchoredPosition = new Vector2(sp.x / scale, sp.y / scale);
                string extra = m.CarriedCount.Value > 0 ? "  (" + m.CarriedCount.Value + ")" : "";
                t.text = m.PlayerName + extra;
                t.color = GameConfig.SlotColor(m.Slot.Value);
            }
            // remove tags of despawned mice
            var dead = tags.Keys.Where(k => k == null || !NetMouse.All.Contains(k)).ToList();
            foreach (var d in dead) { if (tags[d] != null) Destroy(tags[d].gameObject); tags.Remove(d); }
        }

        static string Clock(float seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (s / 60) + ":" + (s % 60).ToString("00");
        }

        // ================================================================ results
        void ShowResults()
        {
            resultsShown = true;
            menuBox.SetActive(false);
            if (resultsBox != null) Destroy(resultsBox);
            var s = S;
            var root = canvas.transform;
            var dim = UIKit.Img(root, "Results", new Color(0, 0, 0, 0.7f), false);
            dim.rectTransform.Anchor(0, 0, 1, 1);
            resultsBox = dim.gameObject;
            var panel = UIKit.PanelBox(dim.transform, "Panel");
            ((RectTransform)panel.transform).Anchor(0.08f, 0.08f, 0.92f, 0.92f);

            var title = UIKit.Label(panel.transform, L10n.T("results"), 64, TextAnchor.MiddleCenter, UIKit.Accent, FontStyle.Bold);
            title.rectTransform.Anchor(0, 1, 1, 1, new Vector2(0, -110), new Vector2(0, -20));

            var sorted = s != null ? Sorted(s) : new List<PlayerInfo>();
            string winner = sorted.Count == 0 ? "" :
                (sorted.Count > 1 && sorted[0].Score == sorted[1].Score ? L10n.T("draw") : L10n.F("winner", sorted[0].Name.ToString()));
            var w = UIKit.Label(panel.transform, winner, 46, TextAnchor.MiddleCenter, UIKit.TextCol, FontStyle.Bold);
            w.rectTransform.Anchor(0, 1, 1, 1, new Vector2(0, -180), new Vector2(0, -110));

            // table
            string[] cols = { "col_player", "col_score", "col_delivered", "col_hits", "col_lost", "col_traps" };
            float[] widths = { 0.30f, 0.14f, 0.14f, 0.14f, 0.14f, 0.14f };
            float top = -200f;
            Row(panel.transform, top, cols.Select(L10n.T).ToArray(), widths, UIKit.DimText, 30);
            for (int i = 0; i < sorted.Count; i++)
            {
                var p = sorted[i];
                var vals = new[] { (i + 1) + ". " + p.Name, p.Score.ToString(), p.Delivered.ToString(), p.CatHits.ToString(), p.Lost.ToString(), p.TrapsHit.ToString() };
                Row(panel.transform, top - 70f * (i + 1), vals, widths, GameConfig.SlotColor(p.Slot), 38);
            }

            // buttons (host decides; clients wait or leave)
            var bar = UIKit.Rect("Buttons", panel.transform).Anchor(0, 0, 1, 0, new Vector2(40, 30), new Vector2(-40, 150));
            if (IsHost)
            {
                var r = UIKit.Btn(bar, L10n.T("restart"), () => S?.RestartMatch(), UIKit.Good, 38);
                ((RectTransform)r.transform).Anchor(0f, 0, 0.32f, 1);
                var n = UIKit.Btn(bar, L10n.T("next_level"), () => S?.NextLevel(), UIKit.Accent, 38);
                n.ButtonText().color = new Color(0.2f, 0.12f, 0.05f);
                ((RectTransform)n.transform).Anchor(0.34f, 0, 0.66f, 1);
                n.interactable = s != null && s.HasNextLevel;
                var l = UIKit.Btn(bar, L10n.T("to_lobby"), () => S?.BackToLobby(), null, 38);
                ((RectTransform)l.transform).Anchor(0.68f, 0, 1f, 1);
            }
            else
            {
                var wait = UIKit.Label(bar, L10n.T("wait_host_choice"), 36, TextAnchor.MiddleLeft, UIKit.DimText);
                wait.rectTransform.Anchor(0, 0, 0.65f, 1);
                var l = UIKit.Btn(bar, L10n.T("leave"), () => ConnectionManager.Instance.Leave(), UIKit.Bad, 38);
                ((RectTransform)l.transform).Anchor(0.68f, 0, 1f, 1);
            }
        }

        static void Row(Transform parent, float y, string[] cells, float[] widths, Color c, int size)
        {
            float x = 0.04f;
            for (int i = 0; i < cells.Length; i++)
            {
                var t = UIKit.Label(parent, cells[i], size, i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, c, i == 0 ? FontStyle.Bold : FontStyle.Normal);
                t.rectTransform.anchorMin = new Vector2(x, 1);
                t.rectTransform.anchorMax = new Vector2(x + widths[i] * 0.92f, 1);
                t.rectTransform.offsetMin = new Vector2(0, y - 64);
                t.rectTransform.offsetMax = new Vector2(0, y);
                x += widths[i] * 0.92f;
            }
        }

        void HideResults()
        {
            resultsShown = false;
            if (resultsBox != null) Destroy(resultsBox);
            resultsBox = null;
        }
    }
}
