using System.Collections.Generic;
using Newtonsoft.Json;
using NpcTrackerMod.Core;
using NpcTrackerMod.Multiplayer;
using Xunit;

namespace NpcTrackerMod.Tests
{
    /// <summary>
    /// Тесты конверсионных хелперов RouteSnapshot (ToList* / ToSet*),
    /// сериализации TilePoint и gzip-обёртки на больших объёмах данных.
    /// Это ровно та часть, из-за которой раньше существовал SerPoint:
    /// данные хранилища (HashSet) должны без потерь переноситься в сериализуемый
    /// вид (List) и обратно.
    /// </summary>
    public class RouteSnapshotConversionTests
    {
        // ── ToList*: данные сохраняются ──────────────────────────────────────────

        [Fact]
        public void ToListMap_PreservesData()
        {
            var src = new Dictionary<string, HashSet<TilePoint>>
            {
                { "Town", new HashSet<TilePoint> { new TilePoint(1, 2), new TilePoint(3, 4) } },
                { "Beach", new HashSet<TilePoint> { new TilePoint(9, 9) } }
            };

            var result = RouteSnapshot.ToListMap(src);

            Assert.Equal(2, result.Count);
            Assert.Equal(2, result["Town"].Count);
            Assert.Contains(new TilePoint(1, 2), result["Town"]);
            Assert.Contains(new TilePoint(3, 4), result["Town"]);
            Assert.Contains(new TilePoint(9, 9), result["Beach"]);
        }

        [Fact]
        public void ToListPaths_PreservesNestedData()
        {
            var src = new Dictionary<string, Dictionary<string, HashSet<TilePoint>>>
            {
                {
                    "Abigail",
                    new Dictionary<string, HashSet<TilePoint>>
                    {
                        { "Town", new HashSet<TilePoint> { new TilePoint(5, 5) } }
                    }
                }
            };

            var result = RouteSnapshot.ToListPaths(src);

            Assert.Single(result);
            Assert.Single(result["Abigail"]);
            Assert.Contains(new TilePoint(5, 5), result["Abigail"]["Town"]);
        }

        [Fact]
        public void ToListTimedPaths_PreservesTimeSlots()
        {
            var src = new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>
            {
                {
                    "Abigail",
                    new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>
                    {
                        { 900, new Dictionary<string, HashSet<TilePoint>>
                            { { "Town", new HashSet<TilePoint> { new TilePoint(1, 1) } } } },
                        { 1200, new Dictionary<string, HashSet<TilePoint>>
                            { { "Saloon", new HashSet<TilePoint> { new TilePoint(30, 15) } } } }
                    }
                }
            };

            var result = RouteSnapshot.ToListTimedPaths(src);

            Assert.Single(result);
            Assert.Equal(2, result["Abigail"].Count);
            Assert.Contains(new TilePoint(1, 1), result["Abigail"][900]["Town"]);
            Assert.Contains(new TilePoint(30, 15), result["Abigail"][1200]["Saloon"]);
        }

        [Fact]
        public void ToListVariantPaths_PreservesVariants()
        {
            var src = new Dictionary<string,
                Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>>
            {
                {
                    "Abigail",
                    new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>
                    {
                        {
                            "rain",
                            new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>
                            {
                                { 1400, new Dictionary<string, HashSet<TilePoint>>
                                    { { "Town", new HashSet<TilePoint> { new TilePoint(12, 30) } } } }
                            }
                        }
                    }
                }
            };

            var result = RouteSnapshot.ToListVariantPaths(src);

            Assert.Single(result);
            Assert.Single(result["Abigail"]);
            Assert.Contains(new TilePoint(12, 30), result["Abigail"]["rain"][1400]["Town"]);
        }

        // ── Null-входы: безопасный пустой результат ──────────────────────────────

