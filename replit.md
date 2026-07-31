# Stardew Valley NPC Tracker Mod

A C# SMAPI mod for Stardew Valley that visualizes NPC schedules, routes, and movement in real time via tile overlays and route rendering.

## Stack

- **Language**: C# (net6.0)
- **Game API**: SMAPI (Stardew Valley Modding API)
- **Rendering**: MonoGame
- **Testing**: xUnit
- **Key packages**: Newtonsoft.Json, Pathoschild.Stardew.ModBuildConfig

## Project structure

```
NpcTrackerMod/          Main mod project
  Core/                 ModState, NpcPathStore
  Rendering/            TileRenderer, RouteRenderer
  Scheduling/           ScheduleProcessor, ScheduleEntryParser, LocationMapper, JsonUtils, CustomScheduleLoader
  Tracking/             NpcTracker, NpcRegistry
  UI/                   TrackingMenu, MenuComponents, TileInspectMenu
  ModEntry.cs           Mod entry point
  ModConfig.cs          Config model
  manifest.json         SMAPI manifest
  i18n/                 Localisation strings (default, ru)

NpcTrackerMod.Tests/    xUnit test project
  JsonUtilsTests.cs
  ScheduleEntryParserTests.cs
```

## How to build & test

This mod cannot run in-game on Replit (requires Stardew Valley + SMAPI locally). However you can build and run unit tests:

```bash
dotnet build NpcTrackerMod.sln
dotnet test NpcTrackerMod.Tests/NpcTrackerMod.Tests.csproj
```

Note: the main mod project references SMAPI/MonoGame assemblies via `Pathoschild.Stardew.ModBuildConfig`, which expects a local game installation. Build may require stubs or a local game path configured.

## User preferences
