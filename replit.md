# Stardew Valley NPC Tracker Mod

A C# SMAPI mod for Stardew Valley that visualizes NPC schedules, routes, and real-time movement as in-game tile overlays.

## Stack

- **Language:** C# (net6.0)
- **Game API:** SMAPI (Stardew Valley Modding API)
- **Rendering:** MonoGame (via SMAPI)
- **Testing:** xUnit
- **Build:** .NET SDK 8 (dotnet CLI)

## Project Structure

```
NpcTrackerMod/          # Main mod source
  Core/                 # ModState, NpcPathStore, helpers
  Rendering/            # TileRenderer, RouteRenderer
  Scheduling/           # Schedule parsing & processing pipeline
  Tracking/             # NpcTracker, NpcRegistry
  UI/                   # TrackingMenu and components
  ModEntry.cs           # SMAPI mod entry point
  ModConfig.cs          # User-facing config options

NpcTrackerMod.Tests/    # xUnit unit tests (schedule parsing)
NpcTrackerMod.sln       # Solution file
```

## Building

> **Note:** The mod depends on SMAPI and MonoGame game libraries that are only available when Stardew Valley is installed locally. Building on Replit will restore NuGet packages but cannot produce a runnable `.dll` without the game files.

```bash
# Restore packages
dotnet restore

# Build (will fail on SMAPI/game references without game files)
dotnet build NpcTrackerMod/NpcTrackerMod.csproj

# Run unit tests (no game files needed)
dotnet test NpcTrackerMod.Tests/NpcTrackerMod.Tests.csproj
```

## Deploying the Mod

1. Build locally with Stardew Valley installed (SMAPI auto-deploys on build via `Pathoschild.Stardew.ModBuildConfig`).
2. Or manually copy the output `.dll` and `manifest.json` into `Stardew Valley/Mods/NpcTrackerMod/`.

## User Preferences
