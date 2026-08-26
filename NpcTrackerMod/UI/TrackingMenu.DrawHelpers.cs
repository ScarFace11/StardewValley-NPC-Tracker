using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary>
    /// Общие примитивы отрисовки, используемые всеми вкладками TrackingMenu.
    /// </summary>
    public partial class TrackingMenu
    {
        // ── SDV Color Palette ───────────────────────────────────────────────────────
        private static readonly Color BgDark      = new Color(62, 44, 28);    // Dark brown background
        private static readonly Color BgMedium     = new Color(101, 77, 50);   // Medium brown
        private static readonly Color BgLight      = new Color(160, 130, 90);  // Light brown
        private static readonly Color BgCream      = new Color(252, 246, 232); // Cream
        private static readonly Color GoldAccent   = new Color(255, 210, 60);  // SDV gold
        private static readonly Color GoldDark     = new Color(180, 145, 50);  // Dark gold
        private static readonly Color TextPrimary  = new Color(255, 245, 230); // Cream text
        private static readonly Color TextSecondary = new Color(200, 185, 155); // Dimmed cream
        private static readonly Color TextDim      = new Color(150, 140, 125);  // Very dim
        private static readonly Color DividerColor = new Color(140, 115, 75, 150); // Divider
        private static readonly Color CardBg       = new Color(252, 246, 232); // Card background
        private static readonly Color CardSelected = new Color(255, 233, 140); // Selected card
        private static readonly Color HoverBg      = new Color(200, 195, 180, 80); // Hover effect

        /// <summary> Рисует стрелку ◄ или ► из игровых курсоров. </summary>
        private static void DrawArrow(SpriteBatch b, Rectangle rect, bool left)
        {
            var src = left
                ? new Rectangle(352, 495, 12, 11)
                : new Rectangle(365, 495, 12, 11);
            b.Draw(Game1.mouseCursors, rect, src, Color.White);
        }

        /// <summary> Рисует горизонтальный разделитель. </summary>
        private void DrawDivider(SpriteBatch b, int y)
        {
            int x1 = BX + PAD;
            int x2 = BX + BOX_W - PAD;
            b.Draw(Game1.staminaRect, new Rectangle(x1, y, x2 - x1, 2),
                new Color(180, 155, 110, 150));
        }

        /// <summary> Рисует текст, выровненный по центру ширины окна. </summary>
        private void DrawCentered(SpriteBatch b, string text, SpriteFont font, int y, Color color)
        {
            var sz = font.MeasureString(text);
            Utility.drawTextWithShadow(b, text, font,
                new Vector2(BX + (BOX_W - sz.X) / 2f, y), color);
        }

        /// <summary> Рисует заголовок секции крупным шрифтом. </summary>
        private void DrawSectionHeader(SpriteBatch b, string text, int x, int y) =>
            Utility.drawTextWithShadow(b, text, Game1.dialogueFont,
                new Vector2(x, y), new Color(90, 70, 50));

        /// <summary> Рисует заголовок группы мелким шрифтом. </summary>
        private void DrawGroupHeader(SpriteBatch b, string text, int x, int y) =>
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(x, y), new Color(110, 90, 65));

        /// <summary>
        /// Обрезает строку с многоточием, чтобы она помещалась в maxWidth пикселей
        /// шрифта font. Используется, чтобы текст не выходил за рамки меню.
        /// </summary>
        private static string TruncateToWidth(SpriteFont font, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return text;
            if (font.MeasureString(text).X <= maxWidth) return text;

            for (int len = text.Length - 1; len > 1; len--)
            {
                if (font.MeasureString(text.Substring(0, len) + "…").X <= maxWidth)
                    return text.Substring(0, len) + "…";
            }
            return "…";
        }

        /// <summary> Рисует пару «Ключ: Значение» и сдвигает y вниз на 26px. </summary>
        private void DrawKV(SpriteBatch b, string key, string value, int x, ref int y)
        {
            Utility.drawTextWithShadow(b, key + ":", Game1.smallFont,
                new Vector2(x, y), new Color(100, 90, 75));
            float offset = Game1.smallFont.MeasureString(key + ":  ").X;

            // Значение обрезается, чтобы не выходить за правую границу меню.
            float maxW = (BX + BOX_W - PAD) - (x + offset);
            string shown = TruncateToWidth(Game1.smallFont, value, maxW);
            Utility.drawTextWithShadow(b, shown, Game1.smallFont,
                new Vector2(x + offset, y), new Color(60, 50, 40));
            y += 26;
        }

        /// <summary> Первый символ заглавным. </summary>
        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        /// <summary>
        /// Draw a status indicator dot (colored circle).
        /// </summary>
        private static void DrawStatusDot(SpriteBatch b, int x, int y, int size, NpcStatus status)
        {
            Color color = status switch
            {
                NpcStatus.Available   => new Color(80, 180, 60),
                NpcStatus.Leaving     => new Color(220, 150, 30),
                NpcStatus.Unavailable => new Color(200, 70, 50),
                _                     => new Color(140, 130, 115)
            };

            // Outer ring (dark)
            b.Draw(Game1.staminaRect,
                new Rectangle(x, y, size, size),
                new Color(color.R / 3, color.G / 3, color.B / 3));
            // Inner fill
            b.Draw(Game1.staminaRect,
                new Rectangle(x + 2, y + 2, size - 4, size - 4),
                color);
        }

        /// <summary>
        /// Draw a magnifying glass icon for search.
        /// </summary>
        private static void DrawSearchIcon(SpriteBatch b, int x, int y, int size)
        {
            // Simple magnifying glass using cursor sprites
            var src = new Rectangle(80, 48, 16, 16); // magnifying glass from sprites
            b.Draw(Game1.mouseCursors, new Rectangle(x, y, size, size), src, Color.White);
        }

        /// <summary>
        /// Get the status of an NPC based on their current location and the player's location.
        /// </summary>
        private NpcStatus GetNpcStatus(string npcName, int currentTime)
        {
            var npc = _registry.GameNpcs;
            if (npc == null) return NpcStatus.Offline;

            NPC character = null;
            foreach (var n in npc)
            {
                if (n?.Name == npcName) { character = n; break; }
            }
            if (character == null) return NpcStatus.Offline;

            if (character.currentLocation == null) return NpcStatus.Offline;

            bool isInLocation = Game1.currentLocation != null &&
                                character.currentLocation.Name == Game1.currentLocation.Name;

            if (!isInLocation) return NpcStatus.Offline;

            // Check if time filter affects this NPC
            if (currentTime >= 0 && currentTime > _state.TimeFilter && _state.TimeFilter >= 0)
                return NpcStatus.Unavailable;

            return NpcStatus.Available;
        }

        /// <summary>
        /// Format game time (integer like 1200) to readable format "12:00 PM".
        /// </summary>
        private static string FormatGameTime(int time)
        {
            if (time < 0) return "All";
            int hours = time / 100;
            int minutes = time % 100;
            return $"{hours:D2}:{minutes:D2}";
        }

        /// <summary>
        /// Get season name localized.
        /// </summary>
        private string GetSeasonName()
        {
            if (!Context.IsWorldReady) return "";
            string season = Game1.currentSeason ?? "";
            return Capitalize(season);
        }

        /// <summary>
        /// Get weather display text for the current game state.
        /// </summary>
        private string GetWeatherText()
        {
            if (!StardewModdingAPI.Context.IsWorldReady) return "-";

            try
            {
                // Access weather through Game1 (compatible with SMAPI)
                string weather = "Sunny";
                
                if (Game1.isRaining)
                    weather = Game1.isLightning ? "Stormy" : "Rainy";
                else if (Game1.isSnowing)
                    weather = "Snowy";
                
                return weather;
            }
            catch
            {
                return "-";
            }
        }
    }
}
