using System.Collections.Generic;
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
        private readonly ModState      _state;
        private readonly NpcRegistry   _registry;
        private readonly RouteRenderer _routeRenderer;
        private readonly TileRenderer  _tileRenderer;

        // Кеш NPC для текущего кадра — пересобирается только при явной инвалидации.
        // Инвалидация вызывается ModEntry через InvalidateNpcCache() при изменении локации
        // или состава NPC, а не поллингом каждый кадр.
        private readonly List<NPC> _cachedNpcs = new List<NPC>();
        private bool _cachedAllLocations;
        private bool _npcCacheDirty = true;

        public NpcTracker(
            ModState      state,
            NpcRegistry   registry,
            RouteRenderer routeRenderer,
            TileRenderer  tileRenderer)
        {
            _state         = state;
            _registry      = registry;
            _routeRenderer = routeRenderer;
            _tileRenderer  = tileRenderer;
        }

        /// <summary>
        /// Сигнализирует, что список NPC нужно пересобрать при следующем кадре.
        /// Вызывается ModEntry при смене локации, варпе или изменении состава NPC —
        /// вместо поллинга CountAllNpcs() каждый кадр.
        /// </summary>
        public void InvalidateNpcCache() => _npcCacheDirty = true;

        /// <summary>
        /// Главный метод отрисовки — вызывается в OnRenderedWorld каждый кадр.
        /// </summary>
        public void DrawPaths(SpriteBatch spriteBatch, Vector2 cameraOffset)
        {
            bool allLocations = _state.SwitchTargetLocations || _state.SwitchGlobalNpcPath;

            // Пересобираем кеш только при явном запросе или смене режима локаций.
            // Инвалидация по событиям (варп, изменение состава) — через InvalidateNpcCache().
            if (_state.SwitchGetNpcPath || allLocations != _cachedAllLocations || _npcCacheDirty)
            {
                _cachedAllLocations = allLocations;
                _npcCacheDirty      = false;
                RebuildNpcCache(allLocations);
            }

            foreach (var npc in _cachedNpcs)
            {
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
        /// Пересобирает список NPC для отрисовки без LINQ-аллокаций.
        /// В обычном режиме — только NPC текущей локации.
        /// В режиме всех локаций / глобального маршрута — NPC из всех локаций.
        /// </summary>
        private void RebuildNpcCache(bool allLocations)
        {
            _cachedNpcs.Clear();
            var tracked = _registry.TotalNpcList;

            if (allLocations)
            {
                foreach (var loc in Game1.locations)
                {
                    if (loc?.characters == null) continue;
                    foreach (var npc in loc.characters)
                    {
                        if (npc != null && !string.IsNullOrWhiteSpace(npc.Name)
                            && tracked.Contains(npc.Name))
                            _cachedNpcs.Add(npc);
                    }
                }
            }
            else
            {
                var chars = Game1.currentLocation?.characters;
                if (chars == null) return;
                foreach (var npc in chars)
                {
                    if (npc != null && !string.IsNullOrWhiteSpace(npc.Name)
                        && tracked.Contains(npc.Name))
                        _cachedNpcs.Add(npc);
                }
            }
        }
    }
}
