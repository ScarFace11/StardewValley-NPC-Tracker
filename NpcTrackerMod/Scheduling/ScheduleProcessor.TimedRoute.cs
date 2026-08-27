using System;
using System.Collections.Generic;
using System.Linq;
using NpcTrackerMod.Core;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Scheduling
{
    /// <summary>
    /// Partial-класс ScheduleProcessor: метод для построения таймовых путей
    /// из кастомных строк расписания (моды: SVE, SpaceCore и т.д.).
    /// </summary>
    public partial class ScheduleProcessor
    {
        /// <summary>
        /// Строит TimedDayPaths и DayPaths из одной строки кастомного расписания.
        /// Используется TransferToProcessor для NPC, у которых npc.Schedule пуст
        /// (Content Patcher загружает расписания после DayStarted).
        /// </summary>
        /// <param name="npc">NPC.</param>
        /// <param name="scheduleKey">Ключ расписания (например, "spring_Mon", "rain").</param>
        /// <param name="rawValue">Сырая строка маршрута (например, "600 Town 5 10/900 SeedShop 25 13").</param>
        public void BuildTimedRoute(NPC npc, string scheduleKey, string rawValue)
        {
            if (npc == null || string.IsNullOrEmpty(rawValue)) return;

            // Парсим строку расписания по "/" — каждый слот = один временной шаг.
            var slots = rawValue.Split('/');

            string lastLocation = npc.currentLocation?.Name;
            int npcX = npc.TilePoint.X;
            int npcY = npc.TilePoint.Y;
            string lastLocationName = null;

            // Результат: timeInt → location → tiles
            var timedPath = new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>();
            var totalPath = new Dictionary<string, HashSet<TilePoint>>();

            foreach (var slot in slots)
            {
                if (ScheduleEntryParser.ShouldSkip(slot)) continue;
                var parts = slot.Split(' ');

                // Слот "TIME bed": NPC возвращается домой спать.
                if (parts.Length == 2 && parts[1] == "bed")
                {
                    string homeMap = npc.DefaultMap;
                    if (!string.IsNullOrEmpty(homeMap))
                    {
                        var homeLoc = Game1.getLocationFromName(homeMap);
                        int bedX = homeLoc?.warps?.Count > 0 ? homeLoc.warps[0].X : 1;
                        int bedY = homeLoc?.warps?.Count > 0 ? homeLoc.warps[0].Y : 1;
                        try
                        {
                            var pathDesc = npc.pathfindToNextScheduleLocation(
                                parts[0], lastLocation, npcX, npcY,
                                homeMap, bedX, bedY, 2, null, null);
                            if (pathDesc?.route != null)
                            {
                                var seg = FilterRouteByLocation(
                                    npc.currentLocation?.Name, pathDesc.route, ref lastLocationName);
                                NpcPathStore.MergeSegments(totalPath, seg);
                                if (seg.Count > 0 && int.TryParse(parts[0], out int bedTime)
                                    && !timedPath.ContainsKey(bedTime))
                                    timedPath[bedTime] = seg;
                            }
                            lastLocation = homeMap;
                            npcX = bedX;
                            npcY = bedY;
                        }
                        catch { lastLocation = homeMap; npcX = bedX; npcY = bedY; }
                    }
                    continue;
                }

                if (parts.Length <= 2) continue;

                ScheduleEntryParser.Parse(parts, lastLocationName,
                    out string time, out string locationName,
                    out int x, out int y,
                    out int facingDir, out string endBehavior, out string endMessage);

                if (IsAnimationLocation(locationName)) continue;
                if (!int.TryParse(time, out int timeInt)) continue;

                try
                {
                    var pathDesc = npc.pathfindToNextScheduleLocation(
                        time, lastLocation, npcX, npcY,
                        locationName, x, y, facingDir, endBehavior, endMessage);

                    if (pathDesc?.route != null)
                    {
                        var seg = FilterRouteByLocation(
                            npc.currentLocation?.Name, pathDesc.route, ref lastLocationName);
                        NpcPathStore.MergeSegments(totalPath, seg);
                        if (seg.Count > 0 && !timedPath.ContainsKey(timeInt))
                            timedPath[timeInt] = seg;
                    }
                    lastLocation = locationName;
                    npcX = x;
                    npcY = y;
                }
                catch (Exception ex)
                {
                    _monitor.Log(
                        $"[TimedRoute] {npc.Name} → {locationName}({x},{y}) @ {time}: {ex.Message}",
                        LogLevel.Debug);
                    lastLocation = locationName;
                    npcX = x;
                    npcY = y;
                }
            }

            if (totalPath.Count == 0) return;

            // Объединяем с существующими данными (может быть несколько ключей расписания).
            _registry.TotalNpcList.Add(npc.Name);

            if (timedPath.Count > 0)
            {
                if (_store.TimedDayPaths.TryGetValue(npc.Name, out var existing))
                {
                    foreach (var kvp in timedPath)
                    {
                        if (!existing.TryGetValue(kvp.Key, out var existSeg))
                            existing[kvp.Key] = kvp.Value;
                        else
                            NpcPathStore.MergeSegments(existSeg, kvp.Value);
                    }
                }
                else
                {
                    _store.TimedDayPaths[npc.Name] = timedPath;
                }
            }

            _store.AddPath(npc, _store.DayPaths, totalPath);

            // Сохраняем ключ активного расписания.
            if (string.IsNullOrEmpty(_store.ActiveScheduleKeys.GetValueOrDefault(npc.Name)))
            {
                string activeKey = ScheduleVariantResolver.GetActiveKey(npc, _monitor);
                if (!string.IsNullOrEmpty(activeKey))
                    _store.ActiveScheduleKeys[npc.Name] = activeKey;
            }
        }
    }
}
