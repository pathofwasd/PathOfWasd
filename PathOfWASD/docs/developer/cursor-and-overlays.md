# Cursor And Overlays

## Overview

The project renders two cursor-related visuals:

- the main virtual cursor used during virtual-cursor mode
- the midpoint preview cursor used while configuring cursor centering

Both are rendered through small native layered windows, not through a fullscreen WPF transparent overlay.

## Main Runtime Objects

### `CursorManager`

File:

- `Managers/Cursor/CursorManager.cs`

Owns:

- logical virtual-cursor coordinates
- lock and unlock transitions between real and virtual cursor modes
- directional midpoint-based cursor placement
- forwarding of movement updates into the renderer

Important methods:

- `LockRealCursor(...)`
- `UnlockRealCursor(...)`
- `StopUnlockRealCursor()`
- `JumpToVirtualCursor()`
- `SetDirectionalCursorPosition(...)`
- `OnMouseMoved(...)`

`CursorManager` is the authoritative cursor-flow layer. If you need to change how cursor movement behaves, change this layer first and keep the renderer passive.

### `ICursorOverlay`

File:

- `Overlays/Cursor/Interfaces/ICursorOverlay.cs`

Purpose:

- isolate cursor rendering from cursor movement logic
- keep the call contract stable across renderer implementations

The current implementation is `NativeCursorOverlay`.

### `NativeCursorOverlay`

File:

- `Overlays/Cursor/NativeCursorOverlay.cs`

Responsibilities:

- load the shared cursor PNG from disk
- scale it to the current cursor size
- create a `NativeCursorWindow` on demand
- move the native window to the requested logical cursor position
- optionally keep the real cursor synchronized when the caller asks for that

It does not decide where the cursor should be. It only draws what `CursorManager` gives it.

### `NativeCursorWindow`

File:

- `Overlays/Cursor/NativeCursorWindow.cs`

Responsibilities:

- host the layered Win32 window
- apply the ARGB bitmap using `UpdateLayeredWindow`
- stay topmost without taking focus
- restore the previous foreground window if the overlay becomes active unexpectedly

This file is the main focus-management boundary. If a game loses focus or audio when the overlay appears, inspect this file first.

### `CursorCenterOverlay`

File:

- `Overlays/Cursor/CenterCursor/CursorCenterOverlay.xaml.cs`

Responsibilities:

- render the midpoint preview cursor
- keep the preview aligned with the current midpoint and center offsets
- park the real cursor at midpoint when midpoint-edit mode is enabled

This uses the same shared cursor PNG as the main virtual cursor.

## Coordinate Model

The project uses two coordinate spaces:

- logical WPF coordinates
- physical screen pixels

Rules:

- midpoint values are stored in logical coordinates
- virtual cursor state is stored in logical coordinates
- Win32 cursor APIs and native layered-window movement use physical pixels
- conversion happens at the boundary through helper methods such as `Helper.ToPhysicalPixels(...)` and `Helper.SetCursorPosDip(...)`

This is why DPI-related bugs usually come from mixing those units in the wrong layer.

## Cursor Asset Flow

1. `CursorImageLoader` stores the active cursor PNG in `LocalAppData/PathOfWASD/cursor.png`.
2. `OverlayService.UploadCursor()` copies the selected file there.
3. `OverlayService` tells both the main overlay and midpoint overlay to reload the image.
4. Each renderer rebuilds its in-memory bitmap and reapplies it to its native window.

Because both renderers read the same file, they always stay visually aligned after a cursor upload.

## Lifecycle Summary

### Entering virtual-cursor mode

1. `OverlayService` calls `CursorManager.LockRealCursor(...)`.
2. `CursorManager` records or restores the logical cursor position.
3. `CursorManager` starts the mouse hook and shows the renderer.
4. Raw mouse input updates the virtual cursor.
5. `NativeCursorOverlay` moves the cursor window to the new position.

### Exiting virtual-cursor mode

1. `OverlayService` calls `CursorManager.JumpToVirtualCursor()` and `StopUnlockRealCursor()`.
2. `CursorManager` moves the real cursor to the virtual position.
3. The virtual cursor renderer hides.
4. Hooks and remapping logic switch back to the disabled state.

## Common Change Rules

When changing cursor behavior:

- change movement math in `CursorManager`
- change rendering behavior in `NativeCursorOverlay` or `NativeCursorWindow`
- change midpoint preview behavior in `CursorCenterOverlay`
- change coordinate conversion only in helper or native-boundary code

Do not:

- move cursor math into the renderer
- store midpoint in physical pixels in one place and logical units in another
- add focus-taking behavior to the native window
