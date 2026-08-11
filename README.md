<div align="center">

# 🌾 Stardew Valley NPC Tracker

### Advanced NPC Route Visualization Mod for Stardew Valley

![Platform](https://img.shields.io/badge/Game-Stardew%20Valley-8BC34A?style=for-the-badge)
![Language](https://img.shields.io/badge/C%23-7.3-239120?style=for-the-badge&logo=csharp)
![Framework](https://img.shields.io/badge/SMAPI-Latest-blue?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)

*A developer tool for visualizing NPC schedules, routes and movement in real time.*

</div>

---

# 📖 About

**Stardew Valley NPC Tracker** is a C# mod for **Stardew Valley** built with **SMAPI**.

The mod visualizes NPC schedules and movement directly in-game, making it easier to inspect AI behavior, debug schedules, and understand how NPCs navigate between locations.

Unlike simple minimap trackers, this project parses schedule data, processes NPC routes, and renders movement overlays dynamically while the game is running.

---

# ✨ Features

✅ Real-time NPC tracking

✅ Visual tile overlay

✅ Route rendering

✅ Schedule parsing

✅ Location mapping

✅ Automatic schedule loading

✅ Modular architecture

✅ Multiplayer route sync (host-authoritative snapshots)

✅ Cross-platform: Windows, Linux, macOS

✅ Tile inspector (mouse wheel)

✅ Unit tests for schedule parsing, snapshots and network envelopes

---

# 🛠 Tech Stack

| Technology | Purpose |
|------------|---------|
| C# 7.3 | Main programming language |
| SMAPI | Stardew Valley modding API |
| MonoGame | Rendering |
| .NET 6.0 | Runtime |
| Visual Studio | Development |
| xUnit / Unit Tests | Testing |

---

# 📂 Project Structure

```
NpcTrackerMod
│
├── Core
│   ├── ModState
│   ├── NpcPathStore
│   ├── TilePoint
│   ├── RouteSnapshot (+ RouteSnapshot.IO)
│   ├── LocalizationHelper
│   └── ScheduleDisplayHelper
│
├── Multiplayer
│   ├── RouteSync
│   └── RouteSyncEnvelope
│
├── Rendering
│   ├── TileRenderer
│   ├── RouteRenderer
│   └── TooltipRenderer
│
├── Scheduling
│   ├── ScheduleProcessor
│   ├── ScheduleEntryParser
│   ├── ScheduleVariantResolver
│   ├── LocationMapper
│   ├── JsonUtils
│   └── CustomScheduleLoader
│
├── Tracking
│   ├── NpcTracker
│   └── NpcRegistry
│
├── UI
│   ├── TrackingMenu (+ вкладки Main/Npc/Settings/Info)
│   ├── MenuComponents
│   └── TileInspectMenu
│
├── i18n (default.json, ru.json)
├── ModEntry.cs
└── ModConfig.cs
```

---

# ⚙ Architecture

The project follows a modular architecture where every subsystem has a dedicated responsibility.

```
               ModEntry
                   │
      ┌────────────┼────────────┐
      │            │            │
 Rendering     Scheduling    Tracking
      │            │            │
      └────────────┼────────────┘
                   │
               ModState
```

---

# 🚀 Installation

### Requirements

- Stardew Valley
- SMAPI (includes the required .NET runtime)

### Steps

1. Install SMAPI.
2. Download the latest release.
3. Copy the mod folder into:

```
Stardew Valley/
└── Mods/
```

4. Launch the game using SMAPI.

---

# 🎮 Functionality

The tracker performs several tasks during gameplay:

- Loads NPC schedules
- Parses route definitions
- Maps locations
- Tracks current NPC positions
- Draws movement paths
- Renders tile overlays
- Updates routes in real time

---

# 🧪 Testing

The solution includes a dedicated testing project.

Current tests cover:

- JSON utilities and schedule parsing
- Schedule entry validation
- Snapshot conversion (TilePoint, RouteSnapshot)
- GZip pack/unpack for multiplayer sync

---

# ⚙ CI & Releases

The repository uses GitHub Actions:

- **`ci.yml`** — builds and runs all tests on Windows, Linux and macOS on every push/PR, and validates the JSON files (manifest + i18n).
- **`release.yml`** — on a `v*` tag, runs the tests and creates a GitHub release with the release zip.

To publish a release:

1. Bump the version in `NpcTrackerMod/manifest.json` and update `CHANGELOG.md`.
2. Tag and push:

```bash
git tag v1.2.0
git push origin v1.2.0
```

The mod needs the game's DLLs to compile, so the release workflow downloads them via SteamCMD. To enable the automatic zip build, add the `STEAM_USERNAME` and `STEAM_PASSWORD` repository secrets (Settings → Secrets and variables → Actions). Without them the release is still created, and you attach the zip built locally (it's generated automatically in `NpcTrackerMod/bin/Release/net6.0/` on every build).

---

# 💡 Design Principles

- Separation of responsibilities
- Modular services
- Clean rendering pipeline
- Reusable schedule processing
- Easy extensibility
- Maintainable architecture

---

# 📈 Possible Future Improvements

- Minimap integration
- Interactive debugging tools
- Lazy pull-based multiplayer sync (instead of full daily snapshots)

---

# 📊 Repository Stats

```
Language:          C#
Architecture:      Modular
Game API:          SMAPI
Rendering:         MonoGame
Testing:           Unit Tests
Project Type:      Game Development / Tooling
```

---

# 📜 License

This project is licensed under the [MIT License](LICENSE).

---

# 🤝 Contributing

Suggestions and improvements are welcome.

Feel free to open an Issue or submit a Pull Request.

---

<div align="center">

### ⭐ If you found this project interesting, consider giving it a star!

Made with ❤️ using C#, SMAPI and Stardew Valley

</div>
