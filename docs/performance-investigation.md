# WASD performance and latency investigation

Investigated September 8, 2026 against upstream `d9c96f8` plus the local experimental cursor-hiding work. The user reports large frame drops on some PCs; mouse polling rate, exact affected build, and whether mouse movement is necessary are unknown. No affected-PC frame-time trace was available.

Following the investigation, the user authorized the small cursor-window fix. It is now applied: two assignments were relocated into the pre-handle branch, with one explanatory comment. The input queue, cancellation behavior, and delays are unchanged. The running application was not restarted during implementation.

The durable regression test at `tests/NativeCursor/NativeCursor.Tests.csproj` failed before the fix (600 production positioning operations for 200 diagonal positions), then passed after it (200). It compares against the old move implementation and checks native/managed coordinates, lazy handle creation, hidden moves, actual layered show/hide cycles with a transparent bitmap, size, negative coordinates, axis-only moves, repeated positions, extended styles, and foreground samples. These checks do not replace visible gameplay or mixed-DPI validation. All 32 existing cursor-visibility checks also passed.

Run the regression test with:

```powershell
dotnet run --project tests/NativeCursor/NativeCursor.Tests.csproj -p:ManagePackageVersionsCentrally=false -p:RestorePackagesWithLockFile=false -v:q
```

The earlier investigation harness below captured the pre-fix evidence. Its original counts are historical; use this durable regression test to verify the fixed production code.

## Recommendation

Start with the redundant moves in `NativeCursorWindow.MoveTo`: move the `Left` and `Top` assignments inside its existing `!IsHandleCreated` branch. This is two relocated assignments, with no new dependency, timer, setting, input delay, or algorithm. The existing `SetWindowPos` call and its flags remain intact.

An isolated candidate demonstrated the intended reduction and matching final coordinates. This is a low-risk optimization, not a promise of zero regressions or a demonstrated fix for the reported FPS loss. Visible rendering, monitor/DPI transitions, focus, and gameplay still need A/B validation. Do not combine this first change with input sequencing or timing changes.

## 1. Confirmed redundant window moves; plausible FPS contributor

`Internals/RawMouseInputHandler.cs:65` reads each raw mouse packet and synchronously invokes the cursor callback. `Managers/Cursor/CursorManager.cs:188` updates and clamps the virtual coordinates, then calls the renderer on the WPF UI thread. This callback already comes from that UI thread; its `Dispatcher.Invoke` is not a separate queued task per packet.

`Overlays/Cursor/NativeCursorOverlay.cs:103` forwards each resulting position to `NativeCursorWindow.MoveTo`, even when the cursor window is hidden during a skill. That method currently:

1. Assigns `Left`, which can move an existing native window.
2. Assigns `Top`, which can move it again.
3. Calls `SetWindowPos` explicitly with the final coordinates.

The isolated Windows test linked the actual production window and interop sources, and compared them against a local candidate copy with only the two assignments relocated. For 200 positions changing both axes, an attached `NativeWindow` counted **600 `WM_WINDOWPOSCHANGING` messages in the original and 200 in the candidate**. Managed bounds and `GetWindowRect` matched at every final position. Pre-handle positioning remained lazy and both windows remained hidden. Neither test window was foreground at the final sample. An earlier run's whole-test foreground-equality assertion failed; because another application's foreground state can change independently, the final check only verifies that neither test window became foreground at the sampled endpoint. This does not certify focus behavior in a visible PoE session.

This proves two redundant position operations per two-axis change, not 67% lower total CPU or a particular FPS gain. At high delivered mouse-event rates, the extra work scales with those events. There is also a native allocation/free and two `GetRawInputData` calls per packet. Their individual CPU contribution has not been measured.

The mouse suppression hook is installed on the same UI thread (`CursorManager.cs:132`), as is the keyboard hook (`HotkeyController.cs:118`). Microsoft documents that low-level mouse callbacks execute on the installing thread and depend on its message loop. This provides a plausible route from heavy UI work to input delays. The FPS effect remains a hypothesis until measured on an affected machine. [Microsoft: low-level mouse hooks](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)

