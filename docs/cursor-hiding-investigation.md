# PathOfWASD cursor hiding: failure history and restoration options

An opt-in implementation was subsequently added; see [implementation and validation notes](developer/cursor-hiding.md). On September 8, 2026, a user confirmed it works in Path of Exile on their setup; testing remains limited. The scope and feasibility judgments below preserve the research assessment made before implementation and that initial in-game confirmation.

**Assessment: the patch-related removal is confirmed. The old overlay is a strong explanation for the input/focus failure, but the exact change inside Path of Exile is unconfirmed. The best external-only restoration candidate is Windows' `MagShowSystemCursor` API. It is technically well matched to an OS-managed cursor, but it has not been tested with current PoE in this investigation.**

The implementation constraint is strict: do not modify, inject into, hook inside, or attach input threads to the PoE process. Cursor utilities that require those techniques are excluded. A Windows cursor-visibility service can be evaluated without introducing any of them. Restoring the old full-screen window is not a sound default.

## Scope and evidence

This assessment covers the public repository at commit `d9c96f80502d6de1498c50ead5ef264232c95aa7`, its earlier cursor implementation, release notes, public issue inventory, and Microsoft cursor/window documentation. The remote HEAD matched that commit when checked on September 8, 2026. Current YoloMouse documentation was checked for relevant capabilities.

No PoE gameplay reproduction, binary instrumentation, cursor mutation, application installation, or product-code modification was performed. Private Discord discussions were not available. Statements about a particular PoE renderer, cursor type, or anti-cheat decision therefore remain unverified unless explicitly sourced below.

## What the history establishes

| Date | Evidence | Meaning |
|---|---|---|
| July 3, 2025 | Version 0.68 release notes describe cursor-visibility modes as experimental and potentially buggy. | Cursor hiding had known fragility before Mirage. [1] |
| March 8, 2026 | Version 1.0.0hotfix says in-game cursor disappearance was removed to make the application usable again. | The missing feature was an explicit compatibility tradeoff. [2] |
| March 8, 2026 | Commit `7cecb72` disables visibility logic and enables click-through. Its message attributes the hotfix to the new PoE patch breaking the overlay. | Direct maintainer evidence connects the patch, the overlay, and the removal. [3] |
| March 9, 2026 | Commit `e2d43dd` replaces the full-screen WPF cursor overlay with small native layered windows. | The eventual implementation removes the large input surface. [4] |
| March 9, 2026 | Release 3.28.0 says it should work with PoE 3.28.0. | The published recovery does not claim cursor hiding was restored. [5] |

The hotfix commit says: “the new POE patch broke old behavior with overlay.” That confirms the maintainer's attribution, rather than independently identifying the internal game regression. [3]

The public issue inventory contained two user issues and the initial pull request; neither user issue documented this failure. The inspected official Mirage patch notes did not identify a corresponding cursor/overlay focus change. This limits the causal claim: there is strong project-side evidence, but no located GGG explanation of the mechanism. [6][7]

## Why the old hiding method could break clicking

The previous `CursorOverlay` was a borderless, topmost WPF window sized to the primary display. Its XAML combined a transparent window with a grid whose background was `#01000000`: alpha 1 out of 255, not zero. The window also specified `Cursor="None"`. [8]

The old `ShowOverlay` then did three important things: it optionally replaced the standard system arrow with a blank cursor, selected a WPF cursor appearance, and called `SetClickThrough(false)` before showing the window. In `AlwaysHide` mode, the hide path could leave the full-screen overlay present and non-click-through. [3][8]

This was not simply painting over the cursor. It arranged for a nearly invisible window to occupy the area under the mouse and supply its own cursor policy. Microsoft documents that layered windows pass mouse events through zero-alpha regions, while `WS_EX_TRANSPARENT` makes the layered window pass mouse events to underlying windows regardless of its shape. The nonzero-alpha surface and disabled click-through therefore matter directly to input targeting. [9]

That creates a conflict: keeping the overlay involved in mouse targeting helps its `Cursor=None` policy take effect, but also interferes with the game being the window under the mouse. An engine change to mouse targeting, capture, activation, or input acceptance could expose this arrangement. Which of those changed in PoE is not established.

There is also a separate activation concern. The old XAML had `Focusable="False"`, but it did not specify `ShowActivated="False"` or the explicit native no-activation protections used today. WPF documents `ShowActivated` as defaulting to true. This supports an activation vulnerability; it does not prove that every failure involved foreground focus changing. [8][10]

