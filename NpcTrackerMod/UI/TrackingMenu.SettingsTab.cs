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

        // Индекс «Свой цвет» (HEX-строка из палитры) — последняя позиция цикла.
        private const int CUSTOM_COLOR_INDEX = 9;

        // Палитра из 36 цветов: 12 колонок × 3 строки.
        private static readonly string[] PaletteHex =
        {
            "#B71C1C", "#D32F2F", "#F44336", "#E53935", "#FF7043", "#FF9800",
            "#FFA726", "#F57C00", "#FFB300", "#FFD54F", "#FFEE58", "#FDD835",
            "#9CCC65", "#7CB342", "#8BC34A", "#43A047", "#4CAF50", "#26A69A",
            "#009688", "#00ACC1", "#00BCD4", "#0288D1", "#039BE5", "#29B6F6",
            "#5C6BC0", "#3F51B5", "#7E57C2", "#9C27B0", "#AB47BC", "#EC407A",
            "#F06292", "#AD1457", "#8D6E63", "#795548", "#607D8B", "#9E9E9E"
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

        // Актуальные цвета «Свой цвет» (заполняются из конфига при открытии меню).
        private Color _customRouteColor = Color.Green;
        private Color _customPosColor   = Color.Blue;

        // Какая строка цвета открыла палитру ("route"/"position"/null).
        private string _pickingColor;

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

        private Rectangle TimeTrackHitRect() =>
            new Rectangle(SliderTrackRect().X, SliderTrackRect().Y - 10,
                SliderTrackRect().Width, SliderTrackRect().Height + 20);

        private int SliderThumbX()
        {
            var   track = SliderTrackRect();
            int   steps = TimeSteps.Length - 1;
            float t     = (float)_timeFilterIndex / Math.Max(1, steps);
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

        // Свотч цвета (клик — открыть палитру)
        private Rectangle ColorSwatchRect(int rowY) =>
            new Rectangle(BX + BOX_W - 168, rowY + 5, 30, 24);

        private Rectangle AlphaTrackRect() =>
            new Rectangle(BX + PAD, AlphaTrackY, BOX_W - PAD * 2, 8);

        private Rectangle AlphaTrackHitRect() =>
            new Rectangle(AlphaTrackRect().X, AlphaTrackRect().Y - 10,
                AlphaTrackRect().Width, AlphaTrackRect().Height + 20);

        private int AlphaThumbX()
        {
            var   track = AlphaTrackRect();
            float alpha = MathHelper.Clamp(_config.RouteAlpha, 0.1f, 1.0f);
            float t     = (alpha - 0.1f) / 0.9f;
            return track.X + (int)(t * track.Width);
        }

        // ── Нижняя строка: сброс настроек ────────────────────────────────────────────
        private Rectangle ResetSettingsBtnRect() =>
            new Rectangle(BX + BOX_W / 2 - 120, BY + 612, 240, 34);

        // ── Палитра цветов ────────────────────────────────────────────────────────────
        private Rectangle PalettePanelRect() =>
            new Rectangle(BX + PAD, BY + 478, BOX_W - PAD * 2, 118);

        private Rectangle PaletteCellRect(int index)
        {
            var panel = PalettePanelRect();
            int col   = index % 12;
            int row   = index / 12;
            return new Rectangle(panel.X + 8 + col * 42, panel.Y + 36 + row * 24, 40, 20);
        }

        // ── Отрисовка ─────────────────────────────────────────────────────────────────

        private void DrawSettingsTab(SpriteBatch b)
        {
            int x = BX + PAD;
            var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

            // Горячие клавиши
            DrawSectionHeader(b, T("settings.keybinds"), x, BY + 62);
            DrawKeybind(b, x, BY + 100, T("settings.keybind.menu"),      _config.MenuKey.ToString(),      "menu",   MenuKeyBtnRect());
            DrawKeybind(b, x, BY + 147, T("settings.keybind.debug"),     _config.DebugKey.ToString(),     "debug",  DebugKeyBtnRect());
            DrawKeybind(b, x, BY + 194, T("settings.keybind.selectNpc"), _config.SelectNpcKey.ToString(), "select", SelectNpcKeyBtnRect());

            TipIfHover(b, MenuKeyBtnRect(),      "settings.keybind.tip");
            TipIfHover(b, DebugKeyBtnRect(),     "settings.keybind.tip");
            TipIfHover(b, SelectNpcKeyBtnRect(), "settings.keybind.tip");

            DrawDivider(b, BY + 248);

            // Фильтр по времени
            DrawSectionHeader(b, T("settings.timeFilter"), x, BY + 264);

            DrawArrow(b, TimePrevBtn(), left: true);
            DrawArrow(b, TimeNextBtn(), left: false);
            TipIfHover(b, TimePrevBtn(), "settings.timeFilter.tip");
            TipIfHover(b, TimeNextBtn(), "settings.timeFilter.tip");

            string timeText = _state.TimeFilter < 0
                ? T("settings.timeFilter.all")
                : RouteRenderer.FormatTime(_state.TimeFilter);
            DrawCentered(b, timeText, Game1.dialogueFont, TimeRowY + 2, new Color(200, 160, 30));

            DrawTimeSlider(b);
            TipIfHover(b, TimeTrackHitRect(), "settings.timeFilter.tip");

            // Метки начала и конца трека: слева — «всё время» (фильтр выключен)
            var   track  = SliderTrackRect();
            float labelY = track.Bottom + 5;
            Utility.drawTextWithShadow(b, T("settings.timeFilter.all"), Game1.smallFont,
                new Vector2(track.X, labelY), Color.Gray);
            var endSz = Game1.smallFont.MeasureString("02:00");
            Utility.drawTextWithShadow(b, "02:00", Game1.smallFont,
                new Vector2(track.Right - endSz.X, labelY), Color.Gray);

            // Внешний вид
            DrawDivider(b, AppearanceSectionY - 12);
            DrawSectionHeader(b, T("settings.appearance"), x, AppearanceSectionY);

            DrawColorRow(b, T("settings.routeColor"),    isRoute: true,  RouteColorRowY);
            DrawColorRow(b, T("settings.positionColor"), isRoute: false, PosColorRowY);
            DrawAlphaRow(b, x);

            TipIfHover(b, ColorPrevBtn(RouteColorRowY), "settings.routeColor.tip");
            TipIfHover(b, ColorNextBtn(RouteColorRowY), "settings.routeColor.tip");
            TipIfHover(b, ColorSwatchRect(RouteColorRowY), "settings.routeColor.tip");
            TipIfHover(b, ColorPrevBtn(PosColorRowY), "settings.positionColor.tip");
            TipIfHover(b, ColorNextBtn(PosColorRowY), "settings.positionColor.tip");
            TipIfHover(b, ColorSwatchRect(PosColorRowY), "settings.positionColor.tip");
            TipIfHover(b, AlphaTrackHitRect(), "settings.alpha.tip");

            // Нижняя строка: сброс настроек
            DrawResetSettingsButton(b);

            // Палитра — поверх секции внешнего вида, пока открыта
            if (_pickingColor != null)
                DrawPalette(b);
        }

        private void TipIfHover(SpriteBatch b, Rectangle rect, string key)
        {
            if (_hoverText != null) return;
            if (rect.Contains(Game1.getMouseX(), Game1.getMouseY()))
                _hoverText = T(key);
        }

        private void DrawTimeSlider(SpriteBatch b)
        {
            var  track    = SliderTrackRect();
            bool hovTrack = TimeTrackHitRect()
                                .Contains(Game1.getMouseX(), Game1.getMouseY());

            b.Draw(Game1.staminaRect, track,
                hovTrack || _draggingSlider
                    ? new Color(180, 155, 100, 200)
                    : new Color(180, 155, 100, 130));

            int thumbX = SliderThumbX();
            int fillW  = thumbX - track.X;
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

        /// <summary> Рисует строку выбора цвета: [Метка]  [◄] [▓] [Name] [►] (свотч кликабелен). </summary>
        private void DrawColorRow(SpriteBatch b, string label, bool isRoute, int rowY)
        {
            int colorIdx = isRoute ? _routeColorIndex : _positionColorIndex;

            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(BX + PAD, rowY + 8), Game1.textColor);

            string colorName = colorIdx >= CUSTOM_COLOR_INDEX
                ? T("settings.custom")
                : ColorNames[colorIdx];
            var    xnaColor  = EffectiveColor(isRoute);

            DrawArrow(b, ColorPrevBtn(rowY), left: true);

            int swatchX = BX + BOX_W - 168;
            b.Draw(Game1.staminaRect, new Rectangle(swatchX, rowY + 5, 30, 24), xnaColor);

            Utility.drawTextWithShadow(b, colorName, Game1.smallFont,
                new Vector2(swatchX + 36, rowY + 8), Game1.textColor);

            DrawArrow(b, ColorNextBtn(rowY), left: false);
        }

        /// <summary> Актуальный цвет строки (именованный или «Свой цвет»). </summary>
        private Color EffectiveColor(bool isRoute) =>
            isRoute
                ? (_routeColorIndex >= CUSTOM_COLOR_INDEX
                    ? _customRouteColor
                    : ModConfig.ParseColor(ColorNames[_routeColorIndex], Color.White))
                : (_positionColorIndex >= CUSTOM_COLOR_INDEX
                    ? _customPosColor
                    : ModConfig.ParseColor(ColorNames[_positionColorIndex], Color.White));

        /// <summary> Рисует строку прозрачности со слайдером 0.1–1.0. </summary>
        private void DrawAlphaRow(SpriteBatch b, int x)
        {
            string alphaLabel = $"{T("settings.alpha")}: {_config.RouteAlpha:P0}";
            Utility.drawTextWithShadow(b, alphaLabel, Game1.smallFont,
                new Vector2(x, AlphaRowY + 8), Game1.textColor);

            var  track    = AlphaTrackRect();
            bool hovAlpha = AlphaTrackHitRect()
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

        private void DrawResetSettingsButton(SpriteBatch b)
        {
            var  rect = ResetSettingsBtnRect();
            bool hov  = rect.Contains(Game1.getMouseX(), Game1.getMouseY());

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height,
                hov ? new Color(190, 140, 90) : new Color(160, 120, 80), 0.85f, false);

            string label = T("settings.reset");
            var    sz    = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(rect.X + (rect.Width  - sz.X) / 2f,
                            rect.Y + (rect.Height - sz.Y) / 2f),
                Color.White);

            if (hov) _hoverText = T("settings.reset.tip");
        }

        /// <summary> Рисует палитру 12×3 цветов поверх секции внешнего вида. </summary>
        private void DrawPalette(SpriteBatch b)
        {
            var panel = PalettePanelRect();
            var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                panel.X, panel.Y, panel.Width, panel.Height, Color.White, 1f, false);

            string title = T("settings.palette.title");
            var    sz    = Game1.smallFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.smallFont,
                new Vector2(panel.X + (panel.Width - sz.X) / 2f, panel.Y + 10),
                new Color(90, 70, 50));

            for (int i = 0; i < PaletteHex.Length; i++)
            {
                var rect = PaletteCellRect(i);
                b.Draw(Game1.staminaRect, rect,
                    ModConfig.ParseColor(PaletteHex[i], Color.White));

                if (rect.Contains(mouse.X, mouse.Y))
                    b.Draw(Game1.staminaRect,
                        new Rectangle(rect.X - 2, rect.Y - 2, rect.Width + 4, 2), Color.White);
            }
        }

        // ── Обработка кликов ──────────────────────────────────────────────────────────

        private void ClickSettings(int x, int y, bool playSound)
        {
            // Открытая палитра перехватывает клики: внутри — выбор цвета, мимо — закрыть.
            if (_pickingColor != null)
            {
                for (int i = 0; i < PaletteHex.Length; i++)
                {
                    if (!PaletteCellRect(i).Contains(x, y)) continue;
                    ApplyPaletteColor(PaletteHex[i]);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }

                _pickingColor = null;
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Горячие клавиши
            if (MenuKeyBtnRect().Contains(x, y))      { StartRebind("menu");   if (playSound) Game1.playSound("smallSelect"); return; }
            if (DebugKeyBtnRect().Contains(x, y))     { StartRebind("debug");  if (playSound) Game1.playSound("smallSelect"); return; }
            if (SelectNpcKeyBtnRect().Contains(x, y)) { StartRebind("select"); if (playSound) Game1.playSound("smallSelect"); return; }

            // Стрелки фильтра времени
            if (TimePrevBtn().Contains(x, y)) { ChangeTimeFilter(-1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (TimeNextBtn().Contains(x, y)) { ChangeTimeFilter(+1); if (playSound) Game1.playSound("smallSelect"); return; }

            // Слайдер времени
            if (TimeTrackHitRect().Contains(x, y))
            {
                _draggingSlider = true;
                ApplyTimeSliderX(x);
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Цвет маршрута
            if (ColorPrevBtn(RouteColorRowY).Contains(x, y)) { CycleRouteColor(-1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (ColorNextBtn(RouteColorRowY).Contains(x, y)) { CycleRouteColor(+1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (ColorSwatchRect(RouteColorRowY).Contains(x, y))
            {
                _pickingColor = "route";
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Цвет позиции
            if (ColorPrevBtn(PosColorRowY).Contains(x, y)) { CyclePosColor(-1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (ColorNextBtn(PosColorRowY).Contains(x, y)) { CyclePosColor(+1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (ColorSwatchRect(PosColorRowY).Contains(x, y))
            {
                _pickingColor = "position";
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Слайдер прозрачности
            if (AlphaTrackHitRect().Contains(x, y))
            {
                _draggingAlpha = true;
                ApplyAlphaSliderX(x);
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Сброс настроек
            if (ResetSettingsBtnRect().Contains(x, y))
            {
                ResetSettings();
                if (playSound) Game1.playSound("bigDeSelect");
            }
        }

        // ── Общие операции (мышь и геймпад используют один путь) ───────────────────────

        private void StartRebind(string target) => _rebindTarget = target;

        private void CycleRouteColor(int dir)
        {
            _routeColorIndex = (_routeColorIndex + dir + CUSTOM_COLOR_INDEX + 1)
                               % (CUSTOM_COLOR_INDEX + 1);

            _config.RouteColor = _routeColorIndex >= CUSTOM_COLOR_INDEX
                ? ColorToHex(_customRouteColor)
                : ColorNames[_routeColorIndex];

            _saveConfig();
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        private void CyclePosColor(int dir)
        {
            _positionColorIndex = (_positionColorIndex + dir + CUSTOM_COLOR_INDEX + 1)
                                  % (CUSTOM_COLOR_INDEX + 1);

            _config.PositionColor = _positionColorIndex >= CUSTOM_COLOR_INDEX
                ? ColorToHex(_customPosColor)
                : ColorNames[_positionColorIndex];

            _saveConfig();
        }

        private void ApplyPaletteColor(string hex)
        {
            var c = ModConfig.ParseColor(hex, Color.White);

            if (_pickingColor == "route")
            {
                _customRouteColor = c;
                _config.RouteColor = hex;
                _routeColorIndex  = CUSTOM_COLOR_INDEX;
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
            }
            else
            {
                _customPosColor   = c;
                _config.PositionColor = hex;
                _positionColorIndex   = CUSTOM_COLOR_INDEX;
            }

            _saveConfig();
            _pickingColor = null;
        }

        private void ResetSettings()
        {
            _config.ResetToDefaults();
            _saveConfig();

            _routeColorIndex    = 0;  // Green
            _positionColorIndex = 1;  // Blue
            _customRouteColor   = Color.Green;
            _customPosColor     = Color.Blue;
            _timeFilterIndex    = 0;
            _state.TimeFilter   = -1;
            _pickingColor       = null;

            _tiles.Alpha = _config.RouteAlpha;
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        // ── Слайдер-логика ────────────────────────────────────────────────────────────

        private void ApplyTimeSliderX(int mouseX)
        {
            var   track  = SliderTrackRect();
            float t      = MathHelper.Clamp((float)(mouseX - track.X) / track.Width, 0f, 1f);
            int   steps  = TimeSteps.Length - 1;
            int   newIdx = (int)(t * steps + 0.5f);
            newIdx = MathHelper.Clamp(newIdx, 0, TimeSteps.Length - 1);
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

        private static string ColorToHex(Color c) =>
            $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static int ColorIndexOf(string colorName)
        {
            if (!string.IsNullOrEmpty(colorName) && colorName[0] == '#')
                return CUSTOM_COLOR_INDEX;

            int idx = Array.FindIndex(ColorNames,
                n => string.Equals(n, colorName, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? idx : 0;
        }
    }
}
