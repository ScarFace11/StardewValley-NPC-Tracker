using System.Linq;
using NpcTrackerMod.Rendering;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Core
{
    /// <summary>
    /// Shared helper for schedule-based display strings.
    /// Centralises the "next destination" lookup so ModEntry and TileInspectMenu
    /// stay in sync when schedule formatting or edge-case handling changes.
    /// </summary>
    public static class ScheduleDisplayHelper
    {
        /// <summary>
        /// Returns a localised "→ Saloon at 12:00" string for the next scheduled
        /// entry of <paramref name="npc"/> after the current in-game time,
        /// or <see langword="null"/> when no future entry exists (or schedule is empty).
        /// </summary>
        public static string GetNextDestinationLabel(NPC npc, ITranslationHelper i18n)
        {
            if (npc?.Schedule == null || npc.Schedule.Count == 0)
                return null;

            int  currentTime = Game1.timeOfDay;
            int? nextTime    = null;

            foreach (int key in npc.Schedule.Keys.OrderBy(k => k))
            {
                if (key > currentTime) { nextTime = key; break; }
            }

            if (!nextTime.HasValue) return null;

            var entry = npc.Schedule[nextTime.Value];
            return LocalizationHelper.Get(i18n, "tooltip.nextAt", new
            {
                location = entry.targetLocationName ?? "?",
                time     = RouteRenderer.FormatTime(nextTime.Value)
            });
        }
    }
}
