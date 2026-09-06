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
    public partial class ScheduleProcessor
    {
        private readonly IMonitor _monitor;
        private readonly NpcPathStore _store;
        private readonly NpcRegistry _registry;
        private readonly LocationMapper _mapper;

        /// <summary>
        /// Кастомные расписания из JSON-файлов модов: NPC → ключ → список строк маршрутов.
        /// Регистрируются CustomScheduleLoader.TransferToProcessor через RegisterCustomSchedule.
        /// Используется как источник для вариантов пошагового режима у модовых NPC,
        /// у которых getMasterScheduleRawData() пуст.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, List<string>>> _customSchedules
            = new Dictionary<string, Dictionary<string, List<string>>>();

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
        /// Строит дневной маршрут NPC из game.Schedule (предвычисленного движком).
        /// Если расписание пустое — делегирует в BuildGlobalRoute.
        /// </summary>
        public void BuildDayRoutes(NPC npc)
        {
            if (npc.Schedule?.Any() != true)
            {
                // Для кастомных NPC расписание может быть ещё не загружено
                // (Content Patcher применяет патчи после DayStarted),
                // а getMasterScheduleRawData() у них пуст.
                // Дневной маршрут строим из зарегистрированных расписаний модов;
                // если их нет — пробуем rawData (тот же источник, что и BuildGlobalRoute).
                if (_customSchedules.TryGetValue(npc.Name, out var custom)
                    && custom.Count > 0)
                {
                    BuildDayRoutesFromCustom(npc, custom);
                }
                else
                {
                    BuildDayRoutesFromRawData(npc);
                }
                return;
            }

            var totalPath = new Dictionary<string, HashSet<TilePoint>>();
            var timedPath = new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>();

            // lastLocationName — локальная переменная, передаётся по ref в FilterRouteByLocation.
            // Сохраняет, в какой локации закончился предыдущий сегмент маршрута, чтобы корректно
            // определить принадлежность следующего сегмента при пересечении варпов.
            string lastLocationName = null;

            foreach (var entry in npc.Schedule)
            {
                var segments = FilterRouteByLocation(npc.currentLocation.Name, entry.Value.route, ref lastLocationName);
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

            // Сохраняем все доступные ключи вариантов расписания.
            // Читается TrackingMenu для отображения списка вариантов в пошаговом режиме.
            PopulateVariantKeys(npc);

            _store.AddPath(npc, _store.DayPaths, totalPath);
        }

        /// <summary>
        /// Строит дневной маршрут кастомного NPC (SVE, SpaceCore и т.д.)
        /// из зарегистрированных расписаний модов. Вызывается каждый день —
        /// как BuildDayRoutes для ванильных NPC.
        /// Тайминговые пути строятся только для активного ключа сегодня;
        /// остальные ключи доступны как варианты в пошаговом режиме.
        /// </summary>
        private void BuildDayRoutesFromCustom(NPC npc, Dictionary<string, List<string>> custom)
        {
            var keys = new List<string>(custom.Keys);
            string activeKey = ScheduleVariantResolver.GetActiveKeyFromKeys(npc, _monitor, keys);

            // Тайминговые пути — только активный ключ сегодня (как npc.Schedule у ванильных).
            if (!string.IsNullOrEmpty(activeKey)
                && custom.TryGetValue(activeKey, out var activePaths)
                && activePaths != null)
            {
                foreach (var path in activePaths)
                    BuildTimedRoute(npc, activeKey, path);
            }

            // Ключи вариантов для чипов пошагового режима.
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            _registry.NpcVariantKeys[npc.Name] = keys;

            // Активный ключ для навигатора пошагового режима.
            // BuildTimedRoute не может его вычислить: rawData у модовых NPC пуст.
            if (!string.IsNullOrEmpty(activeKey))
                _store.ActiveScheduleKeys[npc.Name] = activeKey;
        }

        /// <summary>
        /// Строит дневной маршрут из rawData для NPC, у которых npc.Schedule пуст.
        /// Используется как фолбэк для кастомных NPC (SVE, SpaceCore и т.д.),
        /// чьё расписание загружается Content Patcher после DayStarted.
        /// </summary>
        private void BuildDayRoutesFromRawData(NPC npc)
        {
            if (npc == null) return;

            var rawData = npc.getMasterScheduleRawData();
            if (rawData == null || rawData.Count == 0)
                return;

            var schedule = BuildMasterSchedule(npc, null, null);
            if (schedule.Count == 0) return;

            var totalPath = new Dictionary<string, HashSet<TilePoint>>();
            var timedPath = new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>();
            string lastLocationName = null;

            foreach (var kvp in schedule)
            {
                if (!ScheduleEntryParser.IsValid(kvp.Key, kvp.Value)) continue;

                if (!int.TryParse(kvp.Key, out int timeInt)) continue;

                try
                {
                    var slots = kvp.Value.Split('/');
                    string lastLocation = npc.currentLocation?.Name;
                    int npcX = npc.TilePoint.X;
                    int npcY = npc.TilePoint.Y;

                    foreach (var slot in slots)
                    {
                        if (ScheduleEntryParser.ShouldSkip(slot)) continue;
                        var parts = slot.Split(' ');

                        if (parts.Length == 2 && parts[1] == "bed")
                        {
                            string homeMap = npc.DefaultMap;
                            if (!string.IsNullOrEmpty(homeMap))
                            {
                                var homeLoc = Game1.getLocationFromName(homeMap);
                                int bedX = homeLoc?.warps?.Count > 0 ? homeLoc.warps[0].X : 1;
                                int bedY = homeLoc?.warps?.Count > 0 ? homeLoc.warps[0].Y : 1;
                                var pathDesc = npc.pathfindToNextScheduleLocation(
                                    parts[0], lastLocation, npcX, npcY,
                                    homeMap, bedX, bedY, 2, null, null);
                                if (pathDesc?.route != null)
                                {
                                    var seg = FilterRouteByLocation(
                                        npc.currentLocation?.Name, pathDesc.route, ref lastLocationName);
                                    NpcPathStore.MergeSegments(totalPath, seg);
                                    if (seg.Count > 0 && !timedPath.ContainsKey(timeInt))
                                        timedPath[timeInt] = seg;
                                }
                                lastLocation = homeMap;
                                npcX = bedX;
                                npcY = bedY;
                            }
                            continue;
                        }

                        if (parts.Length <= 2) continue;

                        ScheduleEntryParser.Parse(parts, lastLocationName,
                            out string time, out string locationName,
                            out int x, out int y,
                            out int facingDir, out string endBehavior, out string endMessage);

                        if (IsAnimationLocation(locationName)) continue;

                        var pd = npc.pathfindToNextScheduleLocation(
                            time, lastLocation, npcX, npcY,
                            locationName, x, y, facingDir, endBehavior, endMessage);

                        if (pd?.route != null)
                        {
                            var seg = FilterRouteByLocation(
                                npc.currentLocation?.Name, pd.route, ref lastLocationName);
                            NpcPathStore.MergeSegments(totalPath, seg);
                            if (seg.Count > 0 && !timedPath.ContainsKey(timeInt))
                                timedPath[timeInt] = seg;
                        }
                        lastLocation = locationName;
                        npcX = x;
                        npcY = y;
                    }
                }
                catch (Exception ex)
                {
                    _monitor.Log($"[DayRoutesFromRaw] {npc.Name} @ {kvp.Key}: {ex.Message}", LogLevel.Debug);
                }
            }

            if (totalPath.Count == 0) return;

            _registry.TotalNpcList.Add(npc.Name);
            _store.TimedDayPaths[npc.Name] = timedPath;
            _store.AddPath(npc, _store.DayPaths, totalPath);

            string activeKey = ScheduleVariantResolver.GetActiveKey(npc, _monitor);
            if (!string.IsNullOrEmpty(activeKey))
                _store.ActiveScheduleKeys[npc.Name] = activeKey;

            PopulateVariantKeys(npc);
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

            // Для глобального маршрута берём ВСЕ записи из rawData,
            // а не только активный вариант — чтобы показать полный набор
            // всех возможных маршрутов (дождь, брак, сезон, день недели и т.д.).
            Dictionary<string, string> masterSchedule;
            if (customPath != null)
            {
                masterSchedule = new Dictionary<string, string> { [customPathKey] = customPath };
            }
            else
            {
                var rawData = npc.getMasterScheduleRawData();
                if (rawData == null || rawData.Count == 0)
                    return;
                masterSchedule = new Dictionary<string, string>(rawData.Count);
                foreach (var kvp in rawData)
                {
                    string val = kvp.Value;
                    int redirects = 0;
                    while (val != null && val.StartsWith("GOTO ") && redirects < 10)
                    {
                        string tgt = val.Substring(5).Trim();
                        if (!rawData.TryGetValue(tgt, out val))
                            val = null;
                        redirects++;
                    }
                    if (!string.IsNullOrEmpty(val) && !val.StartsWith("GOTO "))
                        masterSchedule[kvp.Key] = val;
                }
            }
            _registry.TotalNpcList.Add(npc.Name);

            var totalPath = new Dictionary<string, HashSet<TilePoint>>();

            // Состояние прохода по маршруту — локальные переменные, передаются по ref.
            // Это гарантирует атомарность: исключение внутри одного ключа не загрязняет
            // состояние для следующего ключа (в отличие от instance-полей).
            string lastLocationName = null;
            string endLocationName  = null;

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
                        ProcessMasterScheduleEntry(npc, kvp.Key, kvp.Value, totalPath,
                            ref lastLocationName, ref endLocationName);
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log($"Ошибка обработки ключа '{kvp.Key}' для '{npc.Name}': {ex.Message}", LogLevel.Error);
                        // Сбрасываем состояние прохода, чтобы следующий ключ начался чисто.
                        lastLocationName = null;
                        endLocationName  = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка глобального маршрута '{npc.Name}': {ex.Message}", LogLevel.Error);
            }

            _store.AddPath(npc, _store.GlobalPaths, totalPath);
        }

        /// <summary>
        /// Регистрирует кастомное расписание NPC из JSON-файла мода.
        /// Вызывается CustomScheduleLoader для каждого ключа каждого кастомного NPC.
        /// </summary>
        public void RegisterCustomSchedule(string npcName, string key, string rawValue)
        {
            if (string.IsNullOrEmpty(npcName) || string.IsNullOrEmpty(key)) return;

            if (!_customSchedules.TryGetValue(npcName, out var dict))
            {
                dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                _customSchedules[npcName] = dict;
            }

            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<string>();
                dict[key] = list;
            }
            list.Add(rawValue);
        }

        /// <summary>
        /// Строит тайминговые пути для конкретного варианта расписания NPC.
        /// Результат сохраняется в VariantTimedPaths для использования в пошаговом режиме.
        /// Вызывается по запросу из ModEntry при выборе пользователем варианта.
        /// Для кастомных NPC, у которых rawData пуст, использует зарегистрированные расписания модов.
        /// </summary>
        public void BuildVariantTimedRoute(NPC npc, string variantKey)
        {
            if (npc == null || string.IsNullOrEmpty(variantKey)) return;

            // Источник: сначала игровой rawData (ванильные NPC),
            // затем зарегистрированные кастомные расписания (моды).
            var rawData = npc.getMasterScheduleRawData();
            if (!TryGetVariantValue(npc.Name, variantKey, rawData, out string rawValue))
            {
                _monitor.Log($"[VariantRoute] {npc.Name}: ключ '{variantKey}' не найден.", LogLevel.Warn);
                return;
            }

            // Разрешаем GOTO-редиректы верхнего уровня.
            string value = rawValue;
            int redirects = 0;
            while (value != null && value.StartsWith("GOTO ") && redirects < 10)
            {
                string targetKey = value.Substring(5).Trim();
                if (!TryGetVariantValue(npc.Name, targetKey, rawData, out value))
                {
                    _monitor.Log($"[VariantRoute] {npc.Name}: GOTO цель '{targetKey}' не найдена.", LogLevel.Warn);
                    return;
                }
                redirects++;
            }

            if (string.IsNullOrEmpty(value) || value.StartsWith("GOTO "))
            {
                _monitor.Log($"[VariantRoute] {npc.Name}: не удалось разрешить вариант '{variantKey}'.", LogLevel.Warn);
                return;
            }

            var timedPath = new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>();

            string lastLocationName = null;
            string lastLocation     = npc.currentLocation?.Name;
            int    npcX             = npc.TilePoint.X;
            int    npcY             = npc.TilePoint.Y;

            var slots = value.Split('/');
            foreach (var slot in slots)
            {
                if (ScheduleEntryParser.ShouldSkip(slot)) continue;

                var parts = slot.Split(' ');

                // Специальный слот "TIME bed": NPC возвращается домой спать.
                if (parts.Length == 2 && parts[1] == "bed")
                {
                    string homeMap = npc.DefaultMap;
                    if (!string.IsNullOrEmpty(homeMap))
                    {
                        var homeLoc = Game1.getLocationFromName(homeMap);
                        int bedX = homeLoc?.warps?.Count > 0 ? homeLoc.warps[0].X : 1;
                        int bedY = homeLoc?.warps?.Count > 0 ? homeLoc.warps[0].Y : 1;
                        lastLocation = homeMap;
                        npcX = bedX;
                        npcY = bedY;
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
                        locationName, x, y,
                        facingDir, endBehavior, endMessage);

                    if (pathDesc?.route != null)
                    {
                        var segments = FilterRouteByLocation(
                            npc.currentLocation?.Name, pathDesc.route, ref lastLocationName);

                        if (segments.Count > 0)
                            timedPath[timeInt] = segments;
                    }

                    lastLocation = locationName;
                    npcX = x;
                    npcY = y;
                }
                catch (Exception ex)
                {
                    _monitor.Log(
                        $"[VariantRoute] Pathfind: {npc.Name} → {locationName}({x},{y}) @ {time}: {ex.Message}",
                        LogLevel.Error);
                    lastLocation = locationName;
                    npcX = x;
                    npcY = y;
                }
            }

            // Сохраняем в VariantTimedPaths.
            if (!_store.VariantTimedPaths.TryGetValue(npc.Name, out var variantPaths))
            {
                variantPaths = new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>();
                _store.VariantTimedPaths[npc.Name] = variantPaths;
            }
            variantPaths[variantKey] = timedPath;

            _monitor.Log(
                $"[VariantRoute] {npc.Name}: вариант '{variantKey}' построен, {timedPath.Count} шагов.",
                LogLevel.Debug);
        }

        /// <summary>
        /// Ищет сырое значение варианта: сначала в игровом rawData, затем в кастомных расписаниях.
        /// </summary>
        private bool TryGetVariantValue(
            string npcName, string key,
            Dictionary<string, string> rawData, out string value)
        {
            if (rawData != null && rawData.TryGetValue(key, out value))
                return true;

            if (_customSchedules.TryGetValue(npcName, out var dict)
                && dict.TryGetValue(key, out var list)
                && list != null && list.Count > 0)
            {
                value = list[list.Count - 1]; // последний зарегистрированный патч
                return true;
            }

            value = null;
            return false;
        }

        // ── Внутренняя обработка ─────────────────────────────────────────────────

        /// <summary>
        /// Обрабатывает один ключ мастер-расписания (например, "spring_Mon").
        /// <paramref name="lastLocationName"/> и <paramref name="endLocationName"/> передаются
        /// по ref, чтобы состояние прохода не хранилось в полях объекта —
        /// это делает метод безопасным при исключениях и читаемым при отладке.
        /// </summary>
        private void ProcessMasterScheduleEntry(
            NPC npc,
            string key,
            string rawData,
            Dictionary<string, HashSet<TilePoint>> totalPath,
            ref string lastLocationName,
            ref string endLocationName)
        {
            var slots = rawData.Split('/');

            // Используем TilePoint самого NPC — он всегда валиден, в отличие от поиска
            // персонажа в characters (NPC может ещё не переместиться на DayStarted).
            string startLocName = endLocationName ?? npc.currentLocation?.Name;
            endLocationName = null;

            if (!Game1.locations.Any(loc => loc.Name == startLocName))
            {
                _monitor.Log($"Не найдена локация '{startLocName}' для '{npc.Name}'", LogLevel.Warn);
                return;
            }

            string lastLocation = startLocName;
            int npcX = npc.TilePoint.X;
            int npcY = npc.TilePoint.Y;

            lastLocationName = null;

            foreach (var slot in slots)
            {
                if (ScheduleEntryParser.ShouldSkip(slot)) continue;
                var parts = slot.Split(' ');

                // Специальный слот "TIME bed": NPC возвращается домой спать.
                // "bed" — ключевое слово движка, не реальная карта.
                // defaultPosition — protected поле Character, снаружи недоступно;
                // вместо него берём первый warp-тайл домашней локации как точку входа.
                if (parts.Length == 2 && parts[1] == "bed")
                {
                    string homeMap = npc.DefaultMap;
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
                                    FilterRouteByLocation(npc.currentLocation?.Name, pathDesc.route, ref lastLocationName));
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

                ScheduleEntryParser.Parse(parts, lastLocationName,
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
                            FilterRouteByLocation(npc.currentLocation?.Name, pathDesc.route, ref lastLocationName));
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
            endLocationName = lastLocation;
        }

        /// <summary>
        /// Разбивает стек точек маршрута на сегменты по локациям.
        /// Разрыв смежности (> 1 тайл) = переход через варп в новую локацию.
        /// <paramref name="lastLocationName"/> передаётся по ref: метод читает его как
        /// начальную локацию и обновляет при каждом обнаруженном варпе, чтобы вызывающий
        /// код мог использовать итоговое значение для следующего вызова.
        /// </summary>
        public Dictionary<string, HashSet<TilePoint>> FilterRouteByLocation(
            string startLocation, Stack<Point> points, ref string lastLocationName)
        {
            var result = new Dictionary<string, HashSet<TilePoint>>();

            if (string.IsNullOrEmpty(startLocation) || points == null || !points.Any())
            {
                _monitor.Log(
                    $"FilterRoute: пустой маршрут для '{startLocation}' ({points?.Count ?? 0} точек)",
                    LogLevel.Debug);
                return result;
            }

            if (lastLocationName == null)
                lastLocationName = startLocation;

            var  currentSegment = new HashSet<TilePoint>();
            var  prevCoord      = new TilePoint(0, 0);
            bool firstPoint     = true;  // явный флаг вместо (0, 0) как sentinel

            foreach (var pt in points)
            {
                var tile = new TilePoint(pt.X, pt.Y);

                if (firstPoint)
                {
                    firstPoint = false;
                    prevCoord  = tile;
                    currentSegment.Add(tile);
                    continue;
                }

                bool adjacent = Math.Abs(pt.X - prevCoord.X) <= 1 &&
                                Math.Abs(pt.Y - prevCoord.Y) <= 1;

                if (adjacent)
                {
                    currentSegment.Add(tile);
                    prevCoord = tile;
                }
                else
                {
                    AppendSegment(result, lastLocationName, currentSegment);
                    currentSegment = new HashSet<TilePoint>();
                    string dest = _mapper.GetDestination(
                        lastLocationName, new Point(prevCoord.X, prevCoord.Y));
                    if (dest != null) lastLocationName = dest;
                    prevCoord = tile;
                }
            }

            if (currentSegment.Count > 0)
                AppendSegment(result, lastLocationName, currentSegment);

            return result;
        }

        // ── Вспомогательные ──────────────────────────────────────────────────────

        /// <summary>
        /// Заполняет NpcVariantKeys для данного NPC списком всех ключей его сырого расписания.
        /// Для кастомных NPC, у которых getMasterScheduleRawData() пуст,
        /// использует зарегистрированные расписания модов.
        /// </summary>
        private void PopulateVariantKeys(NPC npc)
        {
            var rawData = npc.getMasterScheduleRawData();

            List<string> keys;
            if (rawData != null && rawData.Count > 0)
            {
                keys = new List<string>(rawData.Keys);
            }
            else if (_customSchedules.TryGetValue(npc.Name, out var custom))
            {
                keys = new List<string>(custom.Keys);
            }
            else
            {
                return;
            }

            keys.Sort(StringComparer.OrdinalIgnoreCase);
            _registry.NpcVariantKeys[npc.Name] = keys;
        }

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
            Dictionary<string, HashSet<TilePoint>> result,
            string location,
            HashSet<TilePoint> segment)
        {
            if (!result.TryGetValue(location, out var existing))
                result[location] = new HashSet<TilePoint>(segment);
            else
                existing.UnionWith(segment);
        }
    }
}
