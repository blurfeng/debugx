![](Documents/Images/Debugx.png)

<p align="center">
  <img alt="GitHub Release" src="https://img.shields.io/github/v/release/blurfeng/debugx?color=blue">
  <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/blurfeng/debugx/total?color=green">
  <img alt="GitHub Repo License" src="https://img.shields.io/github/license/blurfeng/debugx?color=blueviolet">
  <img alt="GitHub Repo Issues" src="https://img.shields.io/github/issues/blurfeng/debugx?color=yellow">
</p>

<p align="center">
  🌍
  中文 |
  <a href="./README_EN.md">English</a> |
  <a href="./README_JA.md">日本語</a>
</p>

# Debugx
Unity 专用的调试功能扩展插件。通过配置可以按调试成员分类打印和管理 Debug Log，并将日志文件输出到本地。

你可以阅读 [用户手册](Documents/UserManual_cn.md) 来获得更多信息。

## UPM 安装
使用 UPM（Unity Package Manager）方式安装插件。
```
https://github.com/BlurFeng/Debugx.git?path=DebugxDemo/Assets/Plugins/Debugx
```
1. 复制上面的链接
2. 打开 Unity 编辑器，进入 Window > Package Manager
3. 点击窗口左上角的 + 按钮，选择 "Add package from git URL..."
4. 粘贴链接，将插件安装到你的项目中

## 概要
Debugx 是专为 Unity 引擎开发的调试插件。  
用于按调试成员管理 DebugLog，并将日志文件输出到本地。使用宏 "DEBUG_X" 来启用功能。

在代码中直接使用 Debugx.Log() 即可轻松进行日志打印。  
不同成员使用不同的 key，可以方便地按成员分类打印，并快速定位对应代码的负责人。  
![](Documents/Images/DebugxCode.png)

在 Unity DOTS 的 Burst 环境中，必须使用 DebugxBurst 而非 Debugx，因为许多方法和字段在 Burst 中不可用。  
但由于 Unity DOTS 更新非常频繁，在不同 DOTS 版本下，此方法无法保证完全可靠。  
![](Documents/Images/DebugxBurst.png)