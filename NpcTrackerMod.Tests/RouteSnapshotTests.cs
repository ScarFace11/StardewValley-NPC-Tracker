using System.Collections.Generic;
using Newtonsoft.Json;
using NpcTrackerMod.Core;
using NpcTrackerMod.Multiplayer;
using Xunit;

namespace NpcTrackerMod.Tests
{
    /// <summary>
    /// Тесты слоя синхронизации маршрутов: TilePoint и сериализация RouteSnapshot.
    /// Проверяют именно ту проблему, из-за которой раньше существовал SerPoint
    /// (XNA Point не сериализуется Newtonsoft.Json напрямую), а также gzip-обёртку.
    /// </summary>
    public class RouteSnapshotTests
    {
        // ── TilePoint ─────────────────────────────────────────────────────────────

        [Fact]
        public void TilePoint_EqualityAndHashCode()
        {
            Assert.Equal(new TilePoint(3, 4), new TilePoint(3, 4));
            Assert.Equal(
                new TilePoint(3, 4).GetHashCode(),
                new TilePoint(3, 4).GetHashCode());
            Assert.NotEqual(new TilePoint(3, 4), new TilePoint(4, 3));
            Assert.Equal("(3, 4)", new TilePoint(3, 4).ToString());
        }

        // ── JSON round-trip ───────────────────────────────────────────────────────

        [Fact]
        public void JsonRoundTrip_PreservesAllData()
        {
            var original = SampleSnapshot();

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RouteSnapshot>(json);

            Assert.Equal(Serialize(original), Serialize(restored));
        }

        [Fact]
        public void JsonRoundTrip_EmptySnapshot_Survives()
        {
            var original = new RouteSnapshot();

            var json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<RouteSnapshot>(json);

            Assert.NotNull(restored);
            Assert.Equal(Serialize(original), Serialize(restored));
        }

        [Fact]
        public void ToSetMap_HandlesNullAndEmptyInputs()
        {
            Assert.Empty(RouteSnapshot.ToSetMap(null));
            Assert.Empty(RouteSnapshot.ToSetMap(new Dictionary<string, List<TilePoint>>()));

            var map = new Dictionary<string, List<TilePoint>>
            {
                { "Town", null },                          // null-список → пустое множество
                { "Beach", new List<TilePoint> { new TilePoint(1, 2) } }
            };

            var result = RouteSnapshot.ToSetMap(map);
            Assert.Equal(2, result.Count);
            Assert.Empty(result["Town"]);
            Assert.Contains(new TilePoint(1, 2), result["Beach"]);
        }

        // ── gzip envelope ─────────────────────────────────────────────────────────

        [Fact]
        public void PackUnpack_RoundTripsAllData()
        {
            var snapshot = SampleSnapshot();

            var envelope = RouteSyncEnvelope.Pack(snapshot);
            Assert.True(RouteSyncEnvelope.TryUnpack(envelope, out var restored));

            Assert.Equal(Serialize(snapshot), Serialize(restored));
        }

        [Fact]
        public void Pack_CompressesPayload()
        {
            // Полный снапшот с тайминговыми путями избыточен — сжатие должно
            // дать заметный выигрыш по сравнению с сырым JSON. Выборка должна
            // быть реалистичной: на крошечных снапшотах накладные расходы gzip
            // сравнимы с самими данными, и тест оказывается на границе порога.
            var snapshot = RepetitiveSnapshot();
            string rawJson = JsonConvert.SerializeObject(snapshot);

            var envelope = RouteSyncEnvelope.Pack(snapshot);

            Assert.True(envelope.Data.Length < rawJson.Length / 2,
                $"gzip: {envelope.Data.Length} байт, raw JSON: {rawJson.Length} байт");
        }

        [Fact]
        public void TryUnpack_RejectsWrongVersion()
        {
            var envelope = RouteSyncEnvelope.Pack(new RouteSnapshot());
            envelope.Version = RouteSnapshot.CurrentVersion + 1;

            Assert.False(RouteSyncEnvelope.TryUnpack(envelope, out var snapshot));
            Assert.Null(snapshot);
        }

        [Fact]
        public void TryUnpack_RejectsCorruptData()
        {
            var envelope = new RouteSyncEnvelope
            {
                Version = RouteSnapshot.CurrentVersion,
                Data = new byte[] { 1, 2, 3 }
            };

            Assert.False(RouteSyncEnvelope.TryUnpack(envelope, out _));
        }

        [Fact]
        public void TryUnpack_RejectsNullAndEmpty()
        {
            Assert.False(RouteSyncEnvelope.TryUnpack(null, out _));
            Assert.False(RouteSyncEnvelope.TryUnpack(
                new RouteSyncEnvelope { Version = RouteSnapshot.CurrentVersion }, out _));
        }

        // ── Вспомогательные ───────────────────────────────────────────────────────

        private static string Serialize(RouteSnapshot snapshot)
            => JsonConvert.SerializeObject(snapshot);

        /// <summary>
        /// Реалистичный по объёму снапшот: 10 NPC с дневными, глобальными
        /// и тайминговыми путями (≈15 тыс. тайлов) — тайлы сильно избыточны,
        /// поэтому gzip даёт стабильный многократный выигрыш.
        /// </summary>
        private static RouteSnapshot RepetitiveSnapshot()
        {
            var snapshot = new RouteSnapshot();

            for (int n = 0; n < 10; n++)
            {
                string name = "NPC" + n;

                var locs = new Dictionary<string, List<TilePoint>>
                {
                    { "Town", RepetitiveTiles(100, 0) },
                    { "Beach", RepetitiveTiles(100, 1000) },
                    { "Mountain", RepetitiveTiles(100, 2000) }
                };
                snapshot.DayPaths[name] = locs;
                snapshot.GlobalPaths[name] = locs;

                var timed = new Dictionary<int, Dictionary<string, List<TilePoint>>>();
                for (int t = 0; t < 6; t++)
                {
                    timed[600 + t * 100] = new Dictionary<string, List<TilePoint>>
                    {
                        { "Town", RepetitiveTiles(100, t) },
                        { "Beach", RepetitiveTiles(100, t + 500) }
                    };
                }
                snapshot.TimedDayPaths[name] = timed;
            }

            return snapshot;
        }

        private static List<TilePoint> RepetitiveTiles(int count, int offset)
        {
            var list = new List<TilePoint>(count);
            for (int i = 0; i < count; i++)
                list.Add(new TilePoint(i % 40, (i * 7 + offset) % 40));
            return list;
        }

        private static RouteSnapshot SampleSnapshot()
        {
            var snapshot = new RouteSnapshot
            {
                DayPaths = new Dictionary<string, Dictionary<string, List<TilePoint>>>
                {
                    {
                        "Abigail",
                        new Dictionary<string, List<TilePoint>>
                        {
                            { "Town", new List<TilePoint> { new TilePoint(10, 20), new TilePoint(11, 21) } },
                            { "Saloon", new List<TilePoint> { new TilePoint(30, 15) } }
                        }
                    },
                    {
                        "Pierre",
                        new Dictionary<string, List<TilePoint>>
                        {
                            { "SeedShop", new List<TilePoint> { new TilePoint(5, 5), new TilePoint(5, 6) } }
                        }
                    }
                },
                GlobalPaths = new Dictionary<string, Dictionary<string, List<TilePoint>>>
                {
                    {
                        "Abigail",
                        new Dictionary<string, List<TilePoint>>
                        {
                            { "Mine", new List<TilePoint> { new TilePoint(40, 40) } }
                        }
                    }
                },
                TimedDayPaths = new Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>
                {
                    {
                        "Abigail",
                        new Dictionary<int, Dictionary<string, List<TilePoint>>>
                        {
                            { 900, new Dictionary<string, List<TilePoint>>
                                { { "Town", new List<TilePoint> { new TilePoint(10, 20) } } } },
                            { 1200, new Dictionary<string, List<TilePoint>>
                                { { "Saloon", new List<TilePoint> { new TilePoint(30, 15) } } } }
                        }
                    }
                },
                VariantTimedPaths = new Dictionary<string,
                    Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>>
                {
                    {
                        "Abigail",
                        new Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>
                        {
                            {
                                "rain",
                                new Dictionary<int, Dictionary<string, List<TilePoint>>>
                                {
                                    { 1400, new Dictionary<string, List<TilePoint>>
                                        { { "Town", new List<TilePoint> { new TilePoint(12, 30) } } } }
                                }
                            }
                        }
                    }
                },
                ActiveScheduleKeys = new Dictionary<string, string>
                {
                    { "Abigail", "spring_Mon" }
                },
                NpcVariantKeys = new Dictionary<string, List<string>>
                {
                    { "Abigail", new List<string> { "rain", "spring_Mon" } }
                },
                TotalNpcList = new List<string> { "Abigail", "Pierre" }
            };

            return snapshot;
        }
    }
}
