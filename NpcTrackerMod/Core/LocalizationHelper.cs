using StardewModdingAPI;

namespace NpcTrackerMod.Core
{
    /// <summary>
    /// Централизованный хелпер локализации.
    /// Возвращает перевод по ключу или сам ключ при отсутствии i18n / перевода.
    /// </summary>
    public static class LocalizationHelper
    {
        /// <summary>
        /// Возвращает перевод по ключу с необязательными токенами.
        /// Если i18n == null или ключ не найден — возвращает ключ как есть (не бросает исключений).
        /// </summary>
        public static string Get(ITranslationHelper i18n, string key, object tokens = null)
        {
            if (i18n == null) return key;
            var t = tokens != null ? i18n.Get(key, tokens) : i18n.Get(key);
            return t.HasValue() ? t.ToString() : key;
        }
    }
}
