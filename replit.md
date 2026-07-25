# Stardew Valley NPC Tracker Mod

A C# SMAPI mod for Stardew Valley that visualizes NPC schedules, routes, and real-time movement as colored tile overlays on the game world.

## Stack

| Layer | Technology |
|-------|-----------|
| Language | C# 7.3+ / .NET 6 |
| Game API | SMAPI 4.0+ |
| Rendering | MonoGame / SpriteBatch |
| JSON | Newtonsoft.Json |
| Tests | xUnit |

## Project structure

```
NpcTrackerMod/
  Core/          — ModState, NpcPathStore (state + data)
  Scheduling/    — ScheduleProcessor, ScheduleEntryParser, ScheduleVariantResolver,
                   CustomScheduleLoader, LocationMapper, JsonUtils
  Tracking/      — NpcTracker, NpcRegistry
  Rendering/     — TileRenderer, RouteRenderer
  UI/            — TrackingMenu, TileInspectMenu, MenuComponents
  i18n/          — default.json (English), ru.json (Russian)
ModEntry.cs      — SMAPI entry point
ModConfig.cs     — User config (keybinds, colors, alpha)
```

## Running / building

This mod cannot run standalone — it requires Stardew Valley + SMAPI installed locally.

To compile:
```bash
dotnet build NpcTrackerMod/NpcTrackerMod.csproj
```

To run tests:
```bash
dotnet test NpcTrackerMod.Tests/NpcTrackerMod.Tests.csproj
```

Output `.dll` goes into the game's `Mods/NpcTrackerMod/` folder alongside `manifest.json`.

## Key design decisions

- **ScheduleVariantResolver** selects the single active schedule key per NPC per day (respecting marriage, weather, season, friendship hearts), rather than building a union of all possible routes.
- **CustomScheduleLoader** reads Content Patcher JSON from other mods' `Schedules/` folders. It logs but does not crash on `When`-conditions and skips `FromFile` references (unsupported).
- **i18n** uses SMAPI's built-in translation system (`i18n/default.json` = English, `i18n/ru.json` = Russian). Pass `Helper.Translation` into `TrackingMenu`.
- Colors and opacity are configurable live from the in-game Settings tab (no config.json editing needed).

## User preferences

- Keep all comments and log messages in Russian (matching the original author's style).
- Do not restructure existing class hierarchy unless explicitly requested.
