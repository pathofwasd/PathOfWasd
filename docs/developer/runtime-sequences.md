# Runtime Sequences

## Purpose

This guide explains the end-to-end runtime order that makes PathOfWASD work.

It focuses on:

- entering and leaving WASD mode
- how the real cursor and virtual cursor split apart
- how physical keys are intercepted and turned into placeholder keys
- how mapped skills are ordered so cursor and movement state change before the game receives the real input
- why the small delays exist

This guide is based on the current code path, not historical comments or commit history. Where the code makes the order clear but not the original intent, the explanation below is marked as an inference.

## Core Model

At a high level, the app keeps two cursor concepts alive at the same time:

- the real OS cursor, which the game ultimately receives
- the virtual cursor, which the player sees and moves with raw mouse deltas during WASD mode

The split works like this:

- WASD mode starts
- normal `WM_MOUSEMOVE` traffic is swallowed by a low-level mouse hook
- raw mouse input is still captured through `WM_INPUT`
- raw deltas advance the virtual cursor state
- a small native layered window renders the visible fake cursor
- the real cursor stays under app control
- when a mapped skill needs the real cursor, the app jumps the real cursor to the virtual cursor position at a controlled point in the sequence

The main files involved are:

- `Overlays/Settings/Services/OverlayService.cs`
- `Managers/Cursor/CursorManager.cs`
- `Internals/MouseHook.cs`
- `Internals/RawMouseInputHandler.cs`
- `Overlays/Cursor/NativeCursorOverlay.cs`
- `Overlays/Settings/Services/HotkeyController.cs`
- `Managers/Controller/ControllerManager.cs`
- `Managers/Controller/EventProcessor.cs`
- `Managers/Controller/SkillUpDelayHandler.cs`

## Placeholder Key Model

The controller layer does not primarily reason about the user's original skill keys. It mostly reasons about placeholder keys.

The placeholder set is defined in `Helper.Placeholders`:

- `F13`
- `F14`
- `F15`
- `F16`
- `F17`
- `F18`
- `F19`
- `F20`
- `F21`

The flow is:

1. the settings UI stores the user-selected physical keys
2. `Helper.GetFKeyMaps(...)` and `Helper.GetDirectionalFKeyMaps(...)` assign placeholder keys to those bindings
3. `HotkeyController.RegisterCallbacks(...)` builds `_swapMap` from physical key to placeholder key
4. the low-level keyboard hook suppresses the real physical key
5. the app injects the placeholder key instead
6. controller logic runs on the placeholder key
7. only after the controller side has done its cursor and movement work does the app inject the real physical key literally

That is the main reason the input flow looks more complicated than a normal rebind system. The app needs a staging key so it can separate "gameplay state transition" from "deliver the actual skill input to the game".

## Mouse Click Mapping

Mouse clicks are also folded into the same pipeline.

`MouseClickKeyMapper` hooks low-level mouse events and, while WASD mode is active, turns:

- left click into `Key.NoName`
- right click into `Key.Pa1`
- middle click into `Key.Oem102`

`HotkeyController.BindMouseKeys()` then registers callback chains that route those placeholder channels back into the configured left, right, and middle skill bindings.

The important result is:

- mouse buttons are treated like staged inputs too
- click behavior can go through the same cursor-lock and skill-ordering pipeline as keyboard skills

## Sequence: Entering WASD Mode

This sequence begins in `OverlayService.ActivateVirtualCursor()`.

1. `_isLocked` is set to `true`
2. `CursorManager.LockRealCursor(false, false, true)` is called
3. `CursorManager` initializes virtual cursor position from the current real cursor if needed
4. `MouseHook.StartHook()` begins swallowing normal `WM_MOUSEMOVE`
5. the cursor overlay is shown in `CursorMode.ShowDuringSkills` because the toggle path passed `fromToggle: true`
6. `RawMouseInputHandler.Start()` begins listening for `WM_INPUT`
7. `MouseClickKeyMapper.SkipLogic` is set to `false`
8. `HotkeyController.SkipLogic` is set to `false`
9. `HotkeyController.Rebind()` rebuilds the active hotkey and remap tables

The net effect is:

- visible mouse movement now comes from the virtual cursor overlay
- raw deltas continue to move that virtual cursor
- physical keys and clicks now go through the staged placeholder pipeline

## Sequence: Virtual Cursor Movement

This sequence lives mostly in `CursorManager.OnMouseMoved(...)`.

