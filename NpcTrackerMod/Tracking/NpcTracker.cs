using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NpcTrackerMod.Core;
using NpcTrackerMod.Rendering;
using StardewValley;

namespace NpcTrackerMod.Tracking
{
    /// <summary>
    /// Оркестрирует покадровую отрисовку путей NPC.
    /// Не содержит ни данных, ни логики расписаний — только координирует вызовы.
    /// </summary>
    public class NpcTracker
    {
        private readonly ModState    _state;
        private readonly NpcRegistry _registry;
        private readonly RouteRenderer _routeRenderer;
        private readonly TileRenderer  _tileRenderer;

        public NpcTracker(
            ModState    state,
            NpcRegistry registry,
            RouteRenderer routeRenderer,
            TileRenderer  tileRenderer)
        {
            _state         = state;
            _registry      = registry;
            _routeRenderer = routeRenderer;
            _tileRenderer  = tileRenderer;
        }

        /// <summary>
        /// Главный метод отрисовки — вызывается в OnRenderedWorld каждый кадр.
        /// </summary>
        public void DrawPaths(SpriteBatch spriteBatch, Vector2 cameraOffset)
        {
            bool allLocations = _state.SwitchTargetLocations || _state.SwitchGlobalNpcPath;
            foreach (var npc in GetNpcsToTrack(allLocations, _registry.TotalNpcList))
            {
                if (npc == null || string.IsNullOrWhiteSpace(npc.Name)) continue;
                if (!_state.SwitchTargetNPC || _registry.SelectedNpcNames.Contains(npc.Name))
                {
                    _routeRenderer.DrawRoute(npc);
                    _routeRenderer.DrawPositionTile(npc);
                }
            }

            _tileRenderer.DrawAll(spriteBatch, cameraOffset);
            _state.SwitchGetNpcPath = false;
        }

        /// <summary>
        /// Возвращает NPC, которых нужно визуализировать в текущем кадре.
        /// В обычном режиме — только NPC текущей локации.
        /// В режиме всех локаций / глобального маршрута — NPC из всех локаций.
        /// </summary>
        private static IEnumerable<NPC> GetNpcsToTrack(bool allLocations, HashSet<string> tracked)
        {
            if (!allLocations)
                return Game1.currentLocation?.characters
                    .Where(n => tracked.Contains(n.Name))
                    ?? Enumerable.Empty<NPC>();

            return Game1.locations
                .Where(loc => loc?.characters != null)
                .SelectMany(loc => loc.characters)
                .Where(n => n != null && tracked.Contains(n.Name));
        }
    }
}
