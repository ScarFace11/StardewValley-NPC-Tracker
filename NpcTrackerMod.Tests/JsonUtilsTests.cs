using NpcTrackerMod.Scheduling;
using Xunit;

namespace NpcTrackerMod.Tests
{
    public class JsonUtilsTests
    {
        [Fact]
        public void RemoveComments_NoComments_ReturnsOriginal()
        {
            const string json = "{\"key\": \"value\"}";
            Assert.Equal(json, JsonUtils.RemoveComments(json));
        }

        [Fact]
        public void RemoveComments_SingleLine_Removed()
        {
            const string input    = "{ \"key\": \"value\" // this is a comment\n}";
            const string expected = "{ \"key\": \"value\" \n}";
            Assert.Equal(expected, JsonUtils.RemoveComments(input));
        }

        [Fact]
        public void RemoveComments_Multiline_Removed()
        {
            const string input    = "{ /* remove me */ \"key\": \"value\"}";
            const string expected = "{  \"key\": \"value\"}";
            Assert.Equal(expected, JsonUtils.RemoveComments(input));
        }

        [Fact]
        public void RemoveComments_BothTypes_BothRemoved()
        {
            const string input =
                "{\n" +
                "  // line comment\n" +
                "  \"key\": /* inline */ \"value\"\n" +
                "}";

            string result = JsonUtils.RemoveComments(input);

            Assert.DoesNotContain("// line comment", result);
            Assert.DoesNotContain("/* inline */",     result);
            Assert.Contains("\"key\"",                result);
            Assert.Contains("\"value\"",              result);
        }

        [Fact]
        public void RemoveComments_NullInput_ReturnsNull()
        {
            Assert.Null(JsonUtils.RemoveComments(null));
        }

        [Fact]
        public void RemoveComments_EmptyString_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, JsonUtils.RemoveComments(string.Empty));
        }
    }
}
