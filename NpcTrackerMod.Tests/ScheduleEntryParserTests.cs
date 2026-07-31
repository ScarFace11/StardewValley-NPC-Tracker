using NpcTrackerMod.Scheduling;
using Xunit;

namespace NpcTrackerMod.Tests
{
    public class ScheduleEntryParserTests
    {
        // ── IsValid ───────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("spring", "900 Town 5 10 2", true)]
        [InlineData("",       "900 Town 5 10 2", false)]   // пустой ключ
        [InlineData("spring", "NoSpace",          false)]  // нет пробела → не расписание
        [InlineData("spring", null,               false)]  // null rawData
        public void IsValid_VariousInputs(string key, string raw, bool expected)
            => Assert.Equal(expected, ScheduleEntryParser.IsValid(key, raw));

        // ── ShouldSkip ────────────────────────────────────────────────────────────

        [Theory]
        [InlineData("MAIL sent 900 Town 5 10",    true)]
        [InlineData("friendship 900 Town 5 10",   true)]
        [InlineData("GOTO spring",                true)]
        [InlineData("NO_SCHEDULE",                true)]
        [InlineData("900 Town 5 10 2",            false)]
        public void ShouldSkip_SpecialKeywords(string entry, bool expected)
            => Assert.Equal(expected, ScheduleEntryParser.ShouldSkip(entry));

        // ── Parse: базовые случаи ─────────────────────────────────────────────────

        [Fact]
        public void Parse_TimeLocationXY_CorrectlyParsed()
        {
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10" }, null,
                out var time, out var loc, out var x, out var y,
                out var facing, out var behavior, out var message);

            Assert.Equal("900",  time);
            Assert.Equal("Town", loc);
            Assert.Equal(5,      x);
            Assert.Equal(10,     y);
        }

        [Fact]
        public void Parse_FacingDirection_Parsed()
        {
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10", "3" }, null,
                out _, out _, out _, out _, out var facing, out _, out _);

            Assert.Equal(3, facing);
        }

        [Fact]
        public void Parse_MissingFacing_DefaultsSouth()
        {
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10" }, null,
                out _, out _, out _, out _, out var facing, out _, out _);

            Assert.Equal(2, facing); // 2 = south
        }

        [Fact]
        public void Parse_EndBehavior_Parsed()
        {
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10", "2", "square_8_frames" }, null,
                out _, out _, out _, out _, out _, out var behavior, out _);

            Assert.Equal("square_8_frames", behavior);
        }

        [Fact]
        public void Parse_EndMessage_StringsPrefix_GoesToMessage()
        {
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10", "2", "\"Strings\\Characters:Alex\"" }, null,
                out _, out _, out _, out _, out _, out var behavior, out var message);

            Assert.Null(behavior);
            Assert.Equal("\"Strings\\Characters:Alex\"", message);
        }

        [Fact]
        public void Parse_LocationFallback_UsesLastLocationName()
        {
            // Если первый токен — число (не время, нет пробела перед ним),
            // а второй токен — тоже число, то lokationName должна взяться из lastLocationName
            ScheduleEntryParser.Parse(
                new[] { "900", "5", "10" }, "FarmHouse",
                out _, out var loc, out _, out _, out _, out _, out _);

            Assert.Equal("FarmHouse", loc);
        }

        // ── Parse: граничные случаи ───────────────────────────────────────────────

        [Fact]
        public void Parse_EmptyParts_ReturnsDefaults()
        {
            ScheduleEntryParser.Parse(
                new string[0], null,
                out var time, out var loc, out var x, out var y,
                out var facing, out var behavior, out var message);

            Assert.Equal("0",          time);
            Assert.Equal(string.Empty, loc);
            Assert.Equal(0,            x);
            Assert.Equal(0,            y);
            Assert.Equal(2,            facing);
            Assert.Null(behavior);
            Assert.Null(message);
        }
    }
}
