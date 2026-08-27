using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using NpcTrackerMod.Tracking;
using StardewModdingAPI;
using StardewValley;

namespace NpcTrackerMod.Scheduling
{
    /// <summary>
    /// Загружает расписания NPC из JSON-файлов других модов (Content Patcher формат).
    /// Отвечает только за I/O и парсинг — без игровой логики.
    /// </summary>
    public class CustomScheduleLoader
    {
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly ScheduleProcessor _processor;
        private readonly NpcRegistry _registry;

        /// <summary>
        /// Собранные расписания: имя NPC → ключ расписания → список строк маршрутов.
        /// </summary>
        private readonly Dictionary<string, Dictionary<string, List<string>>> _rawPaths
            = new Dictionary<string, Dictionary<string, List<string>>>();

        private string _currentNpcName;
        private string _currentModName;

        /// <summary> Имя NPC → название мода-источника (из manifest.json). </summary>
        public Dictionary<string, string> NpcModNames { get; } = new Dictionary<string, string>();

        public CustomScheduleLoader(
            IMonitor monitor,
            IModHelper helper,
            ScheduleProcessor processor,
            NpcRegistry registry)
        {
            _monitor = monitor;
            _helper = helper;
            _processor = processor;
            _registry = registry;
        }

        // ── Публичный API ────────────────────────────────────────────────────────

