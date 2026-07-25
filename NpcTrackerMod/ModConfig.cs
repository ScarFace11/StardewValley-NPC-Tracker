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

        /// <summary>Прозрачность тайлов маршрута (0.0–1.0). По умолчанию 0.3.</summary>
        public float RouteAlpha { get; set; } = 0.3f;

        /// <summary>
        /// Цвет тайлов маршрута NPC.
        /// Допустимые значения: Green, Blue, Red, Yellow, Orange, Purple, White, Cyan, Pink.
        /// </summary>
        public string RouteColor { get; set; } = "Green";

        /// <summary>
        /// Цвет тайла текущей позиции NPC.
        /// Допустимые значения: Green, Blue, Red, Yellow, Orange, Purple, White, Cyan, Pink.
        /// </summary>
        public string PositionColor { get; set; } = "Blue";

        // ── Утилиты ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Преобразует строковое имя цвета в XNA Color.
        /// При неверном значении возвращает <paramref name="fallback"/>.
        /// </summary>
        public static Color ParseColor(string name, Color fallback)
        {
            switch ((name ?? string.Empty).ToLowerInvariant())
            {
                case "green": return Color.Green;
                case "blue": return Color.Blue;
                case "red": return Color.Red;
                case "yellow": return Color.Yellow;
                case "orange": return Color.Orange;
                case "purple": return Color.Purple;
                case "white": return Color.White;
                case "cyan": return Color.Cyan;
                case "pink": return Color.Pink;
                default: return fallback;
            }
        }
    }
}