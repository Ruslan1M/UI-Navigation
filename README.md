# UI Navigation

![Unity](https://img.shields.io/badge/Unity-uGUI%20%2B%20Input%20System-222)
![Platforms](https://img.shields.io/badge/built%20for-Android%20TV%20%7C%20gamepad-b07cd8)
![License: MIT](https://img.shields.io/badge/license-MIT-3fb8af)

A lightweight replacement for uGUI's default navigation that makes **focus movement with a TV remote or gamepad predictable**. Built while porting mobile games to Android TV.

## Why

Default `Selectable` navigation happily moves focus to buttons that are off-screen, hidden behind a popup or inside a closed panel, and it loses focus entirely when the selected object is disabled. On a TV, where the remote is the only input, that means a stuck player.

## Features

- **Only reachable targets.** A `Selectable` counts as navigable only if a raycast at its screen centre actually hits it, so off-screen and covered buttons are skipped.
- **Explicit first, automatic second.** Explicit `Navigation` links are respected; otherwise the best candidate in the move direction is picked by angle and distance.
- **Wrap-around** for `Horizontal` / `Vertical` navigation with `wrapAround` enabled.
- **Auto-selection.** When nothing is selected, focus goes to the best element on screen. `BetterUINavigationElement` adds `AutoSelectPriority` and `IgnoreFromAutoSelection`.
- **Smooth scrolling.** Reaching the edge of a horizontal `ScrollRect` scrolls it smoothly instead of jumping.
- **Highlight presets.** `ScriptableObject` presets (*Create → UI → Highlight Preset*) draw focus as a fill overlay, a border sprite or an outline.
- **Profiled.** Raycasts and list rebuilds are wrapped in `ProfilerMarker`s, and lists are reused to avoid allocations.

## Setup

1. Requires the **Input System** package and an `EventSystem` with `InputSystemUIInputModule`.
2. Add `BetterUINavigationInput` to the `EventSystem` object.
3. Optional: add `BetterUINavigationElement` to buttons that should win auto-selection, and create a highlight preset.

## Code map

| File | Role |
|---|---|
| `BetterUINavigationInput.cs` | candidate search, reachability check, explicit / automatic navigation |
| `BetterUINavigationInput.Generated.cs` | input handling, auto-selection, smooth scroll |
| `BetterUINavigationElement.cs` | per-element priority and opt-out |
| `HighlightSystem/*` | focus highlight presets and renderer |
| `SelectableAccessor.cs` | non-allocating access to all active selectables |

## License

MIT
