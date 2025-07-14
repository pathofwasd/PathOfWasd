# Path of WASD

**Path of WASD** is a WPF-based overlay application that enables **WASD movement** in *Path of Exile (PoE)*.

Version 1.0.0 Released! 

<img width="586" height="1103" alt="image" src="https://github.com/user-attachments/assets/268e6d34-9850-4053-b91f-1438d40c7615" />

---

## Join the Discord!
- [Invite link](https://discord.gg/GDkyXtkDzc)
- Best place for updates released for Path of WASD.
- This is a great place to ask questions or submit bugs. I try to be as responsive as possible.

---

## What It Does

- Runs as a transparent overlay using WPF.
- Offers a settings UI to customize keybindings and modes.
- Supports mouse-based aiming alongside keyboard movement.

For a better view of all the features, watch the config video demo
https://www.youtube.com/watch?v=IaNiWAB-JWI

---

## Requirements

- Windows 10 or newer
- [.NET 6 or later runtime](https://dotnet.microsoft.com/en-us/download/dotnet) installed (if not using the self-contained version)
- Path of Exile installed (Steam or standalone)
- That's it!

---

## Getting Started

1. Download the latest `.exe` file from the [Releases](../../releases) tab.
2. Run the application. (It does **not** require installation.)
3. Launch Path of Exile.
4. Use the settings menu to bind movement keys and toggle overlay options.
5. Hover over text in the overlay for a better explaination as to what certain settings do and how they function.

> First-time setup might require running the app as Administrator to ensure overlay functionality.

---

## Important things to keep in mind while using Path of WASD
- Game MUST be in Fullscreen BORDERLESS WINDOWED to work..
- VSync enabled or on auto.
- WASD with menus open will not work well, meaning if you open your inventory and while using WASD to move, weird movements will occur and may not work well. Use hold toggle/toggle WASD mode while in game menus, I go more in depth on this down below.

  **Disclaimer:** This is a third party application and is not affiliated with Grinding Gear Games. Use at your own risk. I am not responsible for any bans or issues that may arise from using this application.
  
---

## How Path of WASD works 
- Path of WASD **DOES NOT** read or write to game memory, it does not modify or adapt the game client or its data, it does not mess with the path of exile process in ANY way.
- Path of WASD **IS NOT** not a mod similar to how the Grim Dawn WASD application is a mod. This **DOES NOT** modifiy any game files.
- Path of WASD purely simulates WASD movement with your mouse while creating a simulated virtual cursor used for aiming skill while the program hijacks your real mouse location and movement to dictate your WASD movement direction, and then on skill use, it teleports your real mouse to your virtual mouse, once done with skills it will auto resume movement direction with real cursor.
- I have gone through great efforts to have this functionality seamless and "behind the scenes", but rest assured, there is no magic or crazy modifications happening, just very detailed specific logic to control your mouse and keyboard actions to simulate WASD movement in point and click games.
- In theory this app would work for any other point and click games that are similar to POE, but this application is specifically fine tuned with POE mechanics and controls in mind.

---

## Can this get me banned?
- This is a third party application and is not affiliated with Grinding Gear Games. Use at your own risk. I am not responsible for any bans or issues that may arise from using this application.
- With that being said, this software is most similar to what would be called a "macro".
- The most commonly cited rule pertaining to macros is "one action per key press."
- Does this app do more than one action per key press?
  - Depends on what you mean by "action"
  - Multiple actions would be automating a sequence of events
      - Popping a flask and logging out, multiple action, BANNABLE
      - Using a timer to do something at a specific interval, having a time based macro that automatically uses a skill ever X seconds, BANNABLE
      - Something that teleports mouse to inventory squares and stashes everything into stash automatically, BANNABLE
      - Using multiple flasks in 1 button, BANNABLE

  -Can I use a macro for attacking in place? (Path of WASD does this)
    -GGG has given this type of macro a reluctant approval.
    Sources; [Email to GGG](https://imgur.com/SVXm1td)
  
- What Kind of actions Path of WASD does:
  1. When pressing W, A, S, or D it will move your mouse to X amount of pixels in the direction of UP(W), DOWN(S), RIGHT(D), LEFT(A) and then press your movement key.
  2. When using a skill, it will teleport your mouse to your virtual mouse (which you control yourself), hold attack in place. (and then you will use the skill as you are pressing the key to do so)
  3. Supports binding mouse click to keyboard press (singular) (almost certain this is fine)
  4. It does more things, but this is the core stuff it does that is "macroing"
 
- The honest truth? Is this bannable? Yes or no?
  - I have no idea, it definitely is in a grey area if you go by the specific exact rules.
  - You are controlling your mouse via your keyboard, its not doing multiple automatic location clicks in areas to perform actions to optimize gameplay.
  - You are controlling where your mouse teleports to... with your mouse.
  - Everything you do in Path of WASD is under YOUR control, even if it may technically be doing "multiple actions", which I cant say it is or isnt.
  - Why am I unsure if it is multiple actions? Because if an action is moving, using a skill, thats all the app really does and you technically controll that, the app just handles how these actions happen in a way that simulates WASD gameplay
 
- Have I tried to reach out to GGG for approval?
  -No. Because I knew the response would be generic, "Use at your own risk"
  -Someone in the Path of WASD discord has reached out to GGG and their response was this: 
      "I would advise against using any programs that interact with the game client to provide an advantage over other players, as this may result in your account being permanently banned. I'm afraid, due to the dynamics of our policies, we're unable to guarantee if a tool is allowed or would remain allowed in the future. If you're unsure if a tool falls within these guidelines, we'd suggest refraining from using such a tool."

- There exists a similar app to mine but is for controller support, it is by bennybroseph
  - Benny's app has been used by thousands and no one has complained about a ban, but the app was for POE and D3, so I am unsure how much POE use it got.
  - [Benny's app](https://github.com/bennybroseph/AutoHotKey_Scripts)

- With all of this said, if ANYONE has a deep connect with a high up at GGG and wants to show them the source code for this or Video demo and get a specific YES or NO, that would be much appreaciated.
I would not personally use this on an account with 100s or 1000s of dollars of MTX or rare items (I say that as I developed and tested this whole app on my account that has POE2 with like 300 dollars of MTX lol..), but if you refuse to play POE without WASD and don't have anything and want to try the game out, this app is perfect.

**Disclaimer:** This is a third party application and is not affiliated with Grinding Gear Games. Use at your own risk. I am not responsible for any bans or issues that may arise from using this application.

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

- If you're unsure, feel free to scan the file on [VirusTotal](https://www.virustotal.com/).

- If you are really skeptical feel free to check out the code here and compile the program yourself, I will not provide support for instructions on how to compile and release your own app. 

---

### Submit Bugs:
  - 🛠 Submit a bug or issue [here on GitHub](../../issues)
  - Join the discord server and report the bug or issue there. [Discord Invite](https://discord.gg/GDkyXtkDzc)

---

Stay tuned, and thanks for using **Path of WASD**!
