# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**语言要求：必须始终使用中文与用户沟通。**
所有面向用户的输出——回复、解释、计划、提问、总结、待办列表、错误说明等——一律使用中文，且贯穿整个会话，不得中途切换回英文。
内部思考过程可以使用英文节省token，但呈现给用户的任何内容都必须是中文。

---

## Collaboration Rules

- Before modifying code, briefly explain the approach first; do not jump straight to code
- When multiple implementation options exist, list them and let me choose rather than picking one yourself
- NEVER perform any git operation automatically (commit, push, branch, reset, etc.); the user handles these. Even without an explicit instruction, do not proactively propose or perform them on the user's behalf
- NEVER create Unity `.meta` files by hand; leave them to Unity's automatic generation. Only create/edit the source asset (e.g. the `.cs`), then let the editor import it

---

## What this is

Debugx is a Unity debug-logging plugin distributed via UPM (`com.blurfeng.debugx`). It lets a team categorize `Debug.Log` output by "member" (a named/keyed developer or subsystem), toggle those categories on/off at edit time, runtime, and per-user, and mirror logs to local files. Everything is gated behind the `DEBUG_X` scripting-define symbol — without it, all log calls compile away (they are `[Conditional("DEBUG_X")]`), so shipping without `DEBUG_X` fully disables the plugin at zero cost.

The repo is simultaneously the **Unity project** (opened at the repo root) and the **plugin source**. The distributable package lives under `Assets/Plugins/Debugx/`.

## Repository layout

- `Assets/Plugins/Debugx/` — the UPM package (this is what ships). `Runtime/` + `Editor/` asmdefs, `Resources/DebugxProjectSettings.asset`, `package.json`, `CHANGELOG.md`.
- `Assets/Source/DebugxTest.cs` — a manual MonoBehaviour smoke test; not part of the package.
- `~DebugxDll/Debugx/` — **separate C# class-library project** whose sole output is `DebugxLog.dll`. This is the source of truth for the core logging classes. See "The DLL workflow" below — this is the single most important thing to understand before editing core logic.
- Root-level `Assembly-CSharp*.csproj`, `Unity.*.csproj`, `Debugx.sln`, `Library/`, `obj/` — Unity-generated; never hand-edit. The `~DebugxDll/` folder is prefixed with `~` so Unity ignores it.
- `Documents/` — user manuals (cn/en/ja). `README*.md` are translations of the same content.

## The DLL workflow (critical)

The core logging types (`Debugx`, `DebugxProjectSettings`, `DebugxMemberInfo`, `DebugxBurst`, `LogOutput`, `IDebugxProjectSettingsAsset`) are **not compiled by Unity**. They are compiled into `DebugxLog.dll` from the `~DebugxDll/Debugx/` project and the built DLL is committed at `Assets/Plugins/Debugx/Runtime/DebugxLog.dll`.

**Why:** so Unity's Console stack trace stops at `Debugx.Log(...)` instead of stepping into the plugin internals. Because the call is inside a precompiled DLL, double-clicking a log jumps to the *caller's* business logic. (This pairs with the generated `DebugxLogger` class name ending in `Logger`, and `[HideInCallstack]`, for the same "stop at the right frame" goal.) If you edit any `.cs` under `~DebugxDll/` you MUST rebuild and re-copy the DLL, or the change has no effect in Unity.

To rebuild after editing core files:
```sh
# from ~DebugxDll/ — targets .NET Framework 3.5, references UnityEngine.dll
msbuild Debugx.sln /p:Configuration=Release
# then copy both artifacts into the package Runtime folder:
cp ~DebugxDll/Debugx/bin/Release/DebugxLog.dll ~DebugxDll/Debugx/bin/Release/DebugxLog.xml \
   Assets/Plugins/Debugx/Runtime/
```
Note: `~DebugxDll/Debugx/Debugx.csproj` hard-codes a `UnityEngine.dll` HintPath (`D:\Engine\Unity\2021.3.6f1\...`) — adjust it to the local Unity install if the build can't resolve `UnityEngine`. There is no post-build copy step; the copy is manual.

## Runtime architecture

Two namespaces: `DebugxLog` (core, both DLL and Unity-side) and `DebugxLog.Editor` (editor tooling), plus `DebugxLog.Tools`.

