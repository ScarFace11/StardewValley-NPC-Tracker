# NpcTrackerMod

A C# SMAPI mod for Stardew Valley that visualizes NPC schedules, routes, and movement in real time.

## Stack

- **Language:** C# 7.3 (targeting net6.0)
- **Mod API:** SMAPI (Stardew Valley Modding API)
- **Rendering:** MonoGame (via SMAPI)
- **Testing:** xUnit

## Project Structure

```
NpcTrackerMod/         # Main mod source
  Core/                # Global state and shared services
  Tracking/            # NPC tracking logic
  Scheduling/          # Schedule parsing and route generation
  Rendering/           # Drawing only — no game logic
  UI/                  # Menus and user interaction
  ModEntry.cs          # SMAPI mod entry point

NpcTrackerMod.Tests/   # Unit tests (xUnit)
NpcTrackerMod.sln      # Solution file
```

## Usage on Replit

This project is used as a **code editor** — the mod runs inside Stardew Valley, not here.

To build locally or in your own environment, you need:
- .NET SDK
- SMAPI installed alongside Stardew Valley
- The `Pathoschild.Stardew.ModBuildConfig` NuGet package will resolve SMAPI references automatically

## Architecture Rules

See `REPLIT_AI_INSTRUCTIONS.md` for detailed coding conventions, SOLID principles, and module responsibilities.

## User Preferences

- Use Replit as a code editor; no run workflow needed.
