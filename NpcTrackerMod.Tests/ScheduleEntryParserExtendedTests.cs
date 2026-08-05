using NpcTrackerMod.Scheduling;
using Xunit;

namespace NpcTrackerMod.Tests
{
    /// <summary>
    /// Расширенные тесты для ScheduleEntryParser, покрывающие пограничные и
    /// малоочевидные случаи, не охваченные в ScheduleEntryParserTests.
    /// </summary>
    public class ScheduleEntryParserExtendedTests
    {
        // ── IsValid: дополнительные случаи ───────────────────────────────────────

        [Theory]
        [InlineData(" ",        "900 Town 5 10", false)]  // ключ только из пробелов
        [InlineData("\t",       "900 Town 5 10", false)]  // ключ только из таба
        [InlineData("spring",   "",              false)]  // пустой rawData (нет пробела)
        [InlineData("spring",   "NoSpace",       false)]  // rawData без пробела
        public void IsValid_EdgeCases(string key, string raw, bool expected)
            => Assert.Equal(expected, ScheduleEntryParser.IsValid(key, raw));

        // " " содержит пробел, но с точки зрения игровой логики это валидная запись,
        // поэтому тест ниже документирует РЕАЛЬНОЕ поведение (не баг):
        [Fact]
        public void IsValid_RawDataWithOnlySpace_ReturnsTrueByDesign()
        {
            // rawData = " " содержит пробел → IsValid = true (парсер не проверяет смысл)
            Assert.True(ScheduleEntryParser.IsValid("spring", " "));
        }

        // ── ShouldSkip: регистр и вложения ───────────────────────────────────────

        [Theory]
        [InlineData("MAIL",              true)]   // только ключевое слово
        [InlineData("mail 900 Town 5",   false)]  // строчный "mail" — НЕ пропускается (регистрозависимо)
        [InlineData("GOTO",              true)]   // только GOTO
        [InlineData("friendship",        true)]   // полный кейворд
        [InlineData("FRIENDSHIP",        false)]  // верхний регистр — NOT пропускается
        [InlineData("NO_SCHEDULE",       true)]
        [InlineData("BEFOREMAIL 900",    true)]   // содержит "MAIL"
        [InlineData("",                  false)]  // пустая строка
        public void ShouldSkip_CaseSensitivityAndEmbedded(string entry, bool expected)
            => Assert.Equal(expected, ScheduleEntryParser.ShouldSkip(entry));

        // ── Parse: локация идёт первой (нет числа в начале) ──────────────────────

        [Fact]
        public void Parse_LocationFirst_NoTime_DefaultTimeZero()
        {
            // Первый токен — не число, значит это локация; time остаётся "0"
            ScheduleEntryParser.Parse(
                new[] { "Town", "5", "10" }, null,
                out var time, out var loc, out var x, out var y,
                out _, out _, out _);

            Assert.Equal("0",    time);
            Assert.Equal("Town", loc);
            Assert.Equal(5,      x);
            Assert.Equal(10,     y);
        }

        [Fact]
        public void Parse_LocationFirst_FacingIncluded()
        {
            ScheduleEntryParser.Parse(
                new[] { "BusStop", "12", "20", "1" }, null,
                out _, out var loc, out var x, out var y, out var facing, out _, out _);

            Assert.Equal("BusStop", loc);
            Assert.Equal(12,        x);
            Assert.Equal(20,        y);
            Assert.Equal(1,         facing);
        }

        // ── Parse: оба слота — behavior И message ────────────────────────────────

        [Fact]
        public void Parse_BehaviorThenMessage_BothCaptured()
        {
            // Слот 6 = анимация, слот 7 = строка диалога
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10", "2", "square_8_frames", "\"Strings\\Characters:Penny\"" },
                null,
                out _, out _, out _, out _, out _, out var behavior, out var message);

