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
        private const int BOX_W = 580;
        private const int BOX_H = 660;   // немного больше — место для секции "Внешний вид"
        private const int TAB_W = 58;
        private const int TAB_H = 110;
        private const int TAB_GAP = 6;
        private const int PAD = 22;
        private const int NPC_ROW_H = 38;
        private const int NPC_VISIBLE = 11;

        // Доступные имена цветов (порядок важен — используется для циклического переключения)
        private static readonly string[] ColorNames =
        {
            "Green", "Blue", "Red", "Yellow", "Orange", "Purple", "White", "Cyan", "Pink"
        };

        // ── Зависимости ──────────────────────────────────────────────────────────────
        private readonly IMonitor _monitor;
        private readonly ModState _state;
        private readonly NpcRegistry _registry;
        private readonly TileRenderer _tiles;
        private readonly ModConfig _config;
        private readonly Action _saveConfig;
        private readonly ITranslationHelper _i18n;  // может быть null

        // ── Локализованные метки вкладок (инициализируются в конструкторе) ───────────
        private string[] _tabLabels;

        // ── Состояние вкладок ─────────────────────────────────────────────────────────
        private int _activeTab;

        // Главное
        private readonly List<ClickableCheckbox> _mainChecks = new List<ClickableCheckbox>();

        // NPC
        private string _npcSearch = string.Empty;
        private string _npcModFilter;
        private bool _searchFocused;
        private int _npcScrollOffset;
        private List<string> _filteredNpcs = new List<string>();

        // Настройки — клавиши и время
        private string _rebindTarget;
        private int _timeFilterIndex;
        private bool _draggingSlider;

        // Настройки — цвета и прозрачность
        private int _routeColorIndex;
        private int _positionColorIndex;
        private bool _draggingAlpha;

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
            Action saveConfig,
            ITranslationHelper i18n = null)
            : base(0, 0, BOX_W, BOX_H)
        {
            _monitor    = monitor    ?? throw new ArgumentNullException(nameof(monitor));
            _state      = state      ?? throw new ArgumentNullException(nameof(state));
            _registry   = registry   ?? throw new ArgumentNullException(nameof(registry));
            _tiles      = tiles      ?? throw new ArgumentNullException(nameof(tiles));
            _config     = config     ?? throw new ArgumentNullException(nameof(config));
            _saveConfig = saveConfig ?? throw new ArgumentNullException(nameof(saveConfig));
            _i18n       = i18n;

            // Инициализируем метки вкладок из переводов (с фолбэком)
            _tabLabels = new[]
            {
                T("tab.main"),
                T("tab.npc"),
                T("tab.settings"),
                T("tab.info")
            };

            // Восстанавливаем UI-индексы из текущего конфига
            int idx = Array.IndexOf(TimeSteps, _state.TimeFilter);
            _timeFilterIndex = idx >= 0 ? idx : 0;

            _routeColorIndex    = ColorIndexOf(_config.RouteColor);
            _positionColorIndex = ColorIndexOf(_config.PositionColor);

            Game1.game1.Window.TextInput += OnWindowTextInput;

            InitPosition();
            RebuildTab();
        }

        // ── Локализация ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает перевод по ключу. Если ITranslationHelper не задан или ключ не найден —
        /// возвращает ключ как есть (не крашится).
        /// </summary>
        private string T(string key)
        {
            if (_i18n == null) return key;
            var t = _i18n.Get(key);
            return t.HasValue() ? t.ToString() : key;
        }

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
            xPositionOnScreen = Game1.viewport.Width  / 2 - BOX_W / 2;
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

        // ── Главная вкладка ───────────────────────────────────────────────────────────

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
                    _state.RouteStepMode = v;
                    _state.RouteStepIndex = 0;
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

        // ── NPC вкладка ───────────────────────────────────────────────────────────────

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

        // ── Позиции NPC-вкладки ───────────────────────────────────────────────────────

        private const int RESET_BTN_W = 130;
        private Rectangle NpcSearchRect() =>
            new Rectangle(BX + PAD, BY + 58, BOX_W - PAD * 2 - RESET_BTN_W - 8, 36);
        private Rectangle NpcResetBtnRect() =>
            new Rectangle(BX + BOX_W - PAD - RESET_BTN_W, BY + 58, RESET_BTN_W, 36);

        private int NpcFilterY => NpcSearchRect().Bottom + 8;
        private int NpcListY   => NpcFilterY + 34;

        private Rectangle NpcRowRect(int visualIdx, int listW) =>
            new Rectangle(BX + PAD, NpcListY + visualIdx * NPC_ROW_H, listW, NPC_ROW_H - 2);

        // ── Позиции Settings-вкладки — клавиши ───────────────────────────────────────

        private Rectangle MenuKeyBtnRect()      => new Rectangle(BX + BOX_W - 185, BY + 103, 162, 34);
        private Rectangle DebugKeyBtnRect()     => new Rectangle(BX + BOX_W - 185, BY + 150, 162, 34);
        private Rectangle SelectNpcKeyBtnRect() => new Rectangle(BX + BOX_W - 185, BY + 197, 162, 34);

        // ── Позиции Settings-вкладки — фильтр времени ────────────────────────────────

        private int TimeRowY => BY + 331;
        private Rectangle TimePrevBtn() => new Rectangle(BX + BOX_W / 2 - 115, TimeRowY, 30, 30);
        private Rectangle TimeNextBtn() => new Rectangle(BX + BOX_W / 2 + 85,  TimeRowY, 30, 30);

        // Кнопки ◄ ► навигатора шагов (navY вычисляется динамически в Draw/Click)
        private Rectangle StepPrevBtn(int navY) => new Rectangle(BX + BOX_W / 2 - 115, navY, 30, 30);
        private Rectangle StepNextBtn(int navY) => new Rectangle(BX + BOX_W / 2 + 85,  navY, 30, 30);

        private Rectangle SliderTrackRect() =>
            new Rectangle(BX + PAD, TimeRowY + 44, BOX_W - PAD * 2, 8);

        private int SliderThumbX()
        {
            if (_timeFilterIndex == 0) return -1;
            var track = SliderTrackRect();
            int steps = TimeSteps.Length - 1;
            float t = (float)(_timeFilterIndex - 1) / Math.Max(1, steps - 1);
            return track.X + (int)(t * track.Width);
        }

        // ── Позиции Settings-вкладки — внешний вид ───────────────────────────────────

        // Секция "Внешний вид" начинается через ~40px после метки под слайдером времени
        private int AppearanceSectionY => TimeRowY + 44 + 8 + 22 + 40;   // ~BY + 449

        private int RouteColorRowY    => AppearanceSectionY + 38;
        private int PosColorRowY      => RouteColorRowY + 38;
        private int AlphaRowY         => PosColorRowY + 38;
        private int AlphaTrackY       => AlphaRowY + 28;

        // Кнопки ◄ ► для строки цвета
        private Rectangle ColorPrevBtn(int rowY) =>
            new Rectangle(BX + BOX_W - 196, rowY + 5, 24, 24);
        private Rectangle ColorNextBtn(int rowY) =>
            new Rectangle(BX + BOX_W - PAD - 24, rowY + 5, 24, 24);

        // Трек слайдера альфа
        private Rectangle AlphaTrackRect() =>
            new Rectangle(BX + PAD, AlphaTrackY, BOX_W - PAD * 2, 8);

        private int AlphaThumbX()
        {
            var track = AlphaTrackRect();
            // AlphaSteps[0]=0.1 .. AlphaSteps[9]=1.0  →  текущий _config.RouteAlpha
            float alpha = MathHelper.Clamp(_config.RouteAlpha, 0.1f, 1.0f);
            float t = (alpha - 0.1f) / 0.9f;
            return track.X + (int)(t * track.Width);
        }

        // ── Позиции вкладок ───────────────────────────────────────────────────────────

        private Rectangle TabRect(int i)
        {
            int startY = BY + (BOX_H - (_tabLabels.Length * TAB_H + (_tabLabels.Length - 1) * TAB_GAP)) / 2;
            return new Rectangle(BX - TAB_W, startY + i * (TAB_H + TAB_GAP), TAB_W, TAB_H);
        }

        // ── Отрисовка ─────────────────────────────────────────────────────────────────

        public override void draw(SpriteBatch b)
        {
            try
            {
                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    BX, BY, BOX_W, BOX_H, Color.White, 1f, true);

                DrawSideTabs(b);
                DrawCentered(b, T("menu.title"), Game1.dialogueFont, BY + PAD, Game1.textColor);

                switch (_activeTab)
                {
                    case 0: DrawMainTab(b);     break;
                    case 1: DrawNpcTab(b);      break;
                    case 2: DrawSettingsTab(b); break;
                    case 3: DrawInfoTab(b);     break;
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
            for (int i = 0; i < _tabLabels.Length; i++)
            {
                var rect   = TabRect(i);
                bool active = i == _activeTab;

                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    rect.X, rect.Y, rect.Width, rect.Height,
                    active ? Color.White : new Color(190, 180, 165), 1f, false);

                var font   = Game1.smallFont;
                var sz     = font.MeasureString(_tabLabels[i]);
                var origin = new Vector2(sz.X / 2f, sz.Y / 2f);
                var pos    = new Vector2(
                    rect.X + rect.Width  / 2f,
                    rect.Y + rect.Height / 2f);

                b.DrawString(font, _tabLabels[i], pos,
                    active ? Game1.textColor : new Color(100, 90, 75),
                    MathHelper.PiOver2, origin, 1f, SpriteEffects.None, 0.86f);
            }
        }

        // ── Главное ───────────────────────────────────────────────────────────────────

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

            // Навигатор шагов — показывается только когда:
            // пошаговый режим включён, NPC выбран, не глобальный маршрут, данные загружены.
            bool showNav = _state.RouteStepMode
                           && _state.SwitchTargetNPC
                           && !_state.SwitchGlobalNpcPath
                           && _state.RouteStepTotal > 0;

            if (showNav)
            {
                int navY = _mainChecks[4].Bounds.Bottom + 14;
                DrawArrow(b, StepPrevBtn(navY), left: true);
                DrawArrow(b, StepNextBtn(navY), left: false);

                string timeStr   = RouteRenderer.FormatTime(_state.RouteStepTime);
                string stepLabel = $"{timeStr}   ({_state.RouteStepIndex + 1} / {_state.RouteStepTotal})";
                DrawCentered(b, stepLabel, Game1.dialogueFont, navY + 2, new Color(200, 160, 30));

                // Ключ активного расписания (например "spring_Mon", "marriage")
                if (!string.IsNullOrEmpty(_state.RouteStepScheduleKey))
                    DrawCentered(b, _state.RouteStepScheduleKey, Game1.smallFont,
                        navY + 36, new Color(130, 100, 60));

                // Подсказка: скролл тоже листает шаги
                if (_state.RouteStepTotal > 1)
                {
                    string hint = T("main.stepHint");
                    DrawCentered(b, hint, Game1.smallFont, navY + 54, Color.Gray);
                }
            }
        }

        // ── NPC ───────────────────────────────────────────────────────────────────────

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
            var rect = NpcSearchRect();
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height,
                _searchFocused ? Color.White : new Color(245, 240, 228), 1f, false);

            bool empty = string.IsNullOrEmpty(_npcSearch);
            string placeholder = T("npc.search.placeholder");
            string display = empty && !_searchFocused
                ? placeholder
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
            int listW = BOX_W - PAD * 2;
            int end   = Math.Min(_npcScrollOffset + NPC_VISIBLE, _filteredNpcs.Count);
            var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

            for (int i = _npcScrollOffset; i < end; i++)
            {
                string npc      = _filteredNpcs[i];
                bool   selected = _state.SwitchTargetNPC && _registry.SelectedNpcNames.Contains(npc);
                var    row      = NpcRowRect(i - _npcScrollOffset, listW);

                if (selected)
                    b.Draw(Game1.staminaRect, row, new Color(255, 215, 90, 90));
                else if (row.Contains(mouse))
                    b.Draw(Game1.staminaRect, row, new Color(200, 195, 180, 55));

                float textY  = row.Y + (row.Height - Game1.dialogueFont.MeasureString("A").Y) / 2f;
                int   textX  = row.X + 10;
                var   gameNpc = _registry.GameNpcs?.FirstOrDefault(n => n?.Name == npc);

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
                    var srcSz = Game1.smallFont.MeasureString(src);
                    float srcY = row.Y + (row.Height - srcSz.Y) / 2f;
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
            var rect         = NpcResetBtnRect();
            bool hasSelection = _state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0;
            var bgColor      = hasSelection ? new Color(200, 80, 60) : new Color(180, 165, 140, 120);
            var textColor    = hasSelection ? Color.White : new Color(140, 130, 115);

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                rect.X, rect.Y, rect.Width, rect.Height, bgColor, 0.85f, false);

            string label = T("npc.reset");
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

            // ── Горячие клавиши ────────────────────────────────────────────────────
            DrawSectionHeader(b, T("settings.keybinds"), x, BY + 62);
            DrawKeybind(b, x, BY + 100, T("settings.keybind.menu"),      _config.MenuKey.ToString(),      "menu",   MenuKeyBtnRect());
            DrawKeybind(b, x, BY + 147, T("settings.keybind.debug"),     _config.DebugKey.ToString(),     "debug",  DebugKeyBtnRect());
            DrawKeybind(b, x, BY + 194, T("settings.keybind.selectNpc"), _config.SelectNpcKey.ToString(), "select", SelectNpcKeyBtnRect());

            DrawDivider(b, BY + 248);

            // ── Фильтр по времени ─────────────────────────────────────────────────
            DrawSectionHeader(b, T("settings.timeFilter"), x, BY + 264);

            DrawArrow(b, TimePrevBtn(), left: true);
            DrawArrow(b, TimeNextBtn(), left: false);

            string timeText = _state.TimeFilter < 0
                ? T("settings.timeFilter.all")
                : RouteRenderer.FormatTime(_state.TimeFilter);
            DrawCentered(b, timeText, Game1.dialogueFont, TimeRowY + 2, new Color(200, 160, 30));

            DrawTimeSlider(b);

            // Метки начала и конца
            var track     = SliderTrackRect();
            float labelY  = track.Bottom + 5;
            string lStart = "06:00";
            string lEnd   = "02:00";
            Utility.drawTextWithShadow(b, lStart, Game1.smallFont,
                new Vector2(track.X, labelY), Color.Gray);
            var endSz = Game1.smallFont.MeasureString(lEnd);
            Utility.drawTextWithShadow(b, lEnd, Game1.smallFont,
                new Vector2(track.Right - endSz.X, labelY), Color.Gray);

            // ── Внешний вид ───────────────────────────────────────────────────────
            DrawDivider(b, AppearanceSectionY - 12);
            DrawSectionHeader(b, T("settings.appearance"), x, AppearanceSectionY);

            DrawColorRow(b, T("settings.routeColor"),     _routeColorIndex,    RouteColorRowY);
            DrawColorRow(b, T("settings.positionColor"),  _positionColorIndex, PosColorRowY);
            DrawAlphaRow(b, x);
        }

        private void DrawTimeSlider(SpriteBatch b)
        {
            var track    = SliderTrackRect();
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

        /// <summary>Рисует строку выбора цвета: [Метка]  [◄] [▓ ColorName] [►]</summary>
        private void DrawColorRow(SpriteBatch b, string label, int colorIdx, int rowY)
        {
            // Метка слева
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(BX + PAD, rowY + 8), Game1.textColor);

            string colorName = ColorNames[colorIdx];
            var    xnaColor  = ModConfig.ParseColor(colorName, Color.White);

            // Кнопка ◄
            DrawArrow(b, ColorPrevBtn(rowY), left: true);

            // Цветной прямоугольник-swatch
            int swatchX = BX + BOX_W - 168;
            b.Draw(Game1.staminaRect, new Rectangle(swatchX, rowY + 5, 30, 24), xnaColor);

            // Имя цвета
            Utility.drawTextWithShadow(b, colorName, Game1.smallFont,
                new Vector2(swatchX + 36, rowY + 8), Game1.textColor);

            // Кнопка ►
            DrawArrow(b, ColorNextBtn(rowY), left: false);
        }

        /// <summary>Рисует строку прозрачности со слайдером 0.1–1.0.</summary>
        private void DrawAlphaRow(SpriteBatch b, int x)
        {
            // Метка
            string alphaLabel = $"{T("settings.alpha")}: {_config.RouteAlpha:P0}";
            Utility.drawTextWithShadow(b, alphaLabel, Game1.smallFont,
                new Vector2(x, AlphaRowY + 8), Game1.textColor);

            var   track   = AlphaTrackRect();
            bool  hovAlpha = new Rectangle(track.X, track.Y - 8, track.Width, track.Height + 16)
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
            var keySz = Game1.smallFont.MeasureString(keyText);
            Utility.drawTextWithShadow(b, keyText, Game1.smallFont,
                new Vector2(
                    btnRect.X + (btnRect.Width  - keySz.X) / 2f,
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

            DrawSectionHeader(b, T("info.currentState"), x, y); y += 36;
            if (Context.IsWorldReady)
            {
                string season = Game1.currentSeason ?? "—";
                DrawKV(b, T("info.date"),
                    $"{Capitalize(season)}, {Game1.dayOfMonth}, {Game1.year}",
                    x, ref y);
                DrawKV(b, T("info.location"),
                    Game1.currentLocation?.Name ?? "—", x, ref y);
            }
            else
            {
                Utility.drawTextWithShadow(b, T("info.worldNotReady"), Game1.smallFont,
                    new Vector2(x, y), Color.Gray);
                y += 26;
            }

            DrawDivider(b, y + 6); y += 26;

            DrawSectionHeader(b, T("info.npcStats"), x, y); y += 36;
            DrawKV(b, T("info.tracked"),    _registry.TotalNpcList.Count.ToString(), x, ref y);
            DrawKV(b, T("info.inLocation"), (Game1.currentLocation?.characters.Count ?? 0).ToString(), x, ref y);
            DrawKV(b, T("info.selected"),
                _state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0
                    ? _registry.SelectedNpcNames.Count.ToString()
                    : T("info.noneSelected"),
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

            DrawSectionHeader(b, T("info.bySource"), x, y); y += 36;

            var groups = _registry.NpcModSource
                .GroupBy(kv => kv.Value)
                .OrderBy(g => g.Key)
                .ToList();

            if (groups.Count == 0)
            {
                Utility.drawTextWithShadow(b, T("info.noData"),
                    Game1.smallFont, new Vector2(x, y), Color.Gray);
                y += 26;
            }
            else
            {
                foreach (var g in groups)
                    DrawKV(b, g.Key, $"{g.Count()} NPC", x, ref y);
            }

            DrawDivider(b, y + 8); y += 24;

            DrawSectionHeader(b, T("info.tip"), x, y); y += 32;
            Utility.drawTextWithShadow(b, T("info.tipText"),
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

                for (int i = 0; i < _tabLabels.Length; i++)
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
                    case 0: ClickMain(x, y, playSound);     break;
                    case 1: ClickNpc(x, y, playSound);      break;
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

        private void ClickNpc(int x, int y, bool playSound)
        {
            if (NpcSearchRect().Contains(x, y))
            {
                _searchFocused = true;
                return;
            }
            _searchFocused = false;

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
                        _state.SwitchTargetNPC = false;
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
            // Горячие клавиши
            if (MenuKeyBtnRect().Contains(x, y))      { _rebindTarget = "menu";   if (playSound) Game1.playSound("smallSelect"); return; }
            if (DebugKeyBtnRect().Contains(x, y))     { _rebindTarget = "debug";  if (playSound) Game1.playSound("smallSelect"); return; }
            if (SelectNpcKeyBtnRect().Contains(x, y)) { _rebindTarget = "select"; if (playSound) Game1.playSound("smallSelect"); return; }

            // Фильтр времени
            if (TimePrevBtn().Contains(x, y)) { ChangeTimeFilter(-1); if (playSound) Game1.playSound("smallSelect"); return; }
            if (TimeNextBtn().Contains(x, y)) { ChangeTimeFilter(+1); if (playSound) Game1.playSound("smallSelect"); return; }

            var timeTrack    = SliderTrackRect();
            var timeTrackHit = new Rectangle(timeTrack.X, timeTrack.Y - 10, timeTrack.Width, timeTrack.Height + 20);
            if (timeTrackHit.Contains(x, y))
            {
                _draggingSlider = true;
                ApplyTimeSliderX(x);
                if (playSound) Game1.playSound("smallSelect");
                return;
            }

            // Цвет маршрута — после смены нужно перерисовать тайлы
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
            var alphaTrack    = AlphaTrackRect();
            var alphaTrackHit = new Rectangle(alphaTrack.X, alphaTrack.Y - 10, alphaTrack.Width, alphaTrack.Height + 20);
            if (alphaTrackHit.Contains(x, y))
            {
                _draggingAlpha = true;
                ApplyAlphaSliderX(x);
                if (playSound) Game1.playSound("smallSelect");
            }
        }

        private void ApplyTimeSliderX(int mouseX)
        {
            var   track  = SliderTrackRect();
            float t      = MathHelper.Clamp((float)(mouseX - track.X) / track.Width, 0f, 1f);
            int   steps  = TimeSteps.Length - 1;
            int   newIdx = 1 + (int)(t * (steps - 1) + 0.5f);
            newIdx = MathHelper.Clamp(newIdx, 1, TimeSteps.Length - 1);
            if (newIdx == _timeFilterIndex) return;
            _timeFilterIndex = newIdx;
            _state.TimeFilter = TimeSteps[_timeFilterIndex];
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        private void ApplyAlphaSliderX(int mouseX)
        {
            var   track = AlphaTrackRect();
            float t     = MathHelper.Clamp((float)(mouseX - track.X) / track.Width, 0f, 1f);
            // Округляем до ближайшего шага из AlphaSteps
            int   idx   = (int)(t * (AlphaSteps.Length - 1) + 0.5f);
            idx = MathHelper.Clamp(idx, 0, AlphaSteps.Length - 1);
            float newAlpha = AlphaSteps[idx];
            if (Math.Abs(newAlpha - _config.RouteAlpha) < 0.001f) return;
            _config.RouteAlpha = newAlpha;
            _tiles.Alpha = newAlpha;
            _saveConfig();
        }

        public override void leftClickHeld(int x, int y)
        {
            if (_activeTab == 2)
            {
                if (_draggingSlider) ApplyTimeSliderX(x);
                if (_draggingAlpha)  ApplyAlphaSliderX(x);
            }
            base.leftClickHeld(x, y);
        }

        public override void releaseLeftClick(int x, int y)
        {
            _draggingSlider = false;
            _draggingAlpha  = false;
            base.releaseLeftClick(x, y);
        }

        public override void receiveKeyPress(Keys key)
        {
            if (_rebindTarget != null)
            {
                if (key != Keys.Escape)
                {
                    var btn = (StardewModdingAPI.SButton)(int)key;
                    if      (_rebindTarget == "menu")   _config.MenuKey      = btn;
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
                return;
            }

            base.receiveKeyPress(key);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            // Главная вкладка: скролл листает шаги когда активен пошаговый режим
            if (_activeTab == 0
                && _state.RouteStepMode
                && _state.SwitchTargetNPC
                && !_state.SwitchGlobalNpcPath
                && _state.RouteStepTotal > 0)
            {
                ChangeStep(direction > 0 ? -1 : 1);
                Game1.playSound("smallSelect");
                return;
            }

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

        private bool HitChip(ref int cx, int cy, string label, int mx, int my)
        {
            int w   = (int)Game1.smallFont.MeasureString(label).X + 16;
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

        /// <summary>
        /// Переключает шаг дневного маршрута и инициирует перерисовку тайлов.
        /// </summary>
        private void ChangeStep(int delta)
        {
            if (_state.RouteStepTotal <= 0) return;
            _state.RouteStepIndex = MathHelper.Clamp(
                _state.RouteStepIndex + delta, 0, _state.RouteStepTotal - 1);
            _tiles.Clear();
            _state.SwitchGetNpcPath = true;
        }

        private static int ColorIndexOf(string colorName)
        {
            int idx = Array.FindIndex(ColorNames,
                n => string.Equals(n, colorName, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? idx : 0;
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
