using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NpcTrackerMod.Core;
using NpcTrackerMod.Tracking;

namespace NpcTrackerMod.Multiplayer
{
    /// <summary>
    /// Сообщение, отправляемое хостом фарм-хэндам при начале дня.
    /// Содержит все предварительно построенные маршруты NPC.
    /// </summary>
    public class RouteMessage
    {
        /// <summary> Идентификатор типа сообщения для SMAPI Multiplayer. </summary>
        public const string MessageType = "DayRoutes";

        // Используем List<SerPoint> вместо HashSet<Point>, чтобы Newtonsoft.Json
        // мог безопасно сериализовать/десериализовать данные без зависимости от
        // специфики MonoGame Point (struct, поля vs свойства, GetHashCode и т.д.).

        /// <summary> Дневные пути: NPC → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<string, List<SerPoint>>> DayPaths { get; set; }
            = new Dictionary<string, Dictionary<string, List<SerPoint>>>();

        /// <summary> Глобальные пути: NPC → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<string, List<SerPoint>>> GlobalPaths { get; set; }
            = new Dictionary<string, Dictionary<string, List<SerPoint>>>();

        /// <summary> Дневные пути по временным слотам: NPC → время → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<int, Dictionary<string, List<SerPoint>>>> TimedDayPaths { get; set; }
            = new Dictionary<string, Dictionary<int, Dictionary<string, List<SerPoint>>>>();

        /// <summary>
        /// Маршруты вариантов расписания: NPC → вариант → время → локация → тайлы.
        /// </summary>
        public Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, List<SerPoint>>>>>
            VariantTimedPaths { get; set; }
            = new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, List<SerPoint>>>>>();

        /// <summary> Ключ активного расписания для каждого NPC. </summary>
        public Dictionary<string, string> ActiveScheduleKeys { get; set; }
            = new Dictionary<string, string>();

        /// <summary> Все доступные ключи вариантов расписания для каждого NPC. </summary>
        public Dictionary<string, List<string>> NpcVariantKeys { get; set; }
            = new Dictionary<string, List<string>>();

        /// <summary> Полный список отслеживаемых NPC. </summary>
        public List<string> TotalNpcList { get; set; }
            = new List<string>();

        // ── Преобразование из хранилища ────────────────────────────────────────────

        /// <summary>
        /// Создаёт сообщение из текущего состояния NpcPathStore и NpcRegistry.
        /// Вызывается хостом перед отправкой фарм-хэндам.
        /// </summary>
        public static RouteMessage FromStore(NpcPathStore store, NpcRegistry registry)
        {
            var msg = new RouteMessage();

            foreach (var npcKvp in store.DayPaths)
                msg.DayPaths[npcKvp.Key] = ConvertLocPaths(npcKvp.Value);

            foreach (var npcKvp in store.GlobalPaths)
                msg.GlobalPaths[npcKvp.Key] = ConvertLocPaths(npcKvp.Value);

            foreach (var npcKvp in store.TimedDayPaths)
            {
                var timedDict = new Dictionary<int, Dictionary<string, List<SerPoint>>>();
                foreach (var timeKvp in npcKvp.Value)
                    timedDict[timeKvp.Key] = ConvertLocPaths(timeKvp.Value);
                msg.TimedDayPaths[npcKvp.Key] = timedDict;
            }

            foreach (var npcKvp in store.VariantTimedPaths)
            {
                var variantDict =
                    new Dictionary<string, Dictionary<int, Dictionary<string, List<SerPoint>>>>();

                foreach (var variantKvp in npcKvp.Value)
                {
                    var timedDict = new Dictionary<int, Dictionary<string, List<SerPoint>>>();
                    foreach (var timeKvp in variantKvp.Value)
                        timedDict[timeKvp.Key] = ConvertLocPaths(timeKvp.Value);
                    variantDict[variantKvp.Key] = timedDict;
                }

                msg.VariantTimedPaths[npcKvp.Key] = variantDict;
            }

            foreach (var kvp in store.ActiveScheduleKeys)
                msg.ActiveScheduleKeys[kvp.Key] = kvp.Value;

            foreach (var kvp in registry.NpcVariantKeys)
                msg.NpcVariantKeys[kvp.Key] = new List<string>(kvp.Value);

            msg.TotalNpcList.AddRange(registry.TotalNpcList);

            return msg;
        }

        // ── Применение к хранилищу ────────────────────────────────────────────────

        /// <summary>
        /// Применяет полный снимок маршрутов к NpcPathStore и NpcRegistry.
        /// Вызывается клиентом после получения сообщения от хоста.
        /// </summary>
        public void ApplyTo(NpcPathStore store, NpcRegistry registry)
        {
            // The message is a complete snapshot, not a delta. Clear locally
            // generated routes first so a client's schedule cannot be merged
            // with the host's schedule.
            store.ClearAll();
            registry.ClearDay();

            foreach (var npcKvp in DayPaths)
                store.DayPaths[npcKvp.Key] = RestoreLocPaths(npcKvp.Value);

            foreach (var npcKvp in GlobalPaths)
                store.GlobalPaths[npcKvp.Key] = RestoreLocPaths(npcKvp.Value);

            foreach (var npcKvp in TimedDayPaths)
            {
                var timedDict = new Dictionary<int, Dictionary<string, HashSet<Point>>>();
                foreach (var timeKvp in npcKvp.Value)
                    timedDict[timeKvp.Key] = RestoreLocPaths(timeKvp.Value);
                store.TimedDayPaths[npcKvp.Key] = timedDict;
            }

            foreach (var npcKvp in VariantTimedPaths)
            {
                var variantDict =
                    new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<Point>>>>();

                foreach (var variantKvp in npcKvp.Value)
                {
                    var timedDict = new Dictionary<int, Dictionary<string, HashSet<Point>>>();
                    foreach (var timeKvp in variantKvp.Value)
                        timedDict[timeKvp.Key] = RestoreLocPaths(timeKvp.Value);
                    variantDict[variantKvp.Key] = timedDict;
                }

                store.VariantTimedPaths[npcKvp.Key] = variantDict;
            }

            foreach (var kvp in ActiveScheduleKeys)
                store.ActiveScheduleKeys[kvp.Key] = kvp.Value;

            foreach (var name in TotalNpcList)
                registry.TotalNpcList.Add(name);

            foreach (var kvp in NpcVariantKeys)
                registry.NpcVariantKeys[kvp.Key] = new List<string>(kvp.Value);
        }

        // ── Вспомогательные ──────────────────────────────────────────────────────

        private static Dictionary<string, List<SerPoint>> ConvertLocPaths(
            Dictionary<string, HashSet<Point>> src)
        {
            var dict = new Dictionary<string, List<SerPoint>>(src.Count);
            foreach (var kvp in src)
            {
                var list = new List<SerPoint>(kvp.Value.Count);
                foreach (var pt in kvp.Value)
                    list.Add(new SerPoint { X = pt.X, Y = pt.Y });
                dict[kvp.Key] = list;
            }
            return dict;
        }

        private static Dictionary<string, HashSet<Point>> RestoreLocPaths(
            Dictionary<string, List<SerPoint>> src)
        {
            var dict = new Dictionary<string, HashSet<Point>>(src.Count);
            foreach (var kvp in src)
            {
                var set = new HashSet<Point>(kvp.Value.Count);
                foreach (var sp in kvp.Value)
                    set.Add(new Point(sp.X, sp.Y));
                dict[kvp.Key] = set;
            }
            return dict;
        }
    }

    /// <summary>
    /// Простая сериализуемая замена Microsoft.Xna.Framework.Point.
    /// Используется вместо Point напрямую, чтобы гарантировать корректную
    /// сериализацию/десериализацию через Newtonsoft.Json независимо от платформы.
    /// </summary>
    public class SerPoint
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
