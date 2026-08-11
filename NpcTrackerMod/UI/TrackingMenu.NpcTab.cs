using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary> Вкладка «NPC»: поиск, фильтр по модам, список NPC с выбором. </summary>
    public partial class TrackingMenu
    {
        // ── Размеры ───────────────────────────────────────────────────────────────────
        private const int RESET_BTN_W = 130;
        private const int CHIP_H      = 28;
        private const int CHIP_GAP    = 6;

        // ── Позиции элементов вкладки ─────────────────────────────────────────────────
        private Rectangle NpcSearchRect() =>
            new Rectangle(BX + PAD, BY + 56, BOX_W - PAD * 2 - RESET_BTN_W - 8, 42);

        private Rectangle NpcResetBtnRect() =>
            new Rectangle(BX + BOX_W - PAD - RESET_BTN_W, BY + 56, RESET_BTN_W, 42);

        private int NpcFilterY => NpcSearchRect().Bottom + 8;

        // Разделитель и список опускаются ниже чипов фильтров (чипы могут переноситься).
        private int NpcDividerY => _modChips.Count == 0
            ? NpcFilterY + CHIP_H + 6
            : _modChips[_modChips.Count - 1].rect.Bottom + 8;

        private int NpcCountY => NpcDividerY + 12;
        private int NpcListY  => NpcCountY + 24;

        private Rectangle NpcRowRect(int visualIdx, int listW) =>
            new Rectangle(BX + PAD, NpcListY + visualIdx * NPC_ROW_H, listW, NPC_ROW_H - 2);

        // Кеш чипов фильтра по источникам — пересчитывается вместе со списком.
        private List<(string label, string mod, Rectangle rect)> _modChips
            = new List<(string label, string mod, Rectangle rect)>();

        // Кеш портретов по имени NPC — вместо LINQ-поиска по GameNpcs на каждый кадр.
        private Dictionary<string, Texture2D> _portraitCache
            = new Dictionary<string, Texture2D>();

        // ── Фильтрация ────────────────────────────────────────────────────────────────

        private void RebuildNpcFilter()
        {
            IEnumerable<string> all = _registry.TotalNpcList.OrderBy(n => n);

            if (!string.IsNullOrEmpty(_npcSearch))
                all = all.Where(n =>
                    n.IndexOf(_npcSearch, StringComparison.OrdinalIgnoreCase) >= 0);

            if (_npcModFilter != null)
                all = all.Where(n =>
                    _registry.NpcModSource.TryGetValue(n, out string src) &&
                    src == _npcModFilter);

            _filteredNpcs    = all.OrderBy(n => n).ToList();
            _npcScrollOffset = Math.Max(0,
                Math.Min(_npcScrollOffset, Math.Max(0, _filteredNpcs.Count - NPC_VISIBLE)));

            _modChips      = ComputeModChipRects();
            _portraitCache = BuildPortraitCache();
        }

        /// <summary>
        /// Собирает портреты всех известных NPC один раз за перестроение списка
        /// (вместо FirstOrDefault по GameNpcs в каждом кадре для каждой строки).
        /// </summary>
        private Dictionary<string, Texture2D> BuildPortraitCache()
        {
            var cache = new Dictionary<string, Texture2D>();
            if (_registry.GameNpcs == null) return cache;

            foreach (var npc in _registry.GameNpcs)
            {
                if (npc?.Name == null || cache.ContainsKey(npc.Name)) continue;
                if (npc.Portrait != null)
                    cache[npc.Name] = npc.Portrait;
            }
            return cache;
        }

        /// <summary>
        /// Считает прямоугольники чипов фильтров с переносом на следующую строку.
        /// Единый источник правды для отрисовки и обработки кликов.
        /// </summary>
        private List<(string label, string mod, Rectangle rect)> ComputeModChipRects()
        {
            var result = new List<(string label, string mod, Rectangle rect)>();

            int cx   = BX + PAD;
            int cy   = NpcFilterY;
            int maxW = BX + PAD + (BOX_W - PAD * 2);

            AddModChip(ref cx, ref cy, maxW, result, T("npc.filter.all"), null);
            foreach (string g in ModGroups())
                AddModChip(ref cx, ref cy, maxW, result, g, g);

            return result;
        }

        private void AddModChip(ref int cx, ref int cy, int maxW,
            List<(string label, string mod, Rectangle rect)> list, string label, string mod)
        {
            int w = (int)Game1.smallFont.MeasureString(label).X + 16;
            if (cx + w > maxW && cx != BX + PAD)
            {
                cx  = BX + PAD;
                cy += CHIP_H + 6;
            }

            list.Add((label, mod, new Rectangle(cx, cy, w, CHIP_H)));
            cx += w + CHIP_GAP;
        }

        // ── Отрисовка ─────────────────────────────────────────────────────────────────

        private void DrawNpcTab(SpriteBatch b)
        {
            DrawSearchBox(b);
            DrawModChips(b);
            DrawResetButton(b);
            DrawDivider(b, NpcDividerY);
            DrawNpcCount(b);
            DrawNpcList(b);
            if (_filteredNpcs.Count > NPC_VISIBLE)
                DrawScrollbar(b);
        }

        private void DrawSearchBox(SpriteBatch b)
        {
            var  rect  = NpcSearchRect();

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height,
                _searchFocused ? Color.White : new Color(245, 240, 228), 1f, false);

            bool   empty       = string.IsNullOrEmpty(_npcSearch);
            string placeholder = T("npc.search.placeholder");
            string display     = empty && !_searchFocused
                ? placeholder
                : _npcSearch + (_searchFocused ? "|" : "");
            Color  textColor   = empty && !_searchFocused ? Color.Gray : Game1.textColor;

            float textY = rect.Y + (rect.Height - Game1.smallFont.MeasureString("A").Y) / 2f;
            Utility.drawTextWithShadow(b, display, Game1.smallFont,
                new Vector2(rect.X + 10, textY), textColor);
        }

        private void DrawModChips(SpriteBatch b)
        {
            foreach (var (label, mod, rect) in _modChips)
            {
                bool active = mod == _npcModFilter;

                var bg = active
                    ? new Color(255, 215, 120)
                    : new Color(215, 205, 188);

                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    rect.X, rect.Y, rect.Width, rect.Height, bg, 0.85f, false);

                // Обрезаем текст по ширине чипа.
                string shown = TruncateToWidth(
                    Game1.smallFont, label, rect.Width - 16);
                var    sz    = Game1.smallFont.MeasureString(shown);
                Utility.drawTextWithShadow(b, shown, Game1.smallFont,
                    new Vector2(rect.X + 8, rect.Y + (rect.Height - sz.Y) / 2f),
                    active ? new Color(110, 65, 15) : Game1.textColor);
            }
        }

        /// <summary> Строка счётчиков: сколько NPC видно/всего и сколько выбрано. </summary>
        private void DrawNpcCount(SpriteBatch b)
        {
            int total     = _registry.TotalNpcList.Count;
            int shown     = _filteredNpcs.Count;
            bool filtered = _npcModFilter != null || !string.IsNullOrEmpty(_npcSearch);

            string text = filtered
                ? T("npc.countFiltered", new { shown, total })
                : T("npc.count", new { count = total });

            if (_state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0)
                text += "   ·   " + T("npc.selectedCount",
                    new { count = _registry.SelectedNpcNames.Count });

            // Не даём строке выйти за рамки меню.
            string shownText = TruncateToWidth(
                Game1.smallFont, text, BOX_W - PAD * 2);
            Utility.drawTextWithShadow(b, shownText, Game1.smallFont,
                new Vector2(BX + PAD, NpcCountY), new Color(110, 95, 70));
        }

        private void DrawNpcList(SpriteBatch b)
        {
            int   listW = BOX_W - PAD * 2;
            int   end   = Math.Min(_npcScrollOffset + NPC_VISIBLE, _filteredNpcs.Count);
            var   mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

            for (int i = _npcScrollOffset; i < end; i++)
            {
                string npc      = _filteredNpcs[i];
                bool   selected = _state.SwitchTargetNPC && _registry.SelectedNpcNames.Contains(npc);
                var    row      = NpcRowRect(i - _npcScrollOffset, listW);

                if (selected)
                    b.Draw(Game1.staminaRect, row, new Color(255, 215, 90, 90));
                else if (row.Contains(mouse))
                    b.Draw(Game1.staminaRect, row, new Color(200, 195, 180, 55));

                float  textY   = row.Y + (row.Height - Game1.dialogueFont.MeasureString("A").Y) / 2f;
                int    textX   = row.X + 10;

                if (_portraitCache.TryGetValue(npc, out Texture2D portrait))
                {
                    int ava  = Math.Min((int)Game1.dialogueFont.MeasureString("A").Y, row.Height - 4);
                    int avaY = row.Y + (row.Height - ava) / 2;
                    b.Draw(portrait,
                        new Rectangle(textX, avaY, ava, ava),
                        new Rectangle(0, 0, 64, 64),
                        Color.White);
                    textX += ava + 6;
                }

                // Имя обрезается, чтобы не наезжать на источник справа.
                int nameMaxW = listW - textX - 8;
                if (_registry.NpcModSource.TryGetValue(npc, out string src))
                {
                    int srcW = (int)Game1.smallFont.MeasureString(src).X;
                    nameMaxW -= srcW + 14;
                }

                Utility.drawTextWithShadow(b,
                    TruncateToWidth(Game1.dialogueFont, npc, nameMaxW),
                    Game1.dialogueFont, new Vector2(textX, textY),
                    selected ? new Color(120, 70, 10) : Game1.textColor);

                if (_registry.NpcModSource.TryGetValue(npc, out src))
                {
                    var   srcSz = Game1.smallFont.MeasureString(src);
                    float srcY  = row.Y + (row.Height - srcSz.Y) / 2f;
                    Utility.drawTextWithShadow(b, src, Game1.smallFont,
                        new Vector2(row.Right - srcSz.X - 10, srcY), Color.Gray);
                }
            }

            if (_filteredNpcs.Count == 0)
            {
                string msg = _registry.TotalNpcList.Count == 0
                    ? T("npc.empty.waitForDay")
                    : T("npc.empty.notFound");
                DrawCentered(b, msg, Game1.smallFont, NpcListY + 60, Color.Gray);
            }
            else if (!(_state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0))
            {
                // Ничего не выбрано — подсказываем, с чего начать.
                int hintY = NpcListY + Math.Min(NPC_VISIBLE, _filteredNpcs.Count) * NPC_ROW_H + 10;
                DrawCentered(b, T("npc.hint.noSelection"), Game1.smallFont,
                    hintY, new Color(120, 110, 90));
            }
        }

        private void DrawScrollbar(SpriteBatch b)
        {
            int trackX = BX + BOX_W - PAD - 8;
            int trackY = NpcListY;
            int trackH = NPC_VISIBLE * NPC_ROW_H;
            int thumbH = Math.Max(20, trackH * NPC_VISIBLE / Math.Max(1, _filteredNpcs.Count));
            int thumbY = trackY + (trackH - thumbH) * _npcScrollOffset /
                         Math.Max(1, _filteredNpcs.Count - NPC_VISIBLE);

            b.Draw(Game1.staminaRect, new Rectangle(trackX, trackY, 6, trackH),
                new Color(180, 165, 140, 100));
            b.Draw(Game1.staminaRect, new Rectangle(trackX, thumbY, 6, thumbH),
                new Color(130, 100, 60, 200));
        }

        private void DrawResetButton(SpriteBatch b)
        {
            var  rect         = NpcResetBtnRect();
            bool hasSelection = _state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0;

            var bgColor = hasSelection
                ? new Color(200, 80, 60)
                : new Color(180, 165, 140, 120);
            var textColor = hasSelection ? Color.White : new Color(140, 130, 115);

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height, bgColor, 0.85f, false);

            string label = T("npc.reset");
            var    sz    = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(rect.X + (rect.Width  - sz.X) / 2f,
                            rect.Y + (rect.Height - sz.Y) / 2f),
                textColor);
        }

        // ── Обработка кликов ──────────────────────────────────────────────────────────

        private void ClickNpc(int x, int y, bool playSound)
        {
            if (NpcSearchRect().Contains(x, y))
            {
                FocusNpcSearch();
                return;
            }
            _searchFocused = false;

            // Чипы-фильтры по моду (тот же список прямоугольников, что и при отрисовке).
            foreach (var (_, mod, rect) in _modChips)
            {
                if (!rect.Contains(x, y)) continue;

                ToggleModFilter(mod);
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Кнопка «Сбросить выбор»
            if (NpcResetBtnRect().Contains(x, y))
            {
                ResetNpcSelection();
                if (playSound) Game1.playSound("bigDeSelect");
                return;
            }

            // Строки NPC-списка
            int listW = BOX_W - PAD * 2;
            for (int i = _npcScrollOffset; i < Math.Min(_npcScrollOffset + NPC_VISIBLE, _filteredNpcs.Count); i++)
            {
                var row = NpcRowRect(i - _npcScrollOffset, listW);
                if (!row.Contains(x, y)) continue;

                ToggleNpcRow(i);
                if (playSound) Game1.playSound("drumkit6");
                return;
            }
        }

        // ── Общие операции (мышь и геймпад используют один путь) ───────────────────────

        private void FocusNpcSearch() => _searchFocused = true;

        private void ToggleModFilter(string mod)
        {
            _npcModFilter = _npcModFilter == mod ? null : mod;
            RebuildNpcFilter();
        }

        private void ResetNpcSelection()
        {
            _registry.SelectedNpcNames.Clear();
            _registry.CurrentNpcName   = null;
            _state.SwitchTargetNPC     = false;
            _state.SelectedVariantKey  = null;
            _state.SwitchBuildVariant  = false;
            _tiles.Clear();
            _registry.CurrentNpcList.Clear();
            _state.SwitchGetNpcPath = true;
            _state.SwitchListFull   = false;
        }

        private void ToggleNpcRow(int index)
        {
            if (index < 0 || index >= _filteredNpcs.Count) return;

            string name = _filteredNpcs[index];
            if (_state.SwitchTargetNPC && _registry.SelectedNpcNames.Contains(name))
            {
                _registry.SelectedNpcNames.Remove(name);
                if (_registry.SelectedNpcNames.Count == 0)
                {
                    _state.SwitchTargetNPC   = false;
                    _registry.CurrentNpcName = null;
                }
            }
            else
            {
                _state.SwitchTargetNPC = true;
                _registry.SelectedNpcNames.Add(name);
                _registry.CurrentNpcName = name;
                _state.NpcSelected = index;
            }

            // При смене NPC сбрасываем выбранный вариант расписания,
            // так как варианты у разных NPC не совпадают.
            _state.SelectedVariantKey = null;
            _state.SwitchBuildVariant = false;

            _tiles.Clear();
            _registry.CurrentNpcList.Clear();
            _state.SwitchGetNpcPath = true;
            _state.SwitchListFull   = false;
        }

        // ── Утилиты ───────────────────────────────────────────────────────────────────

        private IEnumerable<string> ModGroups() =>
            _registry.NpcModSource.Values.Distinct().OrderBy(s => s);
    }
}