            Assert.Equal("square_8_frames",              behavior);
            Assert.Equal("\"Strings\\Characters:Penny\"", message);
        }

        [Fact]
        public void Parse_MessageWithoutBehavior_BehaviorNull()
        {
            // Слот 6 начинается с "Strings\" — значит это message, behavior = null
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10", "2", "\"Strings\\Characters:Alex\"" },
                null,
                out _, out _, out _, out _, out _, out var behavior, out var message);

            Assert.Null(behavior);
            Assert.Equal("\"Strings\\Characters:Alex\"", message);
        }

        // ── Parse: только одна часть (только время) ──────────────────────────────

        [Fact]
        public void Parse_OnlyTime_LocationAndCoordsDefault()
        {
            ScheduleEntryParser.Parse(
                new[] { "1200" }, null,
                out var time, out var loc, out var x, out var y,
                out var facing, out var behavior, out var message);

            Assert.Equal("1200",       time);
            Assert.Equal(string.Empty, loc);
            Assert.Equal(0,            x);
            Assert.Equal(0,            y);
            Assert.Equal(2,            facing);  // дефолт — south
            Assert.Null(behavior);
            Assert.Null(message);
        }

        // ── Parse: две части (время + X, без Y) — координаты не парсятся ─────────

        [Fact]
        public void Parse_TwoParts_NoCoordsExtracted()
        {
            // Недостаточно токенов для X+Y → x и y остаются 0
            ScheduleEntryParser.Parse(
                new[] { "900", "5" }, null,
                out var time, out _, out var x, out var y,
                out _, out _, out _);

            Assert.Equal("900", time);
            Assert.Equal(0,     x);
            Assert.Equal(0,     y);
        }

        // ── Parse: три части (время + X + Y без имени локации) ───────────────────

        [Fact]
        public void Parse_TimeAndTwoNumbers_LocationFromLast()
        {
            // "900 5 10" → время=900, location пуста после слота 1 (число).
            // Слот 2: parts[1]="5" → число → берём lastLocationName
            ScheduleEntryParser.Parse(
                new[] { "900", "5", "10" }, "FarmHouse",
                out var time, out var loc, out var x, out var y,
                out _, out _, out _);

            Assert.Equal("900",       time);
            Assert.Equal("FarmHouse", loc);
            Assert.Equal(5,           x);
            Assert.Equal(10,          y);
        }

        // ── Parse: lastLocationName = null и числовой второй токен ───────────────

        [Fact]
        public void Parse_NumericSecondToken_NullLastLocation_LocationEmpty()
        {
            // Если lastLocationName = null и второй токен — число,
            // locationName будет null (присвоение null из lastLocationName)
            ScheduleEntryParser.Parse(
                new[] { "900", "5", "10" }, null,
                out _, out var loc, out _, out _, out _, out _, out _);

            Assert.Null(loc);
        }

        // ── Parse: facing = 0 (север) задан явно ─────────────────────────────────

        [Fact]
        public void Parse_FacingNorth_Zero()
        {
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "5", "10", "0" }, null,
                out _, out _, out _, out _, out var facing, out _, out _);

            Assert.Equal(0, facing);
        }

        // ── Parse: facing = 1 (восток) ───────────────────────────────────────────

        [Fact]
        public void Parse_FacingEast_One()
        {
            ScheduleEntryParser.Parse(
                new[] { "1400", "Saloon", "30", "15", "1" }, null,
                out _, out _, out _, out _, out var facing, out _, out _);

            Assert.Equal(1, facing);
        }

        // ── Parse: отрицательные координаты ─────────────────────────────────────

        [Fact]
        public void Parse_NegativeCoordinates_ParsedCorrectly()
        {
            ScheduleEntryParser.Parse(
                new[] { "900", "Town", "-3", "-7" }, null,
                out _, out _, out var x, out var y, out _, out _, out _);

            Assert.Equal(-3, x);
            Assert.Equal(-7, y);
        }

        // ── Parse: нестандартно большое время ────────────────────────────────────

        [Fact]
        public void Parse_LargeTimeValue_Stored()
        {
            ScheduleEntryParser.Parse(
                new[] { "2600", "Town", "5", "10" }, null,
                out var time, out _, out _, out _, out _, out _, out _);

            Assert.Equal("2600", time);
        }

        // ── Parse: токен с ведущим нулём ─────────────────────────────────────────

        [Fact]
        public void Parse_LeadingZeroTime_StoredAsString()
        {
            // Время хранится как строка — ведущий ноль сохраняется
            ScheduleEntryParser.Parse(
                new[] { "0900", "Town", "5", "10" }, null,
                out var time, out _, out _, out _, out _, out _, out _);

            Assert.Equal("0900", time);
        }

        // ── Parse: name локации содержит цифры (не только буквы) ─────────────────

        [Fact]
        public void Parse_LocationNameWithDigits_ParsedAsLocation()
        {
            // "Area52" не начинается с цифры → воспринимается как имя локации
            ScheduleEntryParser.Parse(
                new[] { "900", "Area52", "3", "4" }, null,
                out _, out var loc, out _, out _, out _, out _, out _);

            Assert.Equal("Area52", loc);
        }
    }
}
