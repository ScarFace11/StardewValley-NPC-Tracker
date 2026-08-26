using System;
using System.Collections.Generic;
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
        private bool         _availableOnly;  // Filter: only NPCs in current location

        // Настройки — клавиши и слайдеры
        private string _rebindTarget;
        private int    _timeFilterIndex;
        private bool   _draggingSlider;
        private int    _routeColorIndex;
        private int    _positionColorIndex;
        private bool   _draggingAlpha;

        // Прокрутка окна, когда оно выше вьюпорта
        private int  _menuScroll;
        private bool _draggingMenuScroll;

        // Кеш группировки NPC по источникам (вкладка «Инфо»).
        // Пересчитывается только при изменении состава источников, а не каждый кадр.
        private List<(string Source, int Count)> _sourceGroupCache;
        private int _sourceGroupCacheCount = -1;

        // Кнопка закрытия
        private ClickableTextureComponent _closeBtn;

        // Hover-тултип текущего кадра — устанавливается в draw вкладок,
        // рисуется в конце draw (только при наведении, без постоянного текста).
        private string _hoverText;

        // Геймпад: интерактивные регионы текущей вкладки + индекс фокуса.
        // Список пересобирается при перестроении вкладки (RebuildTab).
        private readonly List<(Rectangle Rect, Action Action)> _interactive
            = new List<(Rectangle, Action)>();
        private int  _snapIndex;
        private bool _gamepadActive;

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
            _customRouteColor   = ModConfig.ParseColor(_config.RouteColor, Color.Green);
            _customPosColor     = ModConfig.ParseColor(_config.PositionColor, Color.Blue);

            // Восстанавливаем последнюю открытую вкладку
            int savedTab = _config.LastActiveTab;
            _activeTab = (savedTab >= 0 && savedTab < _tabLabels.Length) ? savedTab : 0;

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
            // Центрируем по горизонтали.
            xPositionOnScreen = Math.Max(8,
                Math.Min(Game1.viewport.Width  / 2 - BOX_W / 2,
                         Math.Max(8, Game1.viewport.Width  - BOX_W - 8)));

            // По вертикали: если окно помещается целиком — центрируем;
            // если оно выше вьюпорта — верх прижат к 8, а содержимое
            // прокручивается полоской справа (_menuScroll).
            int visibleH = Game1.viewport.Height - 16;
            if (BOX_H <= visibleH)
            {
                _menuScroll = 0;
                yPositionOnScreen = Math.Max(8,
                    Math.Min(Game1.viewport.Height / 2 - BOX_H / 2,
                             Math.Max(8, visibleH - BOX_H + 8)));
            }
            else
            {
                _menuScroll = MathHelper.Clamp(_menuScroll, 0, MaxMenuScroll);
                yPositionOnScreen = 8 - _menuScroll;
            }

            _closeBtn = new ClickableTextureComponent(
                new Rectangle(BX + BOX_W - 48, BY - 8, 48, 48),
                Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);
        }

        // ── Прокрутка окна (когда оно выше вьюпорта) ─────────────────────────────────

        /// <summary> Насколько окно выше вьюпорта (0 — помещается целиком). </summary>
        private int MaxMenuScroll => Math.Max(0, BOX_H - (Game1.viewport.Height - 16));

        /// <summary> Полоска прокрутки справа от окна, фиксирована на экране. </summary>
        private Rectangle MenuScrollTrackRect()
        {
            int x = Math.Min(BX + BOX_W + 6, Game1.viewport.Width - 14);
            return new Rectangle(x, 8, 8, Game1.viewport.Height - 16);
        }

        private int MenuScrollThumbH()
        {
            var track = MenuScrollTrackRect();
            return Math.Max(20, track.Height * track.Height / BOX_H);
        }

        private int MenuScrollThumbY()
        {
            var track = MenuScrollTrackRect();
            int max   = MaxMenuScroll;
            if (max <= 0) return track.Y;
            return track.Y + (track.Height - MenuScrollThumbH()) * _menuScroll / max;
        }

        private void DrawMenuScrollbar(SpriteBatch b)
        {
            var track = MenuScrollTrackRect();
            b.Draw(Game1.staminaRect, track, new Color(180, 165, 140, 100));
            b.Draw(Game1.staminaRect,
                new Rectangle(track.X, MenuScrollThumbY(), track.Width, MenuScrollThumbH()),
                new Color(130, 100, 60, 200));
        }

        private void ApplyMenuScrollFromY(int y)
        {
            int max = MaxMenuScroll;
            if (max <= 0) return;

            var  track  = MenuScrollTrackRect();
            int  thumbH = MenuScrollThumbH();
            int  range  = Math.Max(1, track.Height - thumbH);
            float t = MathHelper.Clamp(
                (float)(y - track.Y - thumbH / 2f) / range, 0f, 1f);

            _menuScroll = (int)Math.Round(t * max);
            yPositionOnScreen = 8 - _menuScroll;
        }

        private void RebuildTab()
        {
            _mainChecks.Clear();
            _npcScrollOffset = 0;

            if (_activeTab == 0) BuildMainChecks();
            if (_activeTab == 1) RebuildNpcFilter();

            RebuildInteractive();
        }

        /// <summary>
        /// Переключает вкладку (мышь, клавиатура и геймпад используют один путь).
        /// </summary>
        private void SwitchTab(int tab)
        {
            int next = (tab + _tabLabels.Length) % _tabLabels.Length;
            if (_activeTab == next) return;

            _activeTab     = next;
            _searchFocused = false;
            _rebindTarget  = null;
            _pickingColor  = null;
            RebuildTab();
            Game1.playSound("shwip");

            // Запоминаем последнюю открытую вкладку
            _config.LastActiveTab = next;
            _saveConfig();
        }

        // ── Геймпад: интерактивные регионы ───────────────────────────────────────

        /// <summary>
        /// Пересобирает список интерактивных регионов текущей вкладки.
        /// Прямоугольники совпадают с теми, что используют обработчики кликов, —
        /// фокус-рамка и клики геймпадом бьют ровно в те же элементы.
        /// </summary>
        private void RebuildInteractive()
        {
            _interactive.Clear();
            _snapIndex = 0;

            switch (_activeTab)
            {
                case 0:
                    for (int i = 0; i < _mainChecks.Count; i++)
                    {
                        int idx = i;
                        _interactive.Add((_mainChecks[i].Bounds, () => ToggleMainCheck(idx)));
                    }

                    if (StepNavVisible() && _mainChecks.Count >= 5)
                    {
                        int navY = _mainChecks[4].Bounds.Bottom + 14;
                        _interactive.Add((StepPrevBtn(navY), () => ChangeStep(-1)));
                        _interactive.Add((StepNextBtn(navY), () => ChangeStep(+1)));
                        _interactive.Add((TimelineRect(navY), () => ChangeStepToNext()));
                    }

                    if (VariantsVisible() && _mainChecks.Count >= 5)
                    {
                        int chipStartX = BX + PAD + 6;
                        int chipStartY = VariantsBlockY() + 34;
                        int chipMaxW   = BOX_W - PAD * 2 - 12;

                        foreach (var (key, rect) in ComputeVariantChipRects(chipStartX, chipStartY, chipMaxW))
                        {
                            string k = key;
                            _interactive.Add((rect, () => SelectVariant(k)));
                        }
                    }
                    break;

                case 1:
                    _interactive.Add((NpcSearchRect(), FocusNpcSearch));

                    foreach (var (_, mod, rect) in _modChips)
                    {
                        string m = mod;
                        _interactive.Add((rect, () => ToggleModFilter(m)));
                    }

                    _interactive.Add((NpcResetBtnRect(), ResetNpcSelection));

                    int listW = BOX_W - PAD * 2;
                    for (int i = _npcScrollOffset;
                         i < Math.Min(_npcScrollOffset + NPC_VISIBLE, _filteredNpcs.Count);
                         i++)
                    {
                        int idx = i;
                        _interactive.Add((
                            NpcRowRect(idx - _npcScrollOffset, listW),
                            () => ToggleNpcRow(idx)));
                    }
                    break;

                case 2:
                    _interactive.Add((MenuKeyBtnRect(),      () => StartRebind("menu")));
                    _interactive.Add((DebugKeyBtnRect(),     () => StartRebind("debug")));
                    _interactive.Add((SelectNpcKeyBtnRect(), () => StartRebind("select")));
                    _interactive.Add((TimePrevBtn(), () => ChangeTimeFilter(-1)));
                    _interactive.Add((TimeNextBtn(), () => ChangeTimeFilter(+1)));
                    _interactive.Add((TimeTrackHitRect(), () => ApplyTimeSliderX(SliderThumbX())));
                    _interactive.Add((ColorPrevBtn(RouteColorRowY), () => CycleRouteColor(-1)));
                    _interactive.Add((ColorNextBtn(RouteColorRowY), () => CycleRouteColor(+1)));
                    _interactive.Add((ColorPrevBtn(PosColorRowY),   () => CyclePosColor(-1)));
                    _interactive.Add((ColorNextBtn(PosColorRowY),   () => CyclePosColor(+1)));
                    _interactive.Add((AlphaTrackHitRect(), () => ApplyAlphaSliderX(AlphaThumbX())));
                    _interactive.Add((ResetSettingsBtnRect(), ResetSettings));
                    break;
            }
        }

        private void MoveSnap(int delta)
        {
            if (_interactive.Count == 0) return;
            _snapIndex = (_snapIndex + delta + _interactive.Count) % _interactive.Count;
            Game1.playSound("smallSelect");
        }

        private void ActivateSnapped()
        {
            if (_interactive.Count == 0) return;
            _interactive[_snapIndex].Action?.Invoke();
        }

        public override void receiveGamePadButton(Buttons b)
        {
            _gamepadActive = true;
            try
            {
                switch (b)
                {
                    case Buttons.LeftShoulder:
                    case Buttons.LeftTrigger:
                        SwitchTab(_activeTab - 1);
                        break;
                    case Buttons.RightShoulder:
                    case Buttons.RightTrigger:
                        SwitchTab(_activeTab + 1);
                        break;
                    case Buttons.DPadUp:
                    case Buttons.LeftThumbstickUp:
                        MoveSnap(-1);
                        break;
                    case Buttons.DPadDown:
                    case Buttons.LeftThumbstickDown:
                        MoveSnap(+1);
                        break;
                    case Buttons.DPadLeft:
                    case Buttons.LeftThumbstickLeft:
                        MoveSnap(-1);
                        break;
                    case Buttons.DPadRight:
                    case Buttons.LeftThumbstickRight:
                        MoveSnap(+1);
                        break;
                    case Buttons.A:
                        ActivateSnapped();
                        break;
                    case Buttons.B:
                        exitThisMenu();
                        Game1.playSound("bigDeSelect");
                        break;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка обработки геймпада в меню: {ex.Message}", LogLevel.Error);
            }
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
                _hoverText = null;

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

                DrawSnapFocus(b);

                // Полоска прокрутки, если окно выше вьюпорта.
                if (MaxMenuScroll > 0)
                    DrawMenuScrollbar(b);

                _closeBtn.draw(b);

                // Hover-тултип — только когда курсор над элементом с подсказкой.
                if (!string.IsNullOrEmpty(_hoverText))
                    IClickableMenu.drawHoverText(b, _hoverText, Game1.smallFont);

                drawMouse(b);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка отрисовки меню: {ex.Message}", LogLevel.Error);
                base.draw(b);
                drawMouse(b);
            }
        }

        /// <summary> Рисует золотую рамку вокруг элемента, сфокусированного геймпадом. </summary>
        private void DrawSnapFocus(SpriteBatch b)
        {
            if (!_gamepadActive || _interactive.Count == 0) return;
            if (_snapIndex >= _interactive.Count) return;

            var r = _interactive[_snapIndex].Rect;
            b.Draw(Game1.staminaRect, new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, 2), Color.Gold);
            b.Draw(Game1.staminaRect, new Rectangle(r.X - 2, r.Bottom, r.Width + 4, 2), Color.Gold);
            b.Draw(Game1.staminaRect, new Rectangle(r.X - 2, r.Y - 2, 2, r.Height + 4), Color.Gold);
            b.Draw(Game1.staminaRect, new Rectangle(r.Right, r.Y - 2, 2, r.Height + 4), Color.Gold);
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

                // Полоска прокрутки окна
                if (MaxMenuScroll > 0 && MenuScrollTrackRect().Contains(x, y))
                {
                    _draggingMenuScroll = true;
                    ApplyMenuScrollFromY(y);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }

                for (int i = 0; i < _tabLabels.Length; i++)
                {
                    if (!TabRect(i).Contains(x, y)) continue;
                    SwitchTab(i);
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
            if (_draggingMenuScroll) ApplyMenuScrollFromY(y);
            if (_activeTab == 2)
            {
                if (_draggingSlider) ApplyTimeSliderX(x);
                if (_draggingAlpha)  ApplyAlphaSliderX(x);
            }
            base.leftClickHeld(x, y);
        }

        public override void releaseLeftClick(int x, int y)
        {
            _draggingMenuScroll = false;
            _draggingSlider     = false;
            _draggingAlpha      = false;
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
                SwitchTab(_activeTab + dir);
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

            // Настройки: колёсико больше не меняет фильтр времени.
            // Если окно выше вьюпорта — колесо прокручивает меню.
            if (MaxMenuScroll > 0)
            {
                _menuScroll = MathHelper.Clamp(
                    _menuScroll + (direction > 0 ? -24 : 24), 0, MaxMenuScroll);
                yPositionOnScreen = 8 - _menuScroll;
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
