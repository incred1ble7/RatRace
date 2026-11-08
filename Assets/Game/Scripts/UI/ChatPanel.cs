using MouseGame.Net;
using UnityEngine;
using UnityEngine.UI;

namespace MouseGame.UI
{
    /// <summary>Chat box (log + input). Used in the lobby and in the match HUD.</summary>
    public class ChatPanel : MonoBehaviour
    {
        Text log;
        InputField input;
        ScrollRect scroll;

        public static ChatPanel Create(Transform parent)
        {
            var box = UIKit.PanelBox(parent, "Chat");
            var cp = box.gameObject.AddComponent<ChatPanel>();
            cp.Build(box.rectTransform);
            return cp;
        }

        void Build(RectTransform root)
        {
            var title = UIKit.Label(root, L10n.T("chat"), 30, TextAnchor.MiddleLeft, UIKit.Accent, FontStyle.Bold);
            title.rectTransform.Anchor(0, 1, 1, 1, new Vector2(20, -50), new Vector2(-20, -8));

            var content = UIKit.ScrollList(root, "Log", out scroll, 0f);
            ((RectTransform)scroll.transform).Anchor(0, 0, 1, 1, new Vector2(14, 90), new Vector2(-14, -56));
            log = UIKit.Label(content, "", 28, TextAnchor.LowerLeft);
            log.supportRichText = true;
            log.verticalOverflow = VerticalWrapMode.Overflow;

            input = UIKit.Field(root, L10n.T("chat_hint"), "", 28, 120);
            ((RectTransform)input.transform).Anchor(0, 0, 1, 0, new Vector2(14, 14), new Vector2(-150, 78));
            input.onEndEdit.AddListener(OnEndEdit);
            var send = UIKit.Btn(root, L10n.T("send"), Send, UIKit.Accent, 28);
            send.ButtonText().color = new Color(0.2f, 0.12f, 0.05f);
            ((RectTransform)send.transform).Anchor(1, 0, 1, 0, new Vector2(-140, 14), new Vector2(-14, 78));

            SessionManager.ChatReceived += OnChat;
            Redraw();
        }

        void OnDestroy()
        {
            SessionManager.ChatReceived -= OnChat;
            GameInput.Blocked = false;
        }

        void Update()
        {
            GameInput.Blocked = input != null && input.isFocused;
        }

        void OnEndEdit(string _)
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Send();
#endif
            if (TouchScreenKeyboard.isSupported && input.touchScreenKeyboard != null && input.touchScreenKeyboard.status == TouchScreenKeyboard.Status.Done) Send();
        }

        public void Send()
        {
            if (input == null || string.IsNullOrWhiteSpace(input.text)) return;
            SessionManager.Instance?.SendChat(input.text);
            input.text = "";
        }

        public void Focus()
        {
            if (input == null) return;
            input.Select();
            input.ActivateInputField();
        }

        void OnChat(string name, string text, Color c) => Redraw();

        void Redraw()
        {
            if (log == null) return;
            var sb = new System.Text.StringBuilder();
            var list = SessionManager.ChatLog;
            int start = Mathf.Max(0, list.Count - 25);
            for (int i = start; i < list.Count; i++)
            {
                var (name, text, color) = list[i];
                string safe = text.Replace("<", "‹").Replace(">", "›");
                if (string.IsNullOrEmpty(name)) sb.Append("<i><color=#aaaaaa>").Append(safe).Append("</color></i>");
                else sb.Append("<color=#").Append(UIKit.Hex(color)).Append("><b>").Append(name.Replace("<", "‹")).Append(":</b></color> ").Append(safe);
                if (i < list.Count - 1) sb.Append('\n');
            }
            log.text = sb.ToString();
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 0f;
        }
    }
}
