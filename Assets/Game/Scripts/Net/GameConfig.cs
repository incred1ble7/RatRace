using UnityEngine;

namespace MouseGame.Net
{
    public sealed class MapInfo
    {
        public string Scene;
        public string NameKey;
        public float MatchSeconds;
        public float CatInterval;
        public float CatWarning;
        public float ComboChance;
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
            new MapInfo { Scene = "Game_Kitchen",      NameKey = "map_day",   MatchSeconds = 150f, CatInterval = 2.9f, CatWarning = 0.85f, ComboChance = 0.2f },
            new MapInfo { Scene = "Game_KitchenNight", NameKey = "map_night", MatchSeconds = 150f, CatInterval = 2.3f, CatWarning = 0.72f, ComboChance = 0.35f },
        };

        public const float MoveSpeed = 2.2f;
        public const float CarrySlowPerItem = 0.09f;
        public const float CarryMinSpeedFactor = 0.45f;
        public const int MaxCarry = 8;
        public const float PickupRadius = 0.6f;
        public const float DeliverRadius = 0.75f;

        public const float DashSpeedMultiplier = 2.6f;
        public const float DashDuration = 0.22f;
        public const float DashCooldown = 3.0f;

        public const float TrapCooldown = 4.0f;
        public const float TrapRadius = 0.45f;
        public const float TrapSlowFactor = 0.4f;
        public const float TrapSlowSeconds = 3.0f;
        public const float TrapArmDelay = 0.8f;

        public const float CatStunSeconds = 3.0f;
        public const float CatAimChance = 0.75f;
        public const float CatPawRadius = 1.0f;
        public const float CatSafeHoleRadius = 1.8f;
        public const float CatEndIntervalFactor = 0.65f;
        public const float CatEndWarningFactor = 0.8f;
        public const float CatMinWarning = 0.5f;
        public const float CatGraceSeconds = 1.5f;
        public const float CountdownSeconds = 3f;

        public static readonly int[] FoodValueByTier = { 0, 1, 2, 4 };

        public static readonly Color[] SlotColors =
        {
            new Color(0.95f, 0.35f, 0.30f),
            new Color(0.30f, 0.60f, 0.98f),
            new Color(0.35f, 0.85f, 0.40f),
            new Color(0.98f, 0.80f, 0.25f),
        };

        public static Color SlotColor(int slot) => SlotColors[Mathf.Clamp(slot - 1, 0, SlotColors.Length - 1)];

        public static int MapIndexOfScene(string scene)
        {
            for (int i = 0; i < Maps.Length; i++) if (Maps[i].Scene == scene) return i;
            return -1;
        }
    }
}
