using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;

namespace NpcTrackerMod
{
    /// <summary>
    /// Конфигурация мода, сохраняемая в config.json.
    /// </summary>
    public class ModConfig
    {
        /// <summary> Клавиша открытия меню трекера. </summary>
        public SButton MenuKey { get; set; } = SButton.G;

        /// <summary> Клавиша отладочного вывода варпов. </summary>
        public SButton DebugKey { get; set; } = SButton.Z;

        /// <summary> Клавиша выбора/снятия NPC при наведении на тайл маршрута. </summary>
        public SButton SelectNpcKey { get; set; } = SButton.MouseMiddle;

        // ── Внешний вид ───────────────────────────────────────────────────────────

        /// <summary>Прозрачность тайлов маршрута (0.0–1.0). По умолчанию 0.1.</summary>
        public float RouteAlpha { get; set; } = 0.1f;

        /// <summary>
        /// Цвет тайлов маршрута NPC.
        /// Допустимые значения: именованные (Green, Blue, Red, …) или HEX-строка "#RRGGBB".
        /// </summary>
        public string RouteColor { get; set; } = "Green";

        /// <summary>
        /// Цвет тайла текущей позиции NPC.
        /// Допустимые значения: именованные (Green, Blue, Red, …) или HEX-строка "#RRGGBB".
        /// </summary>
        public string PositionColor { get; set; } = "Blue";

        // ── Утилиты ───────────────────────────────────────────────────────────────

        /// <summary> Восстанавливает значения по умолчанию (кнопка сброса в настройках). </summary>
        public void ResetToDefaults()
        {
            MenuKey      = SButton.G;
            DebugKey     = SButton.Z;
            SelectNpcKey = SButton.MouseMiddle;
            RouteAlpha   = 0.1f;
            RouteColor   = "Green";
            PositionColor = "Blue";
        }

        /// <summary>
        /// Преобразует строку цвета в XNA Color.
        /// Поддерживаются именованные цвета и HEX-строка "#RRGGBB".
        /// При неверном значении возвращает <paramref name="fallback"/>.
        /// </summary>
        public static Color ParseColor(string name, Color fallback)
        {
            string value = (name ?? string.Empty).Trim();

            switch (value.ToLowerInvariant())
            {
                case "green":  return Color.Green;
                case "blue":   return Color.Blue;
                case "red":    return Color.Red;
                case "yellow": return Color.Yellow;
                case "orange": return Color.Orange;
                case "purple": return Color.Purple;
                case "white":  return Color.White;
                case "cyan":   return Color.Cyan;
                case "pink":   return Color.Pink;
            }

            if (value.Length == 7 && value[0] == '#')
            {
                try
                {
                    int r = Convert.ToInt32(value.Substring(1, 2), 16);
                    int g = Convert.ToInt32(value.Substring(3, 2), 16);
                    int b = Convert.ToInt32(value.Substring(5, 2), 16);
                    return new Color(r, g, b);
                }
                catch { /* не HEX — ниже fallback */ }
            }

            return fallback;
        }
    }
}