using System;

namespace NpcTrackerMod.Core
{
    /// <summary>
    /// Сериализуемая замена Microsoft.Xna.Framework.Point для слоя данных.
    /// Не зависит от XNA — поэтому NpcPathStore и RouteSnapshot можно безопасно
    /// сериализовать через Newtonsoft.Json и тестировать без игровых сборок.
    /// </summary>
    public struct TilePoint : IEquatable<TilePoint>
    {
        public int X { get; set; }
        public int Y { get; set; }

        public TilePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TilePoint other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is TilePoint other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => $"({X}, {Y})";
    }
}
