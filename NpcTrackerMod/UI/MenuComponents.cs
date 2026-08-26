using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary> Переключатель с галочкой и текстовой подписью. </summary>
    internal class ClickableCheckbox
    {
        public Rectangle  Bounds    { get; }
        public string     Label     { get; }
        public bool       IsChecked { get; private set; }

        private readonly Action<bool> _onToggle;

        public ClickableCheckbox(Rectangle bounds, string label, bool initial, Action<bool> onToggle)
        {
            Bounds    = bounds;
            Label     = label;
            IsChecked = initial;
            _onToggle = onToggle;
        }

        public bool ContainsPoint(int x, int y) => Bounds.Contains(x, y);

        public void Toggle()
        {
            IsChecked = !IsChecked;
            _onToggle?.Invoke(IsChecked);
        }

        public void Draw(SpriteBatch b)
        {
            try
            {
                var srcRect = IsChecked
                    ? new Rectangle(291, 253, 9, 9)
                    : new Rectangle(273, 253, 9, 9);

                b.Draw(Game1.mouseCursors_1_6,
                    new Vector2(Bounds.X, Bounds.Y),
                    srcRect, Color.White, 0f, Vector2.Zero, 5f, SpriteEffects.None, 0.4f);

                var textPos = new Vector2(
                    Bounds.X + 70,
                    Bounds.Y + Bounds.Height / 2f - Game1.dialogueFont.MeasureString(Label).Y / 2f);

                Utility.drawTextWithShadow(b, Label, Game1.dialogueFont, textPos, Game1.textColor);
            }
            catch { /* игнорируем ошибки отрисовки отдельного компонента */ }
        }
    }

    /// <summary>
    /// NPC availability status for color-coded indicators.
    /// </summary>
    internal enum NpcStatus
    {
        Available,
        Leaving,
        Unavailable,
        Offline
    }
}
