using UnityEngine;

namespace MouseGame.Net
{
    /// <summary>A playable level. Scene must be in Build Settings.</summary>
    public sealed class MapInfo
    {
        public string Scene;
        public string NameKey;          // L10n key
        public float MatchSeconds;
        public float CatInterval;       // seconds between paw strikes
        public float CatWarning;        // seconds the zone blinks before the strike
    }

    public static class GameConfig
    {
        public const int MaxPlayers = 4;
        public const ushort GamePort = 7777;
        public const int DiscoveryPort = 47777;

        public const string BootScene = "Boot";
        public const string MenuScene = "MainMenu";
        public const string LobbyScene = "Lobby";

        public static readonly MapInfo[] Maps =
        {
            new MapInfo { Scene = "Game_Kitchen",      NameKey = "map_day",   MatchSeconds = 150f, CatInterval = 5.0f, CatWarning = 1.5f },
            new MapInfo { Scene = "Game_KitchenNight", NameKey = "map_night", MatchSeconds = 150f, CatInterval = 3.4f, CatWarning = 1.2f },
        };

        // ---- gameplay tuning (shared by server and clients) ----
        public const float MoveSpeed = 2.2f;
        public const float CarrySlowPerItem = 0.09f;   // -9% speed per carried item
        public const float CarryMinSpeedFactor = 0.45f;
        public const int MaxCarry = 8;
        public const float PickupRadius = 0.6f;
        public const float DeliverRadius = 1.3f;

        public const float DashSpeedMultiplier = 2.6f;
        public const float DashDuration = 0.22f;
        public const float DashCooldown = 3.0f;

        public const float TrapCooldown = 4.0f;
        public const float TrapRadius = 0.45f;
        public const float TrapSlowFactor = 0.4f;
        public const float TrapSlowSeconds = 3.0f;
        public const float TrapArmDelay = 0.8f;        // owner can walk away before it arms

        public const float CatStunSeconds = 1.2f;
        public const float CountdownSeconds = 3f;

        public static readonly int[] FoodValueByTier = { 0, 1, 2, 4 };   // tier 1 floor, 2 fridge, 3 table

        public static readonly Color[] SlotColors =
        {
            new Color(0.95f, 0.35f, 0.30f), // 1 red
            new Color(0.30f, 0.60f, 0.98f), // 2 blue
            new Color(0.35f, 0.85f, 0.40f), // 3 green
            new Color(0.98f, 0.80f, 0.25f), // 4 yellow
        };

        public static Color SlotColor(int slot) => SlotColors[Mathf.Clamp(slot - 1, 0, SlotColors.Length - 1)];

        public static int MapIndexOfScene(string scene)
        {
            for (int i = 0; i < Maps.Length; i++) if (Maps[i].Scene == scene) return i;
            return -1;
        }
    }
}
