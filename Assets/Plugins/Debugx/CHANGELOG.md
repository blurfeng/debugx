# Changelog

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
### ### Fixed
- Removed redundant .meta files for invalid .docx documents.
- Resolved .gitignore conflict where NuGet and Unity Packages folders were incorrectly conflated.
- Optimized the auto-generated Debugx class by implementing the DEBUGX_IN_UPM macro via .asmdef version defines. The class is now conditionally compiled using #if !DEBUGX_IN_UPM, effectively resolving naming conflicts between the local Assets development scripts and the plugin scripts when loaded via UPM in the Packages folder.

## [2.2.0] - 2026-02-01
### Changed
- Automatically generate dedicated Log methods for each member based on configuration.
- Updated the Project Settings interface layout.
### Warning
- Removed LogNom and LogMst series methods; use member-specific methods in the auto-generated Debugx script instead.