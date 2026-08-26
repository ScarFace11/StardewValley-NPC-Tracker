using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary>
    /// "Status" tab: world state cards, NPC statistics, source breakdown with
    /// progress bars, tip card, and inspector button.
    /// </summary>
    public partial class TrackingMenu
    {
        // ── Draw ─────────────────────────────────────────────────────────────────────

        private void DrawStatusTab(SpriteBatch b)
        {
            int x = BX + PAD + 6;
            int y = BY + 56;

            // ── World State Cards ────────────────────────────────────────────────────
            DrawSectionHeader(b, T("info.currentState"), x, y);
            y += 30;

            if (Context.IsWorldReady)
            {
                int cardW = (BOX_W - PAD * 2 - 12) / 3;
                int cardH = 60;
                int cardY = y;
                int cardGap = 6;

                // Date card
                string season = GetSeasonName();
                string date = $"{season} {Game1.dayOfMonth}";
                DrawStatCard(b, x, cardY, cardW, cardH,
                    date, T("info.date"), GoldAccent);

                // Location card
                string location = Game1.currentLocation?.Name ?? "-";
                DrawStatCard(b, x + cardW + cardGap, cardY, cardW, cardH,
                    TruncateToWidth(Game1.dialogueFont, location, cardW - 10),
                    T("info.location"), new Color(100, 160, 80));

                // Weather + Time card
                string weather = GetWeatherText();
                string time = Game1.timeOfDay >= 0
                    ? FormatGameTime(Game1.timeOfDay)
                    : "-";
                DrawStatCard(b, x + (cardW + cardGap) * 2, cardY, cardW, cardH,
                    weather + " " + time,
                    T("info.weather"), new Color(80, 140, 200));

                y = cardY + cardH + 16;
            }
            else
            {
                Utility.drawTextWithShadow(b, T("info.worldNotReady"), Game1.smallFont,
                    new Vector2(x, y), Color.Gray);
                y += 40;
            }

            // ── NPC Statistics ───────────────────────────────────────────────────────
            DrawDivider(b, y);
            y += 12;
            DrawSectionHeader(b, T("info.npcStats"), x, y);
            y += 30;

            // Three stat cards in a row
            int statCardW = (BOX_W - PAD * 2 - 12) / 3;
            int statCardH = 56;
            int totalNpc = _registry.TotalNpcList.Count;
            int hereNpc = Game1.currentLocation?.characters.Count ?? 0;
            int selectedNpc = _state.SwitchTargetNPC ? _registry.SelectedNpcNames.Count : 0;
            string selectedName = selectedNpc > 0 ? _registry.SelectedNpcNames.First() : "-";

            DrawStatCard(b, x, y, statCardW, statCardH,
                totalNpc.ToString(), T("info.stats.total"), new Color(100, 160, 80));
            DrawStatCard(b, x + statCardW + 6, y, statCardW, statCardH,
                hereNpc.ToString(), T("info.stats.here"), new Color(80, 140, 200));
            DrawStatCard(b, x + (statCardW + 6) * 2, y, statCardW, statCardH,
                TruncateToWidth(Game1.dialogueFont, selectedName, statCardW - 10),
                T("info.stats.selected"), GoldAccent);

            y += statCardH + 16;

            // ── NPC Sources with Progress Bars ──────────────────────────────────────
            DrawDivider(b, y);
            y += 12;
            DrawSectionHeader(b, T("info.bySource"), x, y);
            y += 30;

            // Build and cache source groups
            if (_sourceGroupCache == null || _sourceGroupCacheCount != _registry.NpcModSource.Count)
            {
                _sourceGroupCache = BuildSourceGroups();
                _sourceGroupCacheCount = _registry.NpcModSource.Count;
            }

            if (_sourceGroupCache.Count == 0)
            {
                Utility.drawTextWithShadow(b, T("info.noData"), Game1.smallFont,
                    new Vector2(x, y), Color.Gray);
                y += 30;
            }
            else
            {
                // Color palette for different sources
                Color[] barColors = {
                    new Color(100, 180, 70),   // Green
                    new Color(220, 160, 40),    // Orange
                    new Color(140, 100, 70),    // Brown
                    new Color(80, 140, 200),    // Blue
                    new Color(180, 80, 150),    // Pink
                    new Color(140, 140, 140),   // Gray
                };

                int barWidth = BOX_W - PAD * 2 - 20;
                int barIdx = 0;

                foreach (var g in _sourceGroupCache)
                {
                    if (barIdx >= 6) break;
                    Color barColor = barColors[barIdx % barColors.Length];
                    DrawProgressBar(b, x + 6, y, barWidth,
                        g.Source, g.Count, totalNpc, barColor);
                    y += 40;
                    barIdx++;
                }
            }

            y += 8;

            // ── Tip Card ─────────────────────────────────────────────────────────────
            DrawDivider(b, y);
            y += 12;
            DrawSectionHeader(b, T("info.tip"), x, y);
            y += 28;

            int tipHeight = 80;
            var tipPanel = new Rectangle(x, y, BOX_W - PAD * 2, tipHeight);
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                tipPanel.X, tipPanel.Y, tipPanel.Width, tipPanel.Height,
                CardBg, 1f, false);

            // Tip text
            string tipText = T("info.tipText");
            string[] tipLines = tipText.Split('\n');
            int tipY = tipPanel.Y + 10;
            foreach (string line in tipLines)
            {
                Utility.drawTextWithShadow(b, line, Game1.smallFont,
                    new Vector2(tipPanel.X + 12, tipY), new Color(100, 90, 70));
                tipY += 18;
            }

            // Inspector button in tip card
            int inspBtnW = 160;
            int inspBtnH = 28;
            var inspBtn = new Rectangle(tipPanel.Right - inspBtnW - 10,
                tipPanel.Y + (tipPanel.Height - inspBtnH) / 2,
                inspBtnW, inspBtnH);

            bool inspHov = inspBtn.Contains(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                inspBtn.X, inspBtn.Y, inspBtn.Width, inspBtn.Height,
                inspHov ? new Color(90, 110, 150) : new Color(70, 88, 120), 0.8f, false);

            string inspLabel = T("info.openInspector");
            var inspSz = Game1.smallFont.MeasureString(inspLabel);
            Utility.drawTextWithShadow(b, inspLabel, Game1.smallFont,
                new Vector2(
                    inspBtn.X + (inspBtn.Width - inspSz.X) / 2f,
                    inspBtn.Y + (inspBtn.Height - inspSz.Y) / 2f),
                Color.White);

            if (inspHov)
                _hoverText = T("tooltip.inspector", new { key = _config.SelectNpcKey.ToString() });
        }

        // ── Click handling ──────────────────────────────────────────────────────────

        private void ClickStatus(int x, int y, bool playSound)
        {
            // Inspector button in tip card
            if (OpenInspectorBtnRect().Contains(x, y))
            {
                OpenInspectorFromStatus();
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Tip panel inspector button
            int tipY = BY + 56;
            if (Context.IsWorldReady)
                tipY += 30 + 60 + 16 + 12 + 30 + 56 + 16 + 12 + 30 + 8 * 18;
            else
                tipY += 40 + 12 + 30 + 56 + 16 + 12 + 30 + 8 * 18;

            int inspBtnW = 160;
            int inspBtnH = 28;
            var tipPanel = new Rectangle(BX + PAD + 6, tipY, BOX_W - PAD * 2 - 12, 80);
            var inspBtn = new Rectangle(tipPanel.Right - inspBtnW - 10,
                tipPanel.Y + (tipPanel.Height - inspBtnH) / 2,
                inspBtnW, inspBtnH);

            if (inspBtn.Contains(x, y))
            {
                OpenInspectorFromStatus();
                if (playSound) Game1.playSound("smallSelect");
            }
        }

        // ── Build source groups ──────────────────────────────────────────────────────

        private List<(string Source, int Count)> BuildSourceGroups()
        {
            var dict = new Dictionary<string, int>();
            foreach (var kvp in _registry.NpcModSource)
            {
                if (dict.TryGetValue(kvp.Value, out int cnt))
                    dict[kvp.Value] = cnt + 1;
                else
                    dict[kvp.Value] = 1;
            }

            var list = new List<(string Source, int Count)>(dict.Count);
            foreach (var kvp in dict)
                list.Add((kvp.Key, kvp.Value));

            list.Sort((a, b) => string.Compare(a.Source, b.Source, StringComparison.Ordinal));
            return list;
        }
    }
}
