using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Scheduling
{
    /// <summary>
    /// Определяет, какой ключ расписания активен для NPC сегодня.
    /// Повторяет логику приоритетов из NPC.getSchedule() игры:
    /// брак → погода → сезон+день → день недели → сезон → дефолт.
    /// </summary>
    public static class ScheduleVariantResolver
    {
        /// <summary>
        /// Возвращает наиболее подходящий ключ расписания для NPC на текущий игровой день.
        /// Если ни один ключ-кандидат не найден — возвращает первый доступный ключ.
        /// </summary>
        public static string GetActiveKey(NPC npc, IMonitor monitor)
        {
            var rawData = npc.getMasterScheduleRawData();
            if (rawData == null || rawData.Count == 0) return null;

            string season    = Game1.currentSeason ?? "spring";
            int    day       = Game1.dayOfMonth;
            bool   isRaining = Game1.isRaining || Game1.isLightning;
            bool   isMarried = Game1.player != null
                               && Game1.player.isMarriedOrRoommates()
                               && !string.IsNullOrEmpty(Game1.player.spouse)
                               && Game1.player.spouse == npc.Name;
            int    hearts    = Game1.player.getFriendshipHeartLevelForNPC(npc.Name);

            string dayOfWeek = GetDayOfWeek(day);

            foreach (string key in CandidateKeys(season, day, dayOfWeek, isMarried, isRaining, hearts))
            {
                if (rawData.ContainsKey(key))
                {
                    monitor?.Log(
                        $"[ScheduleVariantResolver] {npc.Name}: выбран ключ '{key}'" +
                        $" (женат={isMarried}, дождь={isRaining}, сердечки={hearts})",
                        LogLevel.Trace);
                    return key;
                }
            }

            // Фолбэк: первый ключ в словаре
            string fallback = rawData.Keys.First();
            monitor?.Log(
                $"[ScheduleVariantResolver] {npc.Name}: ни один ключ-кандидат не найден, " +
                $"используется '{fallback}'",
                LogLevel.Debug);
            return fallback;
        }

        /// <summary>
        /// Возвращает только одну запись словаря, соответствующую активному ключу.
        /// Если ключ не найден — возвращает всю коллекцию (поведение как раньше).
        /// </summary>
        public static Dictionary<string, string> GetActiveSchedule(NPC npc, IMonitor monitor)
        {
            var rawData = npc.getMasterScheduleRawData();
            if (rawData == null || rawData.Count == 0)
                return new Dictionary<string, string>();

            string key = GetActiveKey(npc, monitor);
            if (key != null && rawData.TryGetValue(key, out string value))
                return new Dictionary<string, string> { [key] = value };

            return rawData;
        }

        // ── Генератор ключей-кандидатов (в порядке убывания приоритета) ───────────

        private static IEnumerable<string> CandidateKeys(
            string season, int day, string dayOfWeek,
            bool isMarried, bool isRaining, int hearts)
        {
            // 1. Брачные расписания (только если NPC — супруг игрока)
            if (isMarried)
            {
                if (isRaining)
                {
                    yield return $"marriage_{season}_rain";
                    yield return "marriage_rain";
                }
                yield return $"marriage_{season}_{day}";
                yield return $"marriage_{season}_{dayOfWeek}";
                yield return $"marriage_{season}";
                yield return "marriage";
            }

            // 2. Погода
            if (isRaining)
            {
                yield return $"{season}_rain";
                yield return $"rain_{day}";
                yield return "rain";
            }

            // 3. Дружба (ищем с наивысшего чётного уровня сердечек вниз)
            int heartStep = (hearts / 2) * 2;
            while (heartStep >= 2)
            {
                yield return $"{season}_{day}_{heartStep}";
                heartStep -= 2;
            }

            // 4. Сезон + конкретный день
            yield return $"{season}_{day}";

            // 5. Сезон + день недели
            yield return $"{season}_{dayOfWeek}";
            yield return dayOfWeek;

            // 6. Только номер дня (без сезона)
            yield return day.ToString();

            // 7. Только сезон
            yield return season;

            // 8. Spring как исторический дефолт (используется во многих модах)
            if (season != "spring") yield return "spring";

            // 9. Явный дефолт
            yield return "default";
            yield return "Default";
        }

        // ── Вспомогательное ──────────────────────────────────────────────────────

        /// <summary>
        /// Возвращает трёхбуквенный день недели из номера дня месяца (1–28).
        /// Stardew-месяц = 4 недели по 7 дней. Неделя: Mon..Sun.
        /// </summary>
        private static string GetDayOfWeek(int dayOfMonth)
        {
            string[] names = { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            return names[(dayOfMonth - 1) % 7];
        }
    }
}