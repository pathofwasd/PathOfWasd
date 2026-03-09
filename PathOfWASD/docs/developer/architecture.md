# Architecture

## Purpose

PathOfWASD is a Windows desktop application that adds a "virtual cursor" control mode on top of another game window. The app lets the player:

- lock into a WASD-driven cursor mode
- map skill keys to placeholder keys and remap them at runtime
- move the real cursor to a configured midpoint for directional actions
- display a second visible cursor and midpoint preview
- edit all of that behavior through a WPF settings window

The project is a WPF app for configuration and app lifecycle, but the active runtime cursor renderer is native Win32.

## Runtime Layers

### Startup and composition

Key files:

- `Startup/App.xaml.cs`
- `Startup/AppStartup.cs`

Responsibilities:

- enforce single-instance behavior
- enable per-monitor DPI awareness
- ensure the default cursor asset exists under `LocalAppData/PathOfWASD`
- register all services with dependency injection
- create the settings overlay and initialize the runtime service

`AppStartup.ConfigureServices()` is the composition root. If a runtime subsystem changes, this is where its lifetime and constructor graph usually need to be updated.

### Settings and UI

Key files:

- `Overlays/Settings/ViewModels/Settings.cs`
- `Overlays/Settings/ViewModels/SettingsViewModel.cs`
- `Overlays/Settings/Views/SettingsOverlay.xaml`
- `Overlays/Settings/Views/SettingsOverlay.xaml.cs`

Responsibilities:

- persist settings to JSON
- expose settings to WPF bindings
- track dirty state
- raise commands and events that the runtime service consumes

The settings window does not directly control the cursor or the controller pipeline. It raises events, and `OverlayService` translates those events into runtime state updates.

### Runtime orchestration

Key file:

- `Overlays/Settings/Services/OverlayService.cs`

Responsibilities:

- bridge settings UI events into runtime behavior
- enable and disable virtual-cursor mode
- synchronize midpoint and cursor-size changes into runtime objects
- coordinate cursor upload reloads
- own the lifetime of hotkeys, global hooks, and overlay windows

If a feature starts in the settings UI and ends in runtime behavior, `OverlayService` is usually the first file to inspect.

### Cursor runtime

Key files:

- `Managers/Cursor/CursorManager.cs`
- `Managers/Cursor/CursorState.cs`
- `Overlays/Cursor/NativeCursorOverlay.cs`
- `Overlays/Cursor/NativeCursorWindow.cs`
- `Overlays/Cursor/CenterCursor/CursorCenterOverlay.xaml.cs`

Responsibilities:

- store the logical virtual-cursor position
- update the real cursor when needed
- render the visible virtual cursor
- render the midpoint-preview cursor
- convert from logical WPF coordinates to physical screen pixels

The important current design rule is:

- `CursorManager` owns cursor math and movement flow
- the overlay layer only renders the position it is given

That split is what allowed the project to move away from the old WPF fullscreen overlay without rewriting the movement logic.

### Input and controller pipeline

Key files:

- `Overlays/Settings/Services/HotkeyController.cs`
- `Managers/Controller/ControllerManager.cs`
- `Managers/Controller/ControllerState.cs`
- `Managers/Controller/EventProcessor.cs`
- `Managers/Controller/KeyStateTracker.cs`

Responsibilities:

- listen for low-level keyboard and hotkey events
- map physical keys to placeholder keys
- decide when a key press is a skill, a directional skill, or raw WASD movement
- serialize controller actions through the channel-based event processor
- drive stand-still, movement, cursor locking, and cursor unlocking

This layer is the core of the gameplay-side behavior. If a skill fires at the wrong time, a WASD key gets stuck, or a mapped key injects out of order, the bug is usually in this layer.

## High-Level Flow

1. `App` starts and calls `AppStartup.Startup()`.
2. `AppStartup` builds the DI container and resolves `OverlayService`.
3. `OverlayService.Initialize()` shows the settings window and pushes current settings into runtime state.
4. `HotkeyController` registers the low-level keyboard hook after the settings window handle is available.
5. When the user enables virtual-cursor mode, `OverlayService` calls into `CursorManager`.
6. `CursorManager` starts or uses raw mouse input and forwards logical cursor positions into `ICursorOverlay`.
7. `NativeCursorOverlay` renders the second cursor in a small layered Win32 window.
8. `ControllerManager` and `ControllerState` process skill and movement transitions based on current held keys and remap state.

## Design Constraints

These constraints are important when changing the project:

- The cursor movement math must stay in `CursorManager`, not in renderer code.
- The native cursor window must stay non-activating and topmost, or Wine and game focus behavior regress.
- Midpoint data is treated as logical WPF coordinates and converted to physical pixels only at Win32 boundaries.
- `HotkeyController` and `ControllerManager` depend on strict ordering. Small timing changes can affect gameplay behavior.
- Significant runtime behavior changes should be reflected in this guide set and in `AGENTS.md`.