- **`Debugx`** (DLL) — the static log API. `Log/LogWarning/LogError(int key | string signature, message, showTime, showNetTag)`. Holds runtime switch state (`enableLog`, `enableLogMember`, `logThisKeyMemberOnly`, per-member enables). `LogAdm*` is a plugin-internal channel that bypasses member switches (still gated by `DEBUG_X`) — do not use it in application code.
- **`DebugxProjectSettings`** (DLL) — the *actual* runtime config object. Singleton lazily loaded from `Resources.Load("DebugxProjectSettings")`; falls back to a default instance if the asset is missing (never returns null). Also holds constants: `DebugxTag = "[Debugx]"`, preset member keys.
- **`DebugxProjectSettingsAsset`** (Unity `ScriptableObject`, in `Resources/`) — the editable, serialized project config. Its `ApplyTo(DebugxProjectSettings)` is the bridge that copies asset data into the DLL's runtime settings. **Settings duality:** in `Application.isEditor`, `ApplyTo` pulls most values from `DebugxStaticData` (per-user `EditorPrefs`/`PlayerPrefs`) instead of the serialized asset fields — so each developer's local toggles win in the editor, while builds use the committed asset values.
- **`DebugxManager`** (`MonoBehaviour`) — auto-created at runtime via `[RuntimeInitializeOnLoadMethod]` (only when `DEBUG_X` is defined). Drives `Debugx.OnAwake/OnDestroy`, sets the platform-specific `LogOutput.DirectoryPath` (editor → project `Logs/`), and starts/stops file recording. `Create()` is `virtual` for project extension.
- **`DebugxStaticData`** — all editor/user-preference state (`*Prefs` = per-user EditorPrefs, `*Set` = defaults), tooltips, and localized (cn/en) UI text via `IsChineseSimplified`.

## Member system

A "member" is a log category with an `int key`, `signature` (name), color, header, and enable flag. Preset members are fixed: `Normal` (key `-1`), `Master` (key `-2`), `Admin` (key `0`, the `LogAdm` channel). Custom members use positive keys. `KeyValid` = `key > 0`. Members are configured in **ProjectSettings > Debugx** (project-wide, edits the asset) and toggled per-user in **Preferences > Debugx**; runtime toggling is via **Window > Debugx > DebugxConsole**.

## Code generation

`DebugxLoggerCodeGenerator` (Editor) generates `Assets/Plugins/Debugx/Runtime/DebugxLogger.cs` — a static class with per-member convenience methods (`LogNormal`, `LogBlur`, `LogWarningMaster`, …) that forward to `Debugx.Log(key, …)`. This is **generated, do not hand-edit**; regenerate via menu **Tools > Debugx > Regenerate DebugxLogger Class** (also runs automatically on member config changes). Method suffixes are PascalCased from the member signature with collision suffixing.

`DEBUGX_IN_UPM` gate: the whole generated file is wrapped in `#if !DEBUGX_IN_UPM`. The Runtime asmdef defines `DEBUGX_IN_UPM` via a `versionDefines` entry keyed on `com.blurfeng.debugx`, so when a consumer installs the plugin **as a UPM package** the package's bundled `DebugxLogger` compiles out and only the copy generated into the consumer's own `Assets/` remains — preventing duplicate-class errors. In this dev repo the plugin lives under `Assets/` (not `Packages/`), so `DEBUGX_IN_UPM` is *not* defined and the generated class is active.

## Conventions

- **Always** gate new public log-side API with `[Conditional("DEBUG_X")]`.
- Changing `DebugxProjectSettings.DebugxTag` requires updating the matching regex in `LogOutput` (they must stay in sync; the tag must contain no regex-special characters).
- Comments and XML docs are bilingual (English then 中文) throughout — match this when adding to core files.
- **Version bumps** touch four places together: `package.json` `version`, the header block + `Version:` in `~DebugxDll/Debugx/Debugx.cs`, `AssemblyInfo.cs`, and `CHANGELOG.md`. Scheme is `major.newFeature.featureOrUpdate.bugfix` (major = breaking). Commit messages use bracketed Chinese tags: `【Bugfix】`, `【dll】`, `【Version】`, `【Docs】`.

## Testing / running

There is no automated test suite. Verification is manual: open the project in Unity (2021.3+, package targets `unity: 2022.3`), ensure `DEBUG_X` is in the project's scripting-define symbols, enter Play mode, and use `Assets/Source/DebugxTest.cs` and the **DebugxConsole > Test** toggles (`EnableAwakeTestLog`, `EnableUpdateTestLog`) to confirm output. Log files land in the project root `Logs/` folder in-editor.
