# PathOfWASD

PathOfWASD is an external Path of Exile 1 utility that lets you move with `WASD` while keeping a separate virtual cursor for aiming, skill placement, and menu interaction.

At a high level, the app keeps track of raw mouse movement in the background, renders a visible virtual cursor, and moves the real cursor only when it needs to hand control back to the game for a skill or interaction.

## Compatibility

- Primary platform: Windows
- Path of Exile version: users have reported this works with the Path of Exile 1 `Mirage` update. `Mirage` launched on March 6, 2026, and is the current live Path of Exile 1 expansion as of March 9, 2026.
- Linux support: community-reported through Wine/Proton. The maintainer has not personally verified this setup.

Official Path of Exile news: <https://www.pathofexile.com/news>

## Basic Setup

1. Start `PathOfWASD.exe`.
2. Open the settings window and configure your hotkeys, cursor settings, midpoint, and skill mappings.
3. Launch Path of Exile.
4. Enable WASD mode with your configured key.

Most day-to-day setup happens in the settings window. If you change cursor art, midpoint placement, or skill mappings, apply those changes before jumping back into the game.

## Linux / Wine / Steam

This workflow was provided by a user. It is included here because they reported success, but it is still untested by the maintainer.

1. Use `protontricks` to open `winecfg` for the Path of Exile Steam prefix.
2. In the `Graphics` tab, enable `Emulate a virtual desktop`.
3. Set the virtual desktop resolution to the resolution you want to use for Path of Exile.
4. Launch Path of Exile through Steam.
5. Use `protontricks` to launch `PathOfWASD` inside the same prefix.
6. Configure PathOfWASD normally and use the same in-app setup flow as on Windows.

Notes:

- The reporting user was on a `32:9` display and found this especially helpful there.
- If you play on a standard monitor, the virtual desktop step may or may not be necessary.
- The reporting user still believed the virtual desktop was important for getting the PathOfWASD overlay to appear correctly over Path of Exile under Wine.

## Technical Docs

For build, debug, publish, and codebase documentation:

- Technical README: [docs/technical-readme.md](docs/technical-readme.md)
- Developer guide: [docs/developer/README.md](docs/developer/README.md)
