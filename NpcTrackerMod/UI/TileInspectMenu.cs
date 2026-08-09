using System;
using System.Collections.Generic;
using System.Linq;
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

        // ── Данные ────────────────────────────────────────────────────────────────
        private readonly Point _tile;
        private readonly List<NpcCardData> _npcData;

        // ── Скролл ────────────────────────────────────────────────────────────────
        private int _scrollOffset;

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
            Action<string> onToggleNpc = null)
            : base(0, 0, BOX_W, 0)
        {
            _monitor     = monitor  ?? throw new ArgumentNullException(nameof(monitor));
            _state       = state    ?? throw new ArgumentNullException(nameof(state));
            _registry    = registry ?? throw new ArgumentNullException(nameof(registry));
            _i18n        = i18n;
            _tile        = tile;
            _onToggleNpc = onToggleNpc;

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
            xPositionOnScreen = Game1.viewport.Width  / 2 - BOX_W / 2;
            yPositionOnScreen = Game1.viewport.Height / 2 - h / 2;
            width  = BOX_W;
            height = h;

            _closeBtn = new ClickableTextureComponent(
                new Rectangle(BX + BOX_W - 48, BY - 8, 48, 48),
                Game1.mouseCursors, new Rectangle(337, 494, 12, 12), 4f);
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

                var schedule     = new List<(string Time, string Location)>();
                string nextDest  = null;

                if (npc?.Schedule != null && npc.Schedule.Count > 0)
                {
                    foreach (int t in npc.Schedule.Keys.OrderBy(k => k))
                    {
                        var entry = npc.Schedule[t];
                        schedule.Add((RouteRenderer.FormatTime(t), entry.targetLocationName ?? "?"));
                    }

                    nextDest = Core.ScheduleDisplayHelper.GetNextDestinationLabel(npc, _i18n);
                }

                _registry.NpcModSource.TryGetValue(npcName, out string source);

                result.Add(new NpcCardData
                {
                    Name            = npcName,
                    TimeInfo        = timeInfo,
                    Source          = source ?? "—",
                    CurrentLocation = npc?.currentLocation?.Name ?? "—",
                    Schedule        = schedule,
                    NextDestination = nextDest,
                    Portrait        = npc?.Portrait
                });
            }

            return result;
        }

        // ── Отрисовка ────────────────────────────────────────────────────────────

        public override void draw(SpriteBatch b)
        {
            try
            {
                int h = BoxH;

                // Фон
                drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                    BX, BY, BOX_W, h, Color.White, 1f, true);

                // Заголовок
                string title = T("inspect.title");
                var titleSz = Game1.dialogueFont.MeasureString(title);
                Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                    new Vector2(BX + BOX_W / 2f - titleSz.X / 2f, BY + PAD),
                    Game1.textColor);

                // Координаты + кол-во NPC
                string coords = T("inspect.coords", new { x = _tile.X, y = _tile.Y, count = _npcData.Count });
                var coordSz = Game1.smallFont.MeasureString(coords);
                Utility.drawTextWithShadow(b, coords, Game1.smallFont,
                    new Vector2(BX + BOX_W / 2f - coordSz.X / 2f, BY + PAD + 38),
                    Color.Gray);

                // Разделитель
                int divY = BY + HEADER_H;
                b.Draw(Game1.staminaRect,
                    new Rectangle(BX + PAD, divY, BOX_W - PAD * 2, 2),
                    new Color(180, 160, 120, 150));

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
                    DrawScrollbar(b, divY + 12, VISIBLE_CARDS * CARD_H);

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

                _closeBtn.draw(b);
                drawMouse(b);
            }
            catch (Exception ex)
            {
                _monitor.Log($"TileInspectMenu.draw: {ex.Message}", LogLevel.Error);
                base.draw(b);
                drawMouse(b);
            }
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

            // Кнопка — справа, по вертикальному центру карточки.
            var btn = SelectBtnRect(cardTop);

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
            int nameMaxW = btn.X - 12 - nameX - 10;   // не заезжаем на кнопку
            string name = TruncateToWidth(Game1.dialogueFont, data.Name, nameMaxW);

            Utility.drawTextWithShadow(b, name, Game1.dialogueFont,
                new Vector2(nameX, py + 2),
                selected ? new Color(140, 78, 0) : Game1.textColor);

            // Чип источника — после имени, с ограничением ширины.
            float nameW = Game1.smallFont.MeasureString(name).X;
            DrawSourceChip(b, data.Source,
                (int)(nameX + nameW + 10), py + 8, btn.X - 12);

            // ── Строка 2: текущая локация + время посещения ──
            string locLine = T("inspect.currentLocation", new { location = data.CurrentLocation });
            if (!string.IsNullOrEmpty(data.TimeInfo))
                locLine += $"  ({data.TimeInfo})";
            Utility.drawTextWithShadow(b,
                TruncateToWidth(Game1.smallFont, locLine, btn.X - 12 - px),
                Game1.smallFont, new Vector2(px, py + 44), new Color(75, 75, 75));

            // ── Строка 3: следующий пункт назначения ──
            if (data.NextDestination != null)
            {
                Utility.drawTextWithShadow(b,
                    TruncateToWidth(Game1.smallFont, data.NextDestination, btn.X - 12 - px),
                    Game1.smallFont, new Vector2(px, py + 66), new Color(50, 120, 55));
            }
            else
            {
                Utility.drawTextWithShadow(b,
                    T("inspect.scheduleFinished"),
                    Game1.smallFont, new Vector2(px, py + 66), Color.Gray);
            }

            // ── Разделитель и расписание в 2 колонки ──
            b.Draw(Game1.staminaRect,
                new Rectangle(px, py + 92, panel.Right - 14 - px, 2),
                new Color(180, 155, 110, 150));

            if (data.Schedule.Count > 0)
            {
                int maxCols = 2;
                int maxRows = 2;
                int maxShow = maxCols * maxRows;            // 4 записи
                int availW  = btn.X - 12 - px;
                int colW    = availW / maxCols;
                var schedColor = new Color(100, 88, 62);
                var timeColor  = new Color(150, 105, 15);

                int col   = 0;
                int shown = 0;
                int sx    = px;
                int sy    = py + 104;

                foreach (var s in data.Schedule)
                {
                    if (shown >= maxShow) break;

                    // Время — золотым, локация — обычным, обрезана по ширине колонки.
                    Utility.drawTextWithShadow(b, s.Time, Game1.smallFont,
                        new Vector2(sx, sy), timeColor);
                    float timeW = Game1.smallFont.MeasureString(s.Time).X;
                    string loc  = TruncateToWidth(
                        Game1.smallFont, s.Location, colW - timeW - 8);
                    Utility.drawTextWithShadow(b, loc, Game1.smallFont,
                        new Vector2(sx + timeW + 6, sy), schedColor);

                    col++;
                    shown++;
                    if (col % maxCols == 0) { sx = px; sy += 20; }
                    else                    { sx = px + colW; }
                }

                if (data.Schedule.Count > maxShow)
                {
                    Utility.drawTextWithShadow(b, $"+{data.Schedule.Count - maxShow}",
                        Game1.smallFont, new Vector2(sx, sy), Color.Gray);
                }
            }

            // ── Кнопка выбрать/снять ──
            bool hov = btn.Contains(Game1.getMouseX(), Game1.getMouseY());
            Color btnBg = selected
                ? (hov ? new Color(190, 55, 35) : new Color(215, 72, 52))
                : (hov ? new Color(65, 138, 50) : new Color(48, 118, 36));

            drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                btn.X, btn.Y, btn.Width, btn.Height, btnBg, 0.8f, false);

            string btnLabel = selected ? T("inspect.btn.deselect") : T("inspect.btn.select");
            var    btnSz    = Game1.smallFont.MeasureString(btnLabel);
            Utility.drawTextWithShadow(b, btnLabel, Game1.smallFont,
                new Vector2(
                    btn.X + (btn.Width  - btnSz.X) / 2f,
                    btn.Y + (btn.Height - btnSz.Y) / 2f),
                Color.White);
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

        /// <summary> Прямоугольник кнопки для карточки с cardTop = верхний Y карточки. </summary>
        private Rectangle SelectBtnRect(int cardTop) =>
            new Rectangle(
                BX + BOX_W - PAD - 18 - BTN_W,
                cardTop + CARD_H / 2 - BTN_H / 2,
                BTN_W,
                BTN_H);

        private void DrawScrollbar(SpriteBatch b, int trackTop, int trackH)
        {
            int trackX = BX + BOX_W - PAD - 12;
            int thumbH  = Math.Max(20, trackH * VISIBLE_CARDS / Math.Max(1, _npcData.Count));
            int thumbY  = trackTop + (trackH - thumbH) * _scrollOffset /
                          Math.Max(1, _npcData.Count - VISIBLE_CARDS);

            b.Draw(Game1.staminaRect, new Rectangle(trackX, trackTop, 6, trackH),
                new Color(180, 165, 140, 100));
            b.Draw(Game1.staminaRect, new Rectangle(trackX, thumbY, 6, thumbH),
                new Color(130, 100, 60, 200));
        }

        // ── Обработка ввода ───────────────────────────────────────────────────────

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            try
            {
                // Закрыть
                if (_closeBtn.containsPoint(x, y))
                {
                    exitThisMenu();
                    if (playSound) Game1.playSound("bigDeSelect");
                    return;
                }

                // Кнопки на карточках
                int divY  = BY + HEADER_H;
                int cardY = divY + 12;
                int end   = Math.Min(_scrollOffset + VISIBLE_CARDS, _npcData.Count);

                for (int i = _scrollOffset; i < end; i++)
                {
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

        /// <summary>
        /// Вызывает callback переключения NPC, зарегистрированный в ModEntry.
        /// UI не содержит бизнес-логики выбора — только делегирует.
        /// </summary>
        private void ToggleNpc(string npcName, bool playSound)
        {
            _onToggleNpc?.Invoke(npcName);
            if (playSound) Game1.playSound("smallSelect");
        }

        public override void receiveScrollWheelAction(int direction)
        {
            if (direction > 0 && _scrollOffset > 0)
                _scrollOffset--;
            else if (direction < 0 && _scrollOffset < _npcData.Count - VISIBLE_CARDS)
                _scrollOffset++;
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
            public string CurrentLocation;
            public List<(string Time, string Location)> Schedule;
            public string NextDestination;
            public Texture2D Portrait;
        }
    }
}