Microsoft also specifically identifies buffered raw-input reads as useful for high-frequency mice. Buffered reading or visual-update coalescing would be larger, more sensitive follow-up changes, not the first patch. Any such change must preserve every input delta, per-event edge clamping, and immediate real-cursor updates during aiming. [Microsoft: raw input](https://learn.microsoft.com/en-us/windows/win32/inputdev/about-raw-input)

## 2. Confirmed event loss and unfinished completion on queue overflow

`Managers/Controller/ControllerManager.cs:58` creates a queue with capacity **two**, using `DropOldest`. `EnqueueEvent` returns a completion task for each event. A successful write can silently remove an older event, and nothing completes that removed event's task. Microsoft's channel implementation documents this overflow behavior. [Microsoft: channels](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/)

The isolated test instantiated the actual production `ControllerManager` with a paused fake event processor. Enqueuing `W down`, `A down`, `W up` delivered only `A down` and `W up`. After draining and completing everything delivered, the original `W down` task was still incomplete. No native keyboard or mouse input was sent.

This is a deterministic correctness bug under backlog. It can lose input transitions and leave callers waiting. The real consumer contains awaited movement/skill work, so processing is not instantaneous. This can explain missed or apparently delayed inputs, but the test does not establish the frequency in actual play, stuck-key symptoms, or an FPS effect.

A fix must preserve meaningful down/up edges and define completion behavior. Merely completing a discarded task does not restore the lost state transition. Merely enlarging the queue postpones overflow. A non-dropping queue is a likely direction, but backlog, rapid direction changes, cancellation, and mode switches need explicit tests before changing this behavior.

## 3. Confirmed canceled skill-release completion leak

`Managers/Controller/SkillUpDelayHandler.cs:33` catches cancellation without completing the event's `Tcs`. `EventProcessor` cancels an outstanding delayed release when another down event for the same key arrives.

The isolated test called the actual production delay handler, canceled during its wait, and awaited the handler's exit. The event's completion task remained pending. `HotkeyController.HandleMappedKeyUp` awaits that completion before reaching literal key-up injection, so the pending completion can interrupt that release pipeline. The exact gameplay symptom needs an end-to-end rapid re-press test.

This requires a defined cancellation outcome and review of overlapping per-key cleanup. Blindly setting success on cancellation can release a newly pressed key. Treat it as a separate input-correctness fix, not an FPS optimization.

## 4. Existing intentional latency

Movement processing requests `8 + 24 = 32 ms` of delay on the ordinary key-down path in `ControllerManager`. The first ordinary mapped skill requests `8 + 16 + 24 = 48 ms` across `HotkeyController` and `ControllerManager` before literal key-down injection. Queueing, dispatcher scheduling, and timer scheduling can add more. These sums describe requested delays along those paths, not measured end-to-end latency for every action.

Skill release also enforces a 200 ms minimum from its recorded controller down time. These waits suspend tasks rather than spin the CPU, so they are relevant to responsiveness, not direct evidence of high CPU. Removing them wholesale could break input ordering and casting behavior.

## Secondary observations

- Raw-input `Stop` removes the WPF hook without unregistering the device. Background raw-input delivery may therefore continue after mode-off. This is a lifecycle cleanup issue, not a demonstrated cause of the mode-on FPS loss.
- `Start` registers raw input twice for the same HWND. This is redundant setup, not evidence of two input streams or two callbacks per packet.
- The cursor bitmap is uploaded when shown or changed, not on every move. Repeated show calls can upload again, but there is no per-packet bitmap upload in `MoveTo`.
- The new optional hiding service uses a 100 ms timer; the high-rate rendering path and controller issues predate that feature. Testing hiding off/on is still useful when comparing builds.

## Evidence and next validation

The isolated harness and candidate are in `artifacts/perf-investigation/`; its `results.log` records the successful run. It links production code but never starts the application, installs input hooks, registers raw input, changes settings, sends input, or accesses PoE. The windows used for the move comparison remain hidden. The build emits the project's existing InputSimulatorPlus compatibility warning.

Reproduce with:

```powershell
dotnet run --project artifacts/perf-investigation/PerfInvestigation.csproj -p:ManagePackageVersionsCentrally=false -p:RestorePackagesWithLockFile=false -v:q
```

For an affected user, compare the same scene with WASD off/on, then stationary mouse versus continuous movement. If supported, compare the current polling rate with 500/1,000 Hz. Record the exact build, average FPS/frame-time spikes, PathOfWASD CPU, game CPU/GPU, and Desktop Window Manager GPU activity. A stationary-mouse drop with no ongoing raw movement would weaken the high-rate move hypothesis. A strong polling-rate dependency would strengthen it.

Validate the two-assignment candidate separately: visible cursor tracking and centering, aimed and directional skills, rapid lock/unlock, toggle off/on, Alt-Tab, focus/clicks, and the affected monitor/DPI setup. A passing local smoke test cannot replace the comparison on a PC that actually exhibits the frame loss.
