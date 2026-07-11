# Changelog

## [2.4.2] - 2026-07-11
### Fixed
- **Editor / runtime Console** stack traces no longer leak Debugx's own forwarder frames. The raw stack the Console receives via `Application.logMessageReceived` is *not* run through Unity's `[HideInCallstack]` stripping — that only happens inside Unity's own Console window — so the generated `DebugxLogger` forwarding frame stayed in the trace and double-clicking a log jumped to the forwarder instead of the caller's business code. `StackTraceParser` now reproduces the stripping itself (reflecting on each frame's method, cached by `Type|Method`, with a name-based fallback for the generated forwarder to survive AOT) and skips those frames in stack display, double-click navigation, and stack copy.
- **Editor Console** occasionally missed compile errors/warnings that Unity's native Console showed. The live channel dropped compile messages and relied on a single `LogEntries` scan at `compilationFinished` to re-add them, but `LogEntries` flushes asynchronously and out of sync with that event — if the one scan ran before an error was flushed, the error was lost for good. Reworked into an absorb-and-reconcile model: the live channel no longer drops compile messages (they appear immediately as Uncategorized), the mirror reconciles against `LogEntries` as the source of truth, and a 1-second bounded re-scan window closes the async-flush race.
- **Editor Console** compile errors did not reappear after **Clear**. Clear now pumps the compile mirror again, re-reading the errors still present in the editor console and re-injecting them.
### Changed
- **Error icon** changed from an exclamation mark to an "x", for clearer distinction from the info icon.

## [2.4.1] - 2026-07-10
### Fixed
- **Debugx Console** tail auto-scroll: when the list was stuck to the bottom, newly added logs did not always reach the very bottom. `ScrollToItem` ran in the same frame the rows changed — before the ScrollView had recomputed its content height / scroll extent — so it landed a row short, and never corrected once the log stream stopped. It now re-scrolls after the content geometry is recomputed. Fixed in both the Editor window and the in-game runtime overlay.
- **Runtime Console** detail/stack pane scrolled too far per mouse-wheel notch; reduced the wheel step (`ScrollView.mouseWheelScrollSize`) for gentler, near line-by-line scrolling.
- **Editor Console** stack-frame hyperlink now underlines only the `path:line` inside the trailing `(at …)` instead of the whole frame line, matching Unity's native Console.
- **Editor Console** detail pane clipped the last wrapped line when scrolled to the bottom (its content height was under-measured); added trailing padding so the last line fully clears the viewport.

## [2.4.0] - 2026-07-05
### Added
- **Debugx Console** — a dedicated, member-aware log viewer that replaces reliance on Unity's native Console, in two forms sharing one capture / filter / collapse model layer:
  - **Editor window** (Window > Debugx > DebugxConsole): captures all Unity logs (non-Debugx grouped as "Uncategorized"); filter by member / type / Debugx-only / search; collapse duplicates; timestamps; script-only or full stack view; multi-select copy; double-click source navigation; Error Pause; compile-log mirroring; Clear on Play / Recompile / Build; and cross-recompile & domain-reload persistence. A collapsible **Editor** side-panel hosts the runtime switches (EnableLog / EnableLogMember / Only-Key) and test toggles.
  - **In-game runtime overlay** (`DebugxRuntimeConsole`, UI Toolkit): a touch-friendly overlay that self-mounts under `DEBUG_X` when enabled and a `Console` PanelSettings asset is present in a Resources folder — the same model layer with member / source / net-tag filters, search-match highlighting, and multi-select copy. Summon via backquote key, three-finger tap, a floating button, or the public `DebugxRuntimeConsole.SetVisible(bool)` API (the key / tap gestures require the legacy Input Manager).
- `Debugx.OnRawLog` — a structured log event carrying full member metadata (key / signature / color / header / net tag / LogType / message / final text), so consumers such as the Debugx Console can read logs with full member context instead of re-parsing formatted text. Near-zero cost: the value-type payload isn't even constructed when there are no subscribers.
- `Debugx.IsDebugxTagged(string)` — a helper that centralizes the `[Debugx]` tag check (kept in sync with `DebugxTag`).
### Removed
- Retired the legacy on-screen IMGUI log overlay (`LogOutput.DrawGUI` and its `DebugxManager.OnGUI` driver), superseded by the Debugx Console above, along with its project settings `drawLogToScreen` / `restrictDrawLogCount` / `maxDrawLogs`. **Breaking:** this drops those public `DebugxProjectSettings` fields, so any external code referencing them must be updated. The feature defaulted off, so runtime behavior is otherwise unchanged.

## [2.3.3] - 2026-05-08
### Fixed
- Synced `allowUnregisteredMember` correctly when applying settings in both Editor and runtime.
- Fixed member foldout cache key persistence to avoid stale/undeletable state entries.
- Added null-safety guards in member settings/reset paths in Project Settings UI.
- Prevented `DebugxLogger` auto-generated method name collisions for similar signatures.
- Hardened member enable prefs parsing to tolerate malformed or duplicate cached data.
- Optimized DLL project code with additional safety checks and fallback logic.
- Removed duplicate `LogOutput` handling.

## [2.3.2] - 2026-02-02
### Changed
- Removed classes that need to be hidden in the Unity layer and rebuilt them into DebugxLog.dll to prevent exposure in the Console window stack. We need the stack to stop at the specific call location of Log.
- Updated the DebugxLoggerCodeGenerator class to generate DebugxLogger classes and member-specific Log methods. Using Logger as the class name allows direct navigation to the Log call location when double-clicking logs in the Console window, instead of navigating to the DebugxLogger class.
### Notice
- If you encounter errors related to duplicate definitions of the Debugx class, please delete the previously auto-generated Assets/Plugins/Debugx/Runtime/Debugx.cs and use DebugxLogger instead.

## [2.3.1] - 2026-02-01
### Fixed
- Addressed a potential editor freeze/hang caused by InitializeOnLoadMethod when loading the plugin via UPM.

## [2.3.0] - 2026-02-01
### Changed
- Promoted DebugxLog.dll source code directly into the Unity project to streamline assembly management and UPM integration.
- Organized project resources and removed the standalone DebugxLog.dll build project.
- Relocated the Unity project to the root directory for a cleaner repository structure.
- Updated .gitignore to be specifically tailored for Unity projects.
- Updated UPM (Unity Package Manager) links.
### Warning
- Due to updates in the project structure and UPM links, you must remove the existing package and re-import it to ensure proper installation.

## [2.2.1] - 2026-02-01
### Fixed
- Removed redundant .meta files for invalid .docx documents.
- Resolved .gitignore conflict where NuGet and Unity Packages folders were incorrectly conflated.
- Optimized the auto-generated Debugx class by implementing the DEBUGX_IN_UPM macro via .asmdef version defines. The class is now conditionally compiled using #if !DEBUGX_IN_UPM, effectively resolving naming conflicts between the local Assets development scripts and the plugin scripts when loaded via UPM in the Packages folder.

## [2.2.0] - 2026-02-01
### Changed
- Automatically generate dedicated Log methods for each member based on configuration.
- Updated the Project Settings interface layout.
### Notice
- Removed LogNom and LogMst series methods; use member-specific methods in the auto-generated Debugx script instead.