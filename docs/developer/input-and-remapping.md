# Input And Remapping

## Overview

The input pipeline translates physical keyboard input into one of three behaviors:

- skill key handling
- directional skill handling
- raw WASD movement handling

The main files are:

- `Overlays/Settings/Services/HotkeyController.cs`
- `Managers/Controller/ControllerManager.cs`
- `Managers/Controller/ControllerState.cs`
- `Managers/Controller/EventProcessor.cs`
- `Managers/Controller/KeyStateTracker.cs`

## Hotkey Layer

### `HotkeyController`

This is the entry point for runtime keyboard behavior.

It owns:

- the low-level keyboard hook
- registered Win32 hotkeys for settings actions
- the swap map from physical keys to placeholder keys
- literal key injection
- key-up coordination through `TaskCompletionSource`

The important concept is the placeholder key map:

- a physical skill key is intercepted
- the app injects a placeholder key instead
- the placeholder is what drives the controller logic
- the original physical key is later injected literally in the correct order

That separation is what allows the app to control ordering between cursor state changes and skill input.

## Controller Layer

### `ControllerManager`

`ControllerManager` serializes logical controller events through a bounded channel.

Its job is to answer:

- is this event a skill event?
- is it a directional skill event?
- is it a WASD movement event?
- should the cursor be unlocked, locked, or repositioned?
- should stand-still or movement keys be injected?

The manager does not listen to raw hooks directly. It receives normalized key events from the hotkey layer.

### `ControllerState`

`ControllerState` derives useful state from:

- currently held keys
- the active movement key
- the active stand-still key
- the manager's current key and current configured toggle keys

This file is where "incoming skill", "incoming directional skill", and "incoming WASD" are computed.

## Ordered Skill Flow

The most important hotkey path is `HandleMappedKeyDown(...)` in `HotkeyController`.

The current flow is:

1. resolve the placeholder key for the physical key
2. install a release completion callback
3. optionally suspend already-held skill keys before a directional skill takes over
4. dispatch controller key-down work on the UI thread
5. wait for controller-side async work to finish
6. inject the literal key-down
7. wait for the real physical key-up
8. run the mapped key-up path

That sequence is intentional. Reordering it can change gameplay behavior.

## WASD Flow

For raw WASD input:

1. `HotkeyController` forwards key-down and key-up into `ControllerManager`
2. `ControllerManager` decides whether movement should start, continue, or stop
3. `CursorManager.SetDirectionalCursorPosition(...)` moves the real cursor around midpoint
4. `ControllerState.MovePlace()` or `DontMovePlace()` holds or releases the movement key

## Midpoint Editing Hotkeys

`HotkeyController.HookCallback(...)` also handles midpoint-edit arrow keys.

When midpoint editing or center-offset editing is active:

- arrow keys update midpoint or center offsets
- the real cursor is repositioned through `Helper.SetCursorPosDip(...)`
- the original arrow-key input is swallowed

That behavior lives in the hook layer because it must intercept global key input while the settings overlay is open.

## Debugging Advice

If a mapped skill behaves incorrectly:

- start in `HotkeyController.HookCallback(...)`
- verify whether the key was treated as physical, placeholder, or literal injection
- inspect `HandleMappedKeyDown(...)` and `HandleMappedKeyUp(...)`
- then inspect `ControllerManager.ProcessEventAsync(...)`

If movement logic is wrong:

- inspect `ControllerManager.PerformIncomingWasdTaskAsync(...)`
- inspect `CursorManager.SetDirectionalCursorPosition(...)`
- inspect midpoint and offset values coming from `OverlayService`

If keys appear stuck:

- inspect `PendingSkillUpCts`
- inspect `_pendingDirectionalRestoreKeys`
- inspect the held-key tracker state

## Change Rules

When changing input behavior:

- preserve the ordering guarantees in `HotkeyController`
- preserve the channel-based serialization in `ControllerManager`
- update this document if the meaning of placeholder keys, directional skills, or release handling changes
