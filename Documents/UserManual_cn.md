# Debugx 用户手册
## 简介
Github：https://github.com/blurfeng/debugx

Debugx 是一个专为 Unity 引擎开发的调试插件。  
该插件可以按调试成员分类管理 DebugLog，并将日志文件输出到本地。通过宏 "DEBUG_X" 来启用功能。

在多人协作开发项目时，所有开发者都使用 UnityEngine.Debug.Log() 会导致日志难以管理和区分。  
当我们测试自己的功能时，不希望被其他人的日志输出干扰。  
只需要将 "DEBUG_X" 宏添加到项目中，并进行简单配置，即可开始使用 Debugx 的功能。  
Debugx 在 ProjectSettings 和 Preferences 中分别提供了配置界面。  
ProjectSettings 中的配置会影响整个项目，而 Preferences 中的用户配置仅影响你的本地环境，不会对项目和其他开发者产生影响。  
DebugxConsole 用于在项目运行时管理打印开关等功能。

## 插件安装与配置
按照本手册快速安装并配置 Debugx 插件。

### 添加插件到项目
从 Releases 页面下载发布包，使用 .unitypackage 文件将 Debugx 插件安装到你的项目中。

或者通过 UPM (Unity Package Manager) 方式安装插件。
```
https://github.com/BlurFeng/Debugx.git?path=Assets/Plugins/Debugx
```

### 添加宏到项目
必须在项目中添加宏 `DEBUG_X` 才能启用日志打印功能。  
在项目发布时，可以移除宏 `DEBUG_X` 来快速禁用 Debugx 的功能。
![](Images/Debugx2.png)

### Debugx 配置
将鼠标悬停在字段上时会显示工具提示，这能更好地帮助你熟悉 Debugx。  
由于可以通过工具提示查看详细说明，因此这里不会对每个选项进行详细介绍。  
![](Images/Debugx3.png)

#### ProjectSettings 项目设置
通过 Editor > ProjectSettings > Debugx 打开 Debugx 项目设置界面。  
项目设置会影响整个项目。当需要添加新的调试成员时，在此处进行配置。
![](Images/Debugx4.png)

##### Toggle 开关设置
这里是各种开关设置。主开关在此显示，调试成员可以在成员信息中单独设置开关。  
![](Images/Debugx5.png)

##### MemberSettings 调试成员设置
成员设置用于配置调试成员。这里有一些预设的成员，它们不能被删除，只能进行有限的编辑。  
可以在自定义成员中添加专属的成员配置，按项目使用者进行区分。  
可以设置开关、签名、颜色等属性。最重要的是成员的 Key，这在日志打印时会用到。每个成员只需要记住自己的 Key 即可。  
![](Images/Debugx6.png)

##### LogOutput 日志输出
日志输出功能会在项目开始运行时启动记录，在项目停止运行时结束记录并输出到本地文件。  
在编辑器环境下，日志文件会输出到项目根目录的 Logs 文件夹中。  
在发布版本中，根据不同平台，日志文件会存储到对应的目录中。  
PC 平台通常在 C:\Users\UserName\AppData\LocalLow\DefaultCompany\ProjectName 目录下。
![](Images/Debugx7.png)

#### Preferences 用户偏好设置
通过 Editor > Preferences > Debugx 打开 Debugx 用户偏好设置界面。  
用户偏好设置仅影响你的本地项目环境，不会影响其他开发者的项目，也不会影响发布版本。  
主要用于不同开发者在本地环境中按个人需求进行配置。每个人通常只会启用自己的调试成员开关，以避免被其他人的调试输出干扰。  
![](Images/Debugx8.png)

## 在代码中使用日志打印
现在可以开始使用日志打印功能了。直接调用 Debugx 类的静态方法来输出日志。  
![](Images/Debugx9.png)

### 打印方法
**DebugxLogger.LogMemberName(msg)**  
调用对应调试成员的 Log 方法来打印日志。成员名称即为在调试成员配置中设置的名称。
**Debugx.Log(key, message)**  
Log 系列方法是最常使用的方法，需要传入 Key 和打印内容。Key 是在调试成员配置中为成员分配的标识。每个成员需要记住并使用自己的 Key。  
**Debugx.LogAdm(message)**  
LogAdm 系列方法是 Debugx 插件开发者专用的！任何人都不应使用此方法，因为通过此方法打印的日志无法通过 DebugxManager 进行开关控制，但仍受到宏 DEBUG_X 的影响。   

## DebugxConsole 控制台
Debugx 控制台主要用于在项目运行时对 Debugx 功能进行开关操作。通过 Window > Debugx > DebugxConsole 打开窗口。  
为了方便使用，可以将其与 Game 标签页放在一起。  
![](Images/Debugx10.png)

### PlayingSettings 运行时设置
项目运行时设置的内容基本与 ProjectSetting 中的相同，但允许在运行时进行调整。  
![](Images/Debugx11.png)
#### Test 测试
测试功能模块。提供了一些便于测试的功能开关，用于确认 Debugx 功能是否正常运行。

## DebugxManager 管理器
DebugxManager 在游戏运行时自动创建，通常无需手动管理。它的主要职责是处理 LogOutput 相关操作。  
只有在项目中添加了 DEBUG_X 宏时，DebugxManager 才会自动创建。