The strongest defensible conclusion is therefore: **the hiding implementation depended on an input-intercepting full-screen overlay, and the maintainer removed that dependency after the game update broke its behavior.** “The overlay necessarily stole foreground focus on every click” would overstate the evidence.

The hotfix also changed several behaviors together. It is not a controlled experiment isolating `SetSystemCursor`, WPF cursor ownership, click-through, and activation independently. The commit alone cannot establish that blanking the system arrow was itself the cause.

## What the current implementation fixes

`NativeCursorWindow` is a small bitmap-sized layered window. It uses `WS_EX_TRANSPARENT`, `WS_EX_NOACTIVATE`, no-activation show/position calls, and `MA_NOACTIVATE` handling. This separates drawing the aiming cursor from taking mouse input. [11]

The visibility modes survive in settings and method signatures, but `NativeCursorOverlay.ShowOverlay` and `HideOverlay` ignore the mode parameter. `SystemCursorManager.HideSystemCursor` remains in the repository but has no runtime caller. It replaces only `OCR_NORMAL`, the standard Windows arrow. [12][13]

Reconnecting that method is a valid small experiment only if the visible game cursor is affected by the standard-arrow slot. It is not evidence of a complete restoration already waiting behind a checkbox.

## The distinction that determines feasibility

There are three relevant cursor implementations:

| Cursor type | Who supplies its appearance? | Consequence |
|---|---|---|
| Standard Windows cursor | A system cursor slot, such as `OCR_NORMAL` | Replacing that slot may hide it. |
| Custom Windows cursor | The game supplies its own `HCURSOR` through Windows cursor APIs | Blanking the standard arrow does not generally replace this separate resource. |
| Software-rendered cursor | The game draws cursor pixels into its rendered image | Windows cursor visibility APIs do not remove those pixels. |

The first two can both be displayed through the operating system. A cursor looking game-specific does not by itself prove that it is software-rendered. Microsoft's cursor APIs distinguish cursor selection, system resource replacement, and visibility state. [14][15][16]

The historical overlay behavior is compatible with an OS-managed custom cursor, but that is only an inference. PoE's current behavior should be measured across normal, hovered, clicked, and alternate cosmetic cursor states before choosing an implementation.

## Restoration approaches, ranked

### 1. Windows Magnification cursor visibility: strongest permitted candidate

Microsoft documents `MagShowSystemCursor(FALSE)` as hiding the system cursor immediately, without the reference-count semantics of `ShowCursor`. This API belongs to `Magnification.dll` and is available from Windows 8. It takes no target process or window argument. Its contract is cursor visibility rather than replacement of a particular standard cursor slot. [28]

The proposed sequence is `MagInitialize`, a checked `MagShowSystemCursor(FALSE)` while WASD hiding is wanted, `MagShowSystemCursor(TRUE)` when normal visibility is required, and `MagUninitialize` on shutdown. The overview requires initialization before other magnification calls and warns that a 32-bit magnifier under 64-bit Windows is unsupported. PathOfWASD already publishes for win-x64. [29][30]

**Engineering inference:** because this acts on system-cursor visibility rather than the game's input target or cursor resource, it should be a strong candidate for an OS-managed custom game cursor as well as the standard arrow. It does not need an input-blocking overlay, game-process handle, DLL injection, or shared input queue. A cursor painted into PoE's own rendered image remains outside this mechanism.

The first experiment should initialize the library and toggle only cursor visibility. Do not create a magnifier window, change fullscreen magnification, or transform input coordinates. The reviewed cursor-function documentation does not state a UIAccess requirement; the overview explicitly assigns that requirement to the separate input-transform function. Actual success must still be checked under an ordinary user account. Do not assume elevation or special signing is required, or that privileges will fix a failure. [28][29]

The design tradeoff is desktop-wide visibility. A small service must restore the cursor when WASD is disabled, focus leaves PoE, the pointer goes into an allowed ordinary-interaction state, or the app exits. An independent timeout/watchdog is appropriate for abnormal termination. Its restoration behavior must be tested alongside accessibility software because this visibility API is not a private per-application setting. Do not fight another accessibility tool with a tight loop.

**Assessment:** genuinely worth a bounded prototype and the first approach to test under the external-only constraint. Likely to fit if PoE's cursor is OS-managed; not verified in-game. If it fails with a valid x64 call sequence, record the result and cursor type before expanding implementation.

### 2. Existing game-cursor replacement utility: excluded by process constraint

