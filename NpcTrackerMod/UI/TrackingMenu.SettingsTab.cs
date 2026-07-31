using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using NpcTrackerMod.Rendering;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary> Вкладка «Настройки»: горячие клавиши, фильтр времени, цвета и прозрачность. </summary>
    public partial class TrackingMenu
    {
        // ── Данные вкладки ────────────────────────────────────────────────────────────
        private static readonly string[] ColorNames =
        {
            "Green", "Blue", "Red", "Yellow", "Orange", "Purple", "White", "Cyan", "Pink"
        };

        private static readonly int[] TimeSteps =
        {
            -1,
            600, 700, 800, 900, 1000, 1100, 1200,
            1300, 1400, 1500, 1600, 1700, 1800,
            1900, 2000, 2100, 2200, 2300, 2400, 2500, 2600
        };

        // Alpha slider: 0.1 .. 1.0 (10 шагов)
        private static readonly float[] AlphaSteps =
        {
            0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1.0f
        };

        // ── Позиции горячих клавиш ────────────────────────────────────────────────────
        private Rectangle MenuKeyBtnRect()      => new Rectangle(BX + BOX_W - 185, BY + 103, 162, 34);
        private Rectangle DebugKeyBtnRect()     => new Rectangle(BX + BOX_W - 185, BY + 150, 162, 34);
        private Rectangle SelectNpcKeyBtnRect() => new Rectangle(BX + BOX_W - 185, BY + 197, 162, 34);

        // ── Позиции фильтра времени ───────────────────────────────────────────────────
        private int       TimeRowY     => BY + 331;
        private Rectangle TimePrevBtn() => new Rectangle(BX + BOX_W / 2 - 115, TimeRowY, 30, 30);
        private Rectangle TimeNextBtn() => new Rectangle(BX + BOX_W / 2 + 85,  TimeRowY, 30, 30);

        private Rectangle SliderTrackRect() =>
            new Rectangle(BX + PAD, TimeRowY + 44, BOX_W - PAD * 2, 8);

        private int SliderThumbX()
        {
            if (_timeFilterIndex == 0) return -1;
            var   track = SliderTrackRect();
            int   steps = TimeSteps.Length - 1;
            float t     = (float)(_timeFilterIndex - 1) / Math.Max(1, steps - 1);
            return track.X + (int)(t * track.Width);
        }

        // ── Позиции секции «Внешний вид» ─────────────────────────────────────────────
        private int AppearanceSectionY => TimeRowY + 44 + 8 + 22 + 40;  // ~BY + 449
        private int RouteColorRowY     => AppearanceSectionY + 38;
        private int PosColorRowY       => RouteColorRowY + 38;
        private int AlphaRowY          => PosColorRowY + 38;
        private int AlphaTrackY        => AlphaRowY + 28;

        // Кнопки ◄ ► для строки цвета
        private Rectangle ColorPrevBtn(int rowY) =>
            new Rectangle(BX + BOX_W - 196, rowY + 5, 24, 24);
        private Rectangle ColorNextBtn(int rowY) =>
            new Rectangle(BX + BOX_W - PAD - 24, rowY + 5, 24, 24);

        private Rectangle AlphaTrackRect() =>
            new Rectangle(BX + PAD, AlphaTrackY, BOX_W - PAD * 2, 8);

        private int AlphaThumbX()
        {
            var   track = AlphaTrackRect();
            float alpha = MathHelper.Clamp(_config.RouteAlpha, 0.1f, 1.0f);
            float t     = (alpha - 0.1f) / 0.9f;
            return track.X + (int)(t * track.Width);
        }

        // ── Отрисовка ─────────────────────────────────────────────────────────────────

        private void DrawSettingsTab(SpriteBatch b)
        {
            int x = BX + PAD;

            // Горячие клавиши
            DrawSectionHeader(b, T("settings.keybinds"), x, BY + 62);
            DrawKeybind(b, x, BY + 100, T("settings.keybind.menu"),      _config.MenuKey.ToString(),      "menu",   MenuKeyBtnRect());
            DrawKeybind(b, x, BY + 147, T("settings.keybind.debug"),     _config.DebugKey.ToString(),     "debug",  DebugKeyBtnRect());
            DrawKeybind(b, x, BY + 194, T("settings.keybind.selectNpc"), _config.SelectNpcKey.ToString(), "select", SelectNpcKeyBtnRect());

            DrawDivider(b, BY + 248);

            // Фильтр по времени
            DrawSectionHeader(b, T("settings.timeFilter"), x, BY + 264);

            DrawArrow(b, TimePrevBtn(), left: true);
            DrawArrow(b, TimeNextBtn(), left: false);

            string timeText = _state.TimeFilter < 0
                ? T("settings.timeFilter.all")
                : RouteRenderer.FormatTime(_state.TimeFilter);
            DrawCentered(b, timeText, Game1.dialogueFont, TimeRowY + 2, new Color(200, 160, 30));

            DrawTimeSlider(b);

            // Метки начала и конца трека
            var   track  = SliderTrackRect();
            float labelY = track.Bottom + 5;
            Utility.drawTextWithShadow(b, "06:00", Game1.smallFont,
                new Vector2(track.X, labelY), Color.Gray);
            var endSz = Game1.smallFont.MeasureString("02:00");
            Utility.drawTextWithShadow(b, "02:00", Game1.smallFont,
                new Vector2(track.Right - endSz.X, labelY), Color.Gray);

            // Внешний вид
            DrawDivider(b, AppearanceSectionY - 12);
            DrawSectionHeader(b, T("settings.appearance"), x, AppearanceSectionY);

            DrawColorRow(b, T("settings.routeColor"),    _routeColorIndex,    RouteColorRowY);
            DrawColorRow(b, T("settings.positionColor"), _positionColorIndex, PosColorRowY);
            DrawAlphaRow(b, x);
        }

        private void DrawTimeSlider(SpriteBatch b)
        {
            var  track    = SliderTrackRect();
            bool hovTrack = new Rectangle(track.X, track.Y - 8, track.Width, track.Height + 16)
                                .Contains(Game1.getMouseX(), Game1.getMouseY());

            b.Draw(Game1.staminaRect, track,
                hovTrack || _draggingSlider
                    ? new Color(180, 155, 100, 200)
                    : new Color(180, 155, 100, 130));

            int thumbX = SliderThumbX();
            if (thumbX >= 0)
            {
                int fillW = thumbX - track.X;
                if (fillW > 0)
                    b.Draw(Game1.staminaRect,
                        new Rectangle(track.X, track.Y, fillW, track.Height),
                        new Color(200, 160, 30, 180));

                bool thumbHov = Math.Abs(Game1.getMouseX() - thumbX) < 12;
                b.Draw(Game1.staminaRect,
                    new Rectangle(thumbX - 5, track.Y - 5, 10, track.Height + 10),
                    _draggingSlider || thumbHov
                        ? new Color(240, 195, 40)
                        : new Color(210, 165, 28));
            }
            else
            {
                b.Draw(Game1.staminaRect,
                    new Rectangle(track.X - 3, track.Y - 5, 8, track.Height + 10),
                    new Color(160, 150, 130, 180));
            }
        }

        /// <summary> Рисует строку выбора цвета: [Метка]  [◄] [▓ ColorName] [►] </summary>
        private void DrawColorRow(SpriteBatch b, string label, int colorIdx, int rowY)
        {
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(BX + PAD, rowY + 8), Game1.textColor);

            string colorName = ColorNames[colorIdx];
            var    xnaColor  = ModConfig.ParseColor(colorName, Color.White);

            DrawArrow(b, ColorPrevBtn(rowY), left: true);

            int swatchX = BX + BOX_W - 168;
            b.Draw(Game1.staminaRect, new Rectangle(swatchX, rowY + 5, 30, 24), xnaColor);

            Utility.drawTextWithShadow(b, colorName, Game1.smallFont,
                new Vector2(swatchX + 36, rowY + 8), Game1.textColor);

            DrawArrow(b, ColorNextBtn(rowY), left: false);
        }

        /// <summary> Рисует строку прозрачности со слайдером 0.1–1.0. </summary>
        private void DrawAlphaRow(SpriteBatch b, int x)
        {
            string alphaLabel = $"{T("settings.alpha")}: {_config.RouteAlpha:P0}";
            Utility.drawTextWithShadow(b, alphaLabel, Game1.smallFont,
                new Vector2(x, AlphaRowY + 8), Game1.textColor);

            var  track    = AlphaTrackRect();
            bool hovAlpha = new Rectangle(track.X, track.Y - 8, track.Width, track.Height + 16)
                                .Contains(Game1.getMouseX(), Game1.getMouseY());

            b.Draw(Game1.staminaRect, track,
                hovAlpha || _draggingAlpha
                    ? new Color(100, 160, 200, 200)
                    : new Color(100, 160, 200, 120));

            int thumbX = AlphaThumbX();
            int fillW  = thumbX - track.X;
            if (fillW > 0)
                b.Draw(Game1.staminaRect,
                    new Rectangle(track.X, track.Y, fillW, track.Height),
                    new Color(80, 140, 200, 180));

            bool thumbHov = Math.Abs(Game1.getMouseX() - thumbX) < 10;
            b.Draw(Game1.staminaRect,
                new Rectangle(thumbX - 5, track.Y - 5, 10, track.Height + 10),
                _draggingAlpha || thumbHov
                    ? new Color(60, 120, 220)
                    : new Color(60, 100, 180));
        }

        private void DrawKeybind(SpriteBatch b, int x, int y, string label, string key,
            string target, Rectangle btnRect)
        {
            bool waiting = _rebindTarget == target;

            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(x, y + 10), Game1.textColor);

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                btnRect.X, btnRect.Y, btnRect.Width, btnRect.Height,
                waiting ? new Color(255, 210, 100) : Color.White, 0.7f, false);

            string keyText = waiting ? T("settings.keybind.waiting") : key;
            var    keySz   = Game1.smallFont.MeasureString(keyText);
            Utility.drawTextWithShadow(b, keyText, Game1.smallFont,
                new Vector2(
                    btnRect.X + (btnRect.Width  - keySz.X) / 2f,
                    btnRect.Y + (btnRect.Height - keySz.Y) / 2f),
                waiting ? new Color(160, 80, 0) : Game1.textColor);
        }

        // ── Обработка кликов ──────────────────────────────────────────────────────────

        private void ClickSettings(int x, int y, bool playSound)
        {
            // Горячие клавиши
            if (MenuKeyBtnRect().Contains(x, y))      { _rebindTarget = "menu";   if (playSound) Game1.playSound("smallSelect"); return; }
            if (DebugKeyBtnRect().Contains(x, y))     { _rebindTarget = "debug";  if (playSound) Game1.playSound("smallSelect"); return; }
            if (SelectNpcKeyBtnRect().Contains(x, y)) { _rebindTarget = "select"; if (playSound) Game1.playSound("smallSelect"); return; }

            // Стрелки фильтра времени
            if (TimePrevBtn().Contains(x, y)) { ChangeTimeFilter(-1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (TimeNextBtn().Contains(x, y)) { ChangeTimeFilter(+1); if (playSound) Game1.playSound("smallSelect"); return; }

            // Слайдер времени
            var timeTrackHit = new Rectangle(
                SliderTrackRect().X, SliderTrackRect().Y - 10,
                SliderTrackRect().Width, SliderTrackRect().Height + 20);
            if (timeTrackHit.Contains(x, y))
            {
                _draggingSlider = true;
                ApplyTimeSliderX(x);
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Цвет маршрута
            if (ColorPrevBtn(RouteColorRowY).Contains(x, y))
            {
                _routeColorIndex = (_routeColorIndex - 1 + ColorNames.Length) % ColorNames.Length;
                _config.RouteColor = ColorNames[_routeColorIndex];
                _saveConfig();
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
                if (playSound) Game1.playSound("smallSelect");
                return;
            }
            if (ColorNextBtn(RouteColorRowY).Contains(x, y))
            {
                _routeColorIndex = (_routeColorIndex + 1) % ColorNames.Length;
                _config.RouteColor = ColorNames[_routeColorIndex];
                _saveConfig();
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Цвет позиции
            if (ColorPrevBtn(PosColorRowY).Contains(x, y))
            {
                _positionColorIndex = (_positionColorIndex - 1 + ColorNames.Length) % ColorNames.Length;
                _config.PositionColor = ColorNames[_positionColorIndex];
                _saveConfig();
                if (playSound) Game1.playSound("smallSelect");
                return;
            }
            if (ColorNextBtn(PosColorRowY).Contains(x, y))
            {
                _positionColorIndex = (_positionColorIndex + 1) % ColorNames.Length;
                _config.PositionColor = ColorNames[_positionColorIndex];
                _saveConfig();
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Слайдер прозрачности
            var alphaTrackHit = new Rectangle(
                AlphaTrackRect().X, AlphaTrackRect().Y - 10,
                AlphaTrackRect().Width, AlphaTrackRect().Height + 20);
            if (alphaTrackHit.Contains(x, y))
            {
                _draggingAlpha = true;
                ApplyAlphaSliderX(x);
                if (playSound) Game1.playSound("smallSelect");
            }
        }

        // ── Слайдер-логика ────────────────────────────────────────────────────────────

        private void ApplyTimeSliderX(int mouseX)
        {
            var   track  = SliderTrackRect();
            float t      = MathHelper.Clamp((float)(mouseX - track.X) / track.Width, 0f, 1f);
            int   steps  = TimeSteps.Length - 1;
            int   newIdx = 1 + (int)(t * (steps - 1) + 0.5f);
            newIdx = MathHelper.Clamp(newIdx, 1, TimeSteps.Length - 1);
            if (newIdx == _timeFilterIndex) return;
            _timeFilterIndex      = newIdx;
            _state.TimeFilter     = TimeSteps[_timeFilterIndex];
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        private void ApplyAlphaSliderX(int mouseX)
        {
            var   track    = AlphaTrackRect();
            float t        = MathHelper.Clamp((float)(mouseX - track.X) / track.Width, 0f, 1f);
            int   idx      = (int)(t * (AlphaSteps.Length - 1) + 0.5f);
            idx = MathHelper.Clamp(idx, 0, AlphaSteps.Length - 1);
            float newAlpha = AlphaSteps[idx];
            if (Math.Abs(newAlpha - _config.RouteAlpha) < 0.001f) return;
            _config.RouteAlpha = newAlpha;
            _tiles.Alpha       = newAlpha;
            _saveConfig();
        }

        private void ChangeTimeFilter(int delta)
        {
            _timeFilterIndex    = MathHelper.Clamp(_timeFilterIndex + delta, 0, TimeSteps.Length - 1);
            _state.TimeFilter   = TimeSteps[_timeFilterIndex];
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        // ── Утилиты ───────────────────────────────────────────────────────────────────

        private static int ColorIndexOf(string colorName)
        {
            int idx = Array.FindIndex(ColorNames,
                n => string.Equals(n, colorName, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? idx : 0;
        }
    }
}
