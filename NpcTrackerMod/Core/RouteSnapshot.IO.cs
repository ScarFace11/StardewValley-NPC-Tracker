using System.Collections.Generic;
using NpcTrackerMod.Tracking;

namespace NpcTrackerMod.Core
{
    /// <summary>
    /// Связка RouteSnapshot с NpcPathStore и NpcRegistry (часть класса вне чистой
    /// части RouteSnapshot.cs — этот файл не включается в тестовый проект).
    /// </summary>
    public partial class RouteSnapshot
    {
        /// <summary>
        /// Собирает полный снимок из хранилища и реестра. Вызывается хостом.
        /// Глобальные пути строятся один раз за сессию и не меняются, поэтому
        /// в ежедневную рассылку их можно не включать (includeGlobalPaths: false) —
        /// фарм-хэнд сохраняет полученные ранее. Для подключившихся посреди дня
        /// нужен полный снимок, иначе у них не будет глобальных путей.
        /// </summary>
        public static RouteSnapshot Capture(
            NpcPathStore store,
            NpcRegistry registry,
            bool includeGlobalPaths = true)
        {
            var snapshot = new RouteSnapshot
            {
                Version = CurrentVersion,
                DayPaths = ToListPaths(store.DayPaths),
                GlobalPaths = includeGlobalPaths
                    ? ToListPaths(store.GlobalPaths)
                    : new Dictionary<string, Dictionary<string, List<TilePoint>>>(),
                TimedDayPaths = ToListTimedPaths(store.TimedDayPaths),
                VariantTimedPaths = ToListVariantPaths(store.VariantTimedPaths),
                ActiveScheduleKeys = new Dictionary<string, string>(store.ActiveScheduleKeys)
            };

            foreach (var name in registry.TotalNpcList)
            {
                if (!string.IsNullOrEmpty(name))
                    snapshot.TotalNpcList.Add(name);
            }

            foreach (var kvp in registry.NpcVariantKeys)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                    snapshot.NpcVariantKeys[kvp.Key] = new List<string>(kvp.Value);
            }

            return snapshot;
        }

        /// <summary>
        /// Применяет снимок к хранилищу и реестру. Атомарно: сначала строятся все
        /// новые словари, и только после успешного построения старые данные
        /// заменяются — исключение посреди применения не оставляет хранилище
        /// в частично очищенном состоянии.
        /// </summary>
        public void ApplyTo(NpcPathStore store, NpcRegistry registry)
        {
            // Фаза 1: построение (хранилище не трогается — сбой здесь безопасен).
            var dayPaths = ToSetPaths(DayPaths);
            var globalPaths = ToSetPaths(GlobalPaths);
            var timedDayPaths = ToSetTimedPaths(TimedDayPaths);
            var variantTimedPaths = ToSetVariantPaths(VariantTimedPaths);
            var activeKeys = new Dictionary<string, string>(
                ActiveScheduleKeys ?? new Dictionary<string, string>());

            // Фаза 2: замена (не может выбросить исключение).
            // ClearDay сбрасывает дневные данные и кеши шагов, но не трогает
            // глобальные пути: в ежедневных снапшотах (со 2-го дня) их нет,
            // и фарм-хэнд должен сохранить полученные ранее.
            store.ClearDay();

            foreach (var kvp in dayPaths)
                store.DayPaths[kvp.Key] = kvp.Value;
            if (globalPaths.Count > 0)
            {
                store.GlobalPaths.Clear();
                foreach (var kvp in globalPaths)
                    store.GlobalPaths[kvp.Key] = kvp.Value;
            }
            foreach (var kvp in timedDayPaths)
                store.TimedDayPaths[kvp.Key] = kvp.Value;
            foreach (var kvp in variantTimedPaths)
                store.VariantTimedPaths[kvp.Key] = kvp.Value;
            foreach (var kvp in activeKeys)
                store.ActiveScheduleKeys[kvp.Key] = kvp.Value;

            // Реестр: заменяются только данные маршрутов; пользовательский выбор
            // NPC (SelectedNpcNames, CurrentNpcName) сохраняется.
            registry.SyncFromSnapshot(TotalNpcList, NpcVariantKeys);
        }
    }
}