YoloMouse's official history documents an invisible cursor in its special bundle, an injection system, a separate passive mode that avoids injection, and historical PoE compatibility improvements. These establish a concrete replacement capability and relevant history, not tested compatibility with current PathOfWASD. [17]

This explains an alternative class of cursor-hiding implementation, but it is not a recommended experiment under the external-only constraint. An invisible assignment for only one cursor state would also leave other states potentially visible.

This approach avoids using the old full-screen input surface as the hiding mechanism. However, normal cursor replacement may use code inside the target process. It changes the project's existing non-injection boundary; passive mode must not be assumed equivalent. Technical compatibility also does not establish game-policy approval. [17][18]

**Assessment:** excluded when it operates through process injection. Passive mode is not a demonstrated substitute and is not recommended here as a workaround.

### 3. Replace system cursors without an overlay: simple but narrow fallback

The existing code replaces `OCR_NORMAL` with a blank resource. Microsoft defines `SetSystemCursor` as replacement of a selected system cursor, not arbitrary game cursors. Expanding to other standard slots would still not cover a game-owned cursor. [13][14]

If testing shows that the current game uses the affected system slot in the required states, this is a relatively small implementation. Otherwise, stop: repeatedly replacing more system slots cannot turn this into general custom-cursor interception.

The old helper is not production-ready evidence: it does not check whether cursor creation or replacement succeeds before marking the cursor replaced. Any revived implementation needs valid cursor resources, checked return values, cleanup, and restoration on focus loss and exit. System-wide replacement also needs recovery after abnormal termination.

**Assessment:** useful inexpensive falsification test; weak general solution for a custom game cursor.

### 4. Attach to the game's input thread and adjust visibility: excluded

Calling `ShowCursor(false)` only in PathOfWASD is insufficient as a general cross-process solution. Microsoft explains that the cursor display counter is thread-local, with sharing possible through thread attachment. [16][19]

`AttachThreadInput` therefore provides a documented basis for an external experiment that shares cursor state with the game's window thread and changes visibility without a full-screen overlay. It is not a dedicated cursor-hiding API: it shares focus and keyboard state too, resets key state on attachment, and introduces synchronization between applications. Microsoft separately documents hang hazards from attached input queues. [20][21]

A carefully isolated experiment would need to establish attachment lifetime, behavior on detach, concurrent changes from the game, balanced visibility changes, focus transitions, and exit recovery. Keeping a helper thread attached indefinitely is not an acceptable assumption of reliability; rapidly attaching and detaching on every skill is also suspect given key-state resets.

**Assessment:** excluded because it couples the helper to PoE's input processing even without injecting code. It cannot remove a software-rendered cursor.

### 5. Implement targeted cursor interception inside the game process: excluded

If the game uses Windows cursor resources, intercepting its cursor-selection path and substituting a transparent cursor can address the appearance at its source. Existing cursor-replacement tools provide precedent for this class of solution. It avoids creating a competing full-screen input window. [17]

A custom implementation would still need to handle cursor changes, restore behavior, process lifetime, and compatibility with updates. A software-rendered cursor would require a different rendering-specific intervention. This is a significant scope and support change for PathOfWASD, which advertises an external input approach. [22]

**Assessment:** excluded by the process constraint. Technical possibility does not make this an acceptable solution for this project.

### 6. Smaller cursor-hiding overlay: does not resolve the underlying conflict

A small non-click-through window placed at the real cursor might impose a hidden cursor appearance, but it still sits exactly where the game needs mouse targeting. It may reduce covered area while preserving the click and hover problem. Making it genuinely click-through removes the dependable basis for imposing its cursor policy on the underlying game. [9][15]

Similarly, `WS_EX_NOACTIVATE` addresses activation, not all hit testing. Repeatedly forcing the game to the foreground does not make the overlay stop receiving mouse targeting.

**Assessment:** not a recommended primary approach. Merely shrinking the old overlay or adding a no-activation flag is not a demonstrated fix.

### Other suggestions that do not establish a solution

Repeated `SetCursor(NULL)` calls from the overlay create a contest with normal cursor selection; the API explicitly notes that the window class can restore its cursor on movement. `ShowCursor` is not a global switch. A transparent image does not erase whatever is behind it. [15][16]

Moving the real cursor outside the game is incompatible with this project's use of its screen position as the movement target. Capture tools that omit the cursor change captured output, not necessarily the local live cursor. A full capture-and-redisplay pipeline would be a different architecture with latency and input-routing work; `IsCursorCaptureEnabled=false` alone does not solve this request. [23]

## A required PathOfWASD integration change

