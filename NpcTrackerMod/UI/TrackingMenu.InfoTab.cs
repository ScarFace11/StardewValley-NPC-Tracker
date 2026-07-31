using System.Linq;
using StardewModdingAPI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Menus;

namespace NpcTrackerMod.UI
{
    /// <summary> Вкладка «Инфо»: текущее состояние мира, статистика NPC, источники. </summary>
    public partial class TrackingMenu
    {
        private void DrawInfoTab(SpriteBatch b)
        {
            int x = BX + PAD + 6;
            int y = BY + 62;

            // Текущее состояние
            DrawSectionHeader(b, T("info.currentState"), x, y); y += 36;
            if (Context.IsWorldReady)
            {
                string season = Game1.currentSeason ?? "—";
                DrawKV(b, T("info.date"),
                    $"{Capitalize(season)}, {Game1.dayOfMonth}, {Game1.year}", x, ref y);
                DrawKV(b, T("info.location"),
                    Game1.currentLocation?.Name ?? "—", x, ref y);
            }
            else
            {
                Utility.drawTextWithShadow(b, T("info.worldNotReady"), Game1.smallFont,
                    new Vector2(x, y), Color.Gray);
                y += 26;
            }

            DrawDivider(b, y + 6); y += 26;

            // Статистика NPC
            DrawSectionHeader(b, T("info.npcStats"), x, y); y += 36;
            DrawKV(b, T("info.tracked"),    _registry.TotalNpcList.Count.ToString(), x, ref y);
            DrawKV(b, T("info.inLocation"), (Game1.currentLocation?.characters.Count ?? 0).ToString(), x, ref y);
            DrawKV(b, T("info.selected"),
                _state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0
                    ? _registry.SelectedNpcNames.Count.ToString()
                    : T("info.noneSelected"),
                x, ref y);

            if (_state.SwitchTargetNPC && _registry.SelectedNpcNames.Count > 0)
            {
                foreach (var sn in _registry.SelectedNpcNames.OrderBy(n => n))
                {
                    DrawKV(b, "  •", sn, x, ref y);
                    if (y > BY + BOX_H - 60) break;
                }
            }

            DrawDivider(b, y + 6); y += 26;

            // Источники NPC
            DrawSectionHeader(b, T("info.bySource"), x, y); y += 36;

            var groups = _registry.NpcModSource
                .GroupBy(kv => kv.Value)
                .OrderBy(g => g.Key)
                .ToList();

            if (groups.Count == 0)
            {
                Utility.drawTextWithShadow(b, T("info.noData"), Game1.smallFont,
                    new Vector2(x, y), Color.Gray);
                y += 26;
            }
            else
            {
                foreach (var g in groups)
                    DrawKV(b, g.Key, $"{g.Count()} NPC", x, ref y);
            }

            DrawDivider(b, y + 8); y += 24;

            DrawSectionHeader(b, T("info.tip"), x, y); y += 32;
            Utility.drawTextWithShadow(b, T("info.tipText"), Game1.smallFont,
                new Vector2(x, y), new Color(120, 110, 90));
        }
    }
}
