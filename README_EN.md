![](Documents/debugx.png)

<p align="center">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/blurfeng/debugx?color=blue">
  <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/blurfeng/debugx/total?color=green">
  <img alt="GitHub Repo License" src="https://img.shields.io/badge/license-MIT-blueviolet">
  <img alt="GitHub Repo Issues" src="https://img.shields.io/github/issues/blurfeng/debugx?color=yellow">
</p>

<p align="center">
  🌍
  <a href="./README.md">中文</a> |
  English |
  <a href="./README_JA.md">日本語</a>
</p>

<p align="center">
  📥
  <a href="#using-upm">Install</a> |
  <a href="#download-the-package">Download</a>
</p>

# Debugx - Unity Debug Log Management Plugin
Debugx is a debug-logging extension plugin for `Unity`, ready to use out of the box. It lets you categorize and manage `Debug.Log` output by **debug member** (a developer or feature module), and write logs to local files.  
In multi-developer projects, having everyone call `UnityEngine.Debug.Log()` makes logs hard to tell apart and manage; and when testing your own feature, you don't want to be disturbed by other people's logs. Debugx uses **member categorization + multi-level switches** so everyone focuses only on their own logs, without interfering with each other.  
All print methods are controlled by the `DEBUG_X` macro: add the macro to enable them; remove it when shipping and every log call is stripped at **compile time**, achieving zero runtime overhead and zero residue in Release builds.  
With auto-generated member-specific methods (e.g. `DebugxLogger.LogBlur("...")`) and the wrapped `DebugxLog.dll`, you can print without memorizing Keys, while double-clicking a console log jumps straight to your business call site instead of into the plugin internals.  
In addition, Debugx has a built-in **Debugx Console** log viewer (an editor window + an in-game runtime overlay) that lets you filter and view logs per member, replacing your reliance on Unity's native Console.

![](Documents/overview.png)

