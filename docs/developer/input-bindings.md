# Arrow movement and Mouse 4/5 Alt Click

Development build; automated checks pass, but PoE validation is still required before release. Existing settings retain WASD and have both new mouse options disabled.

## Usage

1. Turn movement mode off.
2. Under **Mapped Mouse Alt Keys**, enable **Mouse 4 (Back)** and/or **Mouse 5 (Fwd)**.
   Hover an underlined label for help; Mouse 4 is the standard Back mouse input and Mouse 5 is Forward. If mouse software remaps a physical button to a keyboard key or another action, restore its Back/Forward assignment to use the corresponding option.
3. Click the corresponding key box and press the keyboard key assigned to the desired skill in PoE. A separate Skill Keys entry is not required.
4. Save/apply and enable movement mode.

Without the configured **Hold Toggle Mouse Alt Key**, an enabled button sends a normal Mouse 4/5 click. Holding the modifier makes it send the chosen keyboard key. **Reverse Mouse Alt Key Functionality** swaps these modes, as it does for Left/Right/Middle Alt Click. Output type and key are captured on button-down, so releasing the modifier mid-hold still releases the correct output. The app does not automatically synthesize an Alt+key chord.

Disabled buttons and new presses while movement mode is off pass through normally. These are aimed mouse-alt mappings, independent of any directional Skill Keys entry using the same keyboard key. Default key values are NumPad4/NumPad5, but each option is disabled until enabled by the user.

Under **Movement keys**, **Use arrow keys instead of WASD** selects arrow movement. Arrows retain midpoint-adjustment behavior when movement mode is off. This feature does not add MMO keypad or arbitrary movement-key mapping.

## Implementation

`MovementBindings` translates physical arrows to existing logical W/A/S/D directions and adjusts the physical held-key check. No extra movement key is injected.

The existing low-level mouse hook decodes XBUTTON1/XBUTTON2. Enabled buttons enter the existing mapped-click controller pipeline, using two distinct controller slots (CrSel/EraseEof). These slots are registered as aimed skills without consuming any of the nine keyboard skill slots. Normal output uses Windows SendInput XBUTTON events; negative internal markers distinguish these from real keyboard keys and are never emitted as keyboard input. Injected mouse events bypass remapping.

`SkillInputSources` tracks emitted output ownership so keyboard and side buttons sharing a key cannot release each other's hold. Each side press has its own owner, including rapid release/repress sequences. Captured button-ups are consumed after mode-off to match previously swallowed downs. Cleanup releases emitted outputs; generation checks prevent stale asynchronous completions from emitting after rebinding. Existing left/right/middle callback mappings retain their prior path.

Directional skill suspension also releases/restores a held side output. Restoration requires the same physical press to remain held. No polling timer, mouse-movement work, game-process access, or new hook is added.

Changes to movement layout or side mappings require movement mode off. Validation checks the selected movement keys across all skill rows (including directional skills), shortcuts, and enabled mouse mappings. Switching from arrows to WASD with W/A/S/D assigned, or the reverse with arrows assigned, shows a live warning naming every conflicting field and key. Bindings remain untouched; the user can reassign them or select the previous layout again. Save/Apply refuses conflicts. Movement activation validates and synchronizes the entire edited configuration before locking the cursor, so skipping Save cannot bypass validation. Legacy conflicting settings show the same warning without being rewritten. Disabled side mappings retain their keys and are checked again when enabled. Conflicts with active movement keys, app shortcuts, other mouse-alt keys, and internal routing keys are rejected. Side targets may share existing keyboard skill keys or each other. The two new key boxes do not participate in the old auto-swap mechanism, avoiding silent changes to existing settings when defaults load.

Controller queue policy, skill hold delays, and movement delays are unchanged. Existing controller timing limitations are not claimed fixed by this feature.

## Verification

`tests/InputBindings` checks legacy settings, cloning, equality, JSON, Save/Apply/Reset, key capture, conflicts, and all held-direction combinations. It exercises the mouse-hook adapter and actual hotkey pipelines with recorded keyboard/mouse output and a recorded controller. Coverage includes both buttons, the modifier/reverse truth table, modifier changes while held, shared outputs, rapid re-press during pending release, mode-off cleanup, stale completions, directional suspension/restoration, and arrow-to-logical-W delivery. It does not reproduce PoE or the complete controller timing behavior.

```powershell
dotnet run --project tests/InputBindings/InputBindings.Tests.csproj -p:ManagePackageVersionsCentrally=false -p:RestorePackagesWithLockFile=false -v:q
```

Optional `-- --render` loads the real settings XAML/styles without app startup or input services, verifies the enable controls and key capture/save bindings, and writes `artifacts/input-bindings/settings-preview.png`. Run from the repository root.

The existing cursor visibility and native cursor regression suites also pass. In-game acceptance should cover:

- Both buttons disabled, then each enabled: normal clicks and configured keys with the modifier.
- Reverse mode, modifier changes mid-hold, channeling and rapid clicks.
- Simultaneous keyboard/side holds, directional skills while moving, and release before directional restoration.
- Movement mode off while holding, focus changes, and no stuck keys/buttons afterward.
- Original Left/Right/Middle Alt Click and cursor hiding.
- Default WASD and optional arrows, including diagonals and midpoint adjustment while movement mode is off.
