using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NpcTrackerMod.Core;
using NpcTrackerMod.Rendering;
using NpcTrackerMod.Tracking;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary>
    /// Главное меню мода. Четыре боковых вкладки: Главное, NPC, Настройки, Инфо.
    /// </summary>
    public class TrackingMenu : IClickableMenu
    {
        // ── Размеры ──────────────────────────────────────────────────────────────────
        private const int BOX_W = 580;   // ширина основного блока
        private const int BOX_H = 620;   // высота основного блока
        private const int TAB_W = 58;    // ширина боковой вкладки
        private const int TAB_H = 110;   // высота одной вкладки
        private const int TAB_GAP = 6;     // отступ между вкладками
        private const int PAD = 22;    // внутренний отступ контента
        private const int NPC_ROW_H = 38;   // высота строки в списке NPC
        private const int NPC_VISIBLE = 11;   // видимых строк NPC

        private static readonly string[] TabLabels = { "Главное", "NPC", "Настройки", "Инфо" };

        // ── Зависимости ──────────────────────────────────────────────────────────────
        private readonly IMonitor _monitor;
        private readonly ModState _state;
        private readonly NpcRegistry _registry;
        private readonly TileRenderer _tiles;
        private readonly ModConfig _config;
        private readonly Action _saveConfig;

        // ── Состояние вкладок ─────────────────────────────────────────────────────────
        private int _activeTab;

        // Главное
        private readonly List<ClickableCheckbox> _mainChecks = new List<ClickableCheckbox>();

        // NPC
        private string _npcSearch = string.Empty;
        private string _npcModFilter;            // null = все
        private bool _searchFocused;
        private int _npcScrollOffset;
        private List<string> _filteredNpcs = new List<string>();

        // Настройки
        private string _rebindTarget;
        private int _timeFilterIndex;
        private bool _draggingSlider;   // true пока LMB зажата на слайдере

        private static readonly int[] TimeSteps =
        {
            -1,
            600, 700, 800, 900, 1000, 1100, 1200,
            1300, 1400, 1500, 1600, 1700, 1800,
            1900, 2000, 2100, 2200, 2300, 2400, 2500, 2600
        };

        // Кнопка закрытия
        private ClickableTextureComponent _closeBtn;

        // Короткие ссылки на позиции
        private int BX => xPositionOnScreen;
        private int BY => yPositionOnScreen;

        // ── Конструктор ───────────────────────────────────────────────────────────────
        public TrackingMenu(
            IMonitor monitor,
            ModState state,
            NpcRegistry registry,
            TileRenderer tiles,
            ModConfig config,
            Action saveConfig)
            : base(0, 0, BOX_W, BOX_H)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _saveConfig = saveConfig ?? throw new ArgumentNullException(nameof(saveConfig));

            int idx = Array.IndexOf(TimeSteps, _state.TimeFilter);
            _timeFilterIndex = idx >= 0 ? idx : 0;

            // Подписываемся на текстовый ввод ОС — поддерживает любую раскладку
            Game1.game1.Window.TextInput += OnWindowTextInput;

            InitPosition();
            RebuildTab();
        }

        /// <summary>
        /// Получает символ от ОС при вводе текста (кириллица, латиница и т.д.).
        /// Срабатывает только когда поле поиска NPC активно.
        /// </summary>
        private void OnWindowTextInput(object sender, TextInputEventArgs e)
        {
            if (!_searchFocused) return;
            if (_npcSearch.Length >= 30) return;
            if (char.IsControl(e.Character)) return;

            _npcSearch += e.Character;
            _npcScrollOffset = 0;
            RebuildNpcFilter();
        }

        protected override void cleanupBeforeExit()
        {
            Game1.game1.Window.TextInput -= OnWindowTextInput;
            base.cleanupBeforeExit();
        }

        private void InitPosition()
        {
            xPositionOnScreen = Game1.viewport.Width / 2 - BOX_W / 2;
            yPositionOnScreen = Game1.viewport.Height / 2 - BOX_H / 2;

            _closeBtn = new ClickableTextureComponent(
                new Rectangle(BX + BOX_W - 48, BY - 8, 48, 48),
                Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);
        }

        private void RebuildTab()
        {
            _mainChecks.Clear();
            _npcScrollOffset = 0;

            if (_activeTab == 0) BuildMainChecks();
            if (_activeTab == 1) RebuildNpcFilter();
        }

        // ── Главная вкладка — построение ─────────────────────────────────────────────

        private void BuildMainChecks()
        {
            int x = BX + PAD + 6;
            int y = BY + 90;   // отступ под заголовком «Отображение»

            AddCheck(ref y, x, "Включить трекер",
                () => _state.EnableDisplay, v => _state.EnableDisplay = v);
            AddCheck(ref y, x, "Отображение сетки",
                () => _state.DisplayGrid, v => _state.DisplayGrid = v);

            y += 36;  // визуальный разрыв + заголовок «Маршруты»
            y += 28;

            AddCheck(ref y, x, "Показывать во всех локациях",
                () => _state.SwitchTargetLocations, v =>
                {
                    _state.SwitchTargetLocations = v;
                    _tiles.Clear();
                    _state.SwitchGetNpcPath = true;
                    _registry.CurrentNpcList.Clear();
                    _state.SwitchListFull = false;
                });
            AddCheck(ref y, x, "Глобальный маршрут (весь день)",
                () => _state.SwitchGlobalNpcPath, v =>
                {
                    _state.SwitchGlobalNpcPath = v;
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

        // ── NPC вкладка — фильтрация ──────────────────────────────────────────────────

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

            _filteredNpcs = all.OrderBy(n => n).ToList();
            _npcScrollOffset = Math.Max(0,
                Math.Min(_npcScrollOffset, Math.Max(0, _filteredNpcs.Count - NPC_VISIBLE)));
        }

        // ── Позиции NPC-вкладки (единый источник для draw и click) ──────────────────

        private const int RESET_BTN_W = 130;
        private Rectangle NpcSearchRect() =>
            new Rectangle(BX + PAD, BY + 58, BOX_W - PAD * 2 - RESET_BTN_W - 8, 36);
        private Rectangle NpcResetBtnRect() =>
            new Rectangle(BX + BOX_W - PAD - RESET_BTN_W, BY + 58, RESET_BTN_W, 36);

        private int NpcFilterY => NpcSearchRect().Bottom + 8;
        private int NpcListY => NpcFilterY + 34;   // chip 28px + gap 6px

        private Rectangle NpcRowRect(int visualIdx, int listW) =>
            new Rectangle(BX + PAD, NpcListY + visualIdx * NPC_ROW_H, listW, NPC_ROW_H - 2);

        // ── Позиции Settings-вкладки ─────────────────────────────────────────────────

        private Rectangle MenuKeyBtnRect() => new Rectangle(BX + BOX_W - 185, BY + 103, 162, 34);
        private Rectangle DebugKeyBtnRect()    => new Rectangle(BX + BOX_W - 185, BY + 150, 162, 34);
        private Rectangle SelectNpcKeyBtnRect() => new Rectangle(BX + BOX_W - 185, BY + 197, 162, 34);

        private int TimeRowY => BY + 331;
        private Rectangle TimePrevBtn() => new Rectangle(BX + BOX_W / 2 - 115, TimeRowY, 30, 30);
        private Rectangle TimeNextBtn() => new Rectangle(BX + BOX_W / 2 + 85, TimeRowY, 30, 30);

        /// <summary> Трек слайдера под стрелками фильтра времени. </summary>
        private Rectangle SliderTrackRect() =>
            new Rectangle(BX + PAD, TimeRowY + 44, BOX_W - PAD * 2, 8);

        /// <summary>
        /// X-позиция большого пальца слайдера. -1 если фильтр = «все время».
        /// Шаги 1..N (индексы в TimeSteps без нулевого -1 элемента).
        /// </summary>
        private int SliderThumbX()
        {
            if (_timeFilterIndex == 0) return -1;
            var track = SliderTrackRect();
            int steps = TimeSteps.Length - 1;   // кол-во шагов (600..2600)
            float t = (float)(_timeFilterIndex - 1) / Math.Max(1, steps - 1);
            return track.X + (int)(t * track.Width);
        }

        // ── Позиции вкладок ───────────────────────────────────────────────────────────

        private Rectangle TabRect(int i)
        {
            int startY = BY + (BOX_H - (TabLabels.Length * TAB_H + (TabLabels.Length - 1) * TAB_GAP)) / 2;
            return new Rectangle(BX - TAB_W, startY + i * (TAB_H + TAB_GAP), TAB_W, TAB_H);
        }

        // ── Отрисовка ─────────────────────────────────────────────────────────────────

        public override void draw(SpriteBatch b)
        {
            try
            {
                // Основной блок
                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    BX, BY, BOX_W, BOX_H, Color.White, 1f, true);

                // Боковые вкладки
                DrawSideTabs(b);

                // Заголовок
                DrawCentered(b, "NPC Tracker", Game1.dialogueFont, BY + PAD, Game1.textColor);

                // Контент
                switch (_activeTab)
                {
                    case 0: DrawMainTab(b); break;
                    case 1: DrawNpcTab(b); break;
                    case 2: DrawSettingsTab(b); break;
                    case 3: DrawInfoTab(b); break;
                }

                _closeBtn.draw(b);
                drawMouse(b);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка отрисовки меню: {ex.Message}", LogLevel.Error);
                base.draw(b);
                drawMouse(b);
            }
        }

        private void DrawSideTabs(SpriteBatch b)
        {
            for (int i = 0; i < TabLabels.Length; i++)
            {
                var rect = TabRect(i);
                bool active = i == _activeTab;

                // Фон вкладки (активная — белая, неактивная — приглушённая)
                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    rect.X, rect.Y, rect.Width, rect.Height,
                    active ? Color.White : new Color(190, 180, 165), 1f, false);

                // Текст, повёрнутый на 90° по часовой стрелке (Pi/2)
                var font = Game1.smallFont;
                var sz = font.MeasureString(TabLabels[i]);
                var origin = new Vector2(sz.X / 2f, sz.Y / 2f);
                var pos = new Vector2(
                    rect.X + rect.Width / 2f,
                    rect.Y + rect.Height / 2f);

                b.DrawString(font, TabLabels[i], pos,
                    active ? Game1.textColor : new Color(100, 90, 75),
                    MathHelper.PiOver2, origin, 1f, SpriteEffects.None, 0.86f);
            }
        }

        // ── Главное ───────────────────────────────────────────────────────────────────

        private void DrawMainTab(SpriteBatch b)
        {
            if (_mainChecks.Count < 4) return;

            int x = BX + PAD + 6;

            // Группа: Отображение
            int g1Y = _mainChecks[0].Bounds.Y - 30;
            DrawGroupHeader(b, "Отображение", x, g1Y);
            _mainChecks[0].Draw(b);
            _mainChecks[1].Draw(b);

            // Разделитель
            int divY = _mainChecks[1].Bounds.Bottom + 14;
            DrawDivider(b, divY);

            // Группа: Маршруты
            int g2Y = _mainChecks[2].Bounds.Y - 30;
            DrawGroupHeader(b, "Маршруты", x, g2Y);
            _mainChecks[2].Draw(b);
            _mainChecks[3].Draw(b);
        }

        // ── NPC ───────────────────────────────────────────────────────────────────────

        private void DrawNpcTab(SpriteBatch b)
        {
            // Поиск
            DrawSearchBox(b);

            // Фильтры по модам
            DrawModChips(b);

            // Кнопка сброса выбора
            DrawResetButton(b);

            // Разделитель
            DrawDivider(b, NpcListY - 6);

            // Список
            DrawNpcList(b);

            // Скроллбар
            if (_filteredNpcs.Count > NPC_VISIBLE)
                DrawScrollbar(b);
        }

        private void DrawSearchBox(SpriteBatch b)
        {
            var rect = NpcSearchRect();
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height,
                _searchFocused ? Color.White : new Color(245, 240, 228), 1f, false);

            bool empty = string.IsNullOrEmpty(_npcSearch);
            string display = empty && !_searchFocused
                ? "Поиск NPC..."
                : _npcSearch + (_searchFocused ? "|" : "");
            Color textColor = empty && !_searchFocused ? Color.Gray : Game1.textColor;

            float textY = rect.Y + (rect.Height - Game1.smallFont.MeasureString("A").Y) / 2f;
            Utility.drawTextWithShadow(b, display, Game1.smallFont,
                new Vector2(rect.X + 10, textY), textColor);
        }

        private void DrawModChips(SpriteBatch b)
        {
            int cx = BX + PAD;
            int cy = NpcFilterY;
            DrawChip(b, ref cx, cy, "Все", _npcModFilter == null);
            foreach (string g in ModGroups())
                DrawChip(b, ref cx, cy, g, _npcModFilter == g);
        }

        private void DrawChip(SpriteBatch b, ref int x, int y, string label, bool active)
        {
            var sz = Game1.smallFont.MeasureString(label);
            int w = (int)sz.X + 16;
            var r = new Rectangle(x, y, w, 28);

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
            int listW = BOX_W - PAD * 2;
            int end = Math.Min(_npcScrollOffset + NPC_VISIBLE, _filteredNpcs.Count);
            var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

            for (int i = _npcScrollOffset; i < end; i++)
            {
                string npc = _filteredNpcs[i];
                bool selected = _state.SwitchTargetNPC && _registry.SelectedNpcNames.Contains(npc);
                var row = NpcRowRect(i - _npcScrollOffset, listW);

                if (selected)
                    b.Draw(Game1.staminaRect, row, new Color(255, 215, 90, 90));
                else if (row.Contains(mouse))
                    b.Draw(Game1.staminaRect, row, new Color(200, 195, 180, 55));

                float textY = row.Y + (row.Height - Game1.dialogueFont.MeasureString("A").Y) / 2f;

                // ── Аватарка NPC (слева от имени, вписана в высоту строки) ──
                int textX = row.X + 10;
                var gameNpc = _registry.GameNpcs?.FirstOrDefault(n => n?.Name == npc);
                if (gameNpc?.Portrait != null)
                {
                    int ava = Math.Min((int)Game1.dialogueFont.MeasureString("A").Y, row.Height - 4);
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
                    var srcSz = Game1.smallFont.MeasureString(src);
                    float srcY = row.Y + (row.Height - srcSz.Y) / 2f;
                    Utility.drawTextWithShadow(b, src, Game1.smallFont,
                        new Vector2(row.Right - srcSz.X - 10, srcY), Color.Gray);
                }
            }

            if (_filteredNpcs.Count == 0)
            {
                string msg = _registry.TotalNpcList.Count == 0
                    ? "Начните новый день, чтобы\nзагрузить данные NPC"
                    : "NPC не найдены";
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
            var rect = NpcResetBtnRect();
            bool hasSelection = _state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0;
            var bgColor = hasSelection ? new Color(200, 80, 60) : new Color(180, 165, 140, 120);
            var textColor = hasSelection ? Color.White : new Color(140, 130, 115);

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height, bgColor, 0.85f, false);

            string label = "Сбросить выбор";
            var sz = Game1.smallFont.MeasureString(label);
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(rect.X + (rect.Width - sz.X) / 2f,
                            rect.Y + (rect.Height - sz.Y) / 2f),
                textColor);
        }

        // ── Настройки ─────────────────────────────────────────────────────────────────

        private void DrawSettingsTab(SpriteBatch b)
        {
            int x = BX + PAD;

            // Горячие клавиши
            DrawSectionHeader(b, "Горячие клавиши", x, BY + 62);
            DrawKeybind(b, x, BY + 100, "Открыть меню", _config.MenuKey.ToString(), "menu", MenuKeyBtnRect());
            DrawKeybind(b, x, BY + 147, "Отладка варпов", _config.DebugKey.ToString(), "debug", DebugKeyBtnRect());
            DrawKeybind(b, x, BY + 194, "Выбрать NPC", _config.SelectNpcKey.ToString(), "select", SelectNpcKeyBtnRect());

            DrawDivider(b, BY + 248);

            // Фильтр по времени
            DrawSectionHeader(b, "Фильтр по времени", x, BY + 264);

            // Кнопки ◄ ►
            DrawArrow(b, TimePrevBtn(), left: true);
            DrawArrow(b, TimeNextBtn(), left: false);

            string timeText = _state.TimeFilter < 0
                ? "Все время"
                : RouteRenderer.FormatTime(_state.TimeFilter);
            DrawCentered(b, timeText, Game1.dialogueFont, TimeRowY + 2, new Color(200, 160, 30));

            // ── Скруббер (слайдер) ────────────────────────────────────────────────
            var track = SliderTrackRect();
            bool hovTrack = new Rectangle(track.X, track.Y - 8, track.Width, track.Height + 16)
                                .Contains(Game1.getMouseX(), Game1.getMouseY());

            // Трек
            b.Draw(Game1.staminaRect, track,
                hovTrack || _draggingSlider
                    ? new Color(180, 155, 100, 200)
                    : new Color(180, 155, 100, 130));

            // Заполненная часть (от начала до положения ползунка)
            int thumbX = SliderThumbX();
            if (thumbX >= 0)
            {
                int fillW = thumbX - track.X;
                if (fillW > 0)
                    b.Draw(Game1.staminaRect,
                        new Rectangle(track.X, track.Y, fillW, track.Height),
                        new Color(200, 160, 30, 180));

                // Ползунок
                bool thumbHov = Math.Abs(Game1.getMouseX() - thumbX) < 12;
                b.Draw(Game1.staminaRect,
                    new Rectangle(thumbX - 5, track.Y - 5, 10, track.Height + 10),
                    _draggingSlider || thumbHov
                        ? new Color(240, 195, 40)
                        : new Color(210, 165, 28));
            }
            else
            {
                // «Все время» — серый ползунок у левого края
                b.Draw(Game1.staminaRect,
                    new Rectangle(track.X - 3, track.Y - 5, 8, track.Height + 10),
                    new Color(160, 150, 130, 180));
            }

            // Метки начала и конца диапазона
            string labelStart = "06:00";
            string labelEnd   = "02:00";
            float  labelY     = track.Bottom + 5;
            Utility.drawTextWithShadow(b, labelStart, Game1.smallFont,
                new Vector2(track.X, labelY), Color.Gray);
            var endSz = Game1.smallFont.MeasureString(labelEnd);
            Utility.drawTextWithShadow(b, labelEnd, Game1.smallFont,
                new Vector2(track.Right - endSz.X, labelY), Color.Gray);
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

            string keyText = waiting ? "[нажмите клавишу...]" : key;
            var keySz = Game1.smallFont.MeasureString(keyText);
            Utility.drawTextWithShadow(b, keyText, Game1.smallFont,
                new Vector2(
                    btnRect.X + (btnRect.Width - keySz.X) / 2f,
                    btnRect.Y + (btnRect.Height - keySz.Y) / 2f),
                waiting ? new Color(160, 80, 0) : Game1.textColor);
        }

        private static void DrawArrow(SpriteBatch b, Rectangle rect, bool left)
        {
            var src = left
                ? new Rectangle(352, 495, 12, 11)
                : new Rectangle(365, 495, 12, 11);
            b.Draw(Game1.mouseCursors, rect, src, Color.White);
        }

        // ── Инфо ──────────────────────────────────────────────────────────────────────

        private void DrawInfoTab(SpriteBatch b)
        {
            int x = BX + PAD + 6;
            int y = BY + 62;

            // Дата и локация
            DrawSectionHeader(b, "Текущее состояние", x, y); y += 36;
            if (Context.IsWorldReady)
            {
                string season = Game1.currentSeason ?? "—";
                DrawKV(b, "Дата", $"{Capitalize(season)}, день {Game1.dayOfMonth}, год {Game1.year}", x, ref y);
                DrawKV(b, "Локация", Game1.currentLocation?.Name ?? "—", x, ref y);
            }
            else
            {
                Utility.drawTextWithShadow(b, "Мир не загружен", Game1.smallFont,
                    new Vector2(x, y), Color.Gray);
                y += 26;
            }

            DrawDivider(b, y + 6); y += 26;

            // Статистика NPC
            DrawSectionHeader(b, "Статистика NPC", x, y); y += 36;
            DrawKV(b, "Отслеживается", _registry.TotalNpcList.Count.ToString(), x, ref y);
            DrawKV(b, "В текущей локации", (Game1.currentLocation?.characters.Count ?? 0).ToString(), x, ref y);
            DrawKV(b, "Выбрано NPC",
                _state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0
                    ? _registry.SelectedNpcNames.Count.ToString()
                    : "не выбраны",
                x, ref y);
            if (_state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0)
            {
                foreach (var sn in _registry.SelectedNpcNames.OrderBy(n => n))
                {
                    DrawKV(b, "  •", sn, x, ref y);
                    if (y > BY + BOX_H - 60) break;
                }
            }

            DrawDivider(b, y + 6); y += 26;

            // Разбивка по источникам
            DrawSectionHeader(b, "По источнику", x, y); y += 36;

            var groups = _registry.NpcModSource
                .GroupBy(kv => kv.Value)
                .OrderBy(g => g.Key)
                .ToList();

            if (groups.Count == 0)
            {
                Utility.drawTextWithShadow(b, "Данные появятся после начала дня",
                    Game1.smallFont, new Vector2(x, y), Color.Gray);
                y += 26;
            }
            else
            {
                foreach (var g in groups)
                {
                    DrawKV(b, g.Key, $"{g.Count()} NPC", x, ref y);
                }
            }

            DrawDivider(b, y + 8); y += 24;

            // Подсказка
            DrawSectionHeader(b, "Подсказка", x, y); y += 32;
            Utility.drawTextWithShadow(b,
                "Наведите курсор на тайл маршрута,\nчтобы увидеть имя NPC и время посещения.\nКликните — откроется инспектор тайла.",
                Game1.smallFont, new Vector2(x, y), new Color(120, 110, 90));
        }

        // ── Обработка ввода ───────────────────────────────────────────────────────────

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            try
            {
                base.receiveLeftClick(x, y, playSound);

                if (_closeBtn.containsPoint(x, y))
                {
                    exitThisMenu();
                    if (playSound) Game1.playSound("bigDeSelect");
                    return;
                }

                // Вкладки
                for (int i = 0; i < TabLabels.Length; i++)
                {
                    if (!TabRect(i).Contains(x, y)) continue;
                    if (_activeTab != i)
                    {
                        _activeTab = i;
                        _searchFocused = false;
                        RebuildTab();
                        if (playSound) Game1.playSound("shwip");
                    }
                    return;
                }

                switch (_activeTab)
                {
                    case 0: ClickMain(x, y, playSound); break;
                    case 1: ClickNpc(x, y, playSound); break;
                    case 2: ClickSettings(x, y, playSound); break;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка клика в меню: {ex.Message}", LogLevel.Error);
            }
        }

        private void ClickMain(int x, int y, bool playSound)
        {
            foreach (var cb in _mainChecks)
            {
                if (!cb.ContainsPoint(x, y)) continue;
                cb.Toggle();
                if (playSound) Game1.playSound("drumkit6");
                return;
            }
        }

        private void ClickNpc(int x, int y, bool playSound)
        {
            // Поиск
            if (NpcSearchRect().Contains(x, y))
            {
                _searchFocused = true;
                return;
            }
            _searchFocused = false;

            // Чипы фильтра
            int chipX = BX + PAD;
            int chipY = NpcFilterY;

            if (HitChip(ref chipX, chipY, "Все", x, y))
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

            // Кнопка сброса выбора
            if (NpcResetBtnRect().Contains(x, y))
            {
                _registry.SelectedNpcNames.Clear();
                _registry.CurrentNpcName = null;
                _state.SwitchTargetNPC = false;
                _tiles.Clear();
                _registry.CurrentNpcList.Clear();
                _state.SwitchGetNpcPath = true;
                _state.SwitchListFull = false;
                if (playSound) Game1.playSound("bigDeSelect");
                return;
            }

            // Строки NPC
            int listW = BOX_W - PAD * 2;
            for (int i = _npcScrollOffset; i < Math.Min(_npcScrollOffset + NPC_VISIBLE, _filteredNpcs.Count); i++)
            {
                var row = NpcRowRect(i - _npcScrollOffset, listW);
                if (!row.Contains(x, y)) continue;

                string name = _filteredNpcs[i];
                if (_state.SwitchTargetNPC && _registry.SelectedNpcNames.Contains(name))
                {
                    // Повторный клик — снять выбор этого NPC
                    _registry.SelectedNpcNames.Remove(name);
                    if (_registry.SelectedNpcNames.Count == 0)
                    {
                        _state.SwitchTargetNPC = false;
                        _registry.CurrentNpcName = null;
                    }
                }
                else
                {
                    // Добавить NPC к выбранным
                    _state.SwitchTargetNPC = true;
                    _registry.SelectedNpcNames.Add(name);
                    _registry.CurrentNpcName = name;
                    _state.NpcSelected = i;
                }
                _tiles.Clear();
                _registry.CurrentNpcList.Clear();
                _state.SwitchGetNpcPath = true;
                _state.SwitchListFull = false;
                if (playSound) Game1.playSound("drumkit6");
                return;
            }
        }

        private void ClickSettings(int x, int y, bool playSound)
        {
            if (MenuKeyBtnRect().Contains(x, y))
            {
                _rebindTarget = "menu";
                if (playSound) Game1.playSound("smallSelect");
                return;
            }
            if (DebugKeyBtnRect().Contains(x, y))
            {
                _rebindTarget = "debug";
                if (playSound) Game1.playSound("smallSelect");
                return;
            }
            if (SelectNpcKeyBtnRect().Contains(x, y))
            {
                _rebindTarget = "select";
                if (playSound) Game1.playSound("smallSelect");
                return;
            }
            if (TimePrevBtn().Contains(x, y)) { ChangeTimeFilter(-1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (TimeNextBtn().Contains(x, y)) { ChangeTimeFilter(+1); if (playSound) Game1.playSound("smallSelect"); return; }

            // Клик по слайдеру времени
            var track = SliderTrackRect();
            var trackHit = new Rectangle(track.X, track.Y - 10, track.Width, track.Height + 20);
            if (trackHit.Contains(x, y))
            {
                _draggingSlider = true;
                ApplySliderX(x);
                if (playSound) Game1.playSound("smallSelect");
            }
        }

        /// <summary> Вычисляет индекс шага из X-координаты мыши и применяет фильтр. </summary>
        private void ApplySliderX(int mouseX)
        {
            var track = SliderTrackRect();
            float t = MathHelper.Clamp((float)(mouseX - track.X) / track.Width, 0f, 1f);
            int steps  = TimeSteps.Length - 1;   // шаги 1..N
            int newIdx = 1 + (int)(t * (steps - 1) + 0.5f);
            newIdx = MathHelper.Clamp(newIdx, 1, TimeSteps.Length - 1);
            if (newIdx == _timeFilterIndex) return;
            _timeFilterIndex = newIdx;
            _state.TimeFilter = TimeSteps[_timeFilterIndex];
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        public override void leftClickHeld(int x, int y)
        {
            if (_activeTab == 2 && _draggingSlider)
                ApplySliderX(x);
            base.leftClickHeld(x, y);
        }

        public override void releaseLeftClick(int x, int y)
        {
            _draggingSlider = false;
            base.releaseLeftClick(x, y);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (_rebindTarget != null)
            {
                if (key != Keys.Escape)
                {
                    var btn = (StardewModdingAPI.SButton)(int)key;
                    if (_rebindTarget == "menu")        _config.MenuKey      = btn;
                    else if (_rebindTarget == "debug")  _config.DebugKey     = btn;
                    else if (_rebindTarget == "select") _config.SelectNpcKey = btn;
                    _saveConfig();
                }
                _rebindTarget = null;
                return;
            }

            if (_searchFocused)
            {
                if (key == Keys.Escape || key == Keys.Enter)
                {
                    _searchFocused = false;
                }
                else if (key == Keys.Back && _npcSearch.Length > 0)
                {
                    _npcSearch = _npcSearch.Substring(0, _npcSearch.Length - 1);
                    RebuildNpcFilter();
                }
                return; // не передаём базовому классу — он может закрыть меню
            }

            base.receiveKeyPress(key);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            if (_activeTab == 1)
            {
                int delta = direction > 0 ? -1 : 1;
                _npcScrollOffset = Math.Max(0,
                    Math.Min(_npcScrollOffset + delta,
                        Math.Max(0, _filteredNpcs.Count - NPC_VISIBLE)));
                return;
            }

            if (_activeTab == 2)
            {
                ChangeTimeFilter(direction > 0 ? -1 : 1);
                Game1.playSound("smallSelect");
            }
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            InitPosition();
            RebuildTab();
        }

        // ── Вспомогательные ───────────────────────────────────────────────────────────

        /// <summary> Проверяет попадание в чип и сдвигает x вправо. </summary>
        private bool HitChip(ref int cx, int cy, string label, int mx, int my)
        {
            int w = (int)Game1.smallFont.MeasureString(label).X + 16;
            bool hit = new Rectangle(cx, cy, w, 28).Contains(mx, my);
            cx += w + 6;
            return hit;
        }

        private IEnumerable<string> ModGroups() =>
            _registry.NpcModSource.Values.Distinct().OrderBy(s => s);

        private void ChangeTimeFilter(int delta)
        {
            _timeFilterIndex = MathHelper.Clamp(_timeFilterIndex + delta, 0, TimeSteps.Length - 1);
            _state.TimeFilter = TimeSteps[_timeFilterIndex];
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        private void DrawSectionHeader(SpriteBatch b, string text, int x, int y) =>
            Utility.drawTextWithShadow(b, text, Game1.dialogueFont,
                new Vector2(x, y), new Color(90, 70, 50));

        private void DrawGroupHeader(SpriteBatch b, string text, int x, int y) =>
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(x, y), new Color(110, 90, 65));

        private void DrawDivider(SpriteBatch b, int y)
        {
            int x1 = BX + PAD;
            int x2 = BX + BOX_W - PAD;
            b.Draw(Game1.staminaRect, new Rectangle(x1, y, x2 - x1, 2),
                new Color(180, 155, 110, 150));
        }

        private void DrawCentered(SpriteBatch b, string text, SpriteFont font, int y, Color color)
        {
            var sz = font.MeasureString(text);
            Utility.drawTextWithShadow(b, text, font,
                new Vector2(BX + (BOX_W - sz.X) / 2f, y), color);
        }

        private void DrawKV(SpriteBatch b, string key, string value, int x, ref int y)
        {
            Utility.drawTextWithShadow(b, key + ":", Game1.smallFont,
                new Vector2(x, y), new Color(100, 90, 75));
            float offset = Game1.smallFont.MeasureString(key + ":  ").X;
            Utility.drawTextWithShadow(b, value, Game1.smallFont,
                new Vector2(x + offset, y), new Color(60, 50, 40));
            y += 26;
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }
}
