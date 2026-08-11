using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NpcTrackerMod.Core;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Tracking
{
    /// <summary>
    /// Хранит списки NPC и управляет выбором / чёрным списком.
    /// Не содержит данных о путях — только о персонажах.
    /// </summary>
    public class NpcRegistry
    {
        private readonly IMonitor _monitor;
        private readonly NpcPathStore _store;

        /// <summary> Все отслеживаемые NPC (O(1) Contains). </summary>
        public HashSet<string> TotalNpcList { get; } = new HashSet<string>();

        /// <summary> NPC текущей локации в алфавитном порядке. </summary>
        public List<string> CurrentNpcList { get; private set; } = new List<string>();

        /// <summary> Все NPC игры (обновляется в начале дня). </summary>
        public List<NPC> GameNpcs { get; private set; }

        /// <summary> Имя последнего выбранного NPC (сохраняется при RefreshCurrentNpcList). </summary>
        public string CurrentNpcName { get; set; }

        /// <summary> Множество NPC, выбранных для одновременного отслеживания. </summary>
        public HashSet<string> SelectedNpcNames { get; } = new HashSet<string>();

        /// <summary> Источник каждого NPC: "Жители деревни" или название мода. </summary>
        public Dictionary<string, string> NpcModSource { get; } = new Dictionary<string, string>();

        /// <summary>
        /// Все ключи вариантов расписания для каждого NPC.
        /// Заполняется ScheduleProcessor при построении дневного маршрута.
        /// Читается TrackingMenu для отображения выбора варианта в пошаговом режиме.
        /// </summary>
        public Dictionary<string, List<string>> NpcVariantKeys { get; }
            = new Dictionary<string, List<string>>();

        public NpcRegistry(IMonitor monitor, NpcPathStore store)
        {
            _monitor = monitor;
            _store = store;
        }

        // ── Жизненный цикл ────────────────────────────────────────────────────────

        /// <summary>
        /// Собирает список всех NPC из всех локаций игры.
        /// Вызывается в начале каждого дня.
        /// </summary>
        public void RefreshGameNpcs()
        {
            GameNpcs = Game1.locations
                .Where(loc => loc?.characters != null)
                .SelectMany(loc => loc.characters)
                .Where(npc => npc != null)
                .ToList();
        }

        /// <summary>
        /// Обновляет CurrentNpcList под текущую локацию игрока.
        /// Сохраняет выбранный NPC, если он остался в списке.
        /// </summary>
        public void RefreshCurrentNpcList()
        {
            var prevName = CurrentNpcName;

            var inLocation = new HashSet<string>(
                Game1.currentLocation.characters.Select(n => n.Name));

            CurrentNpcList = TotalNpcList
                .Where(n => inLocation.Contains(n))
                .OrderBy(n => n)
                .ToList();

            CurrentNpcName = CurrentNpcList.Contains(prevName)
                ? prevName
                : CurrentNpcList.Count > 0 ? CurrentNpcList[0] : null;
        }

        /// <summary>
        /// Добавляет в CurrentNpcList тех NPC из <paramref name="npcs"/>,
        /// которые есть в TotalNpcList.
        /// </summary>
        public void AddToCurrentList(IEnumerable<NPC> npcs)
        {
            foreach (var npc in npcs)
                if (TotalNpcList.Contains(npc.Name))
                    CurrentNpcList.Add(npc.Name);
        }

        // ── Выбор NPC ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает имя NPC под индексом <paramref name="selectedIndex"/>
        /// или null, если список пуст, индекс некорректен или у NPC нет дневного пути.
        /// </summary>
        public string GetSelectedNpcName(int selectedIndex)
        {
            if (CurrentNpcList.Count == 0 || selectedIndex >= CurrentNpcList.Count)
                return null;

            string name = CurrentNpcList[selectedIndex];
            return _store.DayPaths.ContainsKey(name) ? name : null;
        }

        // ── Сброс ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Очищает дневное состояние (вызывается в начале каждого дня).
        /// </summary>
        public void ClearDay()
        {
            TotalNpcList.Clear();
            CurrentNpcList.Clear();
            CurrentNpcName = null;
            SelectedNpcNames.Clear();
            NpcVariantKeys.Clear();
        }

        /// <summary>
        /// Заменяет данные маршрутов из снапшота хоста (TotalNpcList, NpcVariantKeys),
        /// сохраняя пользовательский выбор NPC (SelectedNpcNames, CurrentNpcName,
        /// CurrentNpcList). Вызывается вместо ClearDay при применении сетевого снапшота,
        /// чтобы подключившийся посреди дня игрок не терял выбор.
        /// </summary>
        public void SyncFromSnapshot(
            IEnumerable<string> totalNpcList,
            Dictionary<string, List<string>> variantKeys)
        {
            TotalNpcList.Clear();
            if (totalNpcList != null)
            {
                foreach (var name in totalNpcList)
                {
                    if (!string.IsNullOrEmpty(name))
                        TotalNpcList.Add(name);
                }
            }

            NpcVariantKeys.Clear();
            if (variantKeys != null)
            {
                foreach (var kvp in variantKeys)
                {
                    if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value != null)
                        NpcVariantKeys[kvp.Key] = new List<string>(kvp.Value);
                }
            }
        }
    }
}
