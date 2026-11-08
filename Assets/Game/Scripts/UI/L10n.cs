using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouseGame.UI
{
    /// <summary>Tiny RU / PL / EN string table. Language is saved in PlayerPrefs.</summary>
    public static class L10n
    {
        public enum Lang { RU = 0, PL = 1, EN = 2 }

        static Lang current = (Lang)PlayerPrefs.GetInt("lang", 0);
        public static Lang Current => current;
        public static event Action Changed;

        public static void Set(Lang l)
        {
            current = l;
            PlayerPrefs.SetInt("lang", (int)l);
            Changed?.Invoke();
        }

        public static void Next() => Set((Lang)(((int)current + 1) % 3));

        public static string T(string key)
        {
            if (Table.TryGetValue(key, out var v)) return v[(int)current];
            return key;
        }

        public static string F(string key, params object[] args) => string.Format(T(key), args);

        //                      RU                                   PL                                      EN
        static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>
        {
            ["title"]          = new[] { "Ограбление холодильника",          "Napad na lodówkę",                     "Fridge Heist" },
            ["subtitle"]       = new[] { "Мыши против кота",                 "Myszy kontra kot",                     "Mice vs the cat" },
            ["your_name"]      = new[] { "Ваше имя",                         "Twoje imię",                           "Your name" },
            ["lobby_name"]     = new[] { "Название игры",                    "Nazwa gry",                            "Game name" },
            ["host"]           = new[] { "Создать игру (Host)",              "Utwórz grę (Host)",                    "Host game" },
            ["join"]           = new[] { "Подключиться",                     "Dołącz",                               "Join" },
            ["join_ip"]        = new[] { "IP адрес хоста",                   "Adres IP hosta",                       "Host IP address" },
            ["servers"]        = new[] { "Доступные игры (Wi‑Fi / LAN)",     "Dostępne gry (Wi‑Fi / LAN)",           "Available games (Wi‑Fi / LAN)" },
            ["refresh"]        = new[] { "Обновить",                         "Odśwież",                              "Refresh" },
            ["searching"]      = new[] { "Поиск игр…",                       "Szukanie gier…",                       "Searching…" },
            ["no_servers"]     = new[] { "Игр не найдено. Нажмите «Обновить» или введите IP.", "Nie znaleziono gier. Odśwież lub wpisz IP.", "No games found. Refresh or enter an IP." },
            ["players_n"]      = new[] { "{0}/{1} игроков",                  "{0}/{1} graczy",                       "{0}/{1} players" },
            ["in_game"]        = new[] { "идёт матч",                        "trwa mecz",                            "in match" },
            ["quit"]           = new[] { "Выход",                            "Wyjście",                              "Quit" },
            ["language"]       = new[] { "Язык: Русский",                    "Język: Polski",                        "Language: English" },
            ["your_ip"]        = new[] { "Ваш IP: {0}",                      "Twoje IP: {0}",                        "Your IP: {0}" },
            ["connecting"]     = new[] { "Подключение…",                     "Łączenie…",                            "Connecting…" },
            ["conn_failed"]    = new[] { "Не удалось подключиться",          "Nie udało się połączyć",               "Could not connect" },
            ["disconnected"]   = new[] { "Соединение потеряно",              "Utracono połączenie",                  "Disconnected" },
            ["host_left"]      = new[] { "Хост закрыл игру",                 "Host zamknął grę",                     "Host closed the game" },
            ["full"]           = new[] { "Игра заполнена",                   "Gra jest pełna",                       "Game is full" },
            ["started"]        = new[] { "Матч уже идёт",                    "Mecz już trwa",                        "Match already started" },

            ["lobby"]          = new[] { "Лобби",                            "Lobby",                                "Lobby" },
            ["players"]        = new[] { "Игроки",                           "Gracze",                               "Players" },
            ["ready"]          = new[] { "Готов",                            "Gotowy",                               "Ready" },
            ["not_ready"]      = new[] { "Не готов",                         "Niegotowy",                            "Not ready" },
            ["set_ready"]      = new[] { "Я готов!",                         "Jestem gotowy!",                       "I'm ready!" },
            ["cancel_ready"]   = new[] { "Отменить готовность",              "Anuluj gotowość",                      "Cancel ready" },
            ["start"]          = new[] { "Начать игру",                      "Rozpocznij grę",                       "Start game" },
            ["wait_ready"]     = new[] { "Ждём, пока все будут готовы",      "Czekamy, aż wszyscy będą gotowi",      "Waiting for everyone to be ready" },
            ["wait_host"]      = new[] { "Ждём, пока хост начнёт",           "Czekamy na start hosta",               "Waiting for the host to start" },
            ["map"]            = new[] { "Карта",                            "Mapa",                                 "Map" },
            ["map_day"]        = new[] { "Кухня — день",                     "Kuchnia — dzień",                      "Kitchen — day" },
            ["map_night"]      = new[] { "Кухня — ночь (кот злее)",          "Kuchnia — noc (kot groźniejszy)",      "Kitchen — night (angrier cat)" },
            ["leave"]          = new[] { "Выйти",                            "Wyjdź",                                "Leave" },
            ["host_tag"]       = new[] { "хост",                             "host",                                 "host" },
            ["you_tag"]        = new[] { "вы",                               "ty",                                   "you" },

            ["chat"]           = new[] { "Чат",                              "Czat",                                 "Chat" },
            ["chat_hint"]      = new[] { "Сообщение…",                       "Wiadomość…",                           "Message…" },
            ["send"]           = new[] { "Отпр.",                            "Wyślij",                               "Send" },
            ["joined"]         = new[] { "{0} подключился",                  "{0} dołączył",                         "{0} joined" },
            ["left"]           = new[] { "{0} вышел",                        "{0} wyszedł",                          "{0} left" },

            ["take"]           = new[] { "Взять",                            "Weź",                                  "Take" },
            ["dash"]           = new[] { "Рывок",                            "Zryw",                                 "Dash" },
            ["trap"]           = new[] { "Ловушка",                          "Pułapka",                              "Trap" },
            ["carry"]          = new[] { "В лапах: {0} (+{1})",              "Niesiesz: {0} (+{1})",                 "Carrying: {0} (+{1})" },
            ["go"]             = new[] { "Вперёд!",                          "Start!",                               "Go!" },
            ["delivered"]      = new[] { "+{0} очков!",                      "+{0} pkt!",                            "+{0} points!" },
            ["cat_hit"]        = new[] { "Кот попал! Потеряно: {0}",         "Kot trafił! Stracono: {0}",            "The cat got you! Lost: {0}" },
            ["trapped"]        = new[] { "Ловушка! Вы замедлены",            "Pułapka! Jesteś spowolniony",          "Trapped! You are slowed" },
            ["full_paws"]      = new[] { "Лапы заняты — несите в норку!",    "Łapy pełne — nieś do nory!",           "Paws full — take it home!" },
            ["hint_pc"]        = new[] { "WASD — бег, E — взять, Shift — рывок, Q — ловушка, Enter — чат", "WASD — ruch, E — weź, Shift — zryw, Q — pułapka, Enter — czat", "WASD move, E take, Shift dash, Q trap, Enter chat" },
            ["your_hole"]      = new[] { "Ваша норка",                       "Twoja nora",                           "Your burrow" },

            ["results"]        = new[] { "Итоги матча",                      "Wyniki meczu",                         "Match results" },
            ["winner"]         = new[] { "Победитель: {0}",                  "Zwycięzca: {0}",                       "Winner: {0}" },
            ["draw"]           = new[] { "Ничья!",                           "Remis!",                               "Draw!" },
            ["col_player"]     = new[] { "Игрок",                            "Gracz",                                "Player" },
            ["col_score"]      = new[] { "Очки",                             "Punkty",                               "Score" },
            ["col_delivered"]  = new[] { "Доставлено",                       "Dostarczono",                          "Delivered" },
            ["col_hits"]       = new[] { "Удары кота",                       "Ciosy kota",                           "Cat hits" },
            ["col_lost"]       = new[] { "Потеряно",                         "Stracono",                             "Lost" },
            ["col_traps"]      = new[] { "Ловушки",                          "Pułapki",                              "Traps" },
            ["restart"]        = new[] { "Заново",                           "Restart",                              "Restart" },
            ["next_level"]     = new[] { "Следующий уровень",                "Następny poziom",                      "Next level" },
            ["to_lobby"]       = new[] { "В лобби",                          "Do lobby",                             "Back to lobby" },
            ["wait_host_choice"] = new[] { "Ждём решения хоста…",            "Czekamy na decyzję hosta…",            "Waiting for the host…" },
            ["menu"]           = new[] { "Меню",                             "Menu",                                 "Menu" },
            ["resume"]         = new[] { "Продолжить",                       "Wróć",                                 "Resume" },
        };
    }
}
