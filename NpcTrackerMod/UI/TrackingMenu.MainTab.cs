using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NpcTrackerMod.Rendering;
using StardewValley;

namespace NpcTrackerMod.UI
{
    /// <summary> Вкладка «Главное»: переключатели режимов, навигатор шагов и выбор варианта расписания. </summary>
    public partial class TrackingMenu
    {
        // ── Позиции навигатора шагов ──────────────────────────────────────────────────
        private Rectangle StepPrevBtn(int navY) => new Rectangle(BX + BOX_W / 2 - 115, navY, 30, 30);
        private Rectangle StepNextBtn(int navY) => new Rectangle(BX + BOX_W / 2 + 85,  navY, 30, 30);

        // Высота навигатора шагов: стрелки (36) + ключ расписания (26) + подсказка (26) = 88.
        // Константа позволяет точно знать, где начинается блок вариантов.
        private const int STEP_NAV_H = 88;

        // ── Построение чекбоксов ──────────────────────────────────────────────────────

        private void BuildMainChecks()
        {
            int x = BX + PAD + 6;
            int y = BY + 90;

            AddCheck(ref y, x, T("main.enable"),
                () => _state.EnableDisplay, v => _state.EnableDisplay = v);
            AddCheck(ref y, x, T("main.grid"),
                () => _state.DisplayGrid, v => _state.DisplayGrid = v);

            y += 36;
            y += 28;

            AddCheck(ref y, x, T("main.allLocations"),
                () => _state.SwitchTargetLocations, v =>
                {
                    _state.SwitchTargetLocations = v;
                    _tiles.Clear();
                    _state.SwitchGetNpcPath = true;
                    _registry.CurrentNpcList.Clear();
                    _state.SwitchListFull = false;
                });
            AddCheck(ref y, x, T("main.globalRoute"),
                () => _state.SwitchGlobalNpcPath, v =>
                {
                    _state.SwitchGlobalNpcPath = v;
                    _tiles.Clear();
                    _state.SwitchGetNpcPath = true;
                });
            AddCheck(ref y, x, T("main.stepMode"),
                () => _state.RouteStepMode, v =>
                {
                    _state.RouteStepMode        = v;
                    _state.RouteStepIndex       = 0;
                    _state.RouteStepScheduleKey = null;
                    _state.SelectedVariantKey   = null;
                    _state.SwitchBuildVariant   = false;
                    _tiles.Clear();
                    _state.SwitchGetNpcPath = true;
                });
        }

        private void AddCheck(ref int y, int x, string label,
            Func<bool> getter, Action<bool> setter)
        {
            _mainChecks.Add(new ClickableCheckbox(
                new Rectangle(x, y, 400, 36), label, getter(), setter));
            y += 44;
        }

        // ── Отрисовка ─────────────────────────────────────────────────────────────────

