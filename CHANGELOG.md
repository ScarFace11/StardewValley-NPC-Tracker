# Changelog

All notable changes to this mod are documented in this file.

## [1.2.0] — unreleased

### Added
- Multiplayer route sync: the host builds routes once and sends a compressed snapshot to farmhands; farmhands no longer compute their own (potentially wrong) routes.
- Tile inspector redesigned with Stardew-style panels, cleaner card layout and text truncation.
- Russian localization (`ru.json`) alongside the English default.
- Cross-platform support verified for Windows, Linux and macOS (UTF-8 sources, no platform-specific APIs).

### Changed
- GitHub Actions CI: builds and runs tests on Windows/Linux/macOS; automatic release zip on version tags.
- `TilePoint` replaces XNA `Point` in the data layer; XNA conversion happens only at the rendering boundary.
- Route snapshots are GZip-compressed and cached per day on the host.
- Snapshots are applied atomically and only accepted from the host player; schema version mismatch falls back to a local build.
- NPC selection is preserved when a snapshot arrives mid-day.
- Schedule key "bed" handling now uses the `DefaultMap` property.

### Removed
- Dead blacklist API (`Blacklist`/`Unblacklist`/`ApplyBlacklist`).
- Unused `System.ValueTuple` package reference.
- Stale `app.config` (.NET Framework artifact).

## [1.1.0] — 2026-07-21

### Added
- NPC tab: browse the full list of vanilla and modded NPCs, track each individually.
- Settings tab: customizable key bindings and a time filter for hourly route tracking.
- Info tab: location details and real-time statistics for tracked NPCs.
- Hover tooltips on route tiles and a wheel-click tile inspector.
- Modded NPC schedule support.

### Changed
- Migrated to .NET 6.0 for compatibility with game 1.6.x.

## [1.0.0] — 2026-07-10

Initial public release: tile grid overlay, NPC position highlight, movement path visualization, destinations, in-game toggle.
