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
    /// Меню-инспектор тайла маршрута.
    /// Открывается по клику на любой тайл, через который проходит хотя бы один NPC.
    /// Отображает карточки всех NPC на тайле с расписанием, источником мода
    /// и кнопками выбора/снятия.
    /// Бизнес-логика переключения NPC вынесена в ModEntry через callback <see cref="_onToggleNpc"/>.
    /// </summary>
    public class TileInspectMenu : IClickableMenu
    {
        // ── Размеры ───────────────────────────────────────────────────────────────
        private const int BOX_W        = 700;
        private const int PAD          = 20;
        private const int HEADER_H     = 72;
        private const int CARD_H       = 172;
        private const int VISIBLE_CARDS = 3;
        private const int BTN_W        = 130;
        private const int MENU_BTN_W   = 86;
        private const int BTN_H        = 34;

        /// <summary> Высота меню зависит от числа NPC (но не более VISIBLE_CARDS карточек). </summary>
        private int BoxH =>
            HEADER_H + 14
            + Math.Min(_npcData.Count, VISIBLE_CARDS) * CARD_H
            + (_npcData.Count > VISIBLE_CARDS ? 30 : 16); // место под счётчик прокрутки

        // ── Зависимости ───────────────────────────────────────────────────────────
        private readonly IMonitor _monitor;
        private readonly ModState _state;
        private readonly NpcRegistry _registry;
        private readonly ITranslationHelper _i18n;

        /// <summary>
        /// Callback, переключающий NPC в множестве выбранных.
        /// Реализован в ModEntry — бизнес-логика выбора не должна жить в UI.
        /// </summary>
        private readonly Action<string> _onToggleNpc;

        /// <summary> Открывает главное меню с этим NPC (кнопка «Меню» на карточке). </summary>
        private readonly Action<string> _onOpenInMenu;

        // ── Данные ────────────────────────────────────────────────────────────────
        private readonly Point _tile;

        /// <summary> Внутреннее имя локации тайла (для бейджа «здесь сейчас»). </summary>
        private readonly string _locationName;

        private readonly List<NpcCardData> _npcData;

        // ── Скролл ────────────────────────────────────────────────────────────────
        private int _scrollOffset;
        private bool _draggingScroll;

        // ── Навигация с клавиатуры/геймпада ───────────────────────────────────────
        private readonly List<(Rectangle Rect, Action Action)> _interactive
            = new List<(Rectangle, Action)>();
        private int  _snapIndex;
        private bool _snapVisible;

        // ── Hover: полное расписание карточки (+N) ─────────────────────────────────
        private List<(int Time, string Location)> _hoverEntries;
        private string _hoverTitle;

        // ── Кнопка закрытия ───────────────────────────────────────────────────────
        private ClickableTextureComponent _closeBtn;

        // Короткие ссылки
        private int BX => xPositionOnScreen;
        private int BY => yPositionOnScreen;

        // ── Конструктор ───────────────────────────────────────────────────────────

        public TileInspectMenu(
            IMonitor monitor,
            ModState state,
            NpcRegistry registry,
            Point tile,
            List<(string NpcName, string TimeInfo)> owners,
            List<NPC> gameNpcs,
            ITranslationHelper i18n = null,
            Action<string> onToggleNpc = null,
            string locationName = null,
            Action<string> onOpenInMenu = null)
            : base(0, 0, BOX_W, 0)
        {
            _monitor     = monitor  ?? throw new ArgumentNullException(nameof(monitor));
            _state       = state    ?? throw new ArgumentNullException(nameof(state));
            _registry    = registry ?? throw new ArgumentNullException(nameof(registry));
            _i18n        = i18n;
            _tile        = tile;
            _locationName = locationName;
            _onToggleNpc = onToggleNpc;
            _onOpenInMenu = onOpenInMenu;

            _npcData = BuildNpcData(owners, gameNpcs ?? new List<NPC>());

            InitPosition();
        }

        // ── Локализация ───────────────────────────────────────────────────────────

        /// <summary> Возвращает перевод по ключу с необязательными токенами. </summary>
        private string T(string key, object tokens = null) =>
            LocalizationHelper.Get(_i18n, key, tokens);

        // ── Инициализация ─────────────────────────────────────────────────────────

        private void InitPosition()
        {
            int h = BoxH;

            // Центрируем, но не даём окну выйти за пределы вьюпорта.
            xPositionOnScreen = Math.Max(8,
                Math.Min(Game1.viewport.Width  / 2 - BOX_W / 2,
                         Math.Max(8, Game1.viewport.Width  - BOX_W - 8)));
            yPositionOnScreen = Math.Max(8,
                Math.Min(Game1.viewport.Height / 2 - h / 2,
                         Math.Max(8, Game1.viewport.Height - h - 8)));
            width  = BOX_W;
            height = h;

            _closeBtn = new ClickableTextureComponent(
                new Rectangle(BX + BOX_W - 48, BY - 8, 48, 48),
                Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);

            RebuildInteractive();
        }

        // ── Сборка данных карточек ────────────────────────────────────────────────

        private List<NpcCardData> BuildNpcData(
            List<(string NpcName, string TimeInfo)> owners,
            List<NPC> gameNpcs)
        {
            var result = new List<NpcCardData>();

            foreach (var (npcName, timeInfo) in owners)
            {
                var npc = gameNpcs.FirstOrDefault(n => n?.Name == npcName);

                _registry.NpcModSource.TryGetValue(npcName, out string source);

                result.Add(new NpcCardData
                {
                    Name     = npcName,
                    TimeInfo = timeInfo,
                    Source   = source ?? "—",
                    Npc      = npc,
                    Schedule = BuildSchedule(npc, npcName),
                    Portrait = npc?.Portrait
                });
            }

            return result;
        }

        /// <summary>
        /// Расписание из данных МОДА (то, что реально отрисовано на карте):
        /// вариант, если выбран и построен, иначе дневные тайминговые пути.
        /// Только при полном отсутствии данных — сырое игровое расписание как фолбэк.
        /// Локации локализуются (DisplayName), времена хранятся исходными (для фильтра).
        /// </summary>
        private List<(int Time, string Location)> BuildSchedule(NPC npc, string npcName)
        {
            var result = new List<(int, string)>();
            var store  = _registry.Store;

            Dictionary<int, Dictionary<string, HashSet<TilePoint>>> timed = null;

            string variant = _state.SelectedVariantKey;
            if (!string.IsNullOrEmpty(variant)
                && store.VariantTimedPaths.TryGetValue(npcName, out var vp)
                && vp.TryGetValue(variant, out timed))
            {
                // Активный вариант — тот же источник, что и у пошагового рендерера.
            }
            else if (store.TimedDayPaths.TryGetValue(npcName, out timed))
            {
                // Активное дневное расписание.
            }

            if (timed != null)
            {
                var keys = new List<int>(timed.Keys);
                keys.Sort();

                foreach (int t in keys)
                {
                    foreach (string loc in timed[t].Keys.OrderBy(l => l, StringComparer.Ordinal))
                        result.Add((t, DisplayLocation(loc)));
                }
                return result;
            }

            // Фолбэк: у мода нет данных — показываем сырое игровое расписание.
            if (npc?.Schedule != null)
            {
                foreach (int t in npc.Schedule.Keys.OrderBy(k => k))
                {
                    var entry = npc.Schedule[t];
                    result.Add((t, DisplayLocation(entry.targetLocationName ?? "?")));
                }
            }

            return result;
        }

        /// <summary> Локализованное имя локации (DisplayName), с фолбэком на внутреннее. </summary>
        private static string DisplayLocation(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            var loc = Game1.getLocationFromName(name);
            return string.IsNullOrEmpty(loc?.DisplayName) ? name : loc.DisplayName;
        }

        /// <summary> Запись расписания скрыта фильтром времени? </summary>
        private bool IsFiltered(int time) => _state.TimeFilter >= 0 && time > _state.TimeFilter;

        // ── Отрисовка ────────────────────────────────────────────────────────────

        public override void draw(SpriteBatch b)
        {
            try
            {
                int h = BoxH;
                _hoverEntries = null;
                _hoverTitle   = null;

                // Фон
                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    BX, BY, BOX_W, h, Color.White, 1f, true);

                // Заголовок
                string title = T("inspect.title");
                var titleSz = Game1.dialogueFont.MeasureString(title);
                Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                    new Vector2(BX + BOX_W / 2f - titleSz.X / 2f, BY + PAD),
                    Game1.textColor);

                // Локация + координаты + кол-во NPC
                string coords = T("inspect.coords", new
                {
                    location = DisplayLocation(_locationName),
                    x = _tile.X,
                    y = _tile.Y,
                    count = _npcData.Count
                });
                var coordSz = Game1.smallFont.MeasureString(coords);
                Utility.drawTextWithShadow(b, coords, Game1.smallFont,
                    new Vector2(BX + BOX_W / 2f - coordSz.X / 2f, BY + PAD + 38),
                    Color.Gray);

                // Разделитель
                int divY = BY + HEADER_H;
                b.Draw(Game1.staminaRect,
                    new Rectangle(BX + PAD, divY, BOX_W - PAD * 2, 2),
                    new Color(180, 160, 120, 150));

                if (_npcData.Count == 0)
                {
                    // Защита от пустого списка (например, гонка при открытии).
                    DrawCentered(b, T("inspect.empty"), BY + divY + 60, Color.Gray);
                }
                else
                {
                    // Карточки
                    int cardY = divY + 12;
                    int end   = Math.Min(_scrollOffset + VISIBLE_CARDS, _npcData.Count);
                    for (int i = _scrollOffset; i < end; i++)
                    {
                        DrawCard(b, _npcData[i], cardY);
                        cardY += CARD_H;
                    }

                    // Скроллбар
                    if (_npcData.Count > VISIBLE_CARDS)
                    {
                        DrawScrollbar(b);

                        // Счётчик
                        string counter = T("inspect.pagination", new
                        {
                            from  = _scrollOffset + 1,
                            to    = end,
                            total = _npcData.Count
                        });
                        var cSz = Game1.smallFont.MeasureString(counter);
                        Utility.drawTextWithShadow(b, counter, Game1.smallFont,
                            new Vector2(BX + BOX_W / 2f - cSz.X / 2f, BY + h - 26),
                            Color.Gray);
                    }
                }

                DrawSnapFocus(b);
                _closeBtn.draw(b);

                // Hover с полным расписанием (+N) — поверх всего.
                if (_hoverEntries != null)
                    DrawScheduleHover(b);

                drawMouse(b);
            }
            catch (Exception ex)
            {
                _monitor.Log($"TileInspectMenu.draw: {ex.Message}", LogLevel.Error);
                base.draw(b);
                drawMouse(b);
            }
        }

        private void DrawCentered(SpriteBatch b, string text, int y, Color color)
        {
            var sz = Game1.smallFont.MeasureString(text);
            Utility.drawTextWithShadow(b, text, Game1.smallFont,
                new Vector2(BX + (BOX_W - sz.X) / 2f, y), color);
        }

        /// <summary> Рисует золотую рамку вокруг элемента, сфокусированного клавиатурой/геймпадом. </summary>
        private void DrawSnapFocus(SpriteBatch b)
        {
            if (!_snapVisible || _interactive.Count == 0) return;
            if (_snapIndex >= _interactive.Count) return;

            var r = _interactive[_snapIndex].Rect;
            b.Draw(Game1.staminaRect, new Rectangle(r.X - 2, r.Y - 2, r.Width + 4, 2), Color.Gold);
            b.Draw(Game1.staminaRect, new Rectangle(r.X - 2, r.Bottom, r.Width + 4, 2), Color.Gold);
            b.Draw(Game1.staminaRect, new Rectangle(r.X - 2, r.Y - 2, 2, r.Height + 4), Color.Gold);
            b.Draw(Game1.staminaRect, new Rectangle(r.Right, r.Y - 2, 2, r.Height + 4), Color.Gold);
        }

        /// <summary> Пересобирает интерактивные регионы: кнопки видимых карточек + закрытие. </summary>
        private void RebuildInteractive()
        {
            _interactive.Clear();
            _snapIndex = 0;

            int divY  = BY + HEADER_H;
            int cardY = divY + 12;
            int end   = Math.Min(_scrollOffset + VISIBLE_CARDS, _npcData.Count);

            for (int i = _scrollOffset; i < end; i++)
            {
                int idx = i;
                var menuBtn = MenuBtnRect(cardY);
                var selBtn  = SelectBtnRect(cardY);
                _interactive.Add((menuBtn, (Action)(() => OpenInMenu(_npcData[idx].Name, playSound: true))));
                _interactive.Add((selBtn,  (Action)(() => ToggleNpc(_npcData[idx].Name, playSound: true))));
                cardY += CARD_H;
            }

            var closeRect = new Rectangle(BX + BOX_W - 48, BY - 8, 48, 48);
            _interactive.Add((closeRect, (Action)CloseInspector));
        }

        public override void receiveGamePadButton(Buttons b)
        {
            switch (b)
            {
                case Buttons.B:
                    CloseInspector();
                    break;
                case Buttons.DPadUp:
                case Buttons.LeftThumbstickUp:
                    MoveSnap(-1);
                    break;
                case Buttons.DPadDown:
                case Buttons.LeftThumbstickDown:
                    MoveSnap(+1);
                    break;
                case Buttons.A:
                    _snapVisible = true;
                    ActivateSnapped();
                    break;
            }
        }

        public override void receiveKeyPress(Keys key)
        {
            switch (key)
            {
                case Keys.Escape:
                    CloseInspector();
                    break;
                case Keys.Up:
                    MoveSnap(-1);
                    break;
                case Keys.Down:
                    MoveSnap(+1);
                    break;
                case Keys.Enter:
                    _snapVisible = true;
                    ActivateSnapped();
                    break;
            }
        }

        private void MoveSnap(int delta)
        {
            if (_interactive.Count == 0) return;
            _snapVisible = true;
            _snapIndex = (_snapIndex + delta + _interactive.Count) % _interactive.Count;
            Game1.playSound("smallSelect");
        }

        private void ActivateSnapped()
        {
            if (_interactive.Count == 0) return;
            _interactive[_snapIndex].Action?.Invoke();
        }

        private void DrawCard(SpriteBatch b, NpcCardData data, int cardTop)
        {
            bool selected = _state.SwitchTargetNPC &&
                            _registry.SelectedNpcNames.Contains(data.Name);

            // Панель в игровом стиле (bevel-рамка menuTexture) вместо плоского прямоугольника.
            var panel = new Rectangle(BX + PAD, cardTop + 6, BOX_W - PAD * 2 - 18, CARD_H - 12);
            var tint  = selected
                ? new Color(255, 233, 140)
                : new Color(252, 246, 232);
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                panel.X, panel.Y, panel.Width, panel.Height, tint, 1f, false);

            // Кнопки — справа, по вертикальному центру карточки.
            var menuBtn = MenuBtnRect(cardTop);
            var selBtn  = SelectBtnRect(cardTop);
            int contentRight = menuBtn.X - 12;

            int px = panel.X + 14;
            int py = panel.Y + 12;

            // ── Строка 1: аватарка + имя + чип источника ──
            int avatarSize = (int)Game1.dialogueFont.MeasureString("A").Y;
            if (data.Portrait != null)
            {
                b.Draw(data.Portrait,
                    new Rectangle(px, py, avatarSize, avatarSize),
                    new Rectangle(0, 0, 64, 64),
                    Color.White);
            }

            int nameX = px + (data.Portrait != null ? avatarSize + 8 : 0);
            int nameMaxW = contentRight - nameX - 10;   // не заезжаем на кнопки
            string name = TruncateToWidth(Game1.dialogueFont, data.Name, nameMaxW);

            Utility.drawTextWithShadow(b, name, Game1.dialogueFont,
                new Vector2(nameX, py + 2),
                selected ? new Color(140, 78, 0) : Game1.textColor);

            // Чип источника — после имени, с ограничением ширины.
            float nameW = Game1.smallFont.MeasureString(name).X;
            DrawSourceChip(b, data.Source,
                (int)(nameX + nameW + 10), py + 8, menuBtn.X - 12);

            // ── Строка 2: текущая локация + время посещения + «здесь сейчас» ──
            bool hereNow = data.Npc != null
                           && !string.IsNullOrEmpty(_locationName)
                           && data.Npc.currentLocation?.Name == _locationName
                           && data.Npc.TilePoint.X == _tile.X
                           && data.Npc.TilePoint.Y == _tile.Y;

            string currentLoc = data.Npc?.currentLocation != null
                ? DisplayLocation(data.Npc.currentLocation.Name)
                : "—";
            string locLine = T("inspect.currentLocation", new { location = currentLoc });
            if (!string.IsNullOrEmpty(data.TimeInfo))
                locLine += $"  ({data.TimeInfo})";
            if (hereNow)
                locLine += "  ● " + T("inspect.hereNow");

            Utility.drawTextWithShadow(b,
                TruncateToWidth(Game1.smallFont, locLine, contentRight - px),
                Game1.smallFont, new Vector2(px, py + 44),
                hereNow ? new Color(30, 120, 45) : new Color(75, 75, 75));

            // ── Строка 3: следующий пункт назначения (живой, каждый кадр) ──
            string nextDest = ScheduleDisplayHelper.GetNextDestinationLabel(data.Npc, _i18n);
            if (nextDest != null)
            {
                Utility.drawTextWithShadow(b,
                    TruncateToWidth(Game1.smallFont, nextDest, contentRight - px),
                    Game1.smallFont, new Vector2(px, py + 66), new Color(50, 120, 55));
            }
            else
            {
                Utility.drawTextWithShadow(b,
                    T("inspect.scheduleFinished"),
                    Game1.smallFont, new Vector2(px, py + 66), Color.Gray);
            }

            // ── Разделитель и расписание в 2 колонки (с затемнением по фильтру времени) ──
            int dividerY = py + 92;
            b.Draw(Game1.staminaRect,
                new Rectangle(px, dividerY, panel.Right - 14 - px, 2),
                new Color(180, 155, 110, 150));

            if (data.Schedule.Count > 0)
            {
                int maxCols = 2;
                int maxRows = 2;
                int maxShow = maxCols * maxRows;            // 4 записи
                int availW  = contentRight - px;
                int colW    = Math.Max(60, availW / maxCols);
                var normalTime = new Color(150, 105, 15);
                var normalLoc  = new Color(100, 88, 62);
                var dimColor   = new Color(155, 148, 135);

                int col   = 0;
                int shown = 0;
                int sx    = px;
                int sy    = py + 104;
                bool anyTruncated = false;

                foreach (var s in data.Schedule)
                {
                    if (shown >= maxShow) break;

                    bool dim = IsFiltered(s.Time);

                    string time = RouteRenderer.FormatTime(s.Time);
                    Utility.drawTextWithShadow(b, time, Game1.smallFont,
                        new Vector2(sx, sy), dim ? dimColor : normalTime);
                    float timeW = Game1.smallFont.MeasureString(time).X;
                    string loc  = TruncateToWidth(
                        Game1.smallFont, s.Location, colW - timeW - 8);
                    if (loc != s.Location && !string.IsNullOrEmpty(s.Location))
                        anyTruncated = true;
                    Utility.drawTextWithShadow(b, loc, Game1.smallFont,
                        new Vector2(sx + timeW + 6, sy), dim ? dimColor : normalLoc);

                    col++;
                    shown++;
                    if (col % maxCols == 0) { sx = px; sy += 20; }
                    else                    { sx = px + colW; }
                }

                if (data.Schedule.Count > maxShow)
                    Utility.drawTextWithShadow(b, $"+{data.Schedule.Count - maxShow}",
                        Game1.smallFont, new Vector2(sx, sy), Color.Gray);

                // Hover с полным расписанием — когда есть скрытые записи или обрезанные строки.
                bool overflow = data.Schedule.Count > maxShow;
                if (overflow || anyTruncated)
                {
                    var schedArea = new Rectangle(
                        px, dividerY, Math.Max(1, contentRight - px),
                        Math.Max(1, panel.Bottom - 4 - dividerY));
                    var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());

                    if (schedArea.Contains(mouse.X, mouse.Y))
                    {
                        _hoverEntries = data.Schedule;
                        _hoverTitle   = data.Name;
                    }
                }
            }

            // ── Кнопка «Меню» ──
            bool menuHov = menuBtn.Contains(Game1.getMouseX(), Game1.getMouseY());
            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                menuBtn.X, menuBtn.Y, menuBtn.Width, menuBtn.Height,
                menuHov ? new Color(90, 110, 150) : new Color(70, 88, 120), 0.8f, false);

            string menuLabel = T("inspect.btn.menu");
            var    menuSz    = Game1.smallFont.MeasureString(menuLabel);
            Utility.drawTextWithShadow(b, menuLabel, Game1.smallFont,
                new Vector2(
                    menuBtn.X + (menuBtn.Width  - menuSz.X) / 2f,
                    menuBtn.Y + (menuBtn.Height - menuSz.Y) / 2f),
                Color.White);

            // ── Кнопка выбрать/снять ──
            bool hov = selBtn.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color btnBg = selected
                ? (hov ? new Color(190, 55, 35) : new Color(215, 72, 52))
                : (hov ? new Color(65, 138, 50) : new Color(48, 118, 36));

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                selBtn.X, selBtn.Y, selBtn.Width, selBtn.Height, btnBg, 0.8f, false);

            string btnLabel = selected ? T("inspect.btn.deselect") : T("inspect.btn.select");
            var    btnSz    = Game1.smallFont.MeasureString(btnLabel);
            Utility.drawTextWithShadow(b, btnLabel, Game1.smallFont,
                new Vector2(
                    selBtn.X + (selBtn.Width  - btnSz.X) / 2f,
                    selBtn.Y + (selBtn.Height - btnSz.Y) / 2f),
                Color.White);
        }

        /// <summary>
        /// Hover-панель с полным расписанием карточки.
        /// Записи после фильтра времени затемняются (как и в карточке).
        /// </summary>
        private void DrawScheduleHover(SpriteBatch b)
        {
            int lineH = 20;
            int pad   = 10;
            float maxW = 0;

            if (!string.IsNullOrEmpty(_hoverTitle))
            {
                float tw = Game1.dialogueFont.MeasureString(_hoverTitle).X;
                if (tw > maxW) maxW = tw;
            }

            foreach (var e in _hoverEntries)
            {
                float tw = Game1.smallFont.MeasureString(
                    RouteRenderer.FormatTime(e.Time) + "  " + e.Location).X;
                if (tw > maxW) maxW = tw;
            }

            int w = (int)maxW + pad * 2 + 12;
            int h = (_hoverEntries.Count + (_hoverTitle != null ? 1 : 0)) * lineH + pad * 2 + 8;

            var mouse = new Point(Game1.getMouseX(), Game1.getMouseY());
            int x = Math.Max(8, Math.Min(mouse.X + 18, Game1.viewport.Width  - w - 8));
            int y = Math.Max(8, Math.Min(mouse.Y + 18, Game1.viewport.Height - h - 8));

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                x, y, w, h, new Color(252, 246, 232), 1f, false);

            int ty = y + pad;

            if (!string.IsNullOrEmpty(_hoverTitle))
            {
                Utility.drawTextWithShadow(b, _hoverTitle, Game1.dialogueFont,
                    new Vector2(x + pad, ty), new Color(90, 70, 50));
                ty += lineH;
            }

            foreach (var e in _hoverEntries)
            {
                bool dim = IsFiltered(e.Time);
                var  timeColor = dim ? new Color(155, 148, 135) : new Color(150, 105, 15);
                var  locColor  = dim ? new Color(155, 148, 135) : new Color(80, 65, 40);

                string time = RouteRenderer.FormatTime(e.Time);
                Utility.drawTextWithShadow(b, time, Game1.smallFont,
                    new Vector2(x + pad, ty), timeColor);
                float tw = Game1.smallFont.MeasureString(time).X;
                Utility.drawTextWithShadow(b, e.Location, Game1.smallFont,
                    new Vector2(x + pad + tw + 8, ty), locColor);

                ty += lineH;
            }
        }

        /// <summary> Рисует маленький чип с именем источника (мода), с ограничением ширины. </summary>
        private static void DrawSourceChip(SpriteBatch b, string source, int x, int y, int maxRight)
        {
            var sz     = Game1.smallFont.MeasureString(source);
            int  maxW  = Math.Max(40, maxRight - x);
            var  rect  = new Rectangle(x, y, Math.Min((int)sz.X + 14, maxW), 24);
            b.Draw(Game1.staminaRect, rect, new Color(185, 165, 120, 160));

            string shown = TruncateToWidth(Game1.smallFont, source, rect.Width - 14);
            Utility.drawTextWithShadow(b, shown, Game1.smallFont,
                new Vector2(rect.X + 7, rect.Y + (rect.Height - sz.Y) / 2f),
                new Color(80, 65, 40));
        }

        /// <summary>
        /// Обрезает строку с многоточием, чтобы она помещалась в maxWidth пикселей
        /// шрифта font. Используется, чтобы текст не выходил за рамки карточек.
        /// </summary>
        private static string TruncateToWidth(SpriteFont font, string text, float maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return text;
            if (font.MeasureString(text).X <= maxWidth) return text;

            for (int len = text.Length - 1; len > 1; len--)
            {
                if (font.MeasureString(text.Substring(0, len) + "…").X <= maxWidth)
                    return text.Substring(0, len) + "…";
            }
            return "…";
        }

        /// <summary> Кнопка «Меню» для карточки с cardTop = верхний Y карточки. </summary>
        private Rectangle MenuBtnRect(int cardTop) =>
            new Rectangle(
                BX + BOX_W - PAD - 18 - BTN_W - 8 - MENU_BTN_W,
                cardTop + CARD_H / 2 - BTN_H / 2,
                MENU_BTN_W,
                BTN_H);

        /// <summary> Кнопка «Выбрать/Снять» для карточки с cardTop = верхний Y карточки. </summary>
        private Rectangle SelectBtnRect(int cardTop) =>
            new Rectangle(
                BX + BOX_W - PAD - 18 - BTN_W,
                cardTop + CARD_H / 2 - BTN_H / 2,
                BTN_W,
                BTN_H);

        // ── Скроллбар ─────────────────────────────────────────────────────────────

        private int ScrollTrackTop => BY + HEADER_H + 12;
        private int ScrollTrackH   => VISIBLE_CARDS * CARD_H;

        private Rectangle ScrollTrackRect() =>
            new Rectangle(BX + BOX_W - PAD - 12, ScrollTrackTop, 6, ScrollTrackH);

        private int ScrollThumbH => Math.Max(20, ScrollTrackH * VISIBLE_CARDS / Math.Max(1, _npcData.Count));

        private int ScrollThumbY
        {
            get
            {
                int maxOff = Math.Max(1, _npcData.Count - VISIBLE_CARDS);
                return ScrollTrackTop + (ScrollTrackH - ScrollThumbH) * _scrollOffset / maxOff;
            }
        }

        private void DrawScrollbar(SpriteBatch b)
        {
            b.Draw(Game1.staminaRect, ScrollTrackRect(), new Color(180, 165, 140, 100));
            b.Draw(Game1.staminaRect,
                new Rectangle(ScrollTrackRect().X, ScrollThumbY, 6, ScrollThumbH),
                new Color(130, 100, 60, 200));
        }

        private void ApplyScrollFromY(int y)
        {
            if (_npcData.Count <= VISIBLE_CARDS) return;

            float t = MathHelper.Clamp(
                (float)(y - ScrollTrackTop - ScrollThumbH / 2f) / (ScrollTrackH - ScrollThumbH),
                0f, 1f);
            _scrollOffset = (int)Math.Round(t * (_npcData.Count - VISIBLE_CARDS));
            RebuildInteractive();
        }

        // ── Обработка ввода ───────────────────────────────────────────────────────

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            try
            {
                // Закрыть
                if (_closeBtn.containsPoint(x, y))
                {
                    CloseInspector();
                    return;
                }

                // Скроллбар — тащим
                if (_npcData.Count > VISIBLE_CARDS && ScrollTrackRect().Contains(x, y))
                {
                    _draggingScroll = true;
                    ApplyScrollFromY(y);
                    if (playSound) Game1.playSound("smallSelect");
                    return;
                }

                // Кнопки на карточках
                int divY  = BY + HEADER_H;
                int cardY = divY + 12;
                int end   = Math.Min(_scrollOffset + VISIBLE_CARDS, _npcData.Count);

                for (int i = _scrollOffset; i < end; i++)
                {
                    if (MenuBtnRect(cardY).Contains(x, y))
                    {
                        OpenInMenu(_npcData[i].Name, playSound);
                        return;
                    }
                    if (SelectBtnRect(cardY).Contains(x, y))
                    {
                        ToggleNpc(_npcData[i].Name, playSound);
                        return;
                    }
                    cardY += CARD_H;
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"TileInspectMenu.receiveLeftClick: {ex.Message}", LogLevel.Error);
            }
        }

        public override void leftClickHeld(int x, int y)
        {
            if (_draggingScroll)
                ApplyScrollFromY(y);
        }

        public override void releaseLeftClick(int x, int y)
        {
            _draggingScroll = false;
        }

        /// <summary>
        /// Вызывает callback переключения NPC, зарегистрированный в ModEntry.
        /// UI не содержит бизнес-логики выбора — только делегирует.
        /// </summary>
        private void ToggleNpc(string npcName, bool playSound)
        {
            _onToggleNpc?.Invoke(npcName);
            if (playSound) Game1.playSound("smallSelect");
        }

        /// <summary> Закрывает инспектор и открывает главное меню с этим NPC. </summary>
        private void OpenInMenu(string npcName, bool playSound)
        {
            _onOpenInMenu?.Invoke(npcName);
            if (playSound) Game1.playSound("smallSelect");
        }

        /// <summary> Закрывает меню (общий путь для мыши, клавиатуры и геймпада). </summary>
        private void CloseInspector()
        {
            exitThisMenu();
            Game1.playSound("bigDeSelect");
        }

        public override void receiveScrollWheelAction(int direction)
        {
            if (direction > 0 && _scrollOffset > 0)
                _scrollOffset--;
            else if (direction < 0 && _scrollOffset < _npcData.Count - VISIBLE_CARDS)
                _scrollOffset++;
            else
                return;

            RebuildInteractive();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            InitPosition();
        }

        // ── Внутренняя модель данных ──────────────────────────────────────────────

        private class NpcCardData
        {
            public string Name;
            public string TimeInfo;
            public string Source;

            /// <summary> Ссылка на игрового NPC — «Сейчас» и «следующий пункт» читаются живьём. </summary>
            public NPC Npc;

            /// <summary> Расписание из данных мода: исходное время (для фильтра) + локация. </summary>
            public List<(int Time, string Location)> Schedule;

            public Texture2D Portrait;
        }
    }
}