        /// <summary>
        /// Обходит папку Mods и загружает расписания из всех папок Schedules/*.json.
        /// Вызывается один раз при старте мода.
        /// </summary>
        public void LoadAll()
        {
            try
            {
                // DirectoryPath указывает на папку мода (…/Mods/NpcTrackerMod).
                // GetDirectoryName возвращает родительский каталог — саму папку Mods,
                // независимо от ОС и расположения игры.
                string modsRoot = Path.GetDirectoryName(_helper.DirectoryPath);

                foreach (var modFolder in Directory.GetDirectories(modsRoot))
                {
                    string schedulesFolder = FindSchedulesFolder(modFolder);
                    if (!string.IsNullOrEmpty(schedulesFolder))
                    {
                        _currentModName = GetModDisplayName(modFolder);
                        LoadFolder(schedulesFolder);
                    }
                }

                _monitor.Log("Все кастомные расписания загружены.", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка загрузки кастомных расписаний: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Передаёт загруженные расписания в ScheduleProcessor для построения глобальных маршрутов.
        /// Вызывается в первый DayStarted, когда GameNpcs уже известны.
        /// </summary>
        public void TransferToProcessor()
        {
            var knownNames = _registry.GameNpcs != null
                ? new HashSet<string>(_registry.GameNpcs.Select(n => n.Name), StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var npcEntry in _rawPaths)
            {
                string rawName = npcEntry.Key;

                // Имя в файле мода может отличаться регистром от игрового
                // ("abigail" вместо "Abigail"). Ищем каноничное написание
                // и дальше используем только его — иначе расписание молча
                // пропускалось бы, а источник мода не совпадал по ключу.
                string actualName = knownNames.FirstOrDefault(
                    n => string.Equals(n, rawName, StringComparison.OrdinalIgnoreCase));

                if (actualName == null)
                {
                    _monitor.Log(
                        $"Пропуск кастомного расписания: '{rawName}' не является активным NPC",
                        LogLevel.Debug);
                    continue;
                }

                // Источник мода тоже ключуем по игровому имени, чтобы вкладка NPC
                // находила его при отображении (TotalNpcList содержит игровые имена).
                if (NpcModNames.TryGetValue(rawName, out string modName) &&
                    !string.Equals(rawName, actualName, StringComparison.Ordinal))
                {
                    NpcModNames.Remove(rawName);
                    NpcModNames[actualName] = modName;
                }

                foreach (var scheduleEntry in npcEntry.Value)
                {
                    foreach (var path in scheduleEntry.Value)
                    {
                        _processor.BuildGlobalRoute(null, actualName, path, scheduleEntry.Key);
                        _processor.BuildTimedRoute(
                            FindNpc(actualName), scheduleEntry.Key, path);
                    }
                }
            }
        }

        // ── Загрузка файлов ───────────────────────────────────────────────────────

        private void LoadFolder(string folderPath)
        {
            foreach (var file in Directory.GetFiles(folderPath, "*.json", SearchOption.AllDirectories))
                LoadFile(file);
        }

        private void LoadFile(string filePath)
        {
            try
            {
                string json = JsonUtils.RemoveComments(File.ReadAllText(filePath));
                var root = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(json);

                if (root == null)
                {
                    _monitor.Log($"Файл пропущен (некорректный JSON): {filePath}", LogLevel.Warn);
                    return;
                }

                _currentNpcName = ExtractNpcName(root, filePath);

                if (root.TryGetValue("Changes", out var changesToken))
                    ProcessChanges(changesToken, filePath);
                else
                    AddScheduleEntries(root, filePath, 0);
            }
            catch (Exception ex)
            {
                _monitor.Log($"Ошибка загрузки файла {filePath}: {ex.Message}", LogLevel.Error);
            }
        }

        private void ProcessChanges(JToken changesToken, string filePath)
        {
            if (changesToken is not JArray changesArray)
            {
                _monitor.Log($"'Changes' не является массивом в {filePath}", LogLevel.Warn);
                return;
            }

            int patchIdx = 0;
            foreach (var change in changesArray)
            {
                patchIdx++;

                // Логируем When-условия: мод не может их вычислить,
                // поэтому берём расписание «как есть» (без условной фильтрации).
                // Это может привести к загрузке маршрута, который неактуален сегодня.
                if (change["When"] is JToken whenToken && whenToken.HasValues)
                {
                    _monitor.Log(
                        $"[CustomScheduleLoader] {filePath} патч #{patchIdx}: " +
                        $"обнаружено условие 'When' ({whenToken}). " +
                        $"Условие игнорируется — маршрут будет загружен безусловно.",
                        LogLevel.Debug);
                }

                if (change["Entries"] is not JToken entriesToken)
                {
                    _monitor.Log($"[CustomScheduleLoader] {filePath} патч #{patchIdx}: нет 'Entries' — пропуск.", LogLevel.Debug);
                    continue;
                }

                if (ContainsI18nTokens(entriesToken))
                {
                    _monitor.Log(
                        $"[CustomScheduleLoader] {filePath} патч #{patchIdx}: " +
                        $"расписание содержит i18n-токены — пропуск (не поддерживается).",
                        LogLevel.Debug);
                    continue;
                }

                if (entriesToken is JObject entriesObj)
                {
                    try
                    {
                        AddScheduleEntries(entriesObj, filePath, patchIdx);
                    }
                    catch (Exception ex)
                    {
                        _monitor.Log(
                            $"[CustomScheduleLoader] Ошибка разбора патча #{patchIdx} в {filePath}: {ex.Message}",
                            LogLevel.Error);
                    }
                }
                else
                {
                    _monitor.Log(
                        $"[CustomScheduleLoader] {filePath} патч #{patchIdx}: 'Entries' не является объектом.",
                        LogLevel.Warn);
                }
            }
        }

        private void AddScheduleEntries(JObject entries, string filePath = null, int patchIdx = 0)
        {
            if (string.IsNullOrEmpty(_currentNpcName)) return;

            if (!_rawPaths.TryGetValue(_currentNpcName, out var npcSchedule))
            {
                npcSchedule = new Dictionary<string, List<string>>();
                _rawPaths[_currentNpcName] = npcSchedule;
                _monitor.Log($"Добавлен кастомный NPC: {_currentNpcName}", LogLevel.Trace);
                if (!NpcModNames.ContainsKey(_currentNpcName))
                    NpcModNames[_currentNpcName] = _currentModName ?? "Unknown";
            }

            foreach (var entry in entries)
            {
                string rawValue = entry.Value?.ToString() ?? string.Empty;

                // FromFile — ссылка на внешний файл; мы не можем его разрешить.
                // Такие записи пропускаем с предупреждением.
                if (rawValue.TrimStart().StartsWith("{{FromFile:", StringComparison.OrdinalIgnoreCase) ||
                    rawValue.TrimStart().StartsWith("\"{{FromFile:", StringComparison.OrdinalIgnoreCase))
                {
                    _monitor.Log(
                        $"[CustomScheduleLoader] {filePath ?? "?"} патч #{patchIdx} ключ '{entry.Key}': " +
                        $"FromFile-ссылка не поддерживается — запись пропущена.",
                        LogLevel.Debug);
                    continue;
                }

                if (!npcSchedule.TryGetValue(entry.Key, out var list))
                {
                    list = new List<string>();
                    npcSchedule[entry.Key] = list;
                }
                list.Add(rawValue);
            }
        }

        // ── Извлечение имени NPC ──────────────────────────────────────────────────

        private string ExtractNpcName(JObject root, string filePath)
        {
            if (root.TryGetValue("Changes", out var changes) && changes is JArray arr)
            {
                foreach (var change in arr)
                {
                    string name = GetNpcNameFromTarget(change);
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            return GetNpcNameFromPath(filePath);
        }

        private static string GetNpcNameFromTarget(JToken change)
        {
            var target = change["Target"]?.ToString();
            return string.IsNullOrEmpty(target) ? null : target.Split('/').Last();
        }

        private static string GetNpcNameFromPath(string filePath)
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            return fileName.Equals("Schedule", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFileName(Path.GetDirectoryName(filePath))
                : fileName;
        }

        // ── Вспомогательные ───────────────────────────────────────────────────────

        private static string GetModDisplayName(string modFolder)
        {
            try
            {
                string manifestPath = Path.Combine(modFolder, "manifest.json");
                if (File.Exists(manifestPath))
                {
                    var manifest = Newtonsoft.Json.JsonConvert.DeserializeObject<JObject>(
                        JsonUtils.RemoveComments(File.ReadAllText(manifestPath)));
                    string name = manifest?["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch { /* fallback to folder name */ }
            return Path.GetFileName(modFolder);
        }

        private static string FindSchedulesFolder(string modRoot)
        {
            try
            {
                return Directory
                    .EnumerateDirectories(modRoot, "*", SearchOption.AllDirectories)
                    .FirstOrDefault(d =>
                        Path.GetFileName(d).Equals("Schedules", StringComparison.OrdinalIgnoreCase))
                    ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private NPC FindNpc(string name)
        {
            return _registry.GameNpcs?.FirstOrDefault(
                n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsI18nTokens(JToken token)
        {
            if (token is not JObject obj) return false;
            foreach (var prop in obj)
                if (prop.Value.ToString().Contains("{{i18n:")) return true;
            return false;
        }

    }
}