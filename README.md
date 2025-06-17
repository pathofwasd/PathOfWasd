# Path of WASD

**Path of WASD** is a WPF-based overlay application that enables **WASD movement** in *Path of Exile (PoE)*. This is an early release aimed at getting feedback from the community. The tool is primarily for players who want a more traditional action RPG control scheme using keyboard movement.

> ⚠️ This is an experimental build. Expect bugs and unpolished behavior.

---

## 🔧 What It Does

- Enables **WASD movement** in Path of Exile (tested on PoE 3.26).
- Runs as a transparent overlay using WPF.
- Offers a minimal settings UI to customize keybindings and modes.
- Supports mouse-based aiming alongside keyboard movement.

---

## 📦 Requirements

- Windows 10 or newer
- [.NET 6 or later runtime](https://dotnet.microsoft.com/en-us/download/dotnet) installed (if not using the self-contained version)
- Path of Exile installed (Steam or standalone)

---

## 🚀 Getting Started

1. Download the latest `.exe` file from the [Releases](../../releases) tab.
2. Run the application. (It does **not** require installation.)
3. Launch Path of Exile.
4. Use the settings menu to bind movement keys and toggle overlay options.

> 🧩 First-time setup might require running the app as Administrator to ensure overlay functionality.

---

### ⚠️ Antivirus Warning / False Positives

- **This program may get flagged by antivirus software or Windows Defender.**

- This is expected due to the way the app works — it’s a standalone `.exe` that uses **low-level Windows API hooks** to:
  - Monitor key presses and mouse input
  - Simulate movement
  - Intercept or redirect cursor actions

- These behaviors are common in malware, which is why some antivirus tools may flag it **even though it’s harmless**.

- Rest assured:
  - The program does **not** access the internet, modify system files, or install anything.
  - It does **not** modify the Path of Exile game files or memory.
  - The code is local and will be open-sourced when I have time to clean it up.

- If you're unsure, feel free to scan the file on [VirusTotal](https://www.virustotal.com/) or wait for the open-source release.

- If you are really skeptical feel free to use dnSpy and reverse engineer the code. It's .NET and not obfuscated. But I mention below I will open source in due time. 

---

## 📢 Status

- This is the **first public build**, uploaded immediately after initial development.
- It has **not been tested** on any machine other than the developer's.
- Expect bugs, crashes, or unexpected behavior.
- Performance and compatibility optimizations will come later.

---

## 📖 Documentation & Support
## Instructions (More tutorials coming soon)

## MOST IMPORTANT THING
- Game MUST be in Fullscreen BORDERLESS WINDOWED to work..
- WASD with menus open will not work well, meaning if you open your inventory and while using WASD to move, weird movements will occur and may not work well. Use hold toggle/toggle WASD mode while in game menus, I go more in depth on this down below.

- Full instructions and tutorials will be added soon.

### Custom Cursor

- You can replace the cursor by placing a file at:  
  `AppData\Local\PathOfWASD`  
  Example: `C:\Users\someuser\AppData\Local\PathOfWASD`

- **IMPORTANT:**  
  If you add a custom cursor:
  - The file name **must be** `cursor.png`
  - It **must be a PNG file**
  - You **must delete** or replace the old one

---

### Midpoint

- The *Midpoint* is the center of your screen with a slight offset — the "middle" of your character in Path of Exile is slightly below the true screen center (at least on my monitor).
- If the calculated midpoint seems off, you can manually set your own.
- The midpoint determines the origin for your WASD movement — if it's not centered well, pressing A or D may cause a slight upward/downward drift.

---

### Movement Offset

- *Movement Offset* controls how far the mouse travels and clicks when pressing W, A, S, or D.
- A value of `100` worked well for my monitor, but it may vary for yours.
- A higher offset = further distance traveled with each tap.
- Generally, you want the **lowest value** that still allows smooth movement while holding WASD.

---

### Hold Toggle WASD Mode

- This is actually a really important keybind. By default, it's set to a weird key (I use Scroll Lock bound to a custom mouse button), but **you’ll want to change it to something you can press easily**.

- This key temporarily **disables WASD mode while held**, then **re-enables it when released**.  
  It’s essential for quickly exiting WASD mode to pick up loot, interact with the UI, or perform other actions with your real cursor.

- For longer actions, you can still use the full **Toggle Off** option, but this hold-toggle is ideal for brief interruptions.

---

### Delays (Advanced)

- These settings help smooth the interaction between movement and using skills.
- I’ll provide a full tutorial soon, but here’s a basic explanation:

#### After Mouse Jump Delay
- Adds a delay **before** the real mouse (hidden) is teleported to the virtual mouse.
- This lets the skill cast from the real mouse’s position (near center) **before** snapping to the virtual one (your cursor target).

#### Before Move Delay
- Adds a delay **after** casting a skill **before** movement resumes via WASD.
- Helps avoid interrupting skill animations by moving too early.

- Different skills may require different timing. Eventually, I want to allow **per-skill custom delays**.
- For now:
  - **Option 1:** No delay (hold each skill key until it activates).
  - **Option 2:** Use a universal delay — I found `100ms` works well.

---

### Key Bindings

- **Movement Key:**  
  The key your *Walk* or *Move* skill is bound to on your skill bar.

- **Stand Key:**  
  The key in Path of Exile's game settings that stops movement while attacking.

### 💡 Tips

- You may notice that many of the default keybinds — aside from the main skill keys — are set to keys that don’t have much of an effect when pressed or held down normally, like Scroll lock or the F keys.  
  This was intentional.

- Feel free to change the keybinds to whatever works best for you, but the more you use **passive keys** (keys that don’t do much outside the program or game), the more likely the app will run smoothly 24/7 without issues or annoyances.

- There may still be a few bugs with keys getting "stuck" down. I did my best to fix these, but some might have slipped through.
  
### For now, you can:
  - 🛠 Submit a bug or issue [here on GitHub](../../issues)
  - 💬 Contact me directly on Discord: `bingtar`

---

![image](https://github.com/user-attachments/assets/bb57edcf-f5be-4d17-b642-a4aeb260f12b)


## 📝 Notes

- ⚠️ **Not affiliated with or endorsed by Grinding Gear Games.**

- ⚙️ **Use at your own risk.**  
  This app does **not** modify game files or memory, but like any overlay or tool that performs semi-automated clicks, caution is advised.

- 🧪 **Limited Testing:**  
  I haven’t had much time to test this. If you do weird things or try to break the app, it will probably break.

- 🧼 **Code Quality & Open Source Plans:**  
  I’ll open-source the project eventually. The code is a rushed mess — it’s my first WPF app. I’ve done a lot of refactoring, but there’s more to do.

- 📨 **Want the code early?**  
  If I haven’t uploaded it yet and it’s been a while, feel free to message me on Discord. I’ll release it manually if needed.

- 🔒 **Why the delay on source release?**  
  Just in case GGG ends up disliking the app and asks for a takedown, I can remove the repo and reduce the chance of signature detection on current builds.  
  (This is probably unlikely, but I’ve already received a lot of hate from some PoE players — so again, **use at your own risk**.)

- 🤷 **Not revolutionary tech:**  
  It was a bit of a pain to make, but not too bad. If the idea becomes popular, someone else will likely create their own version — which might help keep GGG more relaxed about this.

- 🐛 **If it blows up and bugs pile up...**  
  I might not have time to fix everything. If that happens, I’ll just open-source it. Screw it — lol.

- 🎮 **Controller Support Idea:**  
  I’d *love* for someone to take this concept and make it work for controllers. My code could help, but it needs to be cleaned up first to be usable by a community.

- ❓ **Need Help?**  
  Some settings might not be self-explanatory. If you're confused, message me on Discord. I’ll try to post a full tutorial this weekend — I just have a lot of work.  
  **Enjoy!**


## 💡 Future Plans

- TBD
---

Stay tuned, and thanks for helping test **Path of WASD**!
