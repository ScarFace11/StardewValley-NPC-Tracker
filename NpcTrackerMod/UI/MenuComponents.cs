using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary>
    /// SDV-style toggle switch with track and handle.
    /// ON: golden track with bright handle.
    /// OFF: dark track with gray handle.
    /// Draws label to the right of the toggle.
    /// </summary>
    internal class SDVToggle
    {
        public Rectangle  Bounds    { get; }
        public string     Label     { get; }
        public string     Tooltip   { get; set; }
        public bool       IsOn      { get; private set; }

        private readonly Action<bool> _onToggle;

        // Toggle dimensions
        private const int TOGGLE_W = 36;
        private const int TOGGLE_H = 18;
        private const int HANDLE_SIZE = 14;

        public SDVToggle(Rectangle bounds, string label, bool initial, Action<bool> onToggle, string tooltip = null)
        {
            Bounds = bounds;
            Label = label;
            Tooltip = tooltip;
            IsOn = initial;
            _onToggle = onToggle;
        }

        public bool ContainsPoint(int x, int y) => Bounds.Contains(x, y);

        public void Toggle()
        {
            IsOn = !IsOn;
            _onToggle?.Invoke(IsOn);
        }

        public void Draw(SpriteBatch b)
        {
            try
            {
                // Toggle track position: centered vertically, left side of bounds
                int toggleX = Bounds.X;
                int toggleY = Bounds.Y + (Bounds.Height - TOGGLE_H) / 2;

                // Track background
                Color trackColor = IsOn
                    ? new Color(180, 145, 50)   // SDV gold
                    : new Color(90, 80, 65);     // dark brown

                // Draw track as rounded rectangle
                IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    toggleX, toggleY, TOGGLE_W, TOGGLE_H,
                    trackColor, 1f, false);

                // Handle position: slides left/right
                int handleX = IsOn
                    ? toggleX + TOGGLE_W - HANDLE_SIZE - 2
                    : toggleX + 2;
                int handleY = toggleY + (TOGGLE_H - HANDLE_SIZE) / 2;

                // Handle glow when ON
                if (IsOn)
                {
                    var glow = new Rectangle(handleX - 1, handleY - 1, HANDLE_SIZE + 2, HANDLE_SIZE + 2);
                    b.Draw(Game1.staminaRect, glow, new Color(255, 215, 80, 120));
                }

                // Handle
                Color handleColor = IsOn
                    ? new Color(255, 240, 180)
                    : new Color(170, 160, 140);
                b.Draw(Game1.staminaRect,
                    new Rectangle(handleX, handleY, HANDLE_SIZE, HANDLE_SIZE),
                    handleColor);

                // Label to the right of toggle
                float labelX = toggleX + TOGGLE_W + 10;
                float labelY = Bounds.Y + (Bounds.Height - Game1.dialogueFont.MeasureString(Label).Y) / 2f;

                // Dimmed text when disabled (tracker off)
                Color labelColor = IsOn
                    ? Game1.textColor
                    : new Color(150, 140, 125);

                Utility.drawTextWithShadow(b, Label, Game1.dialogueFont,
                    new Vector2(labelX, labelY), labelColor);
            }
            catch { /* ignore render errors on individual component */ }
        }
    }

    /// <summary>
    /// A row in the NPC list: colored indicator dot + bold name + gray location/time.
    /// The entire row is clickable.
    /// </summary>
    internal class NpcRow
    {
        public Rectangle Bounds { get; }
        public string Name { get; }
        public string Location { get; }
        public int Time { get; }
        public NpcStatus Status { get; }
        public bool IsSelected { get; set; }

        public NpcRow(Rectangle bounds, string name, string location, int time, NpcStatus status, bool selected)
        {
            Bounds = bounds;
            Name = name;
            Location = location;
            Time = time;
            Status = status;
            IsSelected = selected;
        }

        public bool ContainsPoint(int x, int y) => Bounds.Contains(x, y);
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

    /// <summary>
    /// SDV-style radio button with circular indicator.
    /// </summary>
    internal class SDVRadioButton
    {
        public Rectangle Bounds { get; }
        public string Label { get; }
        public string Tooltip { get; set; }
        public bool IsSelected { get; set; }
        public Action OnSelect { get; }

        private const int CIRCLE_SIZE = 16;

        public SDVRadioButton(Rectangle bounds, string label, bool selected, Action onSelect, string tooltip = null)
        {
            Bounds = bounds;
            Label = label;
            Tooltip = tooltip;
            IsSelected = selected;
            OnSelect = onSelect;
        }

        public SDVRadioButton() { }

        public bool ContainsPoint(int x, int y) => Bounds.Contains(x, y);

        public void Draw(SpriteBatch b)
        {
            try
            {
                // Radio circle position: centered vertically, left side
                int circleX = Bounds.X;
                int circleY = Bounds.Y + (Bounds.Height - CIRCLE_SIZE) / 2;

                // Outer circle (dark)
                b.Draw(Game1.staminaRect,
                    new Rectangle(circleX, circleY, CIRCLE_SIZE, CIRCLE_SIZE),
                    new Color(90, 80, 65));

                // Inner fill when selected (golden)
                if (IsSelected)
                {
                    int inset = 3;
                    b.Draw(Game1.staminaRect,
                        new Rectangle(circleX + inset, circleY + inset,
                            CIRCLE_SIZE - inset * 2, CIRCLE_SIZE - inset * 2),
                        new Color(255, 210, 60));
                }

                // Label
                float labelX = circleX + CIRCLE_SIZE + 8;
                float labelY = Bounds.Y + (Bounds.Height - Game1.smallFont.MeasureString(Label).Y) / 2f;
                Color labelColor = IsSelected ? Game1.textColor : new Color(140, 130, 115);

                Utility.drawTextWithShadow(b, Label, Game1.smallFont,
                    new Vector2(labelX, labelY), labelColor);
            }
            catch { }
        }
    }
}
