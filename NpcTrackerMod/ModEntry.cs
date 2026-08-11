using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NpcTrackerMod.Core;
using NpcTrackerMod.Multiplayer;
using NpcTrackerMod.Rendering;
using NpcTrackerMod.Scheduling;
using NpcTrackerMod.Tracking;
using NpcTrackerMod.UI;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod
{
    /// <summary>
    /// Точка входа мода. Создаёт все сервисы, подписывается на события SMAPI.
    /// Не хранит публичного состояния — всё через ModState.
    /// </summary>
    public class ModEntry : Mod
    {
        // ── Сервисы (создаются в Entry) ───────────────────────────────────────────
        private ModState _state;
        private NpcPathStore _pathStore;
        private LocationMapper _locationMapper;
        private NpcRegistry _registry;
        private ScheduleProcessor _scheduleProcessor;
        private CustomScheduleLoader _scheduleLoader;
        private TileRenderer _tileRenderer;
        private RouteRenderer _routeRenderer;
        private TooltipRenderer _tooltipRenderer;
        private NpcTracker _tracker;
        private ModConfig _config;

        // ── Служебное состояние ───────────────────────────────────────────────────
        private bool _globalRoutesBuilt;
        private bool _dayActive;
        private string _previousLocationName;
        private RouteSync _routeSync;

        // ── Entry ─────────────────────────────────────────────────────────────────

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<ModConfig>();

            // Core
            _state = new ModState();
            _pathStore = new NpcPathStore(Monitor);

            // Scheduling
            _locationMapper = new LocationMapper(Monitor);
            _registry = new NpcRegistry(Monitor, _pathStore);
            _scheduleProcessor = new ScheduleProcessor(Monitor, _pathStore, _registry, _locationMapper);
            _scheduleLoader = new CustomScheduleLoader(Monitor, helper, _scheduleProcessor, _registry);

            // Подписки на события, не требующие графики
            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

            // Загружаем JSON-расписания модов заранее, чтобы они были готовы к DayStarted
            _scheduleLoader.LoadAll();
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            // Rendering и Tracking инициализируются здесь — GraphicsDevice гарантированно готов
            _tileRenderer = new TileRenderer(Game1.graphics.GraphicsDevice);
            _tileRenderer.Alpha = _config.RouteAlpha;
            _routeRenderer = new RouteRenderer(Monitor, _state, _pathStore, _tileRenderer, _config, Helper.Translation);
            _tooltipRenderer = new TooltipRenderer(_tileRenderer, _registry, _config, Helper.Translation, Monitor);
            _tracker = new NpcTracker(_state, _registry, _routeRenderer, _tileRenderer);

            // Синхронизация маршрутов в мультиплеере: хост рассылает снапшот дня,
            // фарм-хэнды применяют его вместо локального построения.
            _routeSync = new RouteSync(
                Monitor, Helper, ModManifest.UniqueID,
                _pathStore, _registry, _state,
                _tileRenderer, _tracker,
                PopulateModSources);

            // Подписки на события, требующие инициализированного рендерера
            Helper.Events.Input.ButtonPressed += OnButtonPressed;
            Helper.Events.Display.RenderedWorld += OnRenderedWorld;
            Helper.Events.Player.Warped += OnPlayerWarped;
            Helper.Events.Multiplayer.ModMessageReceived += OnModMessageReceived;
            Helper.Events.Multiplayer.PeerConnected += OnPeerConnected;
        }

        // ── SMAPI Events ──────────────────────────────────────────────────────────

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            ClearDay();
            _dayActive = true;

            if (!_state.LocationSet)
            {
                _locationMapper.BuildFromGame();
                _state.LocationSet = true;
            }

            // Собираем GameNpcs из всех локаций
            _registry.RefreshGameNpcs();

            // Маршруты авторитетны у хоста: фарм-хэнды получают готовый снапшот
            // (локальное построение — только фолбэк, если снапшот не пришёл).
            // Это делает кастомные расписания и результаты pathfinding
            // одинаковыми для всех игроков в сохранении.
            if (_routeSync.ShouldSkipLocalBuild)
            {
                // Фолбэк: локальное построение с тем же refresh-танцем, что и ApplySnapshot.
                _routeSync.WaitForHostRoutes(() =>
                {
                    BuildAllRoutes();
                    _tileRenderer.Clear();
                    _state.SwitchGetNpcPath = true;
                    _registry.RefreshCurrentNpcList();
                    _tracker.InvalidateNpcCache();
                });
            }
            else
            {
                BuildAllRoutes();
                _routeSync.BroadcastDayRoutes();
            }
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            _dayActive = false;
            _routeSync.OnDayEnding();
        }

        /// <summary>
        /// Receives the complete route snapshot sent by the host.
        /// Вся логика (проверка отправителя, версия формата, gzip,
        /// отложенное применение) живёт в RouteSync.
        /// </summary>
        private void OnModMessageReceived(object sender, ModMessageReceivedEventArgs e)
            => _routeSync.HandleModMessageReceived(e);

        /// <summary>
        /// Sends the current full route snapshot to a player joining mid-day.
        /// </summary>
        private void OnPeerConnected(object sender, PeerConnectedEventArgs e)
        {
            if (Context.IsMainPlayer && _dayActive)
                _routeSync.SendSnapshotToPeer(e.Peer.PlayerID);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady) return;

            // Пока поиск NPC в фокусе — подавляем только игровые горячие клавиши,
            // но не системные (Escape) и не клики мыши
            if (Game1.activeClickableMenu is TrackingMenu tm && tm.IsSearchFocused)
            {
                // Пропускаем системные кнопки и мышь
                if (e.Button == SButton.Escape ||
                    e.Button == SButton.MouseLeft ||
                    e.Button == SButton.MouseRight ||
                    e.Button == SButton.Enter ||
                    e.Button == SButton.Back )
                {
                    return;
                }

                // Подавляем только буквенно-цифровые и специальные клавиши,
                // которые могут открыть чат, инвентарь и т.д.
                Helper.Input.Suppress(e.Button);
                return;
            }

            if (Game1.activeClickableMenu != null || !Context.IsPlayerFree) return;

            if (e.Button == _config.MenuKey)
            {
                OpenMenu();
            }
            else if (e.Button == _config.DebugKey)
            {
                LogCurrentLocationWarps();
            }
            // Клик по тайлу маршрута — открыть инспектор тайла
            else if (e.Button == _config.SelectNpcKey && _state.EnableDisplay)
            {
                int tx = (int)((Game1.viewport.X + Game1.getMouseX()) / Game1.tileSize);
                int ty = (int)((Game1.viewport.Y + Game1.getMouseY()) / Game1.tileSize);
                var tile = new Point(tx, ty);

                if (_tileRenderer.TileOwners.TryGetValue(tile, out var owners) && owners.Count > 0)
                {
                    Game1.activeClickableMenu = new TileInspectMenu(
                        Monitor, _state, _registry,
                        tile, owners, _registry.GameNpcs, Helper.Translation,
                        ToggleNpc,
                        locationName: Game1.currentLocation?.Name,
                        onOpenInMenu: OpenInspectorNpcInMenu);

                    Helper.Input.Suppress(e.Button);
                    Game1.playSound("smallSelect");
                }
            }
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!_state.EnableDisplay) return;

            try
            {
                var batch = e.SpriteBatch;
                var camera = new Vector2(Game1.viewport.X, Game1.viewport.Y);

                _tracker.DrawPaths(batch, camera);

                if (_state.DisplayGrid)
                    _tileRenderer.DrawGrid(batch, camera);

                // Тултип при наведении на тайл маршрута (логика в TooltipRenderer)
                _tooltipRenderer.Draw(batch);
            }
            catch (Exception ex)
            {
                Monitor.Log($"Ошибка в OnRenderedWorld: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
            }
        }

        private void OnPlayerWarped(object sender, WarpedEventArgs e)
        {
            if (Game1.player.currentLocation.Name == _previousLocationName) return;

            _tileRenderer.Clear();
            _previousLocationName = Game1.player.currentLocation.Name;
            _state.SwitchGetNpcPath = true;
            _state.NpcCount = Game1.player.currentLocation.characters.Count();
            _tracker.InvalidateNpcCache();
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_dayActive) return;

            // Фолбэк для фарм-хэндов: локальное построение, если снапшот хоста не пришёл.
            _routeSync.Tick();

            // Обработка запроса на построение маршрута выбранного варианта расписания.
            // Проверяется каждый тик для минимальной задержки после клика в меню.
            if (_state.SwitchBuildVariant)
            {
                _state.SwitchBuildVariant = false;
                BuildSelectedVariantRoute();
            }

            // Остальное — только каждые 60 тиков (~1 сек).
            if (!e.IsMultipleOf(60)) return;
            if (!_state.EnableDisplay || _state.SwitchGlobalNpcPath || _state.SwitchTargetLocations) return;

            int npcCount = Game1.player.currentLocation.characters.Count();
            if (npcCount == _state.NpcCount) return;

            _tileRenderer.Clear();
            _state.SwitchGetNpcPath = true;
            _state.NpcCount = npcCount;
            _tracker.InvalidateNpcCache();

            _registry.RefreshCurrentNpcList();
        }

        /// <summary>
        /// Строит тайминговые пути для текущего SelectedVariantKey и выбранного NPC.
        /// Вызывается из OnUpdateTicked при SwitchBuildVariant == true.
        /// </summary>
        private void BuildSelectedVariantRoute()
        {
            if (string.IsNullOrEmpty(_state.SelectedVariantKey)) return;

            var npc = _registry.GameNpcs?.FirstOrDefault(
                n => n?.Name == _registry.CurrentNpcName);

            if (npc == null)
            {
                Monitor.Log(
                    $"[BuildVariantRoute] NPC '{_registry.CurrentNpcName}' не найден.",
                    LogLevel.Warn);
                return;
            }

            try
            {
                _scheduleProcessor.BuildVariantTimedRoute(npc, _state.SelectedVariantKey);
                _tileRenderer.Clear();
                _state.RouteStepIndex = 0;
                _state.SwitchGetNpcPath = true;
            }
            catch (Exception ex)
            {
                Monitor.Log(
                    $"Ошибка построения варианта '{_state.SelectedVariantKey}' для {npc.Name}: {ex.Message}",
                    LogLevel.Error);
            }
        }

        // ── Управление выбором NPC ────────────────────────────────────────────────

        /// <summary>
        /// Переключает NPC в множестве выбранных и обновляет состояние для перерисовки.
        /// Вызывается из TileInspectMenu через callback — бизнес-логика выбора
        /// не должна жить в UI.
        /// </summary>
        private void ToggleNpc(string npcName)
        {
            if (_registry.SelectedNpcNames.Contains(npcName))
                _registry.SelectedNpcNames.Remove(npcName);
            else
            {
                _registry.SelectedNpcNames.Add(npcName);
                _registry.CurrentNpcName = npcName;
            }

            _state.SwitchTargetNPC  = _registry.SelectedNpcNames.Count > 0;
            _tileRenderer.Clear();
            _registry.CurrentNpcList.Clear();
            _state.SwitchGetNpcPath = true;
            _state.SwitchListFull   = false;
        }

        /// <summary>
        /// Закрывает инспектор и открывает главное меню с выбранным NPC.
        /// Вызывается из TileInspectMenu кнопкой «Меню».
        /// </summary>
        private void OpenInspectorNpcInMenu(string npcName)
        {
            if (!_registry.SelectedNpcNames.Contains(npcName))
            {
                _registry.SelectedNpcNames.Add(npcName);
                _registry.CurrentNpcName = npcName;
            }

            _state.SwitchTargetNPC = _registry.SelectedNpcNames.Count > 0;
            _tileRenderer.Clear();
            _registry.CurrentNpcList.Clear();
            _state.SwitchGetNpcPath = true;
            _state.SwitchListFull   = false;

            Game1.activeClickableMenu?.exitThisMenu();
            OpenMenu();
        }

        // ── Утилиты ───────────────────────────────────────────────────────────────

        private void PopulateModSources()
        {
            _registry.NpcModSource.Clear();
            foreach (string name in _registry.TotalNpcList)
            {
                _registry.NpcModSource[name] = _scheduleLoader.NpcModNames.TryGetValue(name, out string mod)
                    ? mod
                    : Helper.Translation.Get("source.vanilla").ToString();
            }
        }

        /// <summary>
        /// Строит глобальные (один раз за сессию) и дневные маршруты всех NPC.
        /// Используется хостом в DayStarted и фарм-хэндами как фолбэк,
        /// если снапшот хоста не пришёл.
        /// </summary>
        private void BuildAllRoutes()
        {
            if (!_globalRoutesBuilt)
            {
                _scheduleLoader.TransferToProcessor();
                foreach (var npc in _registry.GameNpcs)
                {
                    try { _scheduleProcessor.BuildGlobalRoute(npc, null, null, null); }
                    catch (Exception ex)
                    { Monitor.Log($"Ошибка глобального маршрута {npc.Name}: {ex.Message}", LogLevel.Warn); }
                }
                _globalRoutesBuilt = true;
            }

            // Дневные маршруты строятся каждый день
            foreach (var npc in _registry.GameNpcs)
            {
                try { _scheduleProcessor.BuildDayRoutes(npc); }
                catch (Exception ex)
                { Monitor.Log($"Ошибка дневного маршрута {npc.Name}: {ex.Message}", LogLevel.Warn); }
            }

            PopulateModSources();
        }

        private void OpenMenu()
        {
            Game1.activeClickableMenu = new TrackingMenu(
                Monitor, _state, _registry, _tileRenderer, _config,
                () => Helper.WriteConfig(_config),
                Helper.Translation);
        }

        private void LogCurrentLocationWarps()
        {
            Monitor.Log(Game1.currentLocation.Name, LogLevel.Info);
            foreach (var w in Game1.currentLocation.warps)
                Monitor.Log($" warp: X={w.X} Y={w.Y} → {w.TargetName}", LogLevel.Debug);
            foreach (var d in Game1.currentLocation.doors.Pairs)
                Monitor.Log($" door: {d}", LogLevel.Debug);
        }

        private void ClearDay()
        {
            _tileRenderer.Clear();
            _tileRenderer.NpcPositionColors.Clear();
            _state.NpcPreviousPositions.Clear();
            _state.SwitchGetNpcPath = false;
            _state.NpcCount = 0;

            // Сбрасываем выбранный вариант — данные варианта будут перестроены в новый день.
            _state.SelectedVariantKey = null;
            _state.SwitchBuildVariant = false;

            _registry.ClearDay();
            _pathStore.ClearDay();
        }
    }
}
