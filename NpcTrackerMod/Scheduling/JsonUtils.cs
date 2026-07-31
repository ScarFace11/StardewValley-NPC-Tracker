using System.Text.RegularExpressions;

namespace NpcTrackerMod.Scheduling
{
    /// <summary>
    /// Утилиты для обработки JSON-расписаний.
    /// Чистый статический класс — без зависимостей на XNA/SMAPI.
    /// </summary>
    public static class JsonUtils
    {
        // Negative lookbehind (?<!:) защищает "://" в URL-строках (http://, https://)
        // от ошибочного удаления как строчных комментариев.
        private static readonly Regex _singleLineComment =
            new Regex(@"(?<!:)//.*?(?=\r?$)", RegexOptions.Multiline | RegexOptions.Compiled);

        private static readonly Regex _multiLineComment =
            new Regex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Удаляет однострочные (// ...) и многострочные (/* ... */) комментарии из JSON.
        /// URL-строки вида "https://..." не затрагиваются.
        /// </summary>
        public static string RemoveComments(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;
            json = _singleLineComment.Replace(json, "");
            json = _multiLineComment.Replace(json, "");
            return json;
        }
    }
}
