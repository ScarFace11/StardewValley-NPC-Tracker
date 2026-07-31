using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NpcTrackerMod.Core
{
    /// <summary>
    /// Единственное место хранения глобального состояния мода.
    /// Передаётся через конструкторы — никакого статического синглтона.
    /// </summary>
    public class ModState
    {
        /// <summary> Отображать пути NPC на экране. </summary>
        public bool EnableDisplay { get; set; }

        /// <summary> Отображать тайловую сетку. </summary>
        public bool DisplayGrid { get; set; }

        /// <summary> Варп-карта локаций уже заполнена. </summary>
        public bool LocationSet { get; set; }

        /// <summary> Список NPC текущей локации был сформирован для режима «один NPC». </summary>
        public bool SwitchListFull { get; set; }

        /// <summary> Режим «один NPC» (вместо всех). </summary>
        public bool SwitchTargetNPC { get; set; }

        /// <summary> Требуется перерисовать пути (сбрасывается после отрисовки). </summary>
        public bool SwitchGetNpcPath { get; set; } = true;

        /// <summary> Показывать глобальный маршрут (по всему расписанию) вместо дневного. </summary>
        public bool SwitchGlobalNpcPath { get; set; }

        /// <summary> Показывать пути NPC во всех локациях, а не только в текущей. </summary>
        public bool SwitchTargetLocations { get; set; }

        /// <summary> Количество NPC в текущей локации (для отслеживания изменений). </summary>
        public int NpcCount { get; set; }

        /// <summary> Индекс выбранного NPC в CurrentNpcList. </summary>
        public int NpcSelected { get; set; }

        /// <summary>
        /// Фильтр по времени дня. -1 = полный дневной маршрут.
        /// Иначе — только отрезки до указанного игрового времени (например, 900, 1200).
        /// </summary>
        public int TimeFilter { get; set; } = -1;

        /// <summary> Предыдущие позиции NPC по имени (для восстановления цвета тайла при движении). </summary>
        public Dictionary<string, Point> NpcPreviousPositions { get; } = new Dictionary<string, Point>();

        // ── Пошаговый режим ───────────────────────────────────────────────────────

        /// <summary>
        /// Пошаговый режим просмотра дневного маршрута.
        /// Каждый шаг — один временной слот из TimedDayPaths.
        /// </summary>
        public bool RouteStepMode { get; set; }

        /// <summary>
        /// Индекс текущего шага (0-based) в отсортированном списке ключей TimedDayPaths.
        /// </summary>
        public int RouteStepIndex { get; set; }

        /// <summary>
        /// Общее число шагов для текущего NPC. Обновляется RouteRenderer при отрисовке.
        /// Читается меню для отображения «шаг X / N».
        /// </summary>
        public int RouteStepTotal { get; set; }

        /// <summary>
        /// Игровое время текущего отображаемого шага. Обновляется RouteRenderer при отрисовке.
        /// Читается меню для отображения метки времени в навигаторе.
        /// </summary>
        public int RouteStepTime { get; set; }

        /// <summary>
        /// Ключ активного расписания текущего NPC в пошаговом режиме
        /// (например, "spring_Mon", "marriage", "rain").
        /// Обновляется RouteRenderer при отрисовке. Null вне пошагового режима.
        /// </summary>
        public string RouteStepScheduleKey { get; set; }

        // ── Выбор варианта расписания ──────────────────────────────────────────────

        /// <summary>
        /// Ключ варианта расписания, выбранного пользователем для отображения
        /// (например, "spring_Mon", "marriage", "rain").
        /// Null означает использование активного дневного расписания (TimedDayPaths).
        /// </summary>
        public string SelectedVariantKey { get; set; }

        /// <summary>
        /// Флаг-запрос на построение тайминговых путей для SelectedVariantKey.
        /// Устанавливается UI при выборе варианта. Обрабатывается и сбрасывается в ModEntry.
        /// </summary>
        public bool SwitchBuildVariant { get; set; }
    }
}
