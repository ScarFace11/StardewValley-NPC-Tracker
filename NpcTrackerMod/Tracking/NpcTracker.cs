using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NpcTrackerMod.Core;
using NpcTrackerMod.Rendering;
using NpcTrackerMod.Scheduling;
using StardewValley;

namespace NpcTrackerMod.Tracking
{
    /// <summary>
    /// Оркестрирует покадровую отрисовку путей NPC.
    /// Не содержит ни данных, ни логики расписаний — только координирует вызовы.
    /// </summary>
    public class NpcTracker
    {
        private readonly ModState _state;
        private readonly NpcRegistry _registry;
        private readonly ScheduleProcessor _processor;
        private readonly RouteRenderer _routeRenderer;
        private readonly TileRenderer _tileRenderer;

        public NpcTracker(
            ModState state,
            NpcRegistry registry,
            ScheduleProcessor processor,
            RouteRenderer routeRenderer,
            TileRenderer tileRenderer)
        {
            _state = state;
            _registry = registry;
            _processor = processor;
            _routeRenderer = routeRenderer;
            _tileRenderer = tileRenderer;
        }

        /// <summary>
        /// Главный метод отрисовки — вызывается в OnRenderedWorld каждый кадр.
        /// </summary>
        public void DrawPaths(SpriteBatch spriteBatch, Vector2 cameraOffset)
        {
            // В глобальном режиме нужны все NPC (не только те, что сейчас в текущей локации),
            // чтобы показать весь набор маршрутов через текущую карту.
            bool allLocations = _state.SwitchTargetLocations || _state.SwitchGlobalNpcPath;
            foreach (var npc in _processor.GetNpcsToTrack(allLocations, _registry.TotalNpcList))
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
    }
}