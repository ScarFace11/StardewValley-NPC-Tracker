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
        /// </summary>
        public static RouteSnapshot Capture(NpcPathStore store, NpcRegistry registry)
        {
            var snapshot = new RouteSnapshot
            {
                Version = CurrentVersion,
                DayPaths = ToListPaths(store.DayPaths),
                GlobalPaths = ToListPaths(store.GlobalPaths),
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
            // ClearAll также сбрасывает кеши отсортированных шагов.
            store.ClearAll();

            foreach (var kvp in dayPaths)
                store.DayPaths[kvp.Key] = kvp.Value;
            foreach (var kvp in globalPaths)
                store.GlobalPaths[kvp.Key] = kvp.Value;
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
