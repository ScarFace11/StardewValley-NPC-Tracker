using System;
using NpcTrackerMod.Core;
using NpcTrackerMod.Rendering;
using NpcTrackerMod.Tracking;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace NpcTrackerMod.Multiplayer
{
    /// <summary>
    /// Синхронизация маршрутов в мультиплеере.
    /// Хост строит маршруты и рассылает фарм-хэндам полный снапшот дня;
    /// фарм-хэнды применяют его вместо локального построения. Снапшот дня
    /// кэшируется и переиспользуется для игроков, подключившихся посреди дня.
    /// </summary>
    public class RouteSync
    {
        /// <summary> Идентификатор типа сообщения для SMAPI Multiplayer. </summary>
        public const string MessageType = "RouteSnapshot";

        // Сколько тиков ждать снапшот хоста, прежде чем строить маршруты локально (~5 сек).
        private const int FallbackTicks = 300;

        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly string _modId;
        private readonly NpcPathStore _store;
        private readonly NpcRegistry _registry;
        private readonly ModState _state;
        private readonly TileRenderer _tileRenderer;
        private readonly NpcTracker _tracker;
        private readonly Action _populateModSources;

        // Снапшот, полученный до готовности мира/рендереров — применяется в DayStarted.
        private RouteSnapshot _pendingSnapshot;

        // Фарм-хэнд ждёт снапшот хоста; фолбэк — локальное построение маршрутов.
        private bool _awaitingSync;
        private int _fallbackTicksLeft;
        private Action _fallbackBuild;

        // Собственный флаг дня: снапшот, пришедший до DayStarted (например, при
        // подключении посреди дня), откладывается и применяется в DayStarted,
        // чтобы ClearDay не стёр его данные.
        private bool _dayActive;

        public RouteSync(
            IMonitor monitor,
            IModHelper helper,
            string modId,
            NpcPathStore store,
            NpcRegistry registry,
            ModState state,
            TileRenderer tileRenderer,
            NpcTracker tracker,
            Action populateModSources)
        {
            _monitor = monitor;
            _helper = helper;
            _modId = modId;
            _store = store;
            _registry = registry;
            _state = state;
            _tileRenderer = tileRenderer;
            _tracker = tracker;
            _populateModSources = populateModSources;
        }

        /// <summary>
        /// Фарм-хэнд в мультиплеере не строит маршруты локально — ждёт снапшот хоста.
        /// Это убирает двойную работу и «мигание» локально построенных маршрутов.
        /// </summary>
        public bool ShouldSkipLocalBuild => Context.IsMultiplayer && !Context.IsMainPlayer;

        /// <summary>
        /// Вызывается в DayStarted на фарм-хэнде. Применяет снапшот, если он уже
        /// пришёл; иначе начинает ожидание с фолбэком на локальное построение.
        /// </summary>
        public void WaitForHostRoutes(Action buildLocalRoutes)
        {
            _dayActive = true;
            _fallbackBuild = buildLocalRoutes;

            if (_pendingSnapshot != null)
            {
                var snapshot = _pendingSnapshot;
                _pendingSnapshot = null;
                ApplySnapshot(snapshot);
                return;
            }

            _awaitingSync = true;
            _fallbackTicksLeft = FallbackTicks;
            _monitor.Log("Ожидание маршрутов от хоста...", LogLevel.Trace);
        }

        /// <summary>
        /// Хост: строит снапшот дня и рассылает всем фарм-хэндам.
        /// Глобальные пути в ежедневную рассылку не включаются — они строятся
        /// один раз за сессию, и фарм-хэнд сохраняет полученные ранее
        /// (см. RouteSnapshot.ApplyTo). Это заметно уменьшает размер пакета.
        /// </summary>
        public void BroadcastDayRoutes()
        {
            if (!Context.IsMultiplayer) return;
            _dayActive = true;
            SendSnapshot(RouteSnapshot.Capture(_store, _registry, includeGlobalPaths: false), null);
        }

        /// <summary> Вызывается при завершении дня: отменяет ожидание и фолбэк. </summary>
        public void OnDayEnding()
        {
            _dayActive = false;
            _awaitingSync = false;
            _fallbackBuild = null;
        }

        /// <summary>
        /// Хост: отправляет игроку, подключившемуся посреди дня, ПОЛНЫЙ снапшот
        /// (с глобальными путями) — в ежедневной рассылке их нет, и у новичка
        /// без полного снимка глобальный режим остался бы пустым.
        /// </summary>
        public void SendSnapshotToPeer(long playerId)
        {
            if (!Context.IsMultiplayer) return;

            SendSnapshot(RouteSnapshot.Capture(_store, _registry), new[] { playerId });
        }

        /// <summary> Тикает каждый апдейт: запускает фолбэк, если снапшот так и не пришёл. </summary>
        public void Tick()
        {
            if (!_awaitingSync) return;

            if (--_fallbackTicksLeft <= 0)
                TriggerFallback();
        }

        /// <summary>
        /// Обработчик ModMessageReceived. Принимает снапшоты только от хоста,
        /// проверяет версию формата и либо применяет, либо откладывает до DayStarted.
        /// </summary>
        public void HandleModMessageReceived(ModMessageReceivedEventArgs e)
        {
            if (e.Type != MessageType || e.FromModID != _modId)
                return;

            // Маршруты авторитетны только у хоста — чужие снапшоты игнорируются.
            if (e.FromPlayerID != Game1.MasterPlayer.UniqueMultiplayerID)
            {
                _monitor.Log("Снапшот маршрутов получен не от хоста — игнорируется.", LogLevel.Warn);
                return;
            }

            RouteSyncEnvelope envelope;
            try
            {
                envelope = e.ReadAs<RouteSyncEnvelope>();
            }
            catch (Exception ex)
            {
                _monitor.Log($"Не удалось прочитать сетевое сообщение маршрутов: {ex.Message}", LogLevel.Error);
                return;
            }

            if (!RouteSyncEnvelope.TryUnpack(envelope, out var snapshot))
            {
                _monitor.Log(
                    "Снапшот маршрутов несовместим или повреждён — используется локальное построение.",
                    LogLevel.Warn);
                TriggerFallback();
                return;
            }

            // Сообщение может прийти до готовности мира, до DayStarted (например,
            // при подключении посреди дня — иначе ClearDay сотрёт его данные)
            // или до инициализации рендереров. В этих случаях — откладываем.
            if (!Context.IsWorldReady || !_dayActive || _tracker == null)
            {
                _pendingSnapshot = snapshot;
                return;
            }

            ApplySnapshot(snapshot);
        }

        // ── Внутреннее ────────────────────────────────────────────────────────────

        private void SendSnapshot(RouteSnapshot snapshot, long[] recipients)
        {
            try
            {
                var envelope = RouteSyncEnvelope.Pack(snapshot);

                _helper.Multiplayer.SendMessage(
                    envelope,
                    MessageType,
                    new[] { _modId },
                    recipients);

                _monitor.Log(
                    $"Отправлен снапшот маршрутов: {envelope.Data.Length} байт (gzip).",
                    LogLevel.Debug);
            }
            catch (Exception ex)
            {
                // Например, превышение лимита размера пакета на очень больших модпаках.
                // Фарм-хэнд в этом случае перейдёт на локальное построение по таймауту.
                _monitor.Log($"Не удалось отправить снапшот маршрутов: {ex.Message}", LogLevel.Error);
            }
        }

        private void ApplySnapshot(RouteSnapshot snapshot)
        {
            try
            {
                snapshot.ApplyTo(_store, _registry);
                _populateModSources?.Invoke();

                _tileRenderer.Clear();
                _state.SwitchGetNpcPath = true;
                _registry.RefreshCurrentNpcList();
                _tracker.InvalidateNpcCache();

                _awaitingSync = false;
                _fallbackBuild = null;
                _pendingSnapshot = null;

                _monitor.Log("Маршруты хоста применены.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Не удалось применить маршруты хоста: {ex.Message}", LogLevel.Error);
                TriggerFallback();
            }
        }

        private void TriggerFallback()
        {
            if (!_awaitingSync || _fallbackBuild == null) return;

            _awaitingSync = false;
            var build = _fallbackBuild;
            _fallbackBuild = null;

            _monitor.Log("Снапшот хоста недоступен — строим маршруты локально.", LogLevel.Warn);
            build();
        }
    }
}