        [Theory]
        [InlineData("ToListMap")]
        [InlineData("ToListPaths")]
        [InlineData("ToListTimed")]
        [InlineData("ToListTimedPaths")]
        [InlineData("ToListVariants")]
        [InlineData("ToListVariantPaths")]
        [InlineData("ToSetMap")]
        [InlineData("ToSetPaths")]
        [InlineData("ToSetTimed")]
        [InlineData("ToSetTimedPaths")]
        [InlineData("ToSetVariants")]
        [InlineData("ToSetVariantPaths")]
        public void NullInput_ReturnsEmptyCollection(string helper)
        {
            switch (helper)
            {
                case "ToListMap":       Assert.Empty(RouteSnapshot.ToListMap(null)); break;
                case "ToListPaths":     Assert.Empty(RouteSnapshot.ToListPaths(null)); break;
                case "ToListTimed":     Assert.Empty(RouteSnapshot.ToListTimed(null)); break;
                case "ToListTimedPaths": Assert.Empty(RouteSnapshot.ToListTimedPaths(null)); break;
                case "ToListVariants":  Assert.Empty(RouteSnapshot.ToListVariants(null)); break;
                case "ToListVariantPaths": Assert.Empty(RouteSnapshot.ToListVariantPaths(null)); break;
                case "ToSetMap":        Assert.Empty(RouteSnapshot.ToSetMap(null)); break;
                case "ToSetPaths":      Assert.Empty(RouteSnapshot.ToSetPaths(null)); break;
                case "ToSetTimed":      Assert.Empty(RouteSnapshot.ToSetTimed(null)); break;
                case "ToSetTimedPaths": Assert.Empty(RouteSnapshot.ToSetTimedPaths(null)); break;
                case "ToSetVariants":   Assert.Empty(RouteSnapshot.ToSetVariants(null)); break;
                case "ToSetVariantPaths": Assert.Empty(RouteSnapshot.ToSetVariantPaths(null)); break;
            }
        }

        // ── Null внутри структуры ─────────────────────────────────────────────────

        [Fact]
        public void ToSetMap_NullInnerList_BecomesEmptySet()
        {
            var src = new Dictionary<string, List<TilePoint>>
            {
                { "Town", null },
                { "Beach", new List<TilePoint> { new TilePoint(1, 2) } }
            };

            var result = RouteSnapshot.ToSetMap(src);

            Assert.Equal(2, result.Count);
            Assert.Empty(result["Town"]);
            Assert.Single(result["Beach"]);
        }

        [Fact]
        public void ToListMap_NullInnerSet_BecomesEmptyList()
        {
            var src = new Dictionary<string, HashSet<TilePoint>>
            {
                { "Town", null }
            };

            var result = RouteSnapshot.ToListMap(src);

            Assert.Single(result);
            Assert.Empty(result["Town"]);
        }

        // ── Round-trip: HashSet → List → HashSet ──────────────────────────────────

        [Fact]
        public void RoundTrip_SetToSet_AllShapes_PreserveData()
        {
            // Дневные/глобальные пути: NPC → локация → тайлы
            var paths = new Dictionary<string, Dictionary<string, HashSet<TilePoint>>>
            {
                {
                    "Abigail",
                    new Dictionary<string, HashSet<TilePoint>>
                    {
                        { "Town", new HashSet<TilePoint> { new TilePoint(1, 2), new TilePoint(3, 4) } }
                    }
                },
                {
                    "Pierre",
                    new Dictionary<string, HashSet<TilePoint>>
                    {
                        { "SeedShop", new HashSet<TilePoint> { new TilePoint(0, 0) } }
                    }
                }
            };

            AssertLocMapsEqual(paths, RouteSnapshot.ToSetPaths(RouteSnapshot.ToListPaths(paths)));

            // Тайминговые пути: NPC → время → локация → тайлы
            var timed = new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>
            {
                {
                    "Abigail",
                    new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>
                    {
                        { 900, new Dictionary<string, HashSet<TilePoint>>
                            { { "Town", new HashSet<TilePoint> { new TilePoint(1, 1) } } } }
                    }
                }
            };
            AssertTimedEqual(timed, RouteSnapshot.ToSetTimedPaths(RouteSnapshot.ToListTimedPaths(timed)));

            // Вариантные пути: NPC → вариант → время → локация → тайлы
            var variants = new Dictionary<string,
                Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>>
            {
                {
                    "Abigail",
                    new Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>
                    {
                        {
                            "rain",
                            new Dictionary<int, Dictionary<string, HashSet<TilePoint>>>
                            {
                                { 1400, new Dictionary<string, HashSet<TilePoint>>
                                    { { "Town", new HashSet<TilePoint> { new TilePoint(12, 30) } } } }
                            }
                        }
                    }
                }
            };
            AssertVariantEqual(
                variants,
                RouteSnapshot.ToSetVariantPaths(RouteSnapshot.ToListVariantPaths(variants)));
        }

