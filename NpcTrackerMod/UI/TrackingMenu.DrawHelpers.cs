using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary>
    /// Common drawing primitives for TrackingMenu tabs.
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

        // Status indicator colors
        private static readonly Color StatusGreen  = new Color(80, 180, 60);   // Available
        private static readonly Color StatusOrange = new Color(220, 150, 30);  // Leaving soon
        private static readonly Color StatusRed    = new Color(200, 70, 50);   // Unavailable
        private static readonly Color StatusGray   = new Color(140, 130, 115); // Offline

        /// <summary> Draw an arrow (left or right) from game cursor sprites. </summary>
        private static void DrawArrow(SpriteBatch b, Rectangle rect, bool left)
        {
            var src = left
                ? new Rectangle(352, 495, 12, 11)
                : new Rectangle(365, 495, 12, 11);
            b.Draw(Game1.mouseCursors, rect, src, Color.White);
        }

        /// <summary> Draw a horizontal divider line. </summary>
        private void DrawDivider(SpriteBatch b, int y)
        {
            int x1 = BX + PAD;
            int x2 = BX + BOX_W - PAD;
            b.Draw(Game1.staminaRect, new Rectangle(x1, y, x2 - x1, 1), DividerColor);
        }

        /// <summary> Draw centered text using dialogue font. </summary>
        private void DrawCentered(SpriteBatch b, string text, SpriteFont font, int y, Color color)
        {
            var sz = font.MeasureString(text);
            Utility.drawTextWithShadow(b, text, font,
                new Vector2(BX + (BOX_W - sz.X) / 2f, y), color);
        }

        /// <summary> Draw section header with golden accent line. </summary>
        private void DrawSectionHeader(SpriteBatch b, string text, int x, int y)
        {
            // Golden accent bar
            b.Draw(Game1.staminaRect, new Rectangle(x, y + 4, 3, 18), GoldAccent);
            // Section title
            Utility.drawTextWithShadow(b, text, Game1.dialogueFont,
                new Vector2(x + 10, y), TextPrimary);
        }

        /// <summary> Draw a group header (smaller text). </summary>
        private void DrawGroupHeader(SpriteBatch b, string text, int x, int y)
        {
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(x, y), TextSecondary);
        }

        /// <summary>
        /// Truncate a string with ellipsis to fit within maxWidth pixels.
        /// </summary>
        private static string TruncateToWidth(SpriteFont font, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return text;
            if (font.MeasureString(text).X <= maxWidth) return text;

            for (int len = text.Length - 1; len > 1; len--)
            {
                if (font.MeasureString(text.Substring(0, len) + "...").X <= maxWidth)
                    return text.Substring(0, len) + "...";
            }
            return "...";
        }

        /// <summary> Draw a key-value pair, advancing y by 26px. </summary>
        private void DrawKV(SpriteBatch b, string key, string value, int x, ref int y)
        {
            Utility.drawTextWithShadow(b, key + ":", Game1.smallFont,
                new Vector2(x, y), TextSecondary);
            float offset = Game1.smallFont.MeasureString(key + ":  ").X;

            float maxW = (BX + BOX_W - PAD) - (x + offset);
            string shown = TruncateToWidth(Game1.smallFont, value, maxW);
            Utility.drawTextWithShadow(b, shown, Game1.smallFont,
                new Vector2(x + offset, y), TextDim);
            y += 26;
        }

        /// <summary> Capitalize first letter. </summary>
        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

        /// <summary>
        /// Draw a status indicator dot (colored circle).
        /// </summary>
        private static void DrawStatusDot(SpriteBatch b, int x, int y, int size, NpcStatus status)
        {
            Color color = status switch
            {
                NpcStatus.Available   => StatusGreen,
                NpcStatus.Leaving     => StatusOrange,
                NpcStatus.Unavailable => StatusRed,
                _                     => StatusGray
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
        /// Draw a status card with large value and label.
        /// </summary>
        private void DrawStatCard(SpriteBatch b, int x, int y, int width, int height,
            string value, string label, Color accentColor)
        {
            // Card background
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                x, y, width, height, CardBg, 1f, false);

            // Accent top border
            b.Draw(Game1.staminaRect,
                new Rectangle(x, y, width, 3), accentColor);

            // Large value
            var valSz = Game1.dialogueFont.MeasureString(value);
            Utility.drawTextWithShadow(b, value, Game1.dialogueFont,
                new Vector2(x + (width - valSz.X) / 2f, y + 12),
                TextPrimary);

            // Label below
            var lblSz = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(x + (width - lblSz.X) / 2f, y + 12 + valSz.Y + 4),
                TextSecondary);
        }

        /// <summary>
        /// Draw a progress bar with label and value.
        /// </summary>
        private void DrawProgressBar(SpriteBatch b, int x, int y, int width,
            string label, int value, int total, Color barColor)
        {
            // Label
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(x, y), TextSecondary);

            int barY = y + 22;
            int barH = 10;
            int barWidth = width - 60;

            // Track
            b.Draw(Game1.staminaRect,
                new Rectangle(x, barY, barWidth, barH),
                new Color(80, 70, 55));

            // Fill
            float ratio = total > 0 ? (float)value / total : 0f;
            if (ratio > 0f)
            {
                b.Draw(Game1.staminaRect,
                    new Rectangle(x, barY, (int)(barWidth * ratio), barH),
                    barColor);
            }

            // Value text
            string valText = value.ToString();
            var valSz = Game1.smallFont.MeasureString(valText);
            Utility.drawTextWithShadow(b, valText, Game1.smallFont,
                new Vector2(x + barWidth + 8, barY - 2),
                TextPrimary);
        }

        /// <summary>
        /// Draw a tooltip at the current mouse position using hover text.
        /// </summary>
        private void ShowTooltip(string text)
        {
            if (_hoverText == null && !string.IsNullOrEmpty(text))
                _hoverText = text;
        }

        /// <summary>
        /// If the mouse is over rect, set the hover text (only if not already set).
        /// </summary>
        private void TipIfHover(SpriteBatch b, Rectangle rect, string key)
        {
            if (_hoverText != null) return;
            if (rect.Contains(Game1.getMouseX(), Game1.getMouseY()))
                _hoverText = T(key);
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
        /// Get weather display text for the current game state.
        /// </summary>
        private string GetWeatherText()
        {
            if (!Context.IsWorldReady) return "-";

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
        /// Draw a magnifying glass icon for search.
        /// </summary>
        private static void DrawSearchIcon(SpriteBatch b, int x, int y, int size)
        {
            // Simple magnifying glass using cursor sprites
            // Use the game's cursor for a search-like icon
            var src = new Rectangle(80, 48, 16, 16); // magnifying glass from sprites
            b.Draw(Game1.mouseCursors, new Rectangle(x, y, size, size), src, Color.White);
        }
    }
}