Even successful external hiding would expose an existing assumption. `CursorManager.UnlockRealCursor` moves the real cursor to the aiming position and calls `HideOverlay`; the current renderer then hides the virtual cursor unconditionally. If the game cursor is also invisible, there is no visible aiming pointer during skills. [12][24]

A restored feature needs separate decisions for real-cursor visibility and virtual-cursor rendering. For an always-hidden real cursor, the aiming overlay should remain visible during skill use and follow the actual aiming coordinates. Raw input already continues updating cursor positions in the current code, but visible synchronization must still be verified. Ordinary pointer mode, inventory interaction, focus loss, and disabling WASD need an explicit visible-pointer policy.

A useful first target is narrower: hide the movement cursor during WASD, restore the game cursor for skills and ordinary interaction. That reduces visual integration changes but requires reliable, fast hide/show synchronization. `MagShowSystemCursor` is a direct API candidate for those transitions. Test a persistent hidden state first to establish basic feasibility, then test transitions with the current controller sequence.

## Tests that would turn candidates into a defensible answer

1. **Identify the cursor mechanism.** With the game in borderless mode, record `GetCursorInfo` visibility and handle information, inspect the associated image using `GetIconInfo`, and correlate with the visible cursor. Repeat across normal, hover, click, menu, and cosmetic states. Do not rely solely on whether a screenshot contains a cursor. Those APIs supply OS-cursor evidence, not a complete proof about every rendered pixel. [25][26]
2. **Separate focus from mouse targeting.** Compare the old release, hotfix, and current version while recording foreground-window identity, the window under the real cursor, overlay visibility/styles, and whether a click activates the game. Foreground focus remaining on PoE while clicks fail would falsify a focus-only explanation.
3. **Isolate the historical variables.** On an experimental build, vary system-arrow blanking, overlay cursor policy, click-through, and activation protection separately. Do not conclude which variable fixed the issue from the combined hotfix alone.
4. **Test the Magnification visibility API independently.** Use a separate x64 helper with checked initialization, a bounded hide interval, guaranteed normal cleanup, and an independent restore watchdog. Start with a controlled desktop window that uses a custom cursor, then test PoE while PathOfWASD is off. If clicks, hover, and foreground focus remain intact, repeat with PathOfWASD and its aiming-cursor integration. Do not attach to or alter the game process. A successful API return is insufficient without observing the actual display and interactions.
5. **Test realistic transitions.** Movement in eight directions, held attacks, directional movement skills, rapid skill taps, item pickup and drag/drop, inventory, toggling off while keys are held, Alt-Tab, second-monitor travel, and game exit must all work without a lost pointer or stuck input.
6. **Bound compatibility claims.** Record game build, Windows version, renderer, cursor cosmetics, DPI, and display mode. Test Wine separately: the experimental Wine release and native focus work are relevant history, not proof that a Windows hiding method works under Wine. [27]

A candidate passes only if the undesired cursor is absent in the intended states, the aiming cursor remains usable, mouse interactions work, and focus/exit recovery restores normal behavior. The first decisive result should drive implementation; a broad input-controller refactor is not necessary to answer this cursor question.

## Decision

**Is the removal explained?** Yes: maintainer history confirms patch-related overlay breakage and deliberate removal of hiding to recover functionality. The old code explains a strong mouse-targeting/activation failure mechanism. Exact engine internals remain unknown.

**Is hiding impossible without touching PoE's process?** No. `MagShowSystemCursor` is a documented external Windows mechanism and the strongest candidate located. Its fit is conditional on the cursor being OS-managed and the game's presentation path respecting that state.

**Can it be promised within the existing external-only design?** Not before an in-game test. The Magnification API supplies a credible implementation path, not a guarantee about PoE. If the unwanted cursor is rendered into the game image, the reviewed external visibility APIs will not remove it.

**Recommended investment:** characterize the cursor and run a bounded `MagShowSystemCursor` prototype. If it passes, add lifecycle restoration and explicit aiming-cursor visibility behavior. Standard-arrow blanking is a narrower fallback. If both fail for the actual cursor/presentation mechanism, retain visible-cursor behavior rather than restoring the intercepting overlay or modifying the game process.

## Sources

Repository sources are maintained by pathofwasd. Microsoft sources describe the Windows API contracts; they do not certify PoE behavior. Dragonrise Games sources describe YoloMouse capabilities; they do not certify this integration. Web sources were checked September 8, 2026.

