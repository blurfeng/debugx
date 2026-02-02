![](Documents/Images/Debugx.png)

<p align="center">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/blurfeng/debugx?color=blue">
  <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/blurfeng/debugx/total?color=green">
  <img alt="GitHub Repo License" src="https://img.shields.io/github/license/blurfeng/debugx?color=blueviolet">
  <img alt="GitHub Repo Issues" src="https://img.shields.io/github/issues/blurfeng/debugx?color=yellow">
</p>

<p align="center">
  🌍
  <a href="./README.md">中文</a> |
  English |
  <a href="./README_JA.md">日本語</a>
</p>

# Debugx
A debugging extension plugin specifically for Unity. Allows configuration-based categorized printing and management of Debug Logs by debugging members, with local log file output.

You can read the [User Manual](Documents/UserManual_en.md) for more information.

# Notes
> [!TIP]
> 1. You must add the macro "DEBUG_X" to your project to enable Debugx functionality.
> 2. After updating the plugin, if the DebugxLogger class is not generated, use the menu Tools > Debugx > Regenerate DebugxLogger Class to force regeneration.
> 3. Due to changes in folder structure and UPM links, versions prior to 2.3.0 cannot be updated normally and require removal of the old version before reinstallation.

## Unity Version Requirement
Unity 2021.3 and above.

## UPM Installation
Install the plugin using UPM (Unity Package Manager).
```
https://github.com/BlurFeng/Debugx.git?path=DebugxDemo/Assets/Plugins/Debugx
```
1. Copy the link above
2. Open Unity Editor, go to Window > Package Manager
3. Click the + button in the upper left corner of the window, select "Add package from git URL..."
4. Paste the link to install the plugin to your project

## Overview
Debugx is a debugging plugin developed specifically for the Unity engine.  
Used to manage DebugLogs by debugging members and output log files locally.
Requires adding the macro "DEBUG_X" in the project to enable Debugx functionality.

### How to Use
First, add the macro "DEBUG_X" to your project to enable Debugx functionality.   
Based on the configured debugging members, corresponding DebugxLogger classes and Log methods for each member will be automatically generated.   
In the code, use methods like DebugxLogger.LogMemberName(msg) or Debugx.Log(key,msg) to easily print logs.   
![](Documents/Images/Debugx_Use.png)