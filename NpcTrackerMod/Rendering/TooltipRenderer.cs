using System;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NpcTrackerMod.Core;
using NpcTrackerMod.Tracking;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.Rendering
{
    /// <summary>
    /// Отрисовывает тултип при наведении курсора на тайл маршрута.
    /// Выделен из ModEntry в соответствии с принципом единственной ответственности:
    /// вся логика визуализации тултипа сосредоточена здесь, а не в точке входа мода.
    /// </summary>
    public class TooltipRenderer
    {
        private readonly TileRenderer       _tiles;
        private readonly NpcRegistry        _registry;
        private readonly ModConfig          _config;
        private readonly ITranslationHelper _i18n;
        private readonly IMonitor           _monitor;

        // Переиспользуемый буфер — не выделяется заново каждый кадр.
        private readonly StringBuilder _sb = new StringBuilder();

        public TooltipRenderer(
            TileRenderer       tiles,
            NpcRegistry        registry,
            ModConfig          config,
            ITranslationHelper i18n,
            IMonitor           monitor)
        {
            _tiles    = tiles    ?? throw new ArgumentNullException(nameof(tiles));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _config   = config   ?? throw new ArgumentNullException(nameof(config));
            _i18n     = i18n;
            _monitor  = monitor  ?? throw new ArgumentNullException(nameof(monitor));
        }

        /// <summary>
        /// Рисует тултип при наведении курсора на тайл маршрута.
        /// Вызывается каждый кадр из OnRenderedWorld.
        /// Ничего не рисует, если курсор не над тайлом маршрута.
        /// </summary>
        public void Draw(SpriteBatch batch)
        {
            int tx = (int)((Game1.viewport.X + Game1.getMouseX()) / Game1.tileSize);
            int ty = (int)((Game1.viewport.Y + Game1.getMouseY()) / Game1.tileSize);
            var hovered = new Point(tx, ty);

            if (!_tiles.TileOwners.TryGetValue(hovered, out var owners) || owners.Count == 0)
                return;

            _sb.Clear();
            foreach (var o in owners)
            {
                if (_sb.Length > 0) _sb.Append('\n');
                _sb.Append(string.IsNullOrEmpty(o.TimeInfo)
                    ? o.NpcName
                    : $"{o.NpcName} ({o.TimeInfo})");

                // Следующая точка расписания
                string nextHint = GetNextScheduleLabel(o.NpcName);
                if (nextHint != null)
                    _sb.Append($"\n  {nextHint}");
            }

            // Подсказка: клик открывает инспектор
            _sb.Append('\n');
            _sb.Append(LocalizationHelper.Get(_i18n, "tooltip.inspector",
                new { key = _config.SelectNpcKey }));

            IClickableMenu.drawHoverText(batch, _sb.ToString(), Game1.smallFont);
        }

        /// <summary>
        /// Возвращает локализованную строку «→ Saloon в 12:00» — следующая запись
        /// расписания NPC после текущего игрового времени.
        /// Null, если данных нет или день уже закончился.
        /// </summary>
        private string GetNextScheduleLabel(string npcName)
        {
            try
            {
                NPC npc = null;
                var gameNpcs = _registry.GameNpcs;
                if (gameNpcs != null)
                {
                    foreach (var n in gameNpcs)
                    {
                        if (n?.Name == npcName) { npc = n; break; }
                    }
                }
                return ScheduleDisplayHelper.GetNextDestinationLabel(npc, _i18n);
            }
            catch (Exception ex)
            {
                _monitor.Log($"GetNextScheduleLabel({npcName}): {ex.Message}", LogLevel.Trace);
                return null;
            }
        }
    }
}