1. [PathOfWASD 0.68 release, July 3, 2025](https://github.com/pathofwasd/PathOfWasd/releases/tag/0.68).
2. [PathOfWASD Mirage hotfix, March 8, 2026](https://github.com/pathofwasd/PathOfWasd/releases/tag/1.0.0hotfix).
3. [Cursor hotfix commit, March 8, 2026](https://github.com/pathofwasd/PathOfWasd/commit/7cecb72b4a2e507281ab2c55562c25e155811610).
4. [Native overlay implementation commit, March 9, 2026](https://github.com/pathofwasd/PathOfWasd/commit/e2d43dd0758593a6288af99c6572cc27f5302506).
5. [PathOfWASD 3.28.0 release, March 9, 2026](https://github.com/pathofwasd/PathOfWasd/releases/tag/3.28.0).
6. [Public issue inventory](https://api.github.com/repos/pathofwasd/PathOfWasd/issues?state=all&per_page=100).
7. [GGG: Content Update 3.28.0 — Path of Exile: Mirage](https://www.pathofexile.com/forum/view-thread/3913392/filter-account-type/staff).
8. [Historical overlay XAML before the hotfix](https://github.com/pathofwasd/PathOfWasd/blob/fd2448cdfb5a2cf49f08418e5661b67bea194400/PathOfWASD/Overlays/Cursor/CursorOverlay.xaml) and [code-behind](https://github.com/pathofwasd/PathOfWasd/blob/fd2448cdfb5a2cf49f08418e5661b67bea194400/PathOfWASD/Overlays/Cursor/CursorOverlay.xaml.cs).
9. [Microsoft: Window Features, Layered Windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows).
10. [Microsoft: Window.ShowActivated](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.showactivated?view=windowsdesktop-9.0).
11. [Current NativeCursorWindow](https://github.com/pathofwasd/PathOfWasd/blob/d9c96f80502d6de1498c50ead5ef264232c95aa7/Overlays/Cursor/NativeCursorWindow.cs).
12. [Current NativeCursorOverlay](https://github.com/pathofwasd/PathOfWasd/blob/d9c96f80502d6de1498c50ead5ef264232c95aa7/Overlays/Cursor/NativeCursorOverlay.cs).
13. [Current SystemCursorManager](https://github.com/pathofwasd/PathOfWasd/blob/d9c96f80502d6de1498c50ead5ef264232c95aa7/Managers/SystemCursorManager.cs).
14. [Microsoft: SetSystemCursor](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setsystemcursor).
15. [Microsoft: SetCursor](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setcursor).
16. [Microsoft: ShowCursor](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-showcursor).
17. [Dragonrise Games: YoloMouse Update Log](https://dragonrisegames.com/yolomouse/log), especially invisible cursor in 1.16.2, passive mode in 1.10.1, injection work in 1.18.0, and historical PoE support in 0.8.0.
18. [Dragonrise Games: YoloMouse help](https://dragonrisegames.com/yolomouse/help).
19. [Raymond Chen, Microsoft: What was the ShowCursor function intended to be used for?, December 17, 2009](https://devblogs.microsoft.com/oldnewthing/20091217-00/?p=15643).
20. [Microsoft: AttachThreadInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-attachthreadinput).
21. [Raymond Chen, Microsoft: I warned you: The dangers of attaching input queues, August 1, 2008](https://devblogs.microsoft.com/oldnewthing/20080801-00/?p=21393).
22. [Current PathOfWASD README](https://github.com/pathofwasd/PathOfWasd/blob/d9c96f80502d6de1498c50ead5ef264232c95aa7/README.md).
23. [Microsoft: GraphicsCaptureSession.IsCursorCaptureEnabled](https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture.graphicscapturesession.iscursorcaptureenabled?view=winrt-26100).
24. [Current CursorManager](https://github.com/pathofwasd/PathOfWasd/blob/d9c96f80502d6de1498c50ead5ef264232c95aa7/Managers/Cursor/CursorManager.cs).
25. [Microsoft: GetCursorInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorinfo).
26. [Microsoft: GetIconInfo](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-geticoninfo).
27. [PathOfWASD experimental Wine release, March 8, 2026](https://github.com/pathofwasd/PathOfWasd/releases/tag/1.0.2_Experimental).
28. [Microsoft: MagShowSystemCursor](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magshowsystemcursor).
29. [Microsoft: Magnification API Overview](https://learn.microsoft.com/en-us/windows/win32/winauto/magapi/magapi-intro).
30. [Microsoft: MagInitialize](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-maginitialize).