        [Fact]
        public void RoundTrip_DuplicatesInList_CollapseToSet()
        {
            var src = new Dictionary<string, List<TilePoint>>
            {
                { "Town", new List<TilePoint>
                    { new TilePoint(1, 2), new TilePoint(1, 2), new TilePoint(3, 4) } }
            };

            var result = RouteSnapshot.ToSetMap(src);

            // Дубликат (1,2) схлопывается в множество.
            Assert.Equal(2, result["Town"].Count);
            Assert.Contains(new TilePoint(1, 2), result["Town"]);
            Assert.Contains(new TilePoint(3, 4), result["Town"]);
        }

        // ── Сериализация TilePoint (регрессия SerPoint) ──────────────────────────

        [Fact]
        public void TilePoint_JsonRoundTrip_PreservesCoordinates()
        {
            var original = new TilePoint(7, 9);

            string json = JsonConvert.SerializeObject(original);
            var restored = JsonConvert.DeserializeObject<TilePoint>(json);

            Assert.Equal(original, restored);
            // Newtonsoft должен сериализовать структуру напрямую, без обёрток.
            Assert.Contains("\"X\":7", json);
            Assert.Contains("\"Y\":9", json);
        }

        [Fact]
        public void TilePoint_HashSet_KeySemantics()
        {
            var set = new HashSet<TilePoint>
            {
                new TilePoint(3, 4),
                new TilePoint(3, 4),
                new TilePoint(4, 3)
            };

            Assert.Equal(2, set.Count);
        }

        // ── gzip-конверт: большие объёмы ─────────────────────────────────────────

        [Fact]
        public void PackUnpack_LargeSnapshot_RoundTripsAndCompresses()
        {
            // ~8 NPC × (дневные 3×200 + тайминговые 8×2×200 тайлов) ≈ 35 тыс. точек.
            var snapshot = LargeSnapshot(npcs: 8, timeSlots: 8, tiles: 200);
            string rawJson = JsonConvert.SerializeObject(snapshot);

            var envelope = RouteSyncEnvelope.Pack(snapshot);

            Assert.True(envelope.Data.Length > 0);
            // Сжатие должно быть существенным (тайлы сильно избыточны).
            Assert.True(
                envelope.Data.Length < rawJson.Length / 3,
                $"gzip: {envelope.Data.Length} байт, raw JSON: {rawJson.Length} байт");

            Assert.True(RouteSyncEnvelope.TryUnpack(envelope, out var restored));
            Assert.Equal(
                JsonConvert.SerializeObject(snapshot),
                JsonConvert.SerializeObject(restored));
        }

        [Fact]
        public void PackUnpack_EmptySnapshot_RoundTrips()
        {
            var snapshot = new RouteSnapshot();

            var envelope = RouteSyncEnvelope.Pack(snapshot);
            Assert.True(RouteSyncEnvelope.TryUnpack(envelope, out var restored));

            Assert.NotNull(restored);
            Assert.Equal(JsonConvert.SerializeObject(snapshot), JsonConvert.SerializeObject(restored));
        }

        [Fact]
        public void JsonRoundTrip_EmptyNestedCollections_Survive()
        {
            var snapshot = new RouteSnapshot
            {
                DayPaths = new Dictionary<string, Dictionary<string, List<TilePoint>>>
                {
                    { "Abigail", new Dictionary<string, List<TilePoint>>() }   // NPC без локаций
                },
                TimedDayPaths = new Dictionary<string, Dictionary<int, Dictionary<string, List<TilePoint>>>>
                {
                    {
                        "Pierre",
                        new Dictionary<int, Dictionary<string, List<TilePoint>>>
                        {
                            { 900, new Dictionary<string, List<TilePoint>>() } // слот без тайлов
                        }
                    }
                }
            };

            string json = JsonConvert.SerializeObject(snapshot);
            var restored = JsonConvert.DeserializeObject<RouteSnapshot>(json);

            Assert.Equal(JsonConvert.SerializeObject(snapshot), JsonConvert.SerializeObject(restored));
            Assert.Empty(restored.DayPaths["Abigail"]);
            Assert.Empty(restored.TimedDayPaths["Pierre"][900]);
        }

