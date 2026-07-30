using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Core
{
    /// <summary>
    /// Чистое хранилище путей NPC. Никакой игровой логики — только данные и merge-операции.
    /// </summary>
    public class NpcPathStore
    {
        private readonly IMonitor _monitor;

        /// <summary> Дневные пути: NPC → локация → набор тайлов. </summary>
        public Dictionary<string, Dictionary<string, HashSet<Point>>> DayPaths { get; }
            = new Dictionary<string, Dictionary<string, HashSet<Point>>>();

        /// <summary> Глобальные пути по всему сырому расписанию: NPC → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<string, HashSet<Point>>> GlobalPaths { get; }
            = new Dictionary<string, Dictionary<string, HashSet<Point>>>();

        /// <summary> Дневные пути по временным слотам: NPC → время → локация → тайлы. </summary>
        public Dictionary<string, Dictionary<int, Dictionary<string, HashSet<Point>>>> TimedDayPaths { get; }
            = new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<Point>>>>();

        /// <summary>
        /// Ключ активного расписания для каждого NPC (например, "spring_Mon", "marriage", "rain").
        /// Заполняется ScheduleProcessor при построении дневного маршрута.
        /// Используется RouteRenderer для отображения в навигаторе пошагового режима.
        /// </summary>
        public Dictionary<string, string> ActiveScheduleKeys { get; }
            = new Dictionary<string, string>();

        public NpcPathStore(IMonitor monitor)
        {
            _monitor = monitor;
        }

        // ── Запись ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Добавляет маршрут NPC в указанный словарь.
        /// Если NPC или локация уже есть — тайлы объединяются (UnionWith).
        /// </summary>
        public void AddPath(NPC npc,
            Dictionary<string, Dictionary<string, HashSet<Point>>> target,
            Dictionary<string, HashSet<Point>> route)
        {
            if (npc?.Name == null) return;

            if (!target.TryGetValue(npc.Name, out var npcPaths))
            {
                npcPaths = new Dictionary<string, HashSet<Point>>();
                target[npc.Name] = npcPaths;
                _monitor.Log($"Добавлен NPC в хранилище: {npc.Name}", LogLevel.Trace);
            }

            foreach (var kvp in route)
            {
                if (!npcPaths.TryGetValue(kvp.Key, out var existing))
                {
                    npcPaths[kvp.Key] = new HashSet<Point>(kvp.Value);
                    _monitor.Log($"Новая локация '{kvp.Key}' для {npc.Name}", LogLevel.Trace);
                }
                else
                {
                    existing.UnionWith(kvp.Value);
                    _monitor.Log($"Обновлены тайлы '{kvp.Key}' для {npc.Name}", LogLevel.Trace);
                }
            }
        }

        /// <summary>
        /// Объединяет сегменты source в target (без дубликатов тайлов).
        /// </summary>
        public static void MergeSegments(
            Dictionary<string, HashSet<Point>> target,
            Dictionary<string, HashSet<Point>> source)
        {
            foreach (var kvp in source)
            {
                if (!target.TryGetValue(kvp.Key, out var pts))
                    target[kvp.Key] = new HashSet<Point>(kvp.Value);
                else
                    pts.UnionWith(kvp.Value);
            }
        }

        // ── Очистка ───────────────────────────────────────────────────────────────

        /// <summary> Сбрасывает дневные данные (вызывается в начале каждого дня). </summary>
        public void ClearDay()
        {
            DayPaths.Clear();
            TimedDayPaths.Clear();
            ActiveScheduleKeys.Clear();
        }

        /// <summary> Полная очистка всех данных. </summary>
        public void ClearAll()
        {
            DayPaths.Clear();
            GlobalPaths.Clear();
            TimedDayPaths.Clear();
            ActiveScheduleKeys.Clear();
        }

        // ── Пошаговый доступ ─────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает отсортированный список временных ключей дневного маршрута NPC.
        /// Используется RouteRenderer и меню для пошагового просмотра.
        /// Пустой список означает, что тайминговых данных нет (fallback на DayPaths).
        /// </summary>
        public List<int> GetStepKeys(string npcName)
        {
            if (string.IsNullOrEmpty(npcName) ||
                !TimedDayPaths.TryGetValue(npcName, out var timedPath) ||
                timedPath == null)
                return new List<int>();

            var keys = new List<int>(timedPath.Keys);
            keys.Sort();
            return keys;
        }
    }
}
