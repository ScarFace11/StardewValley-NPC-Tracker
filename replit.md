# NpcTrackerMod

A C# mod for **Stardew Valley** built with SMAPI. It visualizes NPC schedules, routes, and movement as an in-game overlay.

## Project Structure

```
NpcTrackerMod/          Main mod — C# library loaded by SMAPI at runtime
  Core/                 Global state (ModState) and shared data stores
  Scheduling/           Schedule parsing, route generation, custom schedule loading
  Tracking/             NPC registration and tracking logic
  Rendering/            Tile and route drawing (no game logic)
  UI/                   In-game menus and components
  ModEntry.cs           SMAPI entry point

NpcTrackerMod.Tests/    Unit tests (xUnit) — pure C#, no game dependencies
```

## Tech Stack

| | |
|---|---|
| Language | C# 7.3 |
| Runtime | .NET 6 (mod), .NET Framework 4.8 (tests) |
| Game API | SMAPI (Stardew Valley Modding API) |
| Rendering | MonoGame / XNA |
| Tests | xUnit |

## Running on Replit

### Build & run tests

Use the **Build & Test** workflow (or run from the shell):

```bash
DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet test NpcTrackerMod.Tests/NpcTrackerMod.Tests.csproj --logger 'console;verbosity=normal'
```

The test project compiles and runs fully on Replit — it only covers pure C# logic (schedule parsing, JSON utilities) with no SMAPI or game dependencies.

### Why the main mod can't build here

`NpcTrackerMod.csproj` depends on `Pathoschild.Stardew.ModBuildConfig`, which resolves SMAPI and Stardew Valley assemblies from a local game installation. Those binaries aren't present on Replit, so the main project will fail to restore/build. This is expected and normal for SMAPI mods.

**To build the full mod**, use Visual Studio or `dotnet build` on a machine with Stardew Valley + SMAPI installed (the game path is auto-detected by ModBuildConfig).

## Coding Conventions

See `REPLIT_AI_INSTRUCTIONS.md` for the full ruleset. Key points:

- **C# 7.3** only — no records, init, primary constructors, file-scoped namespaces, etc.
- Strict module separation: Rendering never calculates routes; Scheduling never draws; UI has no business logic
- Dependency injection everywhere — no singletons or `new X()` inside unrelated classes
- Global state lives exclusively in `ModState`
- All public classes and methods must have XML documentation
- Avoid allocations inside `Draw()` (runs 60× per second)
- Log errors through SMAPI `Monitor`, never with empty catch blocks

## User Preferences

- Keep the existing architecture — extend services, don't create new manager/god classes
- Optimize for maintainability over brevity
