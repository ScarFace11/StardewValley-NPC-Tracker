using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;

namespace NpcTrackerMod.Rendering
{
    /// <summary>
    /// Отрисовывает цветные тайлы и сетку на игровом мире.
    /// Не содержит логики NPC — только примитивы рисования.
    /// </summary>
    public class TileRenderer
    {
        /// <summary> Состояние тайлов: тайл → (исходный цвет, текущий цвет, приоритет). </summary>
        public Dictionary<Point, (Color Original, Color Current, int Priority)> TileStates { get; }
            = new Dictionary<Point, (Color, Color, int)>();

        /// <summary> Временные цвета NPC (позиция персонажа — синий тайл). </summary>
        public Dictionary<Point, Color> NpcPositionColors { get; }
            = new Dictionary<Point, Color>();

        /// <summary>
        /// Владельцы тайлов: тайл → список (имя NPC, метка времени).
        /// Используется для тултипа при наведении.
        /// </summary>
        public Dictionary<Point, List<(string NpcName, string TimeInfo)>> TileOwners { get; }
            = new Dictionary<Point, List<(string, string)>>();

        public readonly int TileSize = Game1.tileSize;
        private readonly Texture2D _lineTexture;

        /// <summary>Прозрачность тайлов маршрута (0.0–1.0). Синхронизируется с ModConfig.RouteAlpha.</summary>
        public float Alpha { get; set; } = 0.3f;

        // Кеш тайловой сетки: хранит размеры карты, пересчитывается только при смене локации.
        // Вместо списка всех тайлов храним только ширину/высоту и вычисляем видимый диапазон
        // непосредственно в DrawGrid — это даёт O(видимые тайлы) вместо O(все тайлы).
        private string _cachedGridLocation;
        private int    _cachedGridWidth;
        private int    _cachedGridHeight;

        public TileRenderer(GraphicsDevice graphicsDevice)
        {
            _lineTexture = new Texture2D(graphicsDevice, 1, 1);
            _lineTexture.SetData(new[] { Color.White });
        }

        // ── Регистрация ───────────────────────────────────────────────────────────

        /// <summary>
        /// Помечает тайл для отрисовки с заданным приоритетом.
        /// Тайл с более высоким приоритетом перекрывает предыдущий цвет.
        /// </summary>
        public void MarkTile(Point tile, Color color, int priority)
        {
            if (!TileStates.TryGetValue(tile, out var cur) || cur.Priority < priority)
                TileStates[tile] = (cur.Original, color, priority);
        }

        /// <summary>
        /// Помечает тайл как позицию NPC (синий, приоритет 1).
        /// </summary>
        public void MarkNpcPosition(Point tile, Color color, int priority)
        {
            NpcPositionColors[tile] = color;
            MarkTile(tile, color, priority);
        }

        /// <summary>
        /// Регистрирует NPC как владельца тайла (для тултипа).
        /// Дубликаты одного NPC на том же тайле игнорируются.
        /// </summary>
        public void RegisterOwner(Point tile, string npcName, string timeInfo = null)
        {
            if (!TileOwners.TryGetValue(tile, out var list))
            {
                list = new List<(string, string)>();
                TileOwners[tile] = list;
            }
            foreach (var e in list)
                if (e.NpcName == npcName) return;
            list.Add((npcName, timeInfo));
        }

        /// <summary>
        /// Убирает временный цвет NPC с тайла (при движении персонажа).
        /// Если тайл находился в TileStates только как позиция (Priority == 1),
        /// удаляет его полностью — маршрутных данных под ним нет.
        /// Маршрутные тайлы (Priority 2+) остаются нетронутыми.
        /// </summary>
        public void RestorePosition(Point tile)
        {
            NpcPositionColors.Remove(tile);
            if (TileStates.TryGetValue(tile, out var state) && state.Priority == 1)
                TileStates.Remove(tile);
        }

        // ── Отрисовка ────────────────────────────────────────────────────────────

        /// <summary> Рисует все зарегистрированные цветные тайлы. </summary>
        public void DrawAll(SpriteBatch batch, Vector2 cameraOffset)
        {
            foreach (var kvp in TileStates)
            {
                var pos = new Vector2(kvp.Key.X * TileSize, kvp.Key.Y * TileSize) - cameraOffset;
                var color = NpcPositionColors.TryGetValue(kvp.Key, out var tmp)
                    ? tmp
                    : kvp.Value.Current;
                DrawFilledTile(batch, pos, color);
            }
        }

        /// <summary>
        /// Рисует тайловую сетку для текущей локации.
        /// Рисуются только тайлы в пределах видимого вьюпорта —
        /// O(видимые тайлы), а не O(все тайлы карты).
        /// </summary>
        public void DrawGrid(SpriteBatch batch, Vector2 cameraOffset)
        {
            string locName = Game1.currentLocation?.Name;
            if (locName != _cachedGridLocation)
            {
                _cachedGridLocation = locName;
                if (Game1.currentLocation != null)
                {
                    var map = Game1.currentLocation.Map.Layers[0];
                    _cachedGridWidth  = map.LayerWidth;
                    _cachedGridHeight = map.LayerHeight;
                }
                else
                {
                    _cachedGridWidth  = 0;
                    _cachedGridHeight = 0;
                }
            }

            if (_cachedGridWidth == 0) return;

            // Вычисляем диапазон видимых тайлов прямо из вьюпорта.
            int vpLeft   = Math.Max(0, (int)(cameraOffset.X / TileSize));
            int vpTop    = Math.Max(0, (int)(cameraOffset.Y / TileSize));
            int vpRight  = Math.Min(_cachedGridWidth  - 1, (int)((cameraOffset.X + Game1.viewport.Width)  / TileSize) + 1);
            int vpBottom = Math.Min(_cachedGridHeight - 1, (int)((cameraOffset.Y + Game1.viewport.Height) / TileSize) + 1);

            for (int x = vpLeft; x <= vpRight; x++)
            {
                for (int y = vpTop; y <= vpBottom; y++)
                {
                    var pos = new Vector2(x * TileSize, y * TileSize) - cameraOffset;
                    DrawTileOutline(batch, pos, Color.Black);
                }
            }
        }

        // ── Сброс ────────────────────────────────────────────────────────────────

        /// <summary>Очищает состояние тайлов, владельцев и кеш сетки (смена локации / начало дня).</summary>
        public void Clear()
        {
            TileStates.Clear();
            TileOwners.Clear();
            _cachedGridLocation = null;
        }

        // ── Приватные хелперы ─────────────────────────────────────────────────────

        private void DrawFilledTile(SpriteBatch batch, Vector2 pos, Color color)
        {
            var rect = new Rectangle((int)pos.X, (int)pos.Y, TileSize, TileSize);
            batch.Draw(_lineTexture, rect, new Color(color, Alpha));
        }

        private void DrawTileOutline(SpriteBatch batch, Vector2 pos, Color color)
        {
            int x = (int)pos.X, y = (int)pos.Y;
            batch.Draw(_lineTexture, new Rectangle(x, y, TileSize, 1), color);
            batch.Draw(_lineTexture, new Rectangle(x, y + TileSize - 1, TileSize, 1), color);
            batch.Draw(_lineTexture, new Rectangle(x, y, 1, TileSize), color);
            batch.Draw(_lineTexture, new Rectangle(x + TileSize - 1, y, 1, TileSize), color);
        }
    }
}
