using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Scheduling
{
    /// <summary>
    /// Хранит и разрешает варп-переходы между локациями.
    /// Ключ: имя локации → список (координата варпа, имя целевой локации).
    /// </summary>
    public class LocationMapper
    {
        private readonly IMonitor _monitor;

        /// <summary> Все варп-точки игры: локация → [(тайл, цель)]. </summary>
        public Dictionary<string, List<(Point Tile, string Target)>> TeleportCoords { get; }
            = new Dictionary<string, List<(Point, string)>>();

        public LocationMapper(IMonitor monitor)
        {
            _monitor = monitor;
        }

        /// <summary>
        /// Обходит все локации игры и заполняет TeleportCoords варпами и дверями.
        /// Вызывается один раз в начале первого дня.
        /// </summary>
        public void BuildFromGame()
        {
            TeleportCoords.Clear();

            foreach (var location in Game1.locations)
            {
                var list = new List<(Point, string)>();

                list.AddRange(
                    location.warps.Select(w => (new Point(w.X, w.Y), w.TargetName)));

                list.AddRange(
                    location.doors.Pairs.Select(d => (d.Key, d.Value)));

                TeleportCoords[location.Name] = list;
            }
        }

        /// <summary>
        /// Возвращает имя локации, куда телепортируется NPC из <paramref name="fromLocation"/>
        /// с позиции <paramref name="position"/>. Возвращает <c>null</c> если переход не найден.
        /// </summary>
        public string GetDestination(string fromLocation, Point position)
        {
            if (!TeleportCoords.TryGetValue(fromLocation, out var entries))
                return null;

            foreach (var entry in entries)
            {
                if (entry.Tile == position)
                    return entry.Target;
            }                // Не логируем: отсутствие варпа — нормальная ситуация
                // (разрыв смежности не означает переход между локациями).

            return null;
        }
    }
}