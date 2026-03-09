# Settings And Persistence

## Overview

The settings layer is responsible for:

- persisting JSON settings to `LocalAppData/PathOfWASD`
- exposing bindable WPF properties
- tracking unapplied changes
- raising runtime events without owning the runtime itself

Main files:

- `Overlays/Settings/ViewModels/Settings.cs`
- `Overlays/Settings/ViewModels/SettingsViewModel.cs`
- `Overlays/Settings/Views/SettingsOverlay.xaml`
- `Overlays/Settings/Views/SettingsOverlay.xaml.cs`

## Persistence

### `Settings`

`Settings` is both:

- the persisted settings model
- the `ISettingService` implementation

It stores:

- hotkey assignments
- cursor mode, midpoint, center offsets, and cursor size
- movement and stand-still keys
- sensitivity and timing offsets
- skill-binding lists for normal and directional entries
- settings window size

Storage details:

- folder: `LocalAppData/PathOfWASD`
- file: `settings.json`
- serialization: `System.Text.Json` with enum strings

`Load()` recreates defaults when the file is missing or invalid.

## View Model

### `SettingsViewModel`

`SettingsViewModel` adapts `Settings` for WPF binding and runtime event flow.

It owns:

- `ObservableProperty` values for all user-editable settings
- toggle-entry collections for skill bindings
- dirty-state tracking through `_lastAppliedSettings`
- commands used by the settings UI
- events that `OverlayService` consumes

Key responsibilities:

- rebuild `ToggleEntries` when loading a snapshot
- prevent top-level hotkey collisions by swapping duplicates
- expose midpoint display values
- raise runtime events for cursor upload, midpoint preview, overlay toggles, and save/apply

## Settings Window

### `SettingsOverlay`

The settings window is a WPF shell around the view model. Its code-behind is intentionally thin:

- create tray behavior
- size and position the window
- close to tray instead of always shutting down the app
- connect the view model to the overlay service lifecycle

Most behavior should stay in the view model or runtime service instead of code-behind.

## Save And Apply Model

The app distinguishes between:

- apply
- save

Apply:

- rebuilds a `Settings` snapshot from current view model state
- pushes that state into runtime consumers
- does not persist it to disk

Save:

- rebuilds the same snapshot
- persists it to disk
- updates the "last applied" snapshot
- notifies runtime consumers

That distinction matters when debugging "works now but comes back wrong after restart" issues.

## Toggle Entries

The skill-binding rows are stored as `ToggleKeyEntry` items.

They are split into two output lists when the view model builds a `Settings` snapshot:

- non-directional skill bindings
- directional skill bindings

`Helper.GetFKeyMaps(...)` and `Helper.GetDirectionalFKeyMaps(...)` convert those rows into placeholder-key sets that the runtime uses.

## Cursor Asset Upload

The settings UI does not copy the cursor file itself.

The flow is:

1. the view model raises `VirtualCursorChanged`
2. `OverlayService.UploadCursor()` prompts for a PNG
3. `CursorImageLoader.SaveFromFile(...)` persists the PNG
4. both cursor renderers reload the shared file

## Change Rules

Update this guide when:

- a setting is added or removed
- apply/save semantics change
- the settings file structure changes
- midpoint or cursor-upload behavior changes
- toggle-entry classification changes
