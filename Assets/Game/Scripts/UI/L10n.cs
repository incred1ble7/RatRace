using System.Collections.Generic;

namespace MouseGame.UI
{
    public static class L10n
    {
        public static string T(string key)
        {
            string v;
            return Table.TryGetValue(key, out v) ? v : key;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            ["title"] = "RatRace",
            ["subtitle"] = "Mice vs the cat",
            ["your_name"] = "Your name",
            ["lobby_name"] = "Game name",
            ["host"] = "Host game",
            ["join"] = "Join",
            ["join_ip"] = "Host IP address",
            ["servers"] = "Available games (Wi‑Fi / LAN)",
            ["refresh"] = "Refresh",
            ["searching"] = "Searching…",
            ["no_servers"] = "No games found. Refresh or enter an IP.",
            ["players_n"] = "{0}/{1} players",
            ["in_game"] = "in match",
            ["multiplayer"] = "Multiplayer",
            ["how_to_play"] = "How to play",
            ["back"] = "Back",
            ["rules_text"] =
                "<b>Goal:</b> bring more food to your burrow than your rivals before time runs out.\n\n" +
                "• <b>Take</b> — pick up food nearby. You can carry several pieces, but each one slows you down.\n" +
                "• Points only count for food <b>delivered to your own burrow</b> (the hole in your colour).\n" +
                "• <b>Dash</b> — a short burst of speed to escape. Has a cooldown.\n" +
                "• <b>Trap</b> — dropped behind you, slows a rival. Only one at a time.\n" +
                "• The <b>cat</b> strikes with both paws anywhere in the kitchen — on the table and on the floor. " +
                "A red circle blinks first — run! The cat aims where you are heading and loves mice with full paws. " +
                "A squashed mouse is <b>frozen for 3 seconds</b> and drops half its food for others to grab.\n" +
                "• You are safe <b>under the table or the ramps</b> and right next to your own burrow. The cat gets faster near the end.\n\n" +
                "The best food is on the table. Bank a little now or risk it for more?",
            ["quit"] = "Quit",
            ["your_ip"] = "Your IP: {0}",
            ["connecting"] = "Connecting…",
            ["conn_failed"] = "Could not connect",
            ["disconnected"] = "Disconnected",
            ["host_left"] = "Host closed the game",
            ["full"] = "Game is full",
            ["started"] = "Match already started",
            ["lobby"] = "Lobby",
            ["players"] = "Players",
            ["ready"] = "Ready",
            ["not_ready"] = "Not ready",
            ["set_ready"] = "I'm ready!",
            ["cancel_ready"] = "Cancel ready",
            ["start"] = "Start game",
            ["wait_ready"] = "Waiting for everyone to be ready",
            ["wait_host"] = "Waiting for the host to start",
            ["map"] = "Map",
            ["map_day"] = "Kitchen — day",
            ["map_night"] = "Kitchen — night (angrier cat)",
            ["leave"] = "Leave",
            ["host_tag"] = "host",
            ["you_tag"] = "you",
            ["chat"] = "Chat",
            ["chat_hint"] = "Message…",
            ["send"] = "Send",
            ["joined"] = "{0} joined",
            ["left"] = "{0} left",
            ["take"] = "Take",
            ["dash"] = "Dash",
            ["trap"] = "Trap",
            ["carry"] = "Carrying: {0} (+{1})",
            ["go"] = "Go!",
            ["delivered"] = "+{0} points!",
            ["cat_hit"] = "Squashed by the cat! Lost: {0}",
            ["frozen"] = "FROZEN\n{0:0.0}",
            ["trapped"] = "Trapped! You are slowed",
            ["full_paws"] = "Paws full — take it home!",
            ["hint_pc"] = "WASD move, E take, Shift dash, Q trap, Enter chat",
            ["your_hole"] = "Your burrow",
            ["results"] = "Match results",
            ["winner"] = "Winner: {0}",
            ["draw"] = "Draw!",
            ["col_player"] = "Player",
            ["col_score"] = "Score",
            ["col_delivered"] = "Delivered",
            ["col_hits"] = "Cat hits",
            ["col_lost"] = "Lost",
            ["col_traps"] = "Traps",
            ["restart"] = "Restart",
            ["next_level"] = "Next level",
            ["to_lobby"] = "Back to lobby",
            ["wait_host_choice"] = "Waiting for the host…",
            ["menu"] = "Menu",
            ["resume"] = "Resume",
        };
    }
}