1. raw mouse input arrives through `RawMouseInputHandler.WndProc(...)`
2. the handler forwards `lLastX` and `lLastY` into `CursorManager.OnMouseMoved(...)`
3. deltas are converted from physical pixels into logical WPF coordinates using the current DPI scale
4. sensitivity is applied
5. the virtual cursor position is clamped to the virtual desktop bounds
6. `NativeCursorOverlay.UpdateCursorPosition(...)` moves the native cursor window to the new logical position

While `UserControlsRealCursor` is `false`, the overlay moves but the real cursor is not allowed to track ordinary mouse movement.

At the same time, `MouseHook` is swallowing normal `WM_MOUSEMOVE`, which prevents the focused app from responding to the OS cursor as though it were still freely moving.

## Sequence: Physical Skill Key Down

This is the most important ordered path.

### Step 1: hook interception

`HotkeyController.HookCallback(...)` sees a physical key-down that exists in `_swapMap`.

It then:

1. blocks the original physical key from reaching the game
2. injects the mapped placeholder key with `PlaceholderInjectionTag`
3. returns `1` from the hook to swallow the original event

### Step 2: placeholder dispatch

The same hook later sees the injected placeholder event.

Because `dwExtraInfo` matches `PlaceholderInjectionTag`, it:

1. routes the placeholder key into `_skillDownCallbacks`
2. calls `HandleMappedKeyDown(...)`

### Step 3: install release completion

`HandleMappedKeyDown(...)` creates a `TaskCompletionSource` and installs a placeholder-key release callback.

That completion source is how the key-down path waits for the real physical key-up later before it runs the mapped key-up sequence.

### Step 4: short pre-delay

`await Task.Delay(8)`

Inference:

- this likely gives very recent input state changes a moment to settle before the app decides whether the press is colliding with active WASD state and directional-skill state

### Step 5: directional-skill suspension path

If the placeholder belongs to a directional skill and some WASD key is already held:

1. `ControllerState.AllHeldSkillDown(...)` gathers currently held skill placeholders
2. those held skills are stored in `_suspendedSkillsDuringDirectionalInput`
3. each still-physically-held skill has a synthetic key-up injected with `DirectionalRestoreInjectionTag`

This temporarily clears already-held skills out of the game's view so the directional skill can take precedence cleanly.

### Step 6: controller-side transition

The hotkey layer dispatches into the controller pipeline on the UI thread:

- `ControllerManager.HandleKeyDownAsync(...)`
- then `EventProcessor.ProcessLoopAsync(...)`
- then `ControllerManager.ProcessEventAsync(...)`

Depending on state, the controller pipeline may:

- treat the key as a skill
- treat it as a directional skill
- treat it as plain WASD movement

For a normal skill-down, the important path is:

1. `PerformIncomingSkillDownTaskAsync(...)`
2. possibly release movement if the movement threshold has passed
3. press the stand-still key
4. wait `16ms`
5. call `CursorManager.UnlockRealCursor(...)`
6. `CursorManager` jumps the real cursor to the virtual cursor position
7. the virtual cursor overlay hides
8. `UserControlsRealCursor` becomes `true`

This is the exact reason the placeholder exists. The app needs the cursor and movement transition to complete before the real skill key reaches the game.

### Step 7: post-controller delay

`await Task.Delay(24)`

Inference:

- this likely creates a small safety window after the controller-side unlock and stand-still transition so the real literal skill key reaches the game only after cursor placement and skill-state setup have settled

### Step 8: literal skill injection

The hotkey layer finally injects the original physical key with `LiteralInjectionTag`.

When the hook sees that literal injection:

- it does not re-route it through the placeholder pipeline
- it optionally triggers literal mouse-down behavior for the configured left, right, and middle paths
- otherwise it is allowed through to the game

### Step 9: wait for the real physical release

`HandleMappedKeyDown(...)` then waits on the `TaskCompletionSource` created earlier.

That completion only happens when the original physical key is actually released and the placeholder release callback fires.

## Sequence: Physical Skill Key Up

Once the real physical key is released:

1. `HookCallback(...)` intercepts the physical key-up
2. the app injects the placeholder key-up
3. the placeholder release callback completes the `TaskCompletionSource`
4. `HandleMappedKeyDown(...)` resumes
5. it waits another `8ms`
6. it calls `HandleMappedKeyUp(...)`

The extra `8ms` is another timing buffer.

Inference:

- this likely gives the literal key-down a small separation from the teardown path so the game has time to observe the intended press before the app starts restoring lock and movement state

Inside `HandleMappedKeyUp(...)`:

