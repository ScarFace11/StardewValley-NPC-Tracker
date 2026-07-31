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

        // ── Позиции элементов вкладки ─────────────────────────────────────────────────
        private Rectangle NpcSearchRect() =>
            new Rectangle(BX + PAD, BY + 56, BOX_W - PAD * 2 - RESET_BTN_W - 8, 42);

        private Rectangle NpcResetBtnRect() =>
            new Rectangle(BX + BOX_W - PAD - RESET_BTN_W, BY + 56, RESET_BTN_W, 42);

        private int NpcFilterY => NpcSearchRect().Bottom + 8;
        private int NpcListY   => NpcFilterY + 34;

        private Rectangle NpcRowRect(int visualIdx, int listW) =>
            new Rectangle(BX + PAD, NpcListY + visualIdx * NPC_ROW_H, listW, NPC_ROW_H - 2);

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
        }

        // ── Отрисовка ─────────────────────────────────────────────────────────────────

        private void DrawNpcTab(SpriteBatch b)
        {
            DrawSearchBox(b);
            DrawModChips(b);
            DrawResetButton(b);
            DrawDivider(b, NpcListY - 6);
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
            int cx = BX + PAD;
            int cy = NpcFilterY;
            DrawChip(b, ref cx, cy, T("npc.filter.all"), _npcModFilter == null);
            foreach (string g in ModGroups())
                DrawChip(b, ref cx, cy, g, _npcModFilter == g);
        }

        private void DrawChip(SpriteBatch b, ref int x, int y, string label, bool active)
        {
            var sz = Game1.smallFont.MeasureString(label);
            int w  = (int)sz.X + 16;
            var r  = new Rectangle(x, y, w, 28);

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                r.X, r.Y, r.Width, r.Height,
                active ? new Color(255, 215, 120) : new Color(215, 205, 188), 0.85f, false);

            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(r.X + 8, r.Y + (r.Height - sz.Y) / 2f),
                active ? new Color(110, 65, 15) : Game1.textColor);

            x += w + 6;
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
                var    gameNpc = _registry.GameNpcs?.FirstOrDefault(n => n?.Name == npc);

                if (gameNpc?.Portrait != null)
                {
                    int ava  = Math.Min((int)Game1.dialogueFont.MeasureString("A").Y, row.Height - 4);
                    int avaY = row.Y + (row.Height - ava) / 2;
                    b.Draw(gameNpc.Portrait,
                        new Rectangle(textX, avaY, ava, ava),
                        new Rectangle(0, 0, 64, 64),
                        Color.White);
                    textX += ava + 6;
                }

                Utility.drawTextWithShadow(b, npc, Game1.dialogueFont,
                    new Vector2(textX, textY),
                    selected ? new Color(120, 70, 10) : Game1.textColor);

                if (_registry.NpcModSource.TryGetValue(npc, out string src))
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
            var  bgColor      = hasSelection ? new Color(200, 80, 60) : new Color(180, 165, 140, 120);
            var  textColor    = hasSelection ? Color.White : new Color(140, 130, 115);

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
                _searchFocused = true;
                return;
            }
            _searchFocused = false;

            // Чипы-фильтры по моду
            int chipX = BX + PAD;
            int chipY = NpcFilterY;

            if (HitChip(ref chipX, chipY, T("npc.filter.all"), x, y))
            {
                _npcModFilter = null;
                RebuildNpcFilter();
                if (playSound) Game1.playSound("smallSelect");
                return;
            }
            foreach (string g in ModGroups())
            {
                if (!HitChip(ref chipX, chipY, g, x, y)) continue;
                _npcModFilter = _npcModFilter == g ? null : g;
                RebuildNpcFilter();
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Кнопка «Сбросить выбор»
            if (NpcResetBtnRect().Contains(x, y))
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
                if (playSound) Game1.playSound("bigDeSelect");
                return;
            }

            // Строки NPC-списка
            int listW = BOX_W - PAD * 2;
            for (int i = _npcScrollOffset; i < Math.Min(_npcScrollOffset + NPC_VISIBLE, _filteredNpcs.Count); i++)
            {
                var row = NpcRowRect(i - _npcScrollOffset, listW);
                if (!row.Contains(x, y)) continue;

                string name = _filteredNpcs[i];
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
                    _state.NpcSelected = i;
                }

                // При смене NPC сбрасываем выбранный вариант расписания,
                // так как варианты у разных NPC не совпадают.
                _state.SelectedVariantKey = null;
                _state.SwitchBuildVariant = false;

                _tiles.Clear();
                _registry.CurrentNpcList.Clear();
                _state.SwitchGetNpcPath = true;
                _state.SwitchListFull   = false;
                if (playSound) Game1.playSound("drumkit6");
                return;
            }
        }

        // ── Утилиты ───────────────────────────────────────────────────────────────────

        /// <summary>
        /// Проверяет попадание мыши в чип с заданным текстом.
        /// Сдвигает cx вправо на ширину чипа (для последовательного обхода).
        /// </summary>
        private bool HitChip(ref int cx, int cy, string label, int mx, int my)
        {
            int  w   = (int)Game1.smallFont.MeasureString(label).X + 16;
            bool hit = new Rectangle(cx, cy, w, 28).Contains(mx, my);
            cx += w + 6;
            return hit;
        }

        private IEnumerable<string> ModGroups() =>
            _registry.NpcModSource.Values.Distinct().OrderBy(s => s);
    }
}
