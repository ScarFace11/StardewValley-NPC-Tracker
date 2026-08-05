using NpcTrackerMod.Scheduling;
using Xunit;

namespace NpcTrackerMod.Tests
{
    /// <summary>
    /// Расширенные тесты для JsonUtils, покрывающие пограничные случаи
    /// и документирующие известные ограничения регулярных выражений.
    /// </summary>
    public class JsonUtilsExtendedTests
    {
        // ── Только пробелы / переносы ─────────────────────────────────────────────

        [Fact]
        public void RemoveComments_WhitespaceOnly_ReturnedUnchanged()
        {
            const string input = "   \t  \n  ";
            Assert.Equal(input, JsonUtils.RemoveComments(input));
        }

        // ── Комментарий в самом начале строки ────────────────────────────────────

        [Fact]
        public void RemoveComments_CommentAtStartOfLine_Removed()
        {
            const string input    = "// leading comment\n{\"key\": \"value\"}";
            string result = JsonUtils.RemoveComments(input);

            Assert.DoesNotContain("// leading comment", result);
            Assert.Contains("\"key\"",                  result);
        }

        // ── Несколько строчных комментариев ──────────────────────────────────────

        [Fact]
        public void RemoveComments_MultipleLineComments_AllRemoved()
        {
            const string input =
                "{\n" +
                "  \"a\": 1, // first\n" +
                "  \"b\": 2  // second\n" +
                "}";

            string result = JsonUtils.RemoveComments(input);

            Assert.DoesNotContain("// first",  result);
            Assert.DoesNotContain("// second", result);
            Assert.Contains("\"a\"",           result);
            Assert.Contains("\"b\"",           result);
        }

        // ── Многострочный комментарий на нескольких строках ───────────────────────

        [Fact]
        public void RemoveComments_MultiLineCommentSpanningLines_Removed()
        {
            const string input =
                "{\n" +
                "  /* line one\n" +
                "     line two */\n" +
                "  \"key\": \"value\"\n" +
                "}";

            string result = JsonUtils.RemoveComments(input);

            Assert.DoesNotContain("line one",  result);
            Assert.DoesNotContain("line two",  result);
            Assert.Contains("\"key\"",         result);
        }

        // ── Документация ограничения: // внутри /* */ ────────────────────────────

        /// <summary>
        /// Известное ограничение: однострочный regex применяется РАНЬШЕ многострочного.
        /// Если внутри /* ... */ встречается //, однострочный regex сначала удаляет
        /// "// inside */ "key": "value" }" (до конца строки), а затем многострочный
        /// не находит закрывающего */, поэтому "/* contains" остаётся в выводе.
        /// Тест документирует фактическое поведение как известное ограничение.
        /// </summary>
        [Fact]
        public void RemoveComments_SingleLineInsideMultiLine_KnownLimitation()
        {
            // Порядок применения regex: сначала // , потом /* */
            // → // inside */ "key": "value" } → удаляется однострочным regex
            // → оставшийся "{ /* contains " не имеет закрывающего */ → не удаляется
            const string input = "{ /* contains // inside */ \"key\": \"value\" }";
            string result = JsonUtils.RemoveComments(input);

            // "key" и "value" удаляются вместе с // до конца строки
            Assert.DoesNotContain("\"key\"",   result);
            // "contains" остаётся как часть незакрытого /* ... */
            Assert.Contains("contains", result);
        }

        // ── Несколько блочных комментариев в одной строке ────────────────────────

        [Fact]
        public void RemoveComments_TwoBlockCommentsOnOneLine_BothRemoved()
        {
            const string input    = "{ /* A */ \"x\": /* B */ 1 }";
            const string expected = "{  \"x\":  1 }";
            Assert.Equal(expected, JsonUtils.RemoveComments(input));
        }

        // ── Пустой блочный комментарий /**/ ──────────────────────────────────────

        [Fact]
        public void RemoveComments_EmptyBlockComment_Removed()
        {
            const string input    = "{/**/ \"key\": \"value\"}";
            const string expected = "{ \"key\": \"value\"}";
            Assert.Equal(expected, JsonUtils.RemoveComments(input));
        }

        // ── Строчный комментарий в конце файла без переноса строки ───────────────

        [Fact]
        public void RemoveComments_TrailingLineComment_NoNewline_Removed()
        {
            const string input    = "{\"key\": 1} // trailing";
            string result = JsonUtils.RemoveComments(input);

            Assert.DoesNotContain("trailing", result);
            Assert.Contains("\"key\"",        result);
        }

        // ── CRLF-переносы строк ───────────────────────────────────────────────────

        [Fact]
        public void RemoveComments_CrLfLineEndings_CommentRemoved()
        {
            const string input  = "{\r\n  \"key\": 1 // comment\r\n}";
            string result = JsonUtils.RemoveComments(input);

            Assert.DoesNotContain("// comment", result);
            Assert.Contains("\"key\"",          result);
        }

        // ── Документация ограничения: // внутри строкового значения ──────────────

        /// <summary>
        /// Известное ограничение: JsonUtils.RemoveComments не распознаёт контекст
        /// JSON-строк. Последовательность "//" внутри строкового значения
        /// (не предшествуемая ":") будет удалена вместе с хвостом значения.
        /// Тест документирует фактическое поведение, а не ожидаемый идеал.
        /// </summary>
        [Fact]
        public void RemoveComments_DoubleSlashInsideStringValue_IsStripped_KnownLimitation()
        {
            // "note": "fix // later" → "//" не предшествует ":", поэтому regex удаляет
            // " // later" из значения строки.
            const string input = "{ \"note\": \"fix // later\" }";
            string result = JsonUtils.RemoveComments(input);

            // "// later" удаляется вместе с хвостом строки-значения
            Assert.DoesNotContain("// later", result);
        }

        // ── URL с двойным слешем после двоеточия сохраняется ─────────────────────

        [Fact]
        public void RemoveComments_HttpsUrl_NotStripped()
        {
            const string input = "{ \"url\": \"https://example.com\" }";
            string result = JsonUtils.RemoveComments(input);
            Assert.Contains("https://example.com", result);
        }

        [Fact]
        public void RemoveComments_HttpUrl_NotStripped()
        {
            const string input = "{ \"endpoint\": \"http://localhost:8080/api\" }";
            string result = JsonUtils.RemoveComments(input);
            Assert.Contains("http://localhost:8080/api", result);
        }

        // ── Файл из одних комментариев ────────────────────────────────────────────

        [Fact]
        public void RemoveComments_OnlyComments_ReturnsEmptyishString()
        {
            const string input = "// line one\n// line two\n";
            string result = JsonUtils.RemoveComments(input);

            Assert.DoesNotContain("line one", result);
            Assert.DoesNotContain("line two", result);
        }

        // ── Корректный JSON остаётся разбираемым после удаления комментариев ──────

        [Fact]
        public void RemoveComments_ValidJsonWithComments_CanBeDeserialized()
        {
            const string input =
                "{\n" +
                "  // schedule comment\n" +
                "  \"spring\": \"900 Town 5 10 /* inline */ 2\"\n" +
                "}";

            string cleaned = JsonUtils.RemoveComments(input);

            // Не выбрасывает исключение при парсинге
            var parsed = Newtonsoft.Json.JsonConvert.DeserializeObject
                <System.Collections.Generic.Dictionary<string, string>>(cleaned);

            Assert.NotNull(parsed);
            Assert.True(parsed.ContainsKey("spring"));
        }
    }
}
