using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MouseGame
{
    /// <summary>
    /// One place for player input: touch UI (joystick + buttons) on phones, keyboard/gamepad for editor tests.
    /// Buttons are "pressed this frame" flags consumed by the local player.
    /// </summary>
    public static class GameInput
    {
        public static VirtualJoystick Joystick;
        public static bool Blocked;          // e.g. chat input is focused
        public static Vector2? BotMove;      // test bot override (AutoTestClient)

        static bool take, dash, trap;
        static int frameTake = -1, frameDash = -1, frameTrap = -1;

        public static void PressTake() { take = true; frameTake = Time.frameCount; }
        public static void PressDash() { dash = true; frameDash = Time.frameCount; }
        public static void PressTrap() { trap = true; frameTrap = Time.frameCount; }

        public static Vector2 Move
        {
            get
            {
                if (Blocked) return Vector2.zero;
                if (BotMove.HasValue) return BotMove.Value;
                Vector2 v = Joystick != null ? Joystick.Direction : Vector2.zero;
#if ENABLE_INPUT_SYSTEM
                var kb = Keyboard.current;
                if (kb != null)
                {
                    Vector2 k = Vector2.zero;
                    if (kb.wKey.isPressed || kb.upArrowKey.isPressed) k.y += 1;
                    if (kb.sKey.isPressed || kb.downArrowKey.isPressed) k.y -= 1;
                    if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) k.x += 1;
                    if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) k.x -= 1;
                    if (k != Vector2.zero) v = k.normalized;
                }
                var pad = Gamepad.current;
                if (pad != null && pad.leftStick.ReadValue().sqrMagnitude > 0.02f) v = pad.leftStick.ReadValue();
#endif
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        public static bool Take => Consume(ref take, frameTake) || Key(0);
        public static bool Dash => Consume(ref dash, frameDash) || Key(1);
        public static bool Trap => Consume(ref trap, frameTrap) || Key(2);

        static bool Consume(ref bool flag, int frame)
        {
            bool v = flag && frame >= Time.frameCount - 1;
            flag = false;
            return v && !Blocked;
        }

        static bool Key(int which)
        {
            if (Blocked) return false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            switch (which)
            {
                case 0: return (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                case 1: return (kb != null && (kb.leftShiftKey.wasPressedThisFrame || kb.rightShiftKey.wasPressedThisFrame)) || (pad != null && pad.buttonEast.wasPressedThisFrame);
                default: return (kb != null && kb.qKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
            }
#else
            return false;
#endif
        }

        public static bool IsTouchDevice
        {
            get
            {
#if UNITY_EDITOR
                return true;   // show touch controls in the editor too, so they can be tested with the mouse
#else
                return Application.isMobilePlatform;
#endif
            }
        }
    }
}
