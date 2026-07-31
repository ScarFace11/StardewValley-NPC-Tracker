using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NpcTrackerMod.Rendering;
using StardewValley;

namespace NpcTrackerMod.UI
{
    /// <summary> Вкладка «Главное»: переключатели режимов и навигатор шагов. </summary>
    public partial class TrackingMenu
    {
        // ── Позиции навигатора шагов ──────────────────────────────────────────────────
        private Rectangle StepPrevBtn(int navY) => new Rectangle(BX + BOX_W / 2 - 115, navY, 30, 30);
        private Rectangle StepNextBtn(int navY) => new Rectangle(BX + BOX_W / 2 + 85,  navY, 30, 30);

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
                    _state.RouteStepMode      = v;
                    _state.RouteStepIndex     = 0;
                    _state.RouteStepScheduleKey = null;
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

            if (!showNav) return;

            int navY = _mainChecks[4].Bounds.Bottom + 14;
            DrawArrow(b, StepPrevBtn(navY), left: true);
            DrawArrow(b, StepNextBtn(navY), left: false);

            string timeStr   = RouteRenderer.FormatTime(_state.RouteStepTime);
            string stepLabel = $"{timeStr}   ({_state.RouteStepIndex + 1} / {_state.RouteStepTotal})";
            DrawCentered(b, stepLabel, Game1.dialogueFont, navY + 2, new Color(200, 160, 30));

            if (!string.IsNullOrEmpty(_state.RouteStepScheduleKey))
                DrawCentered(b, _state.RouteStepScheduleKey, Game1.smallFont,
                    navY + 36, new Color(130, 100, 60));

            if (_state.RouteStepTotal > 1)
                DrawCentered(b, T("main.stepHint"), Game1.smallFont, navY + 54, Color.Gray);
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
    }
}
