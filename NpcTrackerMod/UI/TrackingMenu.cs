using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    /// Разбит на partial-файлы по одному на вкладку + общие хелперы отрисовки:
    ///   TrackingMenu.DrawHelpers.cs — DrawArrow, DrawDivider, DrawCentered, …
    ///   TrackingMenu.MainTab.cs    — вкладка «Главное»
    ///   TrackingMenu.NpcTab.cs     — вкладка «NPC»
    ///   TrackingMenu.SettingsTab.cs — вкладка «Настройки»
    ///   TrackingMenu.InfoTab.cs    — вкладка «Инфо»
    /// </summary>
    public partial class TrackingMenu : IClickableMenu
    {
        // ── Размеры ──────────────────────────────────────────────────────────────────
        private const int BOX_W     = 580;
        private const int BOX_H     = 660;
        private const int TAB_W     = 58;
        private const int TAB_H     = 110;
        private const int TAB_GAP   = 6;
        private const int PAD       = 22;
        private const int NPC_ROW_H = 38;
        private const int NPC_VISIBLE = 11;

        // ── Зависимости ──────────────────────────────────────────────────────────────
        private readonly IMonitor            _monitor;
        private readonly ModState            _state;
        private readonly NpcRegistry         _registry;
        private readonly TileRenderer        _tiles;
        private readonly ModConfig           _config;
        private readonly Action              _saveConfig;
        private readonly ITranslationHelper  _i18n;

        // ── Состояние вкладок ─────────────────────────────────────────────────────────
        private string[] _tabLabels;
        private int      _activeTab;

        // Главное
        private readonly List<ClickableCheckbox> _mainChecks = new List<ClickableCheckbox>();

        // NPC
        private string       _npcSearch      = string.Empty;
        private string       _npcModFilter;
        private bool         _searchFocused;
        private int          _npcScrollOffset;
        private List<string> _filteredNpcs   = new List<string>();

        // Настройки — клавиши и слайдеры
        private string _rebindTarget;
        private int    _timeFilterIndex;
        private bool   _draggingSlider;
        private int    _routeColorIndex;
        private int    _positionColorIndex;
        private bool   _draggingAlpha;

        // Кеш группировки NPC по источникам (вкладка «Инфо»).
        // Пересчитывается только при изменении состава источников, а не каждый кадр.
        private List<(string Source, int Count)> _sourceGroupCache;
        private int _sourceGroupCacheCount = -1;

        // Кнопка закрытия
        private ClickableTextureComponent _closeBtn;

        // Короткие ссылки на позиции окна
        private int BX => xPositionOnScreen;
        private int BY => yPositionOnScreen;

        // ── Конструктор ───────────────────────────────────────────────────────────────

        public TrackingMenu(
            IMonitor            monitor,
            ModState            state,
            NpcRegistry         registry,
            TileRenderer        tiles,
            ModConfig           config,
            Action              saveConfig,
            ITranslationHelper  i18n = null)
            : base(0, 0, BOX_W, BOX_H)
        {
            _monitor    = monitor    ?? throw new ArgumentNullException(nameof(monitor));
            _state      = state      ?? throw new ArgumentNullException(nameof(state));
            _registry   = registry   ?? throw new ArgumentNullException(nameof(registry));
            _tiles      = tiles      ?? throw new ArgumentNullException(nameof(tiles));
            _config     = config     ?? throw new ArgumentNullException(nameof(config));
            _saveConfig = saveConfig ?? throw new ArgumentNullException(nameof(saveConfig));
            _i18n       = i18n;

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

        /// <summary> Возвращает перевод по ключу (без токенов). </summary>
        private string T(string key) => LocalizationHelper.Get(_i18n, key);

        /// <summary> Возвращает перевод по ключу с токенами (например, {{count}}). </summary>
        private string T(string key, object tokens) => LocalizationHelper.Get(_i18n, key, tokens);

        /// <summary> True пока поле поиска NPC в фокусе. </summary>
        public bool IsSearchFocused => _searchFocused;

        // ── Инициализация ──────────────────────────────────────────────────────────────

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
                var  rect   = TabRect(i);
                bool active = i == _activeTab;

                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    rect.X, rect.Y, rect.Width, rect.Height,
                    active ? Color.White : new Color(190, 180, 165), 1f, false);

                var  font   = Game1.smallFont;
                var  sz     = font.MeasureString(_tabLabels[i]);
                var  origin = new Vector2(sz.X / 2f, sz.Y / 2f);
                var  pos    = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

                b.DrawString(font, _tabLabels[i], pos,
                    active ? Game1.textColor : new Color(100, 90, 75),
                    MathHelper.PiOver2, origin, 1f, SpriteEffects.None, 0.86f);
            }
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
                        _activeTab     = i;
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

        public override void receiveKeyPress(Microsoft.Xna.Framework.Input.Keys key)
        {
            if (_rebindTarget != null)
            {
                if (key != Microsoft.Xna.Framework.Input.Keys.Escape)
                {
                    var btn = (SButton)(int)key;
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
                if (key == Microsoft.Xna.Framework.Input.Keys.Escape ||
                    key == Microsoft.Xna.Framework.Input.Keys.Enter)
                {
                    _searchFocused = false;
                }
                else if (key == Microsoft.Xna.Framework.Input.Keys.Back && _npcSearch.Length > 0)
                {
                    _npcSearch = _npcSearch.Substring(0, _npcSearch.Length - 1);
                    RebuildNpcFilter();
                }
                return;
            }

            // Стрелки ◄ ► переключают вкладки — удобно без мыши.
            if (key == Microsoft.Xna.Framework.Input.Keys.Left ||
                key == Microsoft.Xna.Framework.Input.Keys.Right)
            {
                int dir = key == Microsoft.Xna.Framework.Input.Keys.Right ? 1 : -1;
                _activeTab = (_activeTab + dir + _tabLabels.Length) % _tabLabels.Length;
                _searchFocused = false;
                RebuildTab();
                Game1.playSound("shwip");
                return;
            }

            base.receiveKeyPress(key);
        }

        public override void receiveScrollWheelAction(int direction)
        {
            // Главная: скролл листает шаги пошагового режима
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
    }
}
