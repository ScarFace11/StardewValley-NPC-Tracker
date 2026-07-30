# NpcTrackerMod

A Stardew Valley SMAPI mod written in C# that visualizes NPC schedules, movement, and routes in real time inside the game.

## Stack

- **Language:** C# 7.3
- **Framework:** .NET 6 (compiled as a library, loaded by SMAPI)
- **Game API:** SMAPI (Stardew Valley Modding API)
- **Rendering:** MonoGame
- **Testing:** xUnit

## Project structure

```
NpcTrackerMod/        Main mod source
  Core/               Global state and shared services
  Rendering/          Draw-only classes (TileRenderer, RouteRenderer)
  Scheduling/         Schedule parsing and route generation
  Tracking/           NPC tracking logic
  UI/                 Menus and user interaction
  ModEntry.cs         SMAPI entry point

NpcTrackerMod.Tests/  xUnit unit tests
NpcTrackerMod.sln     Solution file
```

## How to use on Replit

This project is used here as a **code editor and AI assistant** environment. The mod itself requires Stardew Valley + SMAPI to run and cannot be executed standalone.

To build the mod or run tests from the shell:

```bash
dotnet build NpcTrackerMod.sln
dotnet test NpcTrackerMod.Tests/NpcTrackerMod.Tests.csproj
```

## Architecture rules (see REPLIT_AI_INSTRUCTIONS.md for full details)

- Rendering code must NOT calculate routes
- Scheduling code must NOT draw anything
- Tracking code must NOT parse schedules
- UI must NOT contain business logic
- Global state lives only in `ModState`
- Prefer dependency injection; no singleton access patterns
- C# 7.3 only — do not use newer language features

## User preferences

- Preserve existing architecture, coding style, and XML comments
- Avoid unnecessary refactoring or changes to unrelated files
