using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NpcTrackerMod.Rendering;
using StardewValley;

namespace NpcTrackerMod.UI
{
    /// <summary>
    /// "Main" tab: two sections with SDV toggles, step navigator, variant selector,
    /// and reset buttons at the bottom.
    /// </summary>
    public partial class TrackingMenu
    {
        // ── Step navigator positions ──────────────────────────────────────────────────
        private Rectangle StepPrevBtn(int navY) => new Rectangle(BX + BOX_W / 2 - 115, navY, 30, 30);
        private Rectangle StepNextBtn(int navY) => new Rectangle(BX + BOX_W / 2 + 85, navY, 30, 30);

        private Rectangle TimelineRect(int navY) =>
            new Rectangle(BX + PAD + 6, navY + 56, BOX_W - PAD * 2 - 12, 16);

        private const int STEP_NAV_H = 88;

        // Tooltip keys for toggles (order matches _mainToggles)
        private static readonly string[] MainToggleTips =
        {
            "main.enable.tip",
            "main.grid.tip",
            "main.allLocations.tip",
            "main.globalRoute.tip",
            "main.stepMode.tip"
        };

        // ── Reset button positions ───────────────────────────────────────────────────
        private Rectangle ResetRouteBtnRect() =>
            new Rectangle(BX + PAD, BY + BOX_H - 70, 160, 34);
        private Rectangle RestoreDefaultsBtnRect() =>
            new Rectangle(BX + BOX_W / 2 - 30, BY + BOX_H - 70, 180, 34);

        // ── Draw ─────────────────────────────────────────────────────────────────────

        private void DrawMainTab(SpriteBatch b)
        {
            if (_mainToggles.Count < 5) return;

            int x = BX + PAD + 6;
            var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

            // ── Display Section ──────────────────────────────────────────────────────
            int section1Y = BY + 62;
            DrawSectionHeader(b, T("main.group.display"), x, section1Y);

            _mainToggles[0].Draw(b);
            _mainToggles[1].Draw(b);

            // Divider between sections
            int divY = _mainToggles[1].Bounds.Bottom + 14;
            DrawDivider(b, divY);

            // ── Routes Section ───────────────────────────────────────────────────────
            int section2Y = _mainToggles[2].Bounds.Y - 30;
            DrawSectionHeader(b, T("main.group.routes"), x, section2Y);

            _mainToggles[2].Draw(b);
            _mainToggles[3].Draw(b);
            _mainToggles[4].Draw(b);

            // Hover tooltips for toggles
            for (int i = 0; i < _mainToggles.Count; i++)
            {
                if (_mainToggles[i].ContainsPoint(mouse.X, mouse.Y))
                    _hoverText = T(MainToggleTips[i]);
            }

            // ── Step Navigator ──────────────────────────────────────────────────────
            int navY = _mainToggles[4].Bounds.Bottom + 14;

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
                        time = RouteRenderer.FormatTime(_state.RouteStepTime)
                    });
                    DrawCentered(b, stepLabel, Game1.dialogueFont, navY + 2, new Color(200, 160, 30));

                    if (!string.IsNullOrEmpty(_state.RouteStepScheduleKey))
                        DrawCentered(b,
                            TruncateToWidth(Game1.smallFont, _state.RouteStepScheduleKey, BOX_W - PAD * 2),
                            Game1.smallFont, navY + 34, new Color(130, 100, 60));

                    DrawTimeline(b, navY);

                    // Timeline tooltip
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

            // ── Variant Selector ────────────────────────────────────────────────────
            if (!VariantsVisible()) return;

            int variantBlockY = VariantsBlockY();
            DrawDivider(b, variantBlockY);
            DrawGroupHeader(b, T("main.variant.label"), x, variantBlockY + 10);

            int chipStartX = x;
            int chipStartY = variantBlockY + 34;
            int chipMaxW = BOX_W - PAD * 2 - 12;

            var chips = ComputeVariantChipRects(chipStartX, chipStartY, chipMaxW);
            DrawVariantChips(b, chips);

            // ── Reset Buttons ───────────────────────────────────────────────────────
            int btnY = Math.Min(variantBlockY + 120, BY + BOX_H - 80);
            DrawDivider(b, btnY - 10);

