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
    /// </summary>
    public class TileInspectMenu : IClickableMenu
    {
        // ── Размеры ───────────────────────────────────────────────────────────────
        private const int BOX_W        = 650;
        private const int PAD          = 20;
        private const int HEADER_H     = 72;
        private const int CARD_H       = 148;
        private const int VISIBLE_CARDS = 3;
        private const int BTN_W        = 120;
        private const int BTN_H        = 32;

        /// <summary> Высота меню зависит от числа NPC (но не более VISIBLE_CARDS карточек). </summary>
        private int BoxH =>
            HEADER_H + 14
            + Math.Min(_npcData.Count, VISIBLE_CARDS) * CARD_H
            + (_npcData.Count > VISIBLE_CARDS ? 30 : 16); // место под счётчик прокрутки

        // ── Зависимости ───────────────────────────────────────────────────────────
        private readonly IMonitor _monitor;
        private readonly ModState _state;
        private readonly NpcRegistry _registry;
        private readonly TileRenderer _tiles;
        private readonly ITranslationHelper _i18n;

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
            TileRenderer tiles,
            Point tile,
            List<(string NpcName, string TimeInfo)> owners,
            List<NPC> gameNpcs,
            ITranslationHelper i18n = null)
            : base(0, 0, BOX_W, 0)
        {
            _monitor  = monitor  ?? throw new ArgumentNullException(nameof(monitor));
            _state    = state    ?? throw new ArgumentNullException(nameof(state));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _tiles    = tiles    ?? throw new ArgumentNullException(nameof(tiles));
            _i18n     = i18n;
            _tile     = tile;

            _npcData = BuildNpcData(owners, gameNpcs ?? new List<NPC>());

            InitPosition();
        }

        // ── Локализация ───────────────────────────────────────────────────────────

        /// <summary> Возвращает перевод по ключу с необязательными токенами. </summary>
        private string T(string key, object tokens = null)
        {
            if (_i18n == null) return key;
            var t = tokens != null ? _i18n.Get(key, tokens) : _i18n.Get(key);
            return t.HasValue() ? t.ToString() : key;
        }

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
                    int currentTime = Game1.timeOfDay;
                    int nextTime    = 0;

                    foreach (int t in npc.Schedule.Keys.OrderBy(k => k))
                    {
                        var entry = npc.Schedule[t];
                        string loc = entry.targetLocationName ?? "?";
                        schedule.Add((RouteRenderer.FormatTime(t), loc));
                        if (t > currentTime && nextTime == 0)
                            nextTime = t;
                    }

                    if (nextTime > 0)
                    {
                        var ne = npc.Schedule[nextTime];
                        nextDest = T("tooltip.nextAt", new
                        {
                            location = ne.targetLocationName ?? "?",
                            time     = RouteRenderer.FormatTime(nextTime)
                        });
                    }
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

            var cardRect = new Rectangle(BX + PAD, cardTop, BOX_W - PAD * 2 - 18, CARD_H - 6);

            // Фон карточки
            Color bg = selected
                ? new Color(255, 228, 100, 90)
                : new Color(242, 236, 220, 90);
            b.Draw(Game1.staminaRect, cardRect, bg);

            // Рамка карточки
            int brd         = 2;
            Color borderCol = selected
                ? new Color(200, 148, 18, 220)
                : new Color(180, 160, 120, 110);
            b.Draw(Game1.staminaRect, new Rectangle(cardRect.X, cardRect.Y, cardRect.Width, brd), borderCol);
            b.Draw(Game1.staminaRect, new Rectangle(cardRect.X, cardRect.Bottom - brd, cardRect.Width, brd), borderCol);
            b.Draw(Game1.staminaRect, new Rectangle(cardRect.X, cardRect.Y, brd, cardRect.Height), borderCol);
            b.Draw(Game1.staminaRect, new Rectangle(cardRect.Right - brd, cardRect.Y, brd, cardRect.Height), borderCol);

            int cx = cardRect.X + 10;
            int cy = cardTop   + 8;

            // ── Аватарка NPC (слева от имени, высота = высота шрифта) ──
            int avatarSize = (int)Game1.dialogueFont.MeasureString("A").Y;
            if (data.Portrait != null)
            {
                b.Draw(data.Portrait,
                    new Rectangle(cx, cy, avatarSize, avatarSize),
                    new Rectangle(0, 0, 64, 64),
                    Color.White);
                cx += avatarSize + 6;
            }

            // ── Имя NPC ──
            Color nameCol = selected ? new Color(140, 78, 0) : Game1.textColor;
            Utility.drawTextWithShadow(b, data.Name, Game1.dialogueFont,
                new Vector2(cx, cy), nameCol);

            // Чип источника (мод / ванилла)
            float nameW = Game1.dialogueFont.MeasureString(data.Name).X;
            DrawSourceChip(b, data.Source, (int)(cx + nameW + 10), cy + 6);
            cy += 38;

            // ── Текущая локация + метка тайла ──
            // Метку времени показываем только если она не пустая (для тайлов с фильтром по времени
            // или тайлов позиции NPC — у них timeInfo непустой).
            string locLine = T("inspect.currentLocation", new { location = data.CurrentLocation });
            if (!string.IsNullOrEmpty(data.TimeInfo))
                locLine += $"   ({data.TimeInfo})";
            Utility.drawTextWithShadow(b, locLine, Game1.smallFont,
                new Vector2(cx, cy), new Color(75, 75, 75));
            cy += 22;

            // ── Следующий пункт назначения ──
            if (data.NextDestination != null)
            {
                Utility.drawTextWithShadow(b, data.NextDestination, Game1.smallFont,
                    new Vector2(cx, cy), new Color(50, 120, 55));
                cy += 22;
            }
            else
            {
                Utility.drawTextWithShadow(b, T("inspect.scheduleFinished"), Game1.smallFont,
                    new Vector2(cx, cy), Color.Gray);
                cy += 22;
            }

            // ── Расписание (компактно, до 4 записей) ──
            if (data.Schedule.Count > 0)
            {
                var shown = data.Schedule.Take(4).ToList();
                string sched = string.Join("  ", shown.Select(s => $"{s.Time}→{s.Location}"));
                if (data.Schedule.Count > 4)
                    sched += $"  +{data.Schedule.Count - 4}";

                Utility.drawTextWithShadow(b, sched, Game1.smallFont,
                    new Vector2(cx, cy), new Color(100, 90, 68));
            }

            // ── Кнопка выбрать/снять ──
            var btn   = SelectBtnRect(cardTop);
            bool hov  = btn.Contains(Game1.getMouseX(), Game1.getMouseY());
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

        /// <summary> Рисует маленький чип с именем источника (мода). </summary>
        private static void DrawSourceChip(SpriteBatch b, string source, int x, int y)
        {
            var sz   = Game1.smallFont.MeasureString(source);
            var rect = new Rectangle(x, y, (int)sz.X + 12, 22);
            b.Draw(Game1.staminaRect, rect, new Color(200, 190, 160, 130));
            Utility.drawTextWithShadow(b, source, Game1.smallFont,
                new Vector2(rect.X + 6, rect.Y + (rect.Height - sz.Y) / 2f),
                Color.Gray);
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

        private void ToggleNpc(string npcName, bool playSound)
        {
            if (_registry.SelectedNpcNames.Contains(npcName))
                _registry.SelectedNpcNames.Remove(npcName);
            else
            {
                _registry.SelectedNpcNames.Add(npcName);
                _registry.CurrentNpcName = npcName;
            }

            _state.SwitchTargetNPC = _registry.SelectedNpcNames.Count > 0;

            _tiles.Clear();
            _registry.CurrentNpcList.Clear();
            _state.SwitchGetNpcPath  = true;
            _state.SwitchListFull    = false;

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
