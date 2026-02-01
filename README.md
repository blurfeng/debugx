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

# 注意事项
> [!TIP]
> 1. 插件在 2.3.0 之前的版本因为文件夹结构和 UPM 链接改变，无法正常更新，需要移除旧版本后重新安装。

## Unity 版本要求
Unity 2021.3 及以上版本。

## UPM 安装
使用 UPM（Unity Package Manager）方式安装插件。
```
https://github.com/BlurFeng/Debugx.git?path=Assets/Plugins/Debugx
```
1. 复制上面的链接
2. 打开 Unity 编辑器，进入 Window > Package Manager
3. 点击窗口左上角的 + 按钮，选择 "Add package from git URL..."
4. 粘贴链接，将插件安装到你的项目中

## 概要
Debugx 是专为 Unity 引擎开发的调试插件。  
用于按调试成员管理 DebugLog，并将日志文件输出到本地。   
需要在项目中添加宏 "DEBUG_X" 来启用Debugx功能。

### 如何使用
根据配置的调试成员会自动生成对应的 Log 方法。   
直接在代码中使用 Debugx.LogMemberName() 等方法即可轻松打印日志。   
![](Documents/Images/Debugx_Use.png)

### DOTS Burst 环境
在 Unity DOTS 的 Burst 环境中，必须使用 DebugxBurst 而非 Debugx，因为许多方法和字段在 Burst 中不可用。  
但由于 Unity DOTS 更新非常频繁，在不同 DOTS 版本下，此方法无法保证完全可靠。  
![](Documents/Images/DebugxBurst.png)