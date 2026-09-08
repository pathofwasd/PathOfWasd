# Experimental movement-cursor hiding

`HideMovementCursor` is a new setting, independent of the obsolete `CursorMode` values. It defaults to false, including when loading existing JSON. The checkbox appears above Actions; save/apply activates it. Reset to default turns it off.

The feature calls Windows' `MagInitialize`, `MagShowSystemCursor` and `MagUninitialize`. It does not open the PoE process, inject code, attach input queues, alter game files, or change overlay hit testing. The existing virtual cursor stays unchanged.

`CursorVisibilityService` hides only when all three conditions hold: the option is enabled, WASD mode is active, and the cursor manager has locked the real cursor. It additionally requires a foreground window with title `Path of Exile` and class `POEWindowClass`. Unknown window names fail open, leaving the cursor visible. Window metadata is read through Windows APIs; the game process is not accessed.

`UnlockRealCursor` restores the real cursor before the virtual cursor is hidden. This covers aimed skills and interactions routed through that path. Directional skills that retain the locked cursor keep it hidden. Disabling WASD or using its hold-to-disable control cancels hiding independently of late controller callbacks. Focus changes are checked every 100 ms while the UI is responsive.

Native calls and helper communication run on the UI dispatcher because controller events can originate on a background thread. With the setting off, requests only update an internal flag: no Magnification calls, running timer, helper process, or dispatcher round trip is needed. Enabling can wait up to three seconds for the recovery helper to start; this happens during settings activation, not on each skill.

## Recovery

The helper is another invocation of the same executable with `--cursor-visibility-watchdog`. `Program.Main` routes that invocation before creating WPF resources or installing input hooks. A named marker prevents the existing single-instance startup code from killing this helper together with the old main application.

The main process arms the helper over redirected stdin before hiding, sends visible only after successful restoration, and renews the heartbeat every 100 ms. EOF or a two-second heartbeat timeout makes an armed helper call the Windows restore API and exit. Thus a normal crash, main-process termination, or stalled UI has an independent recovery path. A helper failure disables hiding and makes the main process attempt restoration. Errors latch until the option is switched off and on; they appear below the checkbox.

This is desktop-wide cursor state. It cannot guarantee restoration if Windows refuses the restore operation or both processes are terminated together. Concurrent magnification/accessibility software can affect the same state. The feature does not repeatedly fight another program's cursor settings.

## Verification and limits

On September 8, 2026, a user reported that the experimental build works in Path of Exile. This confirms an initial successful in-game test on that user's setup, not exhaustive coverage of every interaction, renderer, or system. The UI retains the experimental label and notes limited testing.

The focused console checks compile the production service and watchdog against fake native calls, use real child-process IPC, and reference the actual settings/view model. They cover the default-off path, old JSON, clone/dirty tracking, apply/save/reset, skill unlock, dispatcher ownership, focus changes, late events after disabling WASD, initialization/hide failures, helper death, EOF and timeout recovery. They never hide the desktop cursor or write user settings.

Run on Windows:

```powershell
dotnet run --project tests/CursorVisibility/CursorVisibility.Tests.csproj -p:ManagePackageVersionsCentrally=false
```

On this workstation, parent-directory MSBuild files enable central package management and treat release warnings as errors. Validation uses command-line overrides rather than changing those shared files:

```powershell
dotnet publish PathOfWASD.csproj -p:PublishProfile=SingleFile-win-x64 -p:ManagePackageVersionsCentrally=false -p:TreatWarningsAsErrors=false
```

The build retains the repository's existing warnings. The automated checks alone do not establish actual PoE cursor suppression, click/hover compatibility, or zero gameplay regressions; the user report supplies the initial in-game confirmation. `MagShowSystemCursor` can address an OS-managed cursor; it cannot erase a cursor drawn into the game's rendered image. Wine support is unverified.

Before considering the option stable, check the actual game build and renderer: movement in all directions, aimed and directional skills, held skills, item pickup, inventory drag/drop with normal mouse mode, hold-to-disable, Alt-Tab and return, toggling while keys are held, and exiting the app. Confirm that the pointer stays usable and that clicks and focus behave exactly as they did with the option off.
