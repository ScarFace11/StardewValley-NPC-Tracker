using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NpcTrackerMod.Core;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Rendering
{
    /// <summary>
    /// Визуализирует маршруты и позиции NPC через TileRenderer.
    /// Не хранит данных — только читает NpcPathStore и пишет в TileRenderer.
    /// </summary>
    public class RouteRenderer
    {
        private readonly IMonitor _monitor;
        private readonly ModState _state;
        private readonly NpcPathStore _store;
        private readonly TileRenderer _tiles;
        private readonly ModConfig _config;
        private readonly ITranslationHelper _i18n;

        // Переиспользуемый буфер для TimeFilter — не создаётся каждый кадр
        private readonly Dictionary<string, HashSet<TilePoint>> _timedPathBuffer
            = new Dictionary<string, HashSet<TilePoint>>();

        // Цвета стартового и конечного тайла пошагового маршрута.
        // Static readonly — значения не аллоцируются каждый кадр.
        private static readonly Color StepStartColor = new Color(30, 185, 215);  // холодный — откуда стартует
        private static readonly Color StepEndColor   = new Color(215, 100, 0);   // тёплый — куда придёт

        // Кеш распарсенных цветов из конфига — пересчитывается только при смене строки конфига,
        // а не при каждом вызове ParseColor() на каждый тайл каждый кадр.
        private string _cachedRouteColorName;
        private Color  _cachedRouteColor = Color.Green;
        private string _cachedPositionColorName;
        private Color  _cachedPositionColor = Color.Blue;

        // Кеш строки локализации — не нужно обращаться к i18n каждый кадр на каждого NPC.
        private string _hereNowLabel;

        public RouteRenderer(
            IMonitor monitor,
            ModState state,
            NpcPathStore store,
            TileRenderer tiles,
            ModConfig config,
            ITranslationHelper i18n = null)
        {
            _monitor = monitor;
            _state = state;
            _store = store;
            _tiles = tiles;
            _config = config;
            _i18n = i18n;
        }

        // ── Локализация ───────────────────────────────────────────────────────────

        /// <summary> Возвращает перевод по ключу с необязательными токенами. </summary>
        private string T(string key, object tokens = null) =>
            LocalizationHelper.Get(_i18n, key, tokens);

        // ── Кеш цветов ────────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает кешированный цвет маршрута.
        /// Перепарсивает только если строка конфига изменилась.
        /// </summary>
        private Color RouteColor
        {
            get
            {
                if (_config.RouteColor != _cachedRouteColorName)
                {
                    _cachedRouteColorName = _config.RouteColor;
                    _cachedRouteColor     = ModConfig.ParseColor(_config.RouteColor, Color.Green);
                }
                return _cachedRouteColor;
            }
        }

        /// <summary>
        /// Возвращает кешированный цвет позиции NPC.
        /// Перепарсивает только если строка конфига изменилась.
        /// </summary>
        private Color PositionColor
        {
            get
            {
                if (_config.PositionColor != _cachedPositionColorName)
                {
                    _cachedPositionColorName = _config.PositionColor;
                    _cachedPositionColor     = ModConfig.ParseColor(_config.PositionColor, Color.Blue);
                }
                return _cachedPositionColor;
            }
        }

        /// <summary>
        /// Возвращает кешированную строку "здесь сейчас".
        /// Инициализируется один раз — i18n не меняется во время сессии.
        /// </summary>
        private string HereNowLabel => _hereNowLabel ?? (_hereNowLabel = T("route.hereNow"));

        // ── Публичный API ────────────────────────────────────────────────────────

        /// <summary>
        /// Регистрирует в TileRenderer зелёные тайлы маршрута NPC.
        /// Учитывает SwitchGlobalNpcPath и TimeFilter.
        /// </summary>
        public void DrawRoute(NPC npc)
        {
            try
            {
                if (!_state.SwitchGetNpcPath || npc == null) return;

                // Пошаговый режим перехватывает управление до стандартной логики.
                // Работает только для дневного маршрута (TimedDayPaths / VariantTimedPaths).
                if (_state.RouteStepMode && !_state.SwitchGlobalNpcPath)
                {
                    DrawStepRoute(npc);
                    return;
                }

                string timeLabel = null;
                Dictionary<string, HashSet<TilePoint>> pathData = null;

                if (_state.SwitchGlobalNpcPath)
                {
                    if (!_store.GlobalPaths.TryGetValue(npc.Name, out pathData) || pathData == null)
                    {
                        _monitor.Log($"NPC {npc.Name}: нет глобального пути.", LogLevel.Warn);
                        return;
                    }
                }
                else if (_state.TimeFilter >= 0 &&
                         _store.TimedDayPaths.TryGetValue(npc.Name, out var timedPath) &&
                         timedPath.Count > 0)
                {
                    _timedPathBuffer.Clear();
                    int lastTime = -1;
                    // Обычный цикл вместо LINQ-Where — нет аллокаций каждый кадр.
                    foreach (var kvp in timedPath)
                    {
                        if (kvp.Key > _state.TimeFilter) continue;
                        foreach (var loc in kvp.Value)
                        {
                            if (!_timedPathBuffer.TryGetValue(loc.Key, out var pts))
                                _timedPathBuffer[loc.Key] = new HashSet<TilePoint>(loc.Value);
                            else
                                pts.UnionWith(loc.Value);
                        }
                        lastTime = kvp.Key;
                    }
                    if (_timedPathBuffer.Count == 0) return;
                    timeLabel = T("route.upTo", new { time = FormatTime(lastTime) });
                    pathData = _timedPathBuffer;
                }
                else
                {
                    if (!_store.DayPaths.TryGetValue(npc.Name, out pathData) || pathData == null)
                        _store.GlobalPaths.TryGetValue(npc.Name, out pathData);
                }

                if (pathData == null)
                {
                    _monitor.Log($"NPC {npc.Name}: нет данных о пути.", LogLevel.Warn);
                    return;
                }

                var routeColor = RouteColor;

                if (_state.SwitchGlobalNpcPath)
                {
                    // В глобальном режиме рисуем ВСЕ локации из маршрута NPC,
                    // чтобы пользователь видел полный путь через все локации.
                    foreach (var locEntry in pathData)
                    {
                        foreach (var coord in locEntry.Value)
                        {
                            var tile = new Point(coord.X, coord.Y);
                            _tiles.MarkTile(tile, routeColor, 2);
                            _tiles.RegisterOwner(tile, npc.Name, timeLabel);
                        }
                    }
                }
                else
                {
                    // Обычный режим: показываем только текущую локацию.
                    string targetLocation = _state.SwitchTargetLocations
                        ? (Game1.player.currentLocation?.Name ?? string.Empty)
                        : (npc.currentLocation?.Name ?? string.Empty);

                    if (pathData.TryGetValue(targetLocation, out var tileSet))
                    {
                        foreach (var coord in tileSet)
                        {
                            var tile = new Point(coord.X, coord.Y);
                            _tiles.MarkTile(tile, routeColor, 2);
                            _tiles.RegisterOwner(tile, npc.Name, timeLabel);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка отрисовки маршрута {npc?.Name}: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Рисует синий тайл на текущей позиции NPC и убирает его с предыдущей.
        /// </summary>
        public void DrawPositionTile(NPC npc)
        {
            if (Game1.currentLocation != npc.currentLocation) return;

            int half = _tiles.TileSize / 2;
            var currentTile = new Point(
                (int)Math.Floor((npc.Position.X + half) / _tiles.TileSize),
                (int)Math.Floor((npc.Position.Y + half) / _tiles.TileSize));

            string name = npc.Name ?? "unknown";

            if (_state.NpcPreviousPositions.TryGetValue(name, out var prev) && prev != currentTile)
                _tiles.RestorePosition(prev);

            _state.NpcPreviousPositions[name] = currentTile;
            _tiles.MarkNpcPosition(currentTile, PositionColor, 1);
            _tiles.RegisterOwner(currentTile, name, HereNowLabel);
        }

        // ── Утилита ───────────────────────────────────────────────────────────────

        /// <summary> Форматирует игровое время: 930 → "09:30". </summary>
        public static string FormatTime(int gameTime)
        {
            int display = gameTime >= 2400 ? gameTime - 2400 : gameTime;
            return $"{display / 100:D2}:{display % 100:D2}";
        }

        // ── Пошаговый режим ───────────────────────────────────────────────────────

        /// <summary>
        /// Отображает один временной слот маршрута NPC.
        /// Если выбран вариант расписания — использует VariantTimedPaths.
        /// Иначе — стандартный TimedDayPaths активного расписания.
        /// Обновляет RouteStepTotal и RouteStepTime в ModState для навигатора в меню.
        /// </summary>
        private void DrawStepRoute(NPC npc)
        {
            bool useVariant = !string.IsNullOrEmpty(_state.SelectedVariantKey)
                              && _store.VariantTimedPaths.TryGetValue(npc.Name, out var variantPaths)
                              && variantPaths.ContainsKey(_state.SelectedVariantKey);

            List<int> keys;
            Dictionary<int, Dictionary<string, HashSet<TilePoint>>> timedPath;

            if (useVariant)
            {
                keys = _store.GetVariantStepKeys(npc.Name, _state.SelectedVariantKey);
                _store.VariantTimedPaths[npc.Name].TryGetValue(_state.SelectedVariantKey, out timedPath);
            }
            else
            {
                keys = _store.GetStepKeys(npc.Name);
                _store.TimedDayPaths.TryGetValue(npc.Name, out timedPath);
            }

            _state.RouteStepTotal = keys.Count;

            if (keys.Count == 0)
            {
                // Тайминговых данных нет — для варианта просто ждём построения;
                // для активного расписания — fallback на полный DayPath.
                _state.RouteStepScheduleKey = _state.SelectedVariantKey;
                if (useVariant) return;

                _monitor.Log(
                    $"[StepRoute] {npc.Name}: TimedDayPaths пуст, показываем DayPath целиком.",
                    LogLevel.Debug);

                if (!_store.DayPaths.TryGetValue(npc.Name, out var fallback) || fallback == null)
                    return;

                string targetLoc = _state.SwitchTargetLocations
                    ? (Game1.player.currentLocation?.Name ?? string.Empty)
                    : (npc.currentLocation?.Name ?? string.Empty);

                if (fallback.TryGetValue(targetLoc, out var fallbackTiles))
                {
                    var routeColor = RouteColor;
                    foreach (var coord in fallbackTiles)
                    {
                        var tile = new Point(coord.X, coord.Y);
                        _tiles.MarkTile(tile, routeColor, 2);
                        _tiles.RegisterOwner(tile, npc.Name, null);
                    }
                }
                return;
            }

            // Зажимаем индекс на случай смены NPC или перезагрузки данных.
            int idx = Math.Max(0, Math.Min(_state.RouteStepIndex, keys.Count - 1));
            _state.RouteStepIndex = idx;

            int timeKey = keys[idx];
            _state.RouteStepTime = timeKey;

            // Обновляем ключ отображаемого расписания.
            if (useVariant)
                _state.RouteStepScheduleKey = _state.SelectedVariantKey;
            else
            {
                _store.ActiveScheduleKeys.TryGetValue(npc.Name, out string schedKey);
                _state.RouteStepScheduleKey = schedKey;
            }

            if (timedPath == null || !timedPath.TryGetValue(timeKey, out var stepPath))
                return;

            string targetLocation = _state.SwitchTargetLocations
                ? (Game1.player.currentLocation?.Name ?? string.Empty)
                : (npc.currentLocation?.Name ?? string.Empty);

            if (!stepPath.TryGetValue(targetLocation, out var tileSet))
                return;

            // Метка для тултипа: время и порядковый номер шага.
            string label = $"{FormatTime(timeKey)}  ({idx + 1}/{keys.Count})";
            var stepRouteColor = RouteColor;
            foreach (var coord in tileSet)
            {
                var tile = new Point(coord.X, coord.Y);
                _tiles.MarkTile(tile, stepRouteColor, 2);
                _tiles.RegisterOwner(tile, npc.Name, label);
            }

            // Стартовый/конечный тайлы только для активного дневного расписания
            // (у вариантов нет данных npc.Schedule для определения targetTile).
            if (!useVariant)
                DrawStepEndpoints(npc, keys, idx, timeKey, targetLocation, label);
        }

        /// <summary>
        /// Отмечает стартовый и конечный тайлы текущего шага маршрута особыми цветами.
        /// Конечный тайл берётся из npc.Schedule[timeKey].targetTile (официальная цель шага).
        /// Стартовый — конечный тайл предыдущего шага (если шаг не первый).
        /// Не вычисляет маршруты — только регистрирует тайлы в TileRenderer.
        /// </summary>
        private void DrawStepEndpoints(
            NPC npc, List<int> keys, int idx, int timeKey,
            string targetLocation, string label)
        {
            if (npc.Schedule == null) return;

            // Конечный тайл: куда NPC придёт в конце этого шага.
            if (npc.Schedule.TryGetValue(timeKey, out var entry)
                && entry.targetLocationName == targetLocation)
            {
                _tiles.MarkTile(entry.targetTile, StepEndColor, 3);
                _tiles.RegisterOwner(entry.targetTile, npc.Name, label);
            }

            // Стартовый тайл: конечная точка предыдущего шага.
            // Для первого шага (idx == 0) стартовая позиция — домашняя точка NPC,
            // которая находится вне текущей карты в большинстве случаев, поэтому не отображается.
            if (idx <= 0) return;

            int prevKey = keys[idx - 1];
            if (npc.Schedule.TryGetValue(prevKey, out var prevEntry)
                && prevEntry.targetLocationName == targetLocation)
            {
                _tiles.MarkTile(prevEntry.targetTile, StepStartColor, 3);
                _tiles.RegisterOwner(prevEntry.targetTile, npc.Name, label);
            }
        }
    }
}