        private void DrawMainTab(SpriteBatch b)
        {
            if (_mainChecks.Count < 5) return;

            int x = BX + PAD + 6;

            int g1Y = _mainChecks[0].Bounds.Y - 30;
            DrawGroupHeader(b, T("main.group.display"), x, g1Y);
            _mainChecks[0].Draw(b);
            _mainChecks[1].Draw(b);

            int divY = _mainChecks[1].Bounds.Bottom + 14;
            DrawDivider(b, divY);

            int g2Y = _mainChecks[2].Bounds.Y - 30;
            DrawGroupHeader(b, T("main.group.routes"), x, g2Y);
            _mainChecks[2].Draw(b);
            _mainChecks[3].Draw(b);
            _mainChecks[4].Draw(b);

            // Навигатор шагов — только когда пошаговый режим активен и есть данные
            bool showNav = _state.RouteStepMode
                           && _state.SwitchTargetNPC
                           && !_state.SwitchGlobalNpcPath
                           && _state.RouteStepTotal > 0;

            int navY = _mainChecks[4].Bounds.Bottom + 14;

            if (showNav)
            {
                DrawArrow(b, StepPrevBtn(navY), left: true);
                DrawArrow(b, StepNextBtn(navY), left: false);

                string stepLabel = T("main.stepOf", new
                {
                    index = _state.RouteStepIndex + 1,
                    total = _state.RouteStepTotal,
                    time  = RouteRenderer.FormatTime(_state.RouteStepTime)
                });
                DrawCentered(b, stepLabel, Game1.dialogueFont, navY + 2, new Color(200, 160, 30));

                if (!string.IsNullOrEmpty(_state.RouteStepScheduleKey))
                    DrawCentered(b,
                        TruncateToWidth(Game1.smallFont, _state.RouteStepScheduleKey, BOX_W - PAD * 2),
                        Game1.smallFont, navY + 36, new Color(130, 100, 60));

                if (_state.RouteStepTotal > 1)
                    DrawCentered(b, T("main.stepHint"), Game1.smallFont, navY + 54, Color.Gray);
            }

            // Селектор вариантов — показывается когда пошаговый режим включён,
            // выбран NPC и у него есть несколько вариантов расписания.
            bool showVariants = _state.RouteStepMode
                                && _state.SwitchTargetNPC
                                && !_state.SwitchGlobalNpcPath
                                && HasVariantKeys();

            if (!showVariants) return;

            // Блок вариантов начинается ниже навигатора шагов (или ниже checkbox, если нет данных).
            int variantBlockY = showNav
                ? navY + STEP_NAV_H + 8
                : navY + 8;

            DrawDivider(b, variantBlockY);
            DrawGroupHeader(b, T("main.variant.label"), x, variantBlockY + 10);

            int chipStartX = x;
            int chipStartY = variantBlockY + 34;
            int chipMaxW   = BOX_W - PAD * 2 - 12;

            var chips = ComputeVariantChipRects(chipStartX, chipStartY, chipMaxW);
            DrawVariantChips(b, chips);
        }

        // ── Обработка кликов ──────────────────────────────────────────────────────────

        private void ClickMain(int x, int y, bool playSound)
        {
            foreach (var cb in _mainChecks)
            {
                if (!cb.ContainsPoint(x, y)) continue;
                cb.Toggle();
                if (playSound) Game1.playSound("drumkit6");
                return;
            }

            // Навигатор шагов — обрабатываем только когда данные загружены
            if (_state.RouteStepMode && _state.SwitchTargetNPC
                && !_state.SwitchGlobalNpcPath && _state.RouteStepTotal > 0
                && _mainChecks.Count >= 5)
            {
                int navY = _mainChecks[4].Bounds.Bottom + 14;
                if (StepPrevBtn(navY).Contains(x, y)) { ChangeStep(-1); if (playSound) Game1.playSound("smallSelect"); return; }
                if (StepNextBtn(navY).Contains(x, y)) { ChangeStep(+1); if (playSound) Game1.playSound("smallSelect"); return; }
            }

            // Чипы выбора варианта расписания
            if (_state.RouteStepMode && _state.SwitchTargetNPC
                && !_state.SwitchGlobalNpcPath && HasVariantKeys()
                && _mainChecks.Count >= 5)
            {
                bool showNav = _state.RouteStepTotal > 0;
                int navY     = _mainChecks[4].Bounds.Bottom + 14;
                int variantBlockY = showNav ? navY + STEP_NAV_H + 8 : navY + 8;

                int chipStartX = BX + PAD + 6;
                int chipStartY = variantBlockY + 34;
                int chipMaxW   = BOX_W - PAD * 2 - 12;

                var chips = ComputeVariantChipRects(chipStartX, chipStartY, chipMaxW);
                foreach (var (key, rect) in chips)
                {
                    if (!rect.Contains(x, y)) continue;

                    SelectVariant(key);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }
            }
        }

        // ── Утилиты ───────────────────────────────────────────────────────────────────