1. controller-side key-up work runs on the UI thread
2. if the key is not directional, the key is removed from `_suspendedSkillsDuringDirectionalInput`
3. if the key was directional, previously suspended skills are restored by injecting synthetic key-down events with `DirectionalRestoreInjectionTag`
4. the original physical key-up is injected literally

## Sequence: Delayed Skill-Up

Skill-up is not always processed immediately.

`EventProcessor` does this for toggle keys:

1. on skill key-up, create a cancellation token source
2. store it in `PendingSkillUpCts`
3. schedule `SkillUpDelayHandler.DelaySkillUpAsync(...)`

`SkillUpDelayHandler` enforces a minimum hold time of `200ms`.

That means:

- extremely short taps are stretched to at least the threshold
- if the same key goes down again before the delayed release fires, the pending release is canceled

The practical effect is that very short taps still look like intentional skill presses instead of key chatter.

## Sequence: Directional Skill Restore Delay

When restoring suspended skills after a directional skill:

- `InjectDirectionalRestoreKeyDown(...)` waits `40ms` before reinjecting the held skill

Inference:

- this likely gives the game time to register that the directional skill ended before held skills are restored, which reduces overlap between "directional skill still active" and "previous held skill resumes"

## Sequence: Raw WASD Movement

WASD keys also flow through the hook layer, but they do not use the placeholder indirection.

The path is:

1. `HotkeyController` sees raw WASD input
2. it calls `HandleWASDKeyDown(...)` or `HandleWASDKeyUp(...)`
3. `ControllerManager.ProcessEventAsync(...)` classifies it as movement
4. `PerformIncomingWasdTaskAsync(...)` runs
5. `CursorManager.SetDirectionalCursorPosition(...)` moves the real cursor around midpoint based on held WASD keys
6. `ControllerState.MovePlace()` presses the configured movement key
7. `DelayMovementUpState.StartMovement()` starts movement timing

The midpoint is the anchor. The virtual cursor is not the thing that drives directional WASD targeting. The real cursor is moved around midpoint by the directional offset rules.

## Movement Threshold

`DelayMovementUpState` tracks a `600ms` movement threshold.

It matters when entering a skill from movement:

- if movement has been active long enough, skill-down is allowed to stop movement and mark `CanGoUp`
- on skill-up, that movement state can then be fully released

Inference:

- this looks like protection against overly eager movement cancellation during very fresh movement starts
- in other words, a movement that just started is treated differently from one that has been stably active for a while

## Sequence: Leaving WASD Mode Entirely

This happens in `OverlayService.DeactivateVirtualCursor()`.

The current order is:

1. `_isLocked = false`
2. `CursorManager.JumpToVirtualCursor()` moves the real cursor to the virtual cursor location
3. `ControllerState.DontMovePlace()` releases the movement key
4. wait `100ms`
5. `ControllerState.DontStandInPlace()` releases the stand-still key
6. `CursorManager.StopUnlockRealCursor()` hides the overlay, stops the mouse hook, and stops raw input
7. `MouseClickKeyMapper.SkipLogic = true`
8. `HotkeyController.SkipLogic = true`
9. `HotkeyController.RebindOnToOffWASDMode()` restores the disabled-state hotkeys

Inference for the `100ms` delay:

- this likely gives the game a clean window to observe movement release before stand-still is released and the cursor-control stack is torn down

## Why The Order Is Fragile

This project depends on several independent subsystems lining up:

- low-level keyboard hook interception
- low-level mouse hook suppression
- raw mouse input capture
- placeholder-key staging
- channel-based controller serialization
- synthetic key injection
- cursor overlay visibility
- real cursor repositioning
- several timing gaps

Changing one piece without preserving the surrounding order can break:

- skill presses firing before the cursor jump
- held skills remaining active during directional skills
- movement keys sticking
- clicks leaking through at the wrong time
- the game seeing the physical key instead of the staged placeholder

## Files To Inspect For Sequence Bugs

If the runtime order feels wrong, inspect these files in this order:

1. `Overlays/Settings/Services/HotkeyController.cs`
2. `Managers/Controller/EventProcessor.cs`
3. `Managers/Controller/ControllerManager.cs`
4. `Managers/Cursor/CursorManager.cs`
5. `Overlays/Settings/Services/OverlayService.cs`
6. `Overlays/BGFunctionalities/MouseClickKeyMapper.cs`
7. `Internals/MouseHook.cs`
8. `Internals/RawMouseInputHandler.cs`
