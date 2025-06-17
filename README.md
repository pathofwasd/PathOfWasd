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

## 📢 Status

- This is the **first public build**, uploaded immediately after initial development.
- It has **not been tested** on any machine other than the developer's.
- Expect bugs, crashes, or unexpected behavior.
- Performance and compatibility optimizations will come later.

---

## 📖 Documentation & Support

- Full instructions and tutorials will be added soon.
- AppData\Local\PathOfWASD you can replace the cursor here if you want. Ex: C:\Users\someuser\AppData\Local\PathOfWASD
- IMPORTANT: if you add a custom cursor make sure the file name is "cursor" and is a png, also make sure to remove the old one.
- Midpoint is the middle of your screen with a slight offset, as the "middle" of your character in POE is slightly under the screens true middle (atleast for my monitor?).
- But in the case your programs calculated midpoint is wack, you can set your own midpoint.
- Your midpoint is what will dictate your movement locations while pressing WASD, if it is not centered well, A and D may drift slightly up or down.
- Movement Offset is how far your mouse travels and clicks while pressing W, A, S, or D. 100 was the sweet spot for my monitor, may differ for you.
- The larger the offset, the further you will travel after quickly tapping a WASD key. Generally you want the lowest value you can get while still moving while holding WASD.
- The delays are more advanced of features.. Play around with them if you want, I will make a tutorial that goes more indepth on them later. I found those defaults to be the sweet spot.
- The delays are there for seamless transitions between moving and using a skill.
- For "After Mouse Jump Delay" there is a slight delay before your real mouse (which is hidden), is teleported to the virtual one.
- This allows for skills to not performed in the WASD real cursor position (somewhere close to the middle) before moving to the virtual mouse (where you were aiming when pressing the skill).
- For "Before Move Delay" is a delay for how long to wait after pressing a skill to allow movement. So if you tap a skill, it will wait a small delay, before trying to moving in your held down WASD direction.
- Some skills will require more or less of a delay than others, later down the road I want to add the ability to add a custom delay for each skill binding because of this.
- For now you can either have no delay and always hold you skill until the skill performs its action, or have a small one size fits all delay, which I found to be 100ms.
- Movement key is the key walk or move is bound to on your skill bar
- Stand key is the set key in the games bindings for stopping movement while attacking.
- For now, you can:
  - 🛠 Submit a bug or issue [here on GitHub](../../issues)
  - 💬 Contact me directly on Discord: `bingtar`

---

![image](https://github.com/user-attachments/assets/bb57edcf-f5be-4d17-b642-a4aeb260f12b)


## 📝 Notes

- This app is *not affiliated with or endorsed by Grinding Gear Games*.
- Use at your own risk. It does not modify game files or memory, but as with any overlay tool and tool that semi does automated clicks, caution is advised.
- I have not had a lot of time to test this, so if you do weird things or try to break the app, it will likely break.
- Yes I will open source eventually. The code is just a mess and rushed. I did do lots of refactoring, but need to do more. It was my first WPF app.
- If you really really want the source code and I don't have it up yet and it's been a while (in the case I have no time and this starts to slip), then message me on discord and I will just ago ahead and add the source code.
- Another reason I don't want to add the source code immediately is just in the case that GGG just really hates this app and wants it gone, then I can remove the repo and they can sig detect current builds. Doubt this will be necessary or happen. But I got a lot of hate for this app idea by POE players lol.. So use at your own risk
- Some settings may not be self explainitory, if you have questions, message me on discord, I will try to find time to post a tutorital this weekend. I have lots of work. Enjoy!

---

## 💡 Future Plans

- TBD
---

Stay tuned, and thanks for helping test **Path of WASD**!
