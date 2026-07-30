using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NpcTrackerMod;
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
        private readonly Dictionary<string, HashSet<Point>> _timedPathBuffer
            = new Dictionary<string, HashSet<Point>>();

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
        private string T(string key, object tokens = null)
        {
            if (_i18n == null) return key;
            var t = tokens != null ? _i18n.Get(key, tokens) : _i18n.Get(key);
            return t.HasValue() ? t.ToString() : key;
        }

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
                // Работает только для дневного маршрута (TimedDayPaths).
                if (_state.RouteStepMode && !_state.SwitchGlobalNpcPath)
                {
                    DrawStepRoute(npc);
                    return;
                }

                string timeLabel = null;
                Dictionary<string, HashSet<Point>> pathData = null;

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
                         timedPath.Any())
                {
                    _timedPathBuffer.Clear();
                    int lastTime = -1;
                    foreach (var kvp in timedPath.Where(t => t.Key <= _state.TimeFilter))
                    {
                        foreach (var loc in kvp.Value)
                        {
                            if (!_timedPathBuffer.TryGetValue(loc.Key, out var pts))
                                _timedPathBuffer[loc.Key] = new HashSet<Point>(loc.Value);
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

                // В глобальном режиме всегда смотрим тайлы по локации игрока —
                // так отображаются маршруты ВСЕХ NPC через текущую карту,
                // независимо от того, где они находятся прямо сейчас.
                string targetLocation = (_state.SwitchGlobalNpcPath || _state.SwitchTargetLocations)
                    ? (Game1.player.currentLocation?.Name ?? string.Empty)
                    : (npc.currentLocation?.Name ?? string.Empty);

                if (pathData.TryGetValue(targetLocation, out var tileSet))
                {
                    foreach (var coord in tileSet)
                    {
                        _tiles.MarkTile(coord, ModConfig.ParseColor(_config.RouteColor, Color.Green), 2);
                        // timeLabel остаётся null для обычных маршрутов —
                        // тултип покажет только имя NPC без лишней метки
                        _tiles.RegisterOwner(coord, npc.Name, timeLabel);
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
            _tiles.MarkNpcPosition(currentTile, ModConfig.ParseColor(_config.PositionColor, Color.Blue), 1);
            _tiles.RegisterOwner(currentTile, name, T("route.hereNow"));
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
        /// Отображает один временной слот дневного маршрута NPC.
        /// Обновляет RouteStepTotal и RouteStepTime в ModState для навигатора в меню.
        /// </summary>
        private void DrawStepRoute(NPC npc)
        {
            var keys = _store.GetStepKeys(npc.Name);
            _state.RouteStepTotal = keys.Count;

            if (keys.Count == 0)
            {
                // Тайминговых данных нет — fallback на полный DayPath без шагов.
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
                    foreach (var coord in fallbackTiles)
                    {
                        _tiles.MarkTile(coord, ModConfig.ParseColor(_config.RouteColor, Color.Green), 2);
                        _tiles.RegisterOwner(coord, npc.Name, null);
                    }
                }
                return;
            }

            // Зажимаем индекс на случай смены NPC или перезагрузки данных.
            int idx = Math.Max(0, Math.Min(_state.RouteStepIndex, keys.Count - 1));
            _state.RouteStepIndex = idx;

            int timeKey = keys[idx];
            _state.RouteStepTime = timeKey;

            if (!_store.TimedDayPaths.TryGetValue(npc.Name, out var timedPath) ||
                !timedPath.TryGetValue(timeKey, out var stepPath))
                return;

            string targetLocation = _state.SwitchTargetLocations
                ? (Game1.player.currentLocation?.Name ?? string.Empty)
                : (npc.currentLocation?.Name ?? string.Empty);

            if (!stepPath.TryGetValue(targetLocation, out var tileSet))
                return;

            // Метка для тултипа: время и порядковый номер шага.
            string label = $"{FormatTime(timeKey)}  ({idx + 1}/{keys.Count})";
            foreach (var coord in tileSet)
            {
                _tiles.MarkTile(coord, ModConfig.ParseColor(_config.RouteColor, Color.Green), 2);
                _tiles.RegisterOwner(coord, npc.Name, label);
            }
        }
    }
}