        /// <summary> Переключает шаг маршрута и инициирует перерисовку тайлов. </summary>
        private void ChangeStep(int delta)
        {
            if (_state.RouteStepTotal <= 0) return;
            _state.RouteStepIndex = MathHelper.Clamp(
                _state.RouteStepIndex + delta, 0, _state.RouteStepTotal - 1);
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        /// <summary>
        /// Применяет выбор варианта расписания.
        /// null = вернуться к активному дневному расписанию.
        /// Непустой ключ = запросить построение варианта через SwitchBuildVariant.
        /// </summary>
        private void SelectVariant(string key)
        {
            _state.SelectedVariantKey = key;
            _state.RouteStepIndex     = 0;

            if (key == null)
            {
                // Возврат к активному дневному расписанию — данные уже построены.
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
            }
            else
            {
                // Запрашиваем построение — обработается в ModEntry.OnUpdateTicked.
                _state.SwitchBuildVariant = true;
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
            }
        }

        /// <summary>
        /// Возвращает true, если у текущего NPC есть хотя бы один ключ варианта расписания.
        /// </summary>
        private bool HasVariantKeys()
        {
            string npcName = _registry.CurrentNpcName;
            if (string.IsNullOrEmpty(npcName)) return false;
            return _registry.NpcVariantKeys.TryGetValue(npcName, out var keys)
                   && keys != null && keys.Count > 0;
        }

        /// <summary>
        /// Вычисляет прямоугольники для чипов вариантов расписания.
        /// Первый элемент всегда «Сегодня» (null-ключ = активное расписание),
        /// затем все известные ключи по порядку.
        /// Чипы располагаются слева направо с переносом на следующую строку;
        /// ширина одного чипа ограничена шириной окна.
        /// </summary>
        private List<(string key, Rectangle rect)> ComputeVariantChipRects(
            int startX, int startY, int maxWidth)
        {
            var result = new List<(string key, Rectangle rect)>();

            int cx     = startX;
            int cy     = startY;
            int chipH  = 28;
            int chipGap = 5;

            // Первый чип — активное дневное расписание
            string todayLabel = T("main.variant.active");
            int todayW = Math.Min(
                (int)Game1.smallFont.MeasureString(todayLabel).X + 16, maxWidth);
            result.Add((null, new Rectangle(cx, cy, todayW, chipH)));
            cx += todayW + chipGap;

            // Остальные ключи
            if (_registry.NpcVariantKeys.TryGetValue(_registry.CurrentNpcName ?? "", out var keys))
            {
                foreach (string key in keys)
                {
                    int w = Math.Min(
                        (int)Game1.smallFont.MeasureString(key).X + 16, maxWidth);
                    if (cx + w > startX + maxWidth)
                    {
                        cx  = startX;
                        cy += chipH + chipGap;
                    }
                    result.Add((key, new Rectangle(cx, cy, w, chipH)));
                    cx += w + chipGap;
                }
            }

            return result;
        }

        /// <summary>
        /// Рисует чипы вариантов расписания. Выбранный чип подсвечивается жёлтым.
        /// За пределами нижней границы окна чипы не рисуются.
        /// </summary>
        private void DrawVariantChips(SpriteBatch b, List<(string key, Rectangle rect)> chips)
        {
            foreach (var (key, rect) in chips)
            {
                if (rect.Bottom > BY + BOX_H - PAD) break;

                bool isSelected = key == null
                    ? _state.SelectedVariantKey == null
                    : key == _state.SelectedVariantKey;

                var bgColor = isSelected
                    ? new Color(255, 215, 120)
                    : new Color(215, 205, 188);

                string label = key ?? T("main.variant.active");

                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    rect.X, rect.Y, rect.Width, rect.Height, bgColor, 0.85f, false);

                // Обрезаем текст по ширине чипа, чтобы не выходил за рамки.
                string shown = TruncateToWidth(
                    Game1.smallFont, label, rect.Width - 16);
                var    sz    = Game1.smallFont.MeasureString(shown);
                Utility.drawTextWithShadow(b, shown, Game1.smallFont,
                    new Vector2(rect.X + 8, rect.Y + (rect.Height - sz.Y) / 2f),
                    isSelected ? new Color(110, 65, 15) : Game1.textColor);
            }
        }
    }
}
