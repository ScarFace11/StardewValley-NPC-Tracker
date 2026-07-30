using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NpcTrackerMod.Core;
using NpcTrackerMod.Tracking;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;

namespace NpcTrackerMod.Scheduling
{
    /// <summary>
    /// Строит маршруты NPC из расписаний игры и кастомных модов.
    /// </summary>
    public class ScheduleProcessor
    {
        private readonly IMonitor _monitor;
        private readonly NpcPathStore _store;
        private readonly NpcRegistry _registry;
        private readonly LocationMapper _mapper;

        // Состояние текущего прохода по маршруту (сбрасывается после каждого NPC)
        private string _lastLocationName;
        private string _endLocationName;

        public ScheduleProcessor(
            IMonitor monitor,
            NpcPathStore store,
            NpcRegistry registry,
            LocationMapper mapper)
        {
            _monitor = monitor;
            _store = store;
            _registry = registry;
            _mapper = mapper;
        }

        // ── Публичный API ────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает NPC, которых нужно визуализировать в текущем кадре.
        /// </summary>
        public IEnumerable<NPC> GetNpcsToTrack(bool allLocations, HashSet<string> tracked)
        {
            if (!allLocations)
                return Game1.currentLocation?.characters
                    .Where(n => tracked.Contains(n.Name))
                    ?? Enumerable.Empty<NPC>();

            return Game1.locations
                .Where(loc => loc?.characters != null)
                .SelectMany(loc => loc.characters)
                .Where(n => n != null && tracked.Contains(n.Name));
        }

        /// <summary>
        /// Строит дневной маршрут NPC из game.Schedule (предвычисленного движком).
        /// Если расписание пустое — делегирует в BuildGlobalRoute.
        /// </summary>
        public void BuildDayRoutes(NPC npc)
        {
            if (npc.Schedule?.Any() != true)
                return;

            var totalPath = new Dictionary<string, HashSet<Point>>();
            var timedPath = new Dictionary<int, Dictionary<string, HashSet<Point>>>();

            _lastLocationName = null;

            foreach (var entry in npc.Schedule)
            {
                var segments = FilterRouteByLocation(npc.currentLocation.Name, entry.Value.route);
                NpcPathStore.MergeSegments(totalPath, segments);
                if (segments.Count > 0)
                    timedPath[entry.Key] = segments;
            }

            if (totalPath.Count == 0)
            {
                BuildGlobalRoute(npc, null, null, null);
                return;
            }

            _registry.TotalNpcList.Add(npc.Name);
            _store.TimedDayPaths[npc.Name] = timedPath;

            // Сохраняем ключ активного расписания (например, "spring_Mon", "marriage").
            // Читается RouteRenderer для отображения в навигаторе пошагового режима.
            string activeKey = ScheduleVariantResolver.GetActiveKey(npc, _monitor);
            if (!string.IsNullOrEmpty(activeKey))
                _store.ActiveScheduleKeys[npc.Name] = activeKey;

            _lastLocationName = null;
            _store.AddPath(npc, _store.DayPaths, totalPath);
        }

