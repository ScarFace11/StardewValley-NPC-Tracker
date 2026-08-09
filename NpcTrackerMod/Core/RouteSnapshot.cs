using System.Collections.Generic;

namespace NpcTrackerMod.Core
{
    /// <summary>
    /// Полный снимок маршрутов NPC — сериализуемый DTO без зависимостей на XNA/SMAPI.
    /// Создаётся хостом (RouteSnapshot.Capture), передаётся фарм-хэндам в сжатом виде
    /// и применяется (RouteSnapshot.ApplyTo). Использует TilePoint вместо XNA Point,
    /// поэтому Newtonsoft.Json сериализует его напрямую — в отличие от прежнего
    /// обходного пути через SerPoint.
    /// </summary>
    public partial class RouteSnapshot
    {
        /// <summary> Текущая версия формата снимка. </summary>
        public const int CurrentVersion = 1;

        /// <summary> Версия снимка; проверяется получателем перед применением. </summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary> Дневные пути: NPC → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<string, List<TilePoint>>> DayPaths { get; set; }
            = new Dictionary<string, Dictionary<string, List<TilePoint>>>();

        /// <summary> Глобальные пути: NPC → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<string, List<TilePoint>>> GlobalPaths { get; set; }
            = new Dictionary<string, Dictionary<string, List<TilePoint>>>();

        /// <summary> Дневные пути по временным слотам: NPC → время → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>> TimedDayPaths { get; set; }
            = new Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>();

        /// <summary>
        /// Маршруты вариантов расписания: NPC → вариант → время → локация → тайлы.
        /// </summary>
        public Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>>
            VariantTimedPaths { get; set; }
            = new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>>();

        /// <summary> Ключ активного расписания для каждого NPC. </summary>
        public Dictionary<string, string> ActiveScheduleKeys { get; set; }
            = new Dictionary<string, string>();

        /// <summary> Все доступные ключи вариантов расписания для каждого NPC. </summary>
        public Dictionary<string, List<string>> NpcVariantKeys { get; set; }
            = new Dictionary<string, List<string>>();

        /// <summary> Полный список отслеживаемых NPC. </summary>
        public List<string> TotalNpcList { get; set; }
            = new List<string>();

        // ── Преобразования HashSet → List (для сериализации) ──────────────────────

        /// <summary> Копирует карту «локация → тайлы» в сериализуемый вид. </summary>
        internal static Dictionary<string, List<TilePoint>> ToListMap(
            Dictionary<string, HashSet<TilePoint>> src)
        {
            var result = new Dictionary<string, List<TilePoint>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = kvp.Value == null
                    ? new List<TilePoint>()
                    : new List<TilePoint>(kvp.Value);
            }
            return result;
        }

        /// <summary> Копирует карту «NPC → локация → тайлы» в сериализуемый вид. </summary>
        internal static Dictionary<string, Dictionary<string, List<TilePoint>>> ToListPaths(
            Dictionary<string, Dictionary<string, HashSet<TilePoint>>> src)
        {
            var result = new Dictionary<string, Dictionary<string, List<TilePoint>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToListMap(kvp.Value);
            }
            return result;
        }

        /// <summary> Копирует карту «время → локация → тайлы» в сериализуемый вид. </summary>
        internal static Dictionary<int, Dictionary<string, List<TilePoint>>> ToListTimed(
            Dictionary<int, Dictionary<string, HashSet<TilePoint>>> src)
        {
            var result = new Dictionary<int, Dictionary<string, List<TilePoint>>>();
            if (src == null) return result;

            foreach (var kvp in src)
                result[kvp.Key] = ToListMap(kvp.Value);
            return result;
        }

        /// <summary> Копирует карту «NPC → время → локация → тайлы» в сериализуемый вид. </summary>
        internal static Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>> ToListTimedPaths(
            Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>> src)
        {
            var result = new Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToListTimed(kvp.Value);
            }
            return result;
        }

        /// <summary> Копирует карту «NPC → вариант → время → локация → тайлы» в сериализуемый вид. </summary>
        internal static Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>>
            ToListVariantPaths(
                Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>> src)
        {
            var result =
                new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToListVariants(kvp.Value);
            }
            return result;
        }

        /// <summary> Копирует карту «вариант → время → локация → тайлы» в сериализуемый вид. </summary>
        internal static Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>> ToListVariants(
            Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>> src)
        {
            var result = new Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToListTimed(kvp.Value);
            }
            return result;
        }

        // ── Преобразования List → HashSet (из сериализованного вида) ──────────────

        /// <summary> Восстанавливает карту «локация → тайлы» из сериализованного вида. </summary>
        internal static Dictionary<string, HashSet<TilePoint>> ToSetMap(
            Dictionary<string, List<TilePoint>> src)
        {
            var result = new Dictionary<string, HashSet<TilePoint>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                var set = new HashSet<TilePoint>();
                if (kvp.Value != null)
                    foreach (var pt in kvp.Value)
                        set.Add(pt);
                result[kvp.Key] = set;
            }
            return result;
        }

        /// <summary> Восстанавливает карту «NPC → локация → тайлы» из сериализованного вида. </summary>
        internal static Dictionary<string, Dictionary<string, HashSet<TilePoint>>> ToSetPaths(
            Dictionary<string, Dictionary<string, List<TilePoint>>> src)
        {
            var result = new Dictionary<string, Dictionary<string, HashSet<TilePoint>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToSetMap(kvp.Value);
            }
            return result;
        }

        /// <summary> Восстанавливает карту «время → локация → тайлы» из сериализованного вида. </summary>
        internal static Dictionary<int, Dictionary<string, HashSet<TilePoint>>> ToSetTimed(
            Dictionary<int, Dictionary<string, List<TilePoint>>> src)
        {
            var result = new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>();
            if (src == null) return result;

            foreach (var kvp in src)
                result[kvp.Key] = ToSetMap(kvp.Value);
            return result;
        }

        /// <summary> Восстанавливает карту «NPC → время → локация → тайлы» из сериализованного вида. </summary>
        internal static Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>> ToSetTimedPaths(
            Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>> src)
        {
            var result = new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToSetTimed(kvp.Value);
            }
            return result;
        }

        /// <summary> Восстанавливает карту «NPC → вариант → время → локация → тайлы» из сериализованного вида. </summary>
        internal static Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>>
            ToSetVariantPaths(
                Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>> src)
        {
            var result =
                new Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToSetVariants(kvp.Value);
            }
            return result;
        }

        /// <summary> Восстанавливает карту «вариант → время → локация → тайлы» из сериализованного вида. </summary>
        internal static Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>> ToSetVariants(
            Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>> src)
        {
            var result = new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>();
            if (src == null) return result;

            foreach (var kvp in src)
            {
                if (kvp.Key == null) continue;
                result[kvp.Key] = ToSetTimed(kvp.Value);
            }
            return result;
        }
    }
}
