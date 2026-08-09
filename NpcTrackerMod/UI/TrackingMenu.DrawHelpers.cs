using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary>
    /// Общие примитивы отрисовки, используемые всеми вкладками TrackingMenu.
    /// </summary>
    public partial class TrackingMenu
    {
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
    }
}