        /// <summary>
        /// Строит глобальный маршрут NPC по всем записям сырого расписания.
        /// Используется как для built-in NPC (первый день), так и для кастомных (из модов).
        /// </summary>
        public void BuildGlobalRoute(NPC currentNpc, string npcName, string customPath, string customPathKey)
        {
            NPC npc = currentNpc ?? FindNpcByName(npcName);

            if (npc == null)
            {
                _monitor.Log($"NPC '{npcName}' не найден при построении глобального маршрута.", LogLevel.Warn);
                return;
            }

            if (customPath == null && npc.Schedule?.Any() != true)
                return;

            var masterSchedule = BuildMasterSchedule(npc, customPath, customPathKey);
            _registry.TotalNpcList.Add(npc.Name);

            var totalPath = new Dictionary<string, HashSet<Point>>();

            try
            {
                foreach (var kvp in masterSchedule)
                {
                    if (!ScheduleEntryParser.IsValid(kvp.Key, kvp.Value))
                    {
                        _monitor.Log($"NPC {npc.Name}: пропуск невалидного ключа '{kvp.Key}'", LogLevel.Warn);
                        _monitor.Log(kvp.Value, LogLevel.Debug);
                        continue;
                    }

                    try
                    {
                        ProcessMasterScheduleEntry(npc, kvp.Key, kvp.Value, totalPath);
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"Ошибка обработки ключа '{kvp.Key}' для '{npc.Name}': {ex.Message}", LogLevel.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка глобального маршрута '{npc.Name}': {ex.Message}", LogLevel.Error);
            }

            _lastLocationName = null;
            _store.AddPath(npc, _store.GlobalPaths, totalPath);
        }

        // ── Внутренняя обработка ─────────────────────────────────────────────────

        private void ProcessMasterScheduleEntry(
            NPC npc,
            string key,
            string rawData,
            Dictionary<string, HashSet<Point>> totalPath)
        {
            var slots = rawData.Split('/');

            // Используем TilePoint самого NPC — он всегда валиден, в отличие от поиска
            // персонажа в characters (NPC может ещё не переместиться на DayStarted).
            string startLocName = _endLocationName ?? npc.currentLocation?.Name;
            _endLocationName = null;

            if (!Game1.locations.Any(loc => loc.Name == startLocName))
            {
                _monitor.Log($"Не найдена локация '{startLocName}' для '{npc.Name}'", LogLevel.Warn);
                return;
            }

            string lastLocation = startLocName;
            int npcX = npc.TilePoint.X;
            int npcY = npc.TilePoint.Y;

            _lastLocationName = null;

            foreach (var slot in slots)
            {
                if (ScheduleEntryParser.ShouldSkip(slot)) continue;
                var parts = slot.Split(' ');

                // Специальный слот "TIME bed": NPC возвращается домой спать.
                // "bed" — ключевое слово движка, не реальная карта.
                // defaultMap — NetString, используем .Value.
                // defaultPosition — protected поле Character, снаружи недоступно;
                // вместо него берём первый warp-тайл домашней локации как точку входа.
                if (parts.Length == 2 && parts[1] == "bed")
                {
                    string homeMap = npc.defaultMap.Value;
                    if (!string.IsNullOrEmpty(homeMap))
                    {
                        // Warp-тайл у двери — ближайшая разумная точка внутри дома.
                        // Если варпов нет, fallback (1, 1); pathfind поймает исключение.
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
                                NpcPathStore.MergeSegments(totalPath,
                                    FilterRouteByLocation(npc.currentLocation?.Name, pathDesc.route));
                            }

                            lastLocation = homeMap;
                            npcX = bedX;
                            npcY = bedY;
                        }
                        catch (Exception ex)
                        {
                            _monitor.Log(
                                $"Bed pathfind error: {npc.Name} {lastLocation}({npcX},{npcY}) → bed @ {parts[0]}: {ex.Message}",
                                LogLevel.Error);
                            lastLocation = homeMap;
                            npcX = bedX;
                            npcY = bedY;
                        }
                    }
                    continue;
                }

                if (parts.Length <= 2) continue;

                ScheduleEntryParser.Parse(parts, _lastLocationName,
                    out string time, out string locationName,
                    out int x, out int y,
                    out int facingDir, out string endBehavior, out string endMessage);

                // Анимационные локации SpaceCore вида "a1000", "a1200" и т.д. —
                // NPC не перемещается, просто играет анимацию на месте. Пропускаем.
                if (IsAnimationLocation(locationName))
                    continue;

                try
                {
                    var pathDesc = npc.pathfindToNextScheduleLocation(
                        time, lastLocation, npcX, npcY,
                        locationName, x, y,
                        facingDir, endBehavior, endMessage);

                    if (pathDesc?.route != null)
                    {
                        NpcPathStore.MergeSegments(totalPath,
                            FilterRouteByLocation(npc.currentLocation?.Name, pathDesc.route));
                        lastLocation = locationName;
                        npcX = x;
                        npcY = y;
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log(
                        $"Pathfind error: {npc.Name} {lastLocation}({npcX},{npcY}) → {locationName}({x},{y}) @ {time}: {ex.Message}",
                        LogLevel.Error);
                    lastLocation = locationName;
                    npcX = x;
                    npcY = y;
                }
            }

            // Сохраняем конечную локацию — следующий вызов ProcessMasterScheduleEntry
            // (при итерации по нескольким ключам) начнёт именно отсюда.
            _endLocationName = lastLocation;
        }

        /// <summary>
        /// Разбивает стек точек маршрута на сегменты по локациям.
        /// Разрыв смежности (> 1 тайл) = переход через варп в новую локацию.
        /// </summary>
        public Dictionary<string, HashSet<Point>> FilterRouteByLocation(
            string startLocation, Stack<Point> points)
        {
            var result = new Dictionary<string, HashSet<Point>>();

            if (string.IsNullOrEmpty(startLocation) || points == null || !points.Any())
            {
                _monitor.Log(
                    $"FilterRoute: пустой маршрут для '{startLocation}' ({points?.Count ?? 0} точек)",
                    LogLevel.Debug);
                return result;
            }

            if (_lastLocationName == null)
                _lastLocationName = startLocation;

            var currentSegment = new HashSet<Point>();
            var prevCoord = Point.Zero;

            foreach (var pt in points)
            {
                if (prevCoord == Point.Zero)
                {
                    prevCoord = pt;
                    currentSegment.Add(pt);
                    continue;
                }

                bool adjacent = Math.Abs(pt.X - prevCoord.X) <= 1 &&
                                Math.Abs(pt.Y - prevCoord.Y) <= 1;

                if (adjacent)
                {
                    currentSegment.Add(pt);
                    prevCoord = pt;
                }
                else
                {
                    AppendSegment(result, _lastLocationName, currentSegment);
                    currentSegment = new HashSet<Point>();
                    string dest = _mapper.GetDestination(_lastLocationName, prevCoord);
                    if (dest != null) _lastLocationName = dest;
                    prevCoord = pt;
                }
            }

            if (currentSegment.Count > 0)
                AppendSegment(result, _lastLocationName, currentSegment);

            return result;
        }

        // ── Вспомогательные ──────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает true, если имя локации является анимационным ключом SpaceCore
        /// (формат: буква 'a' + цифры, например a1000, a1200).
        /// Такие локации не являются картами — NPC просто играет анимацию на месте.
        /// </summary>
        private static bool IsAnimationLocation(string locationName)
        {
            if (string.IsNullOrEmpty(locationName) || locationName.Length < 2)
                return false;
            if (locationName[0] != 'a')
                return false;
            for (int i = 1; i < locationName.Length; i++)
                if (!char.IsDigit(locationName[i]))
                    return false;
            return true;
        }

        private NPC FindNpcByName(string name)
            => _registry.GameNpcs?.FirstOrDefault(
                n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

        private Dictionary<string, string> BuildMasterSchedule(NPC npc, string customPath, string customKey)
        {
            if (customPath != null)
                return new Dictionary<string, string> { [customKey] = customPath };

            if (npc.Schedule == null || !npc.Schedule.Any())
                return new Dictionary<string, string>();

            // Используем ScheduleVariantResolver для выбора активного варианта расписания.
            // Это учитывает статус брака, погоду, сезон, сердечки дружбы —
            // вместо того чтобы строить «объединение» всех возможных маршрутов.
            var activeSchedule = ScheduleVariantResolver.GetActiveSchedule(npc, _monitor);

            // Разрешаем GOTO-редиректы верхнего уровня.
            // Если значение ключа — "GOTO <target>", подставляем значение целевого ключа.
            // Цикл защищён счётчиком, чтобы не уйти в бесконечную рекурсию.
            var rawData = npc.getMasterScheduleRawData();
            if (rawData == null || rawData.Count == 0)
                return activeSchedule;

            var resolved = new Dictionary<string, string>(activeSchedule.Count);
            foreach (var kvp in activeSchedule)
            {
                string value = kvp.Value;
                int redirects = 0;
                while (value != null && value.StartsWith("GOTO ") && redirects < 10)
                {
                    string targetKey = value.Substring(5).Trim();
                    if (!rawData.TryGetValue(targetKey, out value))
                    {
                        _monitor.Log(
                            $"[BuildMasterSchedule] {npc.Name}: GOTO цель '{targetKey}' не найдена.",
                            LogLevel.Warn);
                        value = null;
                    }
                    redirects++;
                }

                if (!string.IsNullOrEmpty(value) && !value.StartsWith("GOTO "))
                    resolved[kvp.Key] = value;
                else
                    _monitor.Log(
                        $"[BuildMasterSchedule] {npc.Name}: ключ '{kvp.Key}' пропущен после разрешения GOTO.",
                        LogLevel.Debug);
            }
            return resolved;
        }

        private static void AppendSegment(
            Dictionary<string, HashSet<Point>> result,
            string location,
            HashSet<Point> segment)
        {
            if (!result.TryGetValue(location, out var existing))
                result[location] = new HashSet<Point>(segment);
            else
                existing.UnionWith(segment);
        }
    }
}