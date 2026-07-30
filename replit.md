# NpcTrackerMod

A C# SMAPI mod for Stardew Valley that visualizes NPC schedules, routes, and movement in real time using tile overlays.

## Stack

| Layer | Technology |
|---|---|
| Language | C# 7.3 |
| Game API | SMAPI (Stardew Valley Modding API) |
| Rendering | MonoGame (via SMAPI) |
| Runtime | .NET 6 (net6.0) |
| Build tool | `dotnet` + `Pathoschild.Stardew.ModBuildConfig` |

## Project structure

```
NpcTrackerMod/
├── Core/           Global state (ModState) and shared services (NpcPathStore)
├── Rendering/      Draw-only classes — TileRenderer, RouteRenderer
├── Scheduling/     Schedule parsing, route generation, location mapping
├── Tracking/       NPC tracking logic — NpcTracker, NpcRegistry
├── UI/             Menus and user interaction — TrackingMenu, TileInspectMenu
├── ModEntry.cs     SMAPI mod entry point
├── ModConfig.cs    User-configurable mod settings
└── manifest.json   SMAPI mod manifest
```

## Working on Replit

**.NET 8 SDK is installed** — code editing and IntelliSense work in the editor.

**Compilation is not possible on Replit.** The build system (`Pathoschild.Stardew.ModBuildConfig`) must resolve SMAPI and game assemblies from a local Stardew Valley installation. There are no public NuGet packages for these DLLs. Build the mod locally:

```bash
# On your local machine (with Stardew Valley installed)
cd NpcTrackerMod
dotnet build
```

The output `.dll` goes in `%AppData%/StardewValley/Mods/NpcTrackerMod/` alongside `manifest.json` and the `i18n/` folder.

## Architecture rules (from REPLIT_AI_INSTRUCTIONS.md)

- **Rendering** classes only draw — no route calculations, no game state changes.
- **Scheduling** classes only parse — no rendering.
- **Tracking** classes only track — no schedule parsing.
- **UI** classes contain no business logic.
- Global state lives exclusively in `ModState`.
- Use dependency injection; never create singleton instances or `new` services inside random classes.
- Target **C# 7.3** — no `record`, `init`, `required`, file-scoped namespaces, or primary constructors.
- Every `Draw()` path must avoid allocations and LINQ.

## User preferences

- Follow the architecture rules and coding style defined in `REPLIT_AI_INSTRUCTIONS.md` for all code changes.