            DrawResetRouteButton(b, btnY);
            DrawRestoreDefaultsButton(b, btnY);
        }

        /// <summary> Draw the step navigator timeline: track, step markers, fill, and handle. </summary>
        private void DrawTimeline(SpriteBatch b, int navY)
        {
            var track = TimelineRect(navY);
            int total = _state.RouteStepTotal;
            if (total <= 0) return;

            int trackY = track.Y + 4;

            // Track
            b.Draw(Game1.staminaRect, new Rectangle(track.X, trackY, track.Width, 8),
                new Color(180, 155, 100, 130));

            // Step markers
            for (int i = 0; i < total; i++)
            {
                int x = TimelineStepX(i, track);
                b.Draw(Game1.staminaRect, new Rectangle(x - 1, trackY, 2, 8),
                    new Color(130, 100, 60, 180));
            }

            // Fill up to current step
            int curX = TimelineStepX(_state.RouteStepIndex, track);
            if (curX > track.X)
                b.Draw(Game1.staminaRect, new Rectangle(track.X, trackY, curX - track.X, 8),
                    new Color(200, 160, 30, 170));

            // Handle
            bool hov = track.Contains(Game1.getMouseX(), Game1.getMouseY());
            b.Draw(Game1.staminaRect, new Rectangle(curX - 5, track.Y, 10, 14),
                hov ? new Color(240, 195, 40) : new Color(210, 165, 28));
        }

        /// <summary> Draw the reset route button. </summary>
        private void DrawResetRouteButton(SpriteBatch b, int y)
        {
            var rect = ResetRouteBtnRect();
            rect.Y = y;
            bool hov = rect.Contains(Game1.getMouseX(), Game1.getMouseY());

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height,
                hov ? new Color(190, 140, 90) : new Color(160, 120, 80), 0.85f, false);

            string label = T("main.resetRoute");
            var sz = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(rect.X + (rect.Width - sz.X) / 2f,
                            rect.Y + (rect.Height - sz.Y) / 2f),
                Color.White);

            if (hov) _hoverText = T("main.resetRoute.tip");
        }

        /// <summary> Draw the restore defaults button. </summary>
        private void DrawRestoreDefaultsButton(SpriteBatch b, int y)
        {
            var rect = RestoreDefaultsBtnRect();
            rect.Y = y;
            bool hov = rect.Contains(Game1.getMouseX(), Game1.getMouseY());

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height,
                hov ? new Color(190, 140, 90) : new Color(160, 120, 80), 0.85f, false);

            string label = T("main.restoreDefaults");
            var sz = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(rect.X + (rect.Width - sz.X) / 2f,
                            rect.Y + (rect.Height - sz.Y) / 2f),
                Color.White);

            if (hov) _hoverText = T("main.restoreDefaults.tip");
        }

        // ── Click handling ──────────────────────────────────────────────────────────

        private void ClickMain(int x, int y, bool playSound)
        {
            // Toggle switches
            for (int i = 0; i < _mainToggles.Count; i++)
            {
                if (!_mainToggles[i].ContainsPoint(x, y)) continue;
                ToggleMainSwitch(i);
                if (playSound) Game1.playSound("drumkit6");
                return;
            }

            // Step navigator
            if (StepNavVisible() && _mainToggles.Count >= 5)
            {
                int navY = _mainToggles[4].Bounds.Bottom + 14;
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

            // Variant chips
            if (VariantsVisible() && _mainToggles.Count >= 5)
            {
                int chipStartX = BX + PAD + 6;
                int chipStartY = VariantsBlockY() + 34;
                int chipMaxW = BOX_W - PAD * 2 - 12;

                var chips = ComputeVariantChipRects(chipStartX, chipStartY, chipMaxW);
                foreach (var (key, rect) in chips)
                {
                    if (!rect.Contains(x, y)) continue;

                    SelectVariant(key);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }
            }

            // Reset buttons
            int btnY = _mainToggles.Count >= 5
                ? Math.Min(VariantsBlockY() + 120, BY + BOX_H - 80)
                : BY + BOX_H - 80;

            if (ResetRouteBtnRect().Contains(x, btnY + 34 / 2))
            {
                // Reset route: clear tiles and rebuild
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
                if (playSound) Game1.playSound("bigDeSelect");
                return;
            }
            if (RestoreDefaultsBtnRect().Contains(x, btnY + 34 / 2))
            {
                ResetSettings();
                if (playSound) Game1.playSound("bigDeSelect");
                return;
            }
        }

        // ── Utilities ───────────────────────────────────────────────────────────────

        private bool StepNavVisible() =>
            _state.RouteStepMode
            && _state.SwitchTargetNPC
            && !_state.SwitchGlobalNpcPath
            && _state.RouteStepTotal > 0;

        private bool VariantsVisible() =>
            _state.RouteStepMode
            && _state.SwitchTargetNPC
            && !_state.SwitchGlobalNpcPath
            && HasVariantKeys();

        private int VariantsBlockY()
        {
            if (_mainToggles.Count < 5) return 0;
            int navY = _mainToggles[4].Bounds.Bottom + 14;
            return StepNavVisible() ? navY + STEP_NAV_H + 8 : navY + 8;
        }

        private int TimelineStepX(int stepIndex, Rectangle track)
        {
            if (_state.RouteStepTotal <= 1) return track.X + track.Width / 2;
            float t = (float)stepIndex / (_state.RouteStepTotal - 1);
            return track.X + (int)(t * track.Width);
        }



        private void ChangeStep(int delta)
        {
            if (_state.RouteStepTotal <= 0) return;
            _state.RouteStepIndex = MathHelper.Clamp(
                _state.RouteStepIndex + delta, 0, _state.RouteStepTotal - 1);
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        private void ChangeStepTo(int index)
        {
            if (_state.RouteStepTotal <= 0) return;
            _state.RouteStepIndex = MathHelper.Clamp(
                index, 0, _state.RouteStepTotal - 1);
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        private void ChangeStepToNext() => ChangeStep(+1);

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

        private void SelectVariant(string key)
        {
            _state.SelectedVariantKey = key;
            _state.RouteStepIndex = 0;

            if (key == null)
            {
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
            }
            else
            {
                _state.SwitchBuildVariant = true;
                _tiles.Clear();
                _state.SwitchGetNpcPath = true;
            }
        }

        private bool HasVariantKeys()
        {
            string npcName = _registry.CurrentNpcName;
            if (string.IsNullOrEmpty(npcName)) return false;
            return _registry.NpcVariantKeys.TryGetValue(npcName, out var keys)
                   && keys != null && keys.Count > 0;
        }

        private List<(string key, Rectangle rect)> ComputeVariantChipRects(
            int startX, int startY, int maxWidth)
        {
            var result = new List<(string key, Rectangle rect)>();

            int cx = startX;
            int cy = startY;
            int chipH = 28;
            int chipGap = 5;

            string todayLabel = T("main.variant.active");
            int todayW = Math.Min(
                (int)Game1.smallFont.MeasureString(todayLabel).X + 16, maxWidth);
            result.Add((null, new Rectangle(cx, cy, todayW, chipH)));
            cx += todayW + chipGap;

            if (_registry.NpcVariantKeys.TryGetValue(_registry.CurrentNpcName ?? "", out var keys))
            {
                foreach (string key in keys)
                {
                    int w = Math.Min(
                        (int)Game1.smallFont.MeasureString(key).X + 16, maxWidth);
                    if (cx + w > startX + maxWidth)
                    {
                        cx = startX;
                        cy += chipH + chipGap;
                    }
                    result.Add((key, new Rectangle(cx, cy, w, chipH)));
                    cx += w + chipGap;
                }
            }

            return result;
        }

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

                string shown = TruncateToWidth(
                    Game1.smallFont, label, rect.Width - 16);
                var sz = Game1.smallFont.MeasureString(shown);
                Utility.drawTextWithShadow(b, shown, Game1.smallFont,
                    new Vector2(rect.X + 8, rect.Y + (rect.Height - sz.Y) / 2f),
                    isSelected ? new Color(110, 65, 15) : Game1.textColor);
            }
        }
    }
}
