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
Debugx is a debugging plugin specifically developed for the Unity engine.  
Used to manage DebugLog by debugging members and output log files locally. Use the macro "DEBUG_X" to enable functionality.

Use Debugx.Log() directly in your code for easy log printing.  
Different members use different keys, allowing convenient categorized printing by member and quick identification of corresponding code owners.  
![](Documents/Images/DebugxCode.png)

In Unity DOTS Burst environment, you must use DebugxBurst instead of Debugx, as many methods and fields are unavailable in Burst.  
However, since Unity DOTS updates very frequently, this method cannot guarantee complete reliability across different DOTS versions.  
![](Documents/Images/DebugxBurst.png)