        // ── Вспомогательные ───────────────────────────────────────────────────────

        private static List<TilePoint> MakeTiles(int count, int offset)
        {
            var list = new List<TilePoint>(count);
            for (int i = 0; i < count; i++)
                list.Add(new TilePoint(i % 40, (i * 7 + offset) % 40));
            return list;
        }

        private static RouteSnapshot LargeSnapshot(int npcs, int timeSlots, int tiles)
        {
            var snapshot = new RouteSnapshot();

            for (int n = 0; n < npcs; n++)
            {
                string name = "NPC" + n;
                snapshot.TotalNpcList.Add(name);

                var locs = new Dictionary<string, List<TilePoint>>
                {
                    { "Town", MakeTiles(tiles, 0) },
                    { "Beach", MakeTiles(tiles, 1000) },
                    { "Mountain", MakeTiles(tiles, 2000) }
                };
                snapshot.DayPaths[name] = locs;
                snapshot.GlobalPaths[name] = locs;

                var timed = new Dictionary<int, Dictionary<string, List<TilePoint>>>();
                for (int t = 0; t < timeSlots; t++)
                {
                    timed[600 + t * 100] = new Dictionary<string, List<TilePoint>>
                    {
                        { "Town", MakeTiles(tiles, t) },
                        { "Beach", MakeTiles(tiles, t + 500) }
                    };
                }
                snapshot.TimedDayPaths[name] = timed;
            }

            return snapshot;
        }

        private static void AssertLocMapsEqual(
            Dictionary<string, Dictionary<string, HashSet<TilePoint>>> expected,
            Dictionary<string, Dictionary<string, HashSet<TilePoint>>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var npc in expected)
            {
                Assert.True(actual.ContainsKey(npc.Key), $"missing NPC {npc.Key}");
                AssertLocMapEqual(npc.Value, actual[npc.Key]);
            }
        }

        private static void AssertLocMapEqual(
            Dictionary<string, HashSet<TilePoint>> expected,
            Dictionary<string, HashSet<TilePoint>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var kvp in expected)
            {
                Assert.True(actual.ContainsKey(kvp.Key), $"missing location {kvp.Key}");
                Assert.True(actual[kvp.Key].SetEquals(kvp.Value),
                    $"tiles differ for location {kvp.Key}");
            }
        }

        private static void AssertTimedEqual(
            Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>> expected,
            Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var npc in expected)
            {
                Assert.True(actual.ContainsKey(npc.Key), $"missing NPC {npc.Key}");
                Assert.Equal(npc.Value.Count, actual[npc.Key].Count);
                foreach (var slot in npc.Value)
                {
                    Assert.True(actual[npc.Key].ContainsKey(slot.Key), $"missing time {slot.Key}");
                    AssertLocMapEqual(slot.Value, actual[npc.Key][slot.Key]);
                }
            }
        }

        private static void AssertVariantEqual(
            Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>> expected,
            Dictionary<string, Dictionary<string, Dictionary<int, Dictionary<string, HashSet<TilePoint>>>>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            foreach (var npc in expected)
            {
                Assert.True(actual.ContainsKey(npc.Key), $"missing NPC {npc.Key}");
                Assert.Equal(npc.Value.Count, actual[npc.Key].Count);
                foreach (var variant in npc.Value)
                {
                    Assert.True(actual[npc.Key].ContainsKey(variant.Key), $"missing variant {variant.Key}");
                    Assert.Equal(variant.Value.Count, actual[npc.Key][variant.Key].Count);
                    foreach (var slot in variant.Value)
                    {
                        Assert.True(
                            actual[npc.Key][variant.Key].ContainsKey(slot.Key),
                            $"missing time {slot.Key}");
                        AssertLocMapEqual(slot.Value, actual[npc.Key][variant.Key][slot.Key]);
                    }
                }
            }
        }
    }
}