## 📜 Table of Contents
- [Introduction](#introduction)
  - [Features](#features)
- [💻 Requirements](#-requirements)
- [🌱 Quick Start](#-quick-start)
  - [1.Install the Plugin](#1install-the-plugin)
  - [2.Add the DEBUG_X Macro](#2add-the-debug_x-macro)
  - [3.Configure Debug Members](#3configure-debug-members)
  - [4.Print Logs in Code](#4print-logs-in-code)
- [⚙️ Configuration Guide](#-configuration-guide)
  - [Configuration UI & Tooltips](#configuration-ui--tooltips)
  - [ProjectSettings](#projectsettings)
  - [Preferences](#preferences)
- [✍️ Printing Logs in Code](#-printing-logs-in-code)
  - [Print Methods](#print-methods)
  - [Preset Members & Keys](#preset-members--keys)
  - [Runtime Switches](#runtime-switches)
- [🎛️ Debugx Console](#-debugx-console)
  - [Editor Window](#editor-window)
  - [Toolbar & Filtering](#toolbar--filtering)
  - [List & Detail Panel](#list--detail-panel)
  - [Editor Panel](#editor-panel)
  - [In-Game Runtime Overlay](#in-game-runtime-overlay)
- [🧩 DebugxManager](#-debugxmanager)
- [⚠️ Notes](#-notes)

## Introduction
With Debugx, in multi-developer projects you can categorize and centrally manage logs per debug member, avoiding everyone's `Debug.Log` getting mixed together and hard to distinguish.  
Debugx provides configuration UIs in both `ProjectSettings` and `Preferences`: settings in `ProjectSettings` affect the **entire project**; user settings in `Preferences` affect **only your local environment** and never impact the project or other developers. In addition, `Debugx Console` is a built-in log viewer (an editor window + an in-game runtime overlay) for viewing and filtering logs and managing print switches at project **runtime**.  
All business-facing print methods are marked with `[Conditional("DEBUG_X")]`, so without the `DEBUG_X` macro these calls are stripped entirely at compile time and incur no runtime overhead.

### Features
| Feature | Description |
| --- | --- |
| Member-based logging | Categorize prints per "debug member" (developer / module); each member has its own switch, signature and color — logs are clear and non-interfering. |
| Three-level switches | Project level (`ProjectSettings`), local user level (`Preferences`) and runtime level (`DebugxConsole` / code) — combine them freely. |
| One-macro on/off (DEBUG_X) | All print methods are marked `[Conditional("DEBUG_X")]`; remove the macro and every log call vanishes at compile time — zero overhead, zero residue in Release. |
| Auto-generated member methods | Member-specific methods such as `DebugxLogger.LogXxx()` are generated from your member config — call `LogBlur("...")` without memorizing Keys. |
| Precise stack navigation | Core code is wrapped in `DebugxLog.dll`; combined with the `Logger` naming and `[HideInCallstack]`, double-clicking a console log jumps straight to the business call site instead of the plugin internals. |
| Local log output | Logs are recorded to local files at runtime: the editor writes to the project `Logs/`, and each platform writes to its own directory; per-level stack traces, whether to record non-Debugx logs, etc. are configurable. |
| Rich print options | Supports timestamp, network tag (Server / Client), color, signature and Header; provides `Log` / `LogWarning` / `LogError`. |
| Built-in log console | The `Debugx Console` log viewer comes in two forms — an **editor window** and an **in-game runtime overlay**: multi-dimensional filtering by member / type / search, collapse & dedup, timestamps, stack navigation and compile-log mirroring — replacing your reliance on the native Console. |
| Editor-friendly | Integrated into `ProjectSettings` and `Preferences` with Tooltips on fields; adapts to Dark / Light skins; the UI switches between Chinese and English by system language. |

## 💻 Requirements
- `Unity 2022.3` or newer (older versions are untested).
- You must add the `DEBUG_X` macro to your project to enable the features (see [2.Add the DEBUG_X Macro](#2add-the-debug_x-macro)).
- No third-party dependencies.

## 🌱 Quick Start
Install the plugin whichever way you prefer, then follow the steps below to add Debugx to your project.

### 1.Install the Plugin
#### Using UPM
Install via UPM (Unity Package Manager):
```
https://github.com/BlurFeng/Debugx.git?path=Assets/Plugins/Debugx
```
1. Copy the link above.
2. Open `Window -> Package Manager`.
3. Click the `+` button in the top-left corner and select `Add package from git URL...`.
4. Paste the link and click `Install` to add the plugin to your project.

#### Download the Package
Download the latest `.unitypackage` from the [Releases](https://github.com/blurfeng/debugx/releases) page, then import it into your project.

### 2.Add the DEBUG_X Macro
You must add the `DEBUG_X` macro to your project to enable log printing. Add `DEBUG_X` under `Project Settings -> Player -> Other Settings -> Scripting Define Symbols`.  
When shipping, remove the `DEBUG_X` macro to quickly disable all Debugx features (the related calls are stripped at compile time).  
![](Documents/qs_macro_1.png)

### 3.Configure Debug Members
Open `Editor -> Project Settings -> Debugx` and configure members under **Debug Members**.  
Each member has a unique `Key`, `Signature` (name), color, switch and other properties. **The most important one is the member's `Key`**, which is used when printing logs — each member only needs to remember their own `Key`.  
After saving, Debugx **auto-generates** a dedicated print method for each member (see [4.Print Logs in Code](#4print-logs-in-code)).  
![](Documents/qs_member_1.png)

### 4.Print Logs in Code
Now you can print logs in code. You can use **member-specific methods** (no need to memorize Keys) or the **generic methods** (which take a Key):

```csharp
using DebugxLog;

// Member-specific methods (recommended, no need to memorize Keys). Method names are generated from the member's Signature.
DebugxLogger.LogBlur("Hello from Blur.");
DebugxLogger.LogWarningBlur("Something looks off.");
DebugxLogger.LogErrorBlur("Something went wrong.");

// Generic methods (require the member Key).
Debugx.Log(1, "Hello from key 1.");
Debugx.LogWarning(1, "Warning from key 1.");
Debugx.LogError(1, "Error from key 1.");
```

> [!TIP]
> The `DebugxLogger` class is **auto-generated** from your member config. If `DebugxLogger` isn't generated after updating the plugin, or a newly added member has no method, use the menu `Tools -> Debugx -> Regenerate DebugxLogger Class` to force regeneration.

> [!TIP]
> At this point Debugx already works. To learn more about the configuration and usage, continue reading the [Configuration Guide](#-configuration-guide) and [Printing Logs in Code](#-printing-logs-in-code) below.

## ⚙️ Configuration Guide
Debugx configuration lives in two places: `ProjectSettings` (affects the entire project) and `Preferences` (affects only your local environment). The key options are covered below; hover over any field to see its Tooltip for more details.

### Configuration UI & Tooltips
Hovering over a field shows a Tooltip, which helps you get familiar with Debugx. Since detailed descriptions are available via Tooltips, they aren't repeated one by one here.  
![](Documents/cfg_tooltip_1.png)

### ProjectSettings
Open `Editor -> Project Settings -> Debugx`. Project settings affect the entire project — configure here when you need to add debug members or adjust global default behavior.  
![](Documents/cfg_projectsettings_1.png)

#### Toggle Settings
Default values for the various global switches. The master switches are shown here, and each debug member can also set its own switch in the member info. Main options:
- `enableLogDefault`: default value of the master log switch. When off, no member logs are printed.
- `enableLogMemberDefault`: default value of the member-log master switch.
- `allowUnregisteredMember`: whether members that aren't registered (no matching Key / signature) may print.
- `logThisKeyMemberOnlyDefault`: print logs only for a specific Key member; `0` disables this filter.

![](Documents/cfg_toggle_1.png)

#### Member Settings
Member settings configure the debug members. There are some **preset members** (see [Preset Members & Keys](#preset-members--keys)) that cannot be deleted and can only be edited in a limited way. You can add your own configs under **custom members**, distinguished per project user.  
Main properties per member:
- `Key`: the member's unique identifier, used when printing. **Each member only needs to remember their own Key.**
- `Signature`: the name, also used to generate the `DebugxLogger` method name (e.g. `Blur` -> `LogBlur`).
- `Color`: the log color, to quickly tell members apart in the console.
- `Header`: an optional log prefix label.
- `EnableDefault`: the default switch for this member's logs.

![](Documents/cfg_member_1.png)

#### LogOutput
Log output starts recording when the project starts running, and stops and writes to a local file when the project stops. Main options:
- `logOutput`: whether to output logs to a local file.
- `enableLogStackTrace` / `enableWarningStackTrace` / `enableErrorStackTrace`: whether to record stack traces for the Log / Warning / Error types respectively.
- `recordAllNonDebugxLogs`: whether to record all logs not printed by Debugx.

Where log files are written:
- **Editor**: the `Logs` folder in the project root.
- **Release builds**: stored in a platform-specific directory. On PC this is usually `C:\Users\UserName\AppData\LocalLow\CompanyName\ProductName`; on mobile it's the corresponding persistent data directory.

![](Documents/cfg_logoutput_1.png)

### Preferences
Open `Editor -> Preferences -> Debugx`.  
User preferences affect **only your local project environment** — they never affect other developers or Release builds. They're mainly for different developers to configure locally to taste; typically each person only enables their own debug member switches to avoid being disturbed by others' output.  
![](Documents/cfg_preferences_1.png)

> [!NOTE]
> When running in the editor, the effective config is your local `Preferences`; in Release builds, the effective config is the project config committed in `ProjectSettings`.

## ✍️ Printing Logs in Code
Call the static methods of `DebugxLogger` or `Debugx` to output logs. All print methods are controlled by the `DEBUG_X` macro.  
![](Documents/code_1.png)

### Print Methods
**`DebugxLogger.LogXxx(message, showTime, showNetTag)`**  
Calls the dedicated method of the corresponding debug member, where `Xxx` is the member's Signature. This is the recommended way — **no need to memorize Keys**. `LogWarningXxx` / `LogErrorXxx` are also provided.

**`Debugx.Log(key, message, showTime, showNetTag)`**  
The most generic method; takes the member `Key` and the content to print. The `Key` is the identifier assigned to the member in the member config. `Debugx.LogWarning` / `Debugx.LogError` are also provided; you can also use the **signature** instead of the Key: `Debugx.Log(signature, message, ...)`.

Common parameters:
- `showTime`: whether to show a timestamp in the log.
- `showNetTag`: whether to show the network tag (Server / Client). This depends on the project side and only takes effect after you set a "is this a server" check via `Debugx.SetServerCheck(Func<bool>)`.

**`Debugx.LogAdm(message)`**  
The `LogAdm` family is **for Debugx plugin developers only** and should not be used by others. Logs printed through it are not controlled by `DebugxManager`'s member switches, but are still affected by the `DEBUG_X` macro.

### Preset Members & Keys
Debugx ships with a few fixed preset members whose Keys are reserved — do not use them for custom members:
- `Normal` (Key `-1`): normal member.
- `Master` (Key `-2`): master member.
- `Admin` (Key `0`): administrator member, corresponding to the `LogAdm` channel.

Use **positive integer** Keys for custom members (only `Key > 0` is treated as a valid custom Key).

### Runtime Switches
You can control printing dynamically from code at runtime:
- `Debugx.SetMemberEnable(int key, bool enable)`: toggle a member's logs (also available via `DebugxManager.Instance.SetMemberEnable(...)`).
- `Debugx.enableLog` / `Debugx.enableLogMember`: the master log switch / member-log master switch.
- `Debugx.logThisKeyMemberOnly`: when set to a Key, only that Key member's logs are printed (`0` disables this filter).

You can also adjust these switches visually at runtime via the [Debugx Console](#-debugx-console)'s Editor panel / in-game overlay.

## 🎛️ Debugx Console
`Debugx Console` is a **dedicated log viewer** that captures, filters, collapses and displays logs around "debug members", replacing your reliance on Unity's native Console. It comes in two forms that share the same capture / filter / collapse model:

- **Editor window** — docked in the editor, with the most complete feature set.
- **In-game runtime overlay** — summoned at runtime on device / in builds, touch-friendly.

> [!NOTE]
> In older versions `DebugxConsole` was just a runtime switch control panel. It's now upgraded into a full log viewer; the former runtime switches and test toggles are housed in the window's expandable [Editor Panel](#editor-panel), and the old "on-screen log drawing" feature has been removed and replaced by the [In-Game Runtime Overlay](#in-game-runtime-overlay).

### Editor Window
Open it via `Window -> Debugx -> DebugxConsole`. For convenience you can dock it alongside the native `Console` / `Game` tabs. The window captures every Unity log in the project: Debugx member logs are grouped by member; non-Debugx ordinary logs (`Debug.Log`, engine, third-party) are all grouped under **Uncategorized**.  
![](Documents/console_editor_1.png)
<!-- Screenshot placeholder: full view of the editor Debugx Console window (toolbar + log list + detail pane below) -->

### Toolbar & Filtering
From left to right the toolbar provides the following controls (secondary controls auto-hide by priority when the window is too narrow):
- **Clear**: clear logs immediately. Its dropdown to the right lets you check **auto-clear timings** — on entering Play / on recompile / on build (all on by default, matching the native Console).
- **Collapse**: collapse repeated logs with identical content, showing the count as a badge on the right.
- **Error Pause**: pause playback when an Error / exception occurs (Play mode only).
- **Members**: filter by member, multi-select; includes `All`, each member's `[key] signature`, plus the `Admin` / `Unregistered` / `Uncategorized` pseudo-members.
- **Editor**: show / hide the expandable [Editor Panel](#editor-panel) below — the view options, in-game Console toggle, runtime switches, test toggles and UI language all live there.
- **Search box**: live text filtering (substring match, case-insensitive).
- **Log / Warning / Error**: three type toggles with **counts**, at the far right of the toolbar; click to toggle whether that level is shown (counts over 999 show as `999+`).

![](Documents/console_editor_toolbar_1.png)
<!-- Screenshot placeholder: close-up of the editor Console toolbar, with each button labeled -->

### List & Detail Panel
- Each row shows: type icon, timestamp (optional), message, collapse count.
- **Single-click** selects and shows the full message and stack in the detail pane below; **double-click** (or Enter) jumps to the source location of the first business script frame in the stack.
- In the detail pane, stack frames that carry source info are shown as **clickable** (bold on hover), and clicking opens the corresponding file and line in an external editor.
- **Multi-select copy** is supported: `Ctrl/Cmd + C` copies the selected items (with stack), `Ctrl/Cmd + Shift + C` copies only the message; the right-click menu offers both as well.
- The list **auto-scrolls to the newest when pinned to the bottom**; scrolling up pauses auto-scroll, and scrolling back to the bottom resumes it.
- Logs are **persisted across recompiles / domain reloads** (cleared after an editor restart); compile errors are mirrored in automatically, and on consecutive compile failures only the latest batch is kept (matching the native Console).

### Editor Panel
Click **Editor** in the toolbar to expand it. This panel gathers the view options pulled out of the toolbar together with the old control panel's runtime / test switches in one place, convenient for tuning while running in the editor:
- **View options** (always available): `Debugx Only` (show only logs tagged `[Debugx]`, hiding Uncategorized), `Show Timestamp` (per-row time column), `Stack: Script Only` (show only business script frames in the detail stack, hiding engine and plugin-internal frames).
- **Enable in-game Console**: whether to auto-create the [In-Game Runtime Overlay](#in-game-runtime-overlay) on the next entry into Play (off by default; affects **editor Play mode only** — on device you must enable it in code, see that section).
- **Runtime switches** (Play mode only): `EnableLog` (master log switch), `EnableLogMember` (member-log master switch), `Only this key` (print only the member with the given Key, `0` = off). These rewrite `Debugx`'s runtime state live. (Set per-member switches in `Preferences -> Debugx` or in game code.)
- **Test toggles**: `Awake test log` / `Update test log`, to quickly confirm Debugx is working.
- **UI Language**: switch between 中 / EN.

![](Documents/console_editor_2.png)
<!-- Screenshot placeholder: editor Console with the Editor panel expanded (view options + overlay enable toggle + runtime switches + test toggles + language) -->

### In-Game Runtime Overlay
`DebugxRuntimeConsole` is a UI Toolkit-based in-game log overlay for viewing logs directly on device / in builds. It is **off** by default and auto-creates only when the following conditions are met:

1. The project has the `DEBUG_X` macro (the overlay is only compiled in when the macro is present).
2. The runtime-overlay switch `DebugxStaticData.RuntimeConsoleEnabled` is enabled (off by default):
   - **Editor Play mode**: check **Enable in-game Console** in the `Debugx Console -> Editor` panel (takes effect on the next entry into Play).
   - **Device / builds**: the editor checkbox **is not carried into builds**; you must set `DebugxStaticData.RuntimeConsoleEnabled = true` in your game code (a build's `PlayerPrefs` starts empty, off by default).

> [!NOTE]
> The overlay needs a UI Toolkit Panel Settings asset named `Console` located under `Resources`. **The plugin already bundles this asset** (`Resources/Console`), so usually no extra action is needed; if it's missing (stripped / deleted), the overlay won't enable and prints a hint in the Console — in that case just create a Panel Settings named `Console` in any `Resources` folder.

Once the conditions are met, the overlay auto-creates after the first scene loads (`DontDestroyOnLoad`), hidden by default. **How to summon / dismiss:**
- Desktop: press the **backquote key** (`` ` `` / `BackQuote`).
- Touch: **tap with three fingers** simultaneously on the screen.
- Universal: the floating **Debugx** button in the corner of the screen; or call `DebugxRuntimeConsole.SetVisible(true/false)` in your game code to bind your own gesture / hotkey.

> [!NOTE]
> The backquote key and three-finger gesture rely on the legacy **Input Manager**. If your project only enables the new **Input System**, summon it via the floating button or the `SetVisible(...)` API instead.

The overlay's features are largely the same as the editor version (adapted for touch): `Clear`, `Copy`, `Collapse`, `Debugx Only`, `Members` member filtering, `Time` timestamps, `Net` network tag (cycles All / Server / Client), `Log / Warning / Error` type filtering with counts, and search (matched substrings highlighted). In addition, the **Source** popup provides runtime switches (`EnableLog` / `EnableLogMember` / `Only Key` plus per-member switches), directly rewriting real print behavior. Selecting a log shows its message and stack text below (source navigation is not supported at runtime).  
![](Documents/console_ingame_1.png)
<!-- Screenshot placeholder: the in-game runtime overlay on device / in Play mode (toolbar + log list + detail) -->

> [!TIP]
> The runtime overlay's ring buffer defaults to a capacity of 1000 entries (mobile-friendly).

## 🧩 DebugxManager
`DebugxManager` is **created automatically** at runtime and usually needs no manual management. Its main job is to handle `LogOutput` operations (start/stop recording, set the output path, etc.).  
`DebugxManager` is auto-created at runtime via `[RuntimeInitializeOnLoadMethod]` only when the `DEBUG_X` macro is present. Its `Create()` method is `virtual` so project subclasses can extend it.

## ⚠️ Notes
> [!TIP]
> 1. You must add the `DEBUG_X` macro to your project to enable Debugx.
> 2. If the `DebugxLogger` class isn't generated after updating the plugin, use the menu `Tools -> Debugx -> Regenerate DebugxLogger Class` to force regeneration.
> 3. Versions before `2.3.0` cannot be updated normally due to changes in folder structure and UPM links — remove the old version and reinstall.
