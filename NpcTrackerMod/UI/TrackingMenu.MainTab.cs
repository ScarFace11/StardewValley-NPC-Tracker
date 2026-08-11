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

        // Таймлайн шагов: кликабельная шкала с отметками шагов и бегунком.
        private Rectangle TimelineRect(int navY) =>
            new Rectangle(BX + PAD + 6, navY + 56, BOX_W - PAD * 2 - 12, 16);

        // Высота навигатора шагов: подпись (30) + ключ расписания (22) + таймлайн (20).
        // Константа позволяет точно знать, где начинается блок вариантов.
        private const int STEP_NAV_H = 88;

        // Ключи тултипов для чекбоксов (порядок совпадает с _mainChecks).
        private static readonly string[] MainCheckTips =
        {
            "main.enable.tip",
            "main.grid.tip",
            "main.allLocations.tip",
            "main.globalRoute.tip",
            "main.stepMode.tip"
        };

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
            var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

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

            // Hover-тултипы чекбоксов — только при наведении.
            for (int i = 0; i < _mainChecks.Count; i++)
            {
                if (_mainChecks[i].ContainsPoint(mouse.X, mouse.Y))
                    _hoverText = T(MainCheckTips[i]);
            }

            int navY = _mainChecks[4].Bounds.Bottom + 14;

            // Пошаговый режим: навигатор или дружелюбная подсказка, что мешает.
            if (_state.RouteStepMode)
            {
                if (StepNavVisible())
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
                            Game1.smallFont, navY + 34, new Color(130, 100, 60));

                    DrawTimeline(b, navY);

                    // Тултип таймлайна.
                    if (StepPrevBtn(navY).Contains(mouse.X, mouse.Y) ||
                        StepNextBtn(navY).Contains(mouse.X, mouse.Y) ||
                        TimelineRect(navY).Contains(mouse.X, mouse.Y))
                    {
                        _hoverText = T("main.stepHint");
                    }
                }
                else
                {
                    string hint;
                    if (!_state.SwitchTargetNPC)
                        hint = T("main.stepNoNpc");
                    else if (_state.SwitchGlobalNpcPath)
                        hint = T("main.stepNoGlobal");
                    else
                        hint = T("main.stepNoData");

                    DrawCentered(b, hint, Game1.smallFont, navY, new Color(120, 110, 90));
                }
            }

            // Селектор вариантов — когда пошаговый режим включён,
            // выбран NPC и у него есть несколько вариантов расписания.
            if (!VariantsVisible()) return;

            int variantBlockY = VariantsBlockY();

            DrawDivider(b, variantBlockY);
            DrawGroupHeader(b, T("main.variant.label"), x, variantBlockY + 10);

            int chipStartX = x;
            int chipStartY = variantBlockY + 34;
            int chipMaxW   = BOX_W - PAD * 2 - 12;

            var chips = ComputeVariantChipRects(chipStartX, chipStartY, chipMaxW);
            DrawVariantChips(b, chips);
        }

        /// <summary> Таймлайн пошагового режима: трек, отметки шагов, бегунок. </summary>
        private void DrawTimeline(SpriteBatch b, int navY)
        {
            var track = TimelineRect(navY);
            int total = _state.RouteStepTotal;
            if (total <= 0) return;

            int trackY = track.Y + 4;

            // Трек
            b.Draw(Game1.staminaRect, new Rectangle(track.X, trackY, track.Width, 8),
                new Color(180, 155, 100, 130));

            // Отметки шагов
            for (int i = 0; i < total; i++)
            {
                int x = TimelineStepX(i, track);
                b.Draw(Game1.staminaRect, new Rectangle(x - 1, trackY, 2, 8),
                    new Color(130, 100, 60, 180));
            }

            // Заполненная часть до текущего шага
            int curX = TimelineStepX(_state.RouteStepIndex, track);
            if (curX > track.X)
                b.Draw(Game1.staminaRect, new Rectangle(track.X, trackY, curX - track.X, 8),
                    new Color(200, 160, 30, 170));

            // Бегунок
            bool hov = track.Contains(Game1.getMouseX(), Game1.getMouseY());
            b.Draw(Game1.staminaRect, new Rectangle(curX - 5, track.Y, 10, 14),
                hov ? new Color(240, 195, 40) : new Color(210, 165, 28));
        }

        // ── Обработка кликов ──────────────────────────────────────────────────────────

        private void ClickMain(int x, int y, bool playSound)
        {
            for (int i = 0; i < _mainChecks.Count; i++)
            {
                if (!_mainChecks[i].ContainsPoint(x, y)) continue;
                ToggleMainCheck(i);
                if (playSound) Game1.playSound("drumkit6");
                return;
            }

            // Навигатор шагов — обрабатываем только когда данные загружены
            if (StepNavVisible() && _mainChecks.Count >= 5)
            {
                int navY = _mainChecks[4].Bounds.Bottom + 14;
                if (StepPrevBtn(navY).Contains(x, y))
                {
                    ChangeStep(-1);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }
                if (StepNextBtn(navY).Contains(x, y))
                {
                    ChangeStep(+1);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }
                if (TimelineRect(navY).Contains(x, y))
                {
                    ChangeStepFromX(x, navY);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }
            }

            // Чипы выбора варианта расписания
            if (VariantsVisible() && _mainChecks.Count >= 5)
            {
                int chipStartX = BX + PAD + 6;
                int chipStartY = VariantsBlockY() + 34;
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

        /// <summary> Видимость навигатора шагов: режим включён, NPC выбран, есть шаги. </summary>
        private bool StepNavVisible() =>
            _state.RouteStepMode
            && _state.SwitchTargetNPC
            && !_state.SwitchGlobalNpcPath
            && _state.RouteStepTotal > 0;

        /// <summary> Видимость блока вариантов расписания. </summary>
        private bool VariantsVisible() =>
            _state.RouteStepMode
            && _state.SwitchTargetNPC
            && !_state.SwitchGlobalNpcPath
            && HasVariantKeys();

        /// <summary> Y верхней границы блока вариантов (зависит от видимости навигатора). </summary>
        private int VariantsBlockY()
        {
            if (_mainChecks.Count < 5) return 0;
            int navY = _mainChecks[4].Bounds.Bottom + 14;
            return StepNavVisible() ? navY + STEP_NAV_H + 8 : navY + 8;
        }

        /// <summary> X бегунка таймлайна для шага <paramref name="stepIndex"/>. </summary>
        private int TimelineStepX(int stepIndex, Rectangle track)
        {
            if (_state.RouteStepTotal <= 1) return track.X + track.Width / 2;
            float t = (float)stepIndex / (_state.RouteStepTotal - 1);
            return track.X + (int)(t * track.Width);
        }

        /// <summary> Переключает чекбокс по индексу (общий путь для мыши и геймпада). </summary>
        private void ToggleMainCheck(int index)
        {
            if (index < 0 || index >= _mainChecks.Count) return;
            _mainChecks[index].Toggle();
        }

        /// <summary> Переключает шаг маршрута и инициирует перерисовку тайлов. </summary>
        private void ChangeStep(int delta)
        {
            if (_state.RouteStepTotal <= 0) return;
            _state.RouteStepIndex = MathHelper.Clamp(
                _state.RouteStepIndex + delta, 0, _state.RouteStepTotal - 1);
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        /// <summary> Переход к конкретному шагу (клик по таймлайну, геймпад). </summary>
        private void ChangeStepTo(int index)
        {
            if (_state.RouteStepTotal <= 0) return;
            _state.RouteStepIndex = MathHelper.Clamp(
                index, 0, _state.RouteStepTotal - 1);
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        /// <summary> Геймпад на таймлайне: следующий шаг. </summary>
        private void ChangeStepToNext() => ChangeStep(+1);

        /// <summary> Переход к шагу по позиции мыши на таймлайне. </summary>
        private void ChangeStepFromX(int mouseX, int navY)
        {
            if (_state.RouteStepTotal <= 0) return;
            var track = TimelineRect(navY);
            float t = MathHelper.Clamp(
                (float)(mouseX - track.X) / track.Width, 0f, 1f);
            int idx = _state.RouteStepTotal <= 1
                ? 0
                : (int)Math.Round(t * (_state.RouteStepTotal - 1));
            ChangeStepTo(idx);
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
