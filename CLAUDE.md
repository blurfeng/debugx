# CLAUDE.md

本文件为 Claude Code (claude.ai/code) 在本仓库中工作时提供指引。

**语言要求：必须始终使用中文与用户沟通。**
所有面向用户的输出——回复、解释、计划、提问、总结、待办列表、错误说明等——一律使用中文，且贯穿整个会话，不得中途切换回英文。
内部思考过程可以使用英文节省token，但呈现给用户的任何内容都必须是中文。

---

## 协作规则

- 修改代码前，先简要说明思路，不要一上来就写代码
- 存在多种实现方案时，列出选项让我选择，而不是自己替我拍板
- 绝不自动执行任何 git 操作（commit、push、branch、reset 等）；这些由用户处理。即便没有明确指示，也不要主动代用户提议或执行。**唯一例外**：用户明确说“发布”时，按下文“发布流程”执行其中列出的 git 操作
- 绝不手写 Unity 的 `.meta` 文件；交给 Unity 自动生成。只创建/编辑源资源（如 `.cs`），再让编辑器导入

---

## 这是什么

Debugx 是一个通过 UPM 分发的 Unity 调试日志插件（`com.blurfeng.debugx`）。它让团队按“成员”（member，一个具名/带键的开发者或子系统）对 `Debug.Log` 输出分类，可在编辑时、运行时、按用户开关这些分类，并把日志镜像到本地文件。一切都由 `DEBUG_X` scripting-define 符号门控——没有它时，所有日志调用都会被编译移除（它们是 `[Conditional("DEBUG_X")]`），因此不带 `DEBUG_X` 发布即以零成本完全禁用插件。

本仓库同时是 **Unity 工程**（在仓库根目录打开）与 **插件源码**。可分发的包位于 `Assets/Plugins/Debugx/`。

## 仓库结构

- `Assets/Plugins/Debugx/` —— UPM 包（这是发布物）。`Runtime/` + `Editor/` asmdef、`Resources/DebugxProjectSettings.asset`、`package.json`、`CHANGELOG.md`。
- `Assets/Source/DebugxTest.cs` —— 手动的 MonoBehaviour 冒烟测试；不属于包。
- `~DebugxDll/Debugx/` —— **独立的 C# 类库工程**，唯一产物是 `DebugxLog.dll`。这是核心日志类的真源（source of truth）。见下文“DLL 工作流”——这是编辑核心逻辑前最需要理解的一点。
- 根目录的 `Assembly-CSharp*.csproj`、`Unity.*.csproj`、`Debugx.sln`、`Library/`、`obj/` —— Unity 生成，绝不手改。`~DebugxDll/` 文件夹以 `~` 前缀命名，故 Unity 会忽略它。
- `Documents/` —— 用户手册（cn/en/ja）。`README*.md` 是同一内容的翻译。

## DLL 工作流（关键）

核心日志类型（`Debugx`、`DebugxProjectSettings`、`DebugxMemberInfo`、`DebugxBurst`、`LogOutput`、`IDebugxProjectSettingsAsset`）**不由 Unity 编译**。它们由 `~DebugxDll/Debugx/` 工程编译进 `DebugxLog.dll`，构建产物提交在 `Assets/Plugins/Debugx/Runtime/DebugxLog.dll`。

**为什么：** 让 Unity 的 Console 堆栈在 `Debugx.Log(...)` 处停下，而不是步进到插件内部。由于调用发生在预编译的 DLL 内，双击日志会跳转到 *调用方* 的业务逻辑。（这与生成的 `DebugxLogger` 类名以 `Logger` 结尾、以及 `[HideInCallstack]` 配合，服务于同一个“停在正确帧”的目标。）若你改动了 `~DebugxDll/` 下的任何 `.cs`，**必须**重建并重新拷贝 DLL，否则改动在 Unity 中不生效。

编辑核心文件后重建：
```sh
# 在 ~DebugxDll/ 下执行 —— 目标框架 .NET Framework 3.5，引用 UnityEngine.dll
msbuild Debugx.sln /p:Configuration=Release
# 然后把两个产物都拷进包的 Runtime 文件夹：
cp ~DebugxDll/Debugx/bin/Release/DebugxLog.dll ~DebugxDll/Debugx/bin/Release/DebugxLog.xml \
   Assets/Plugins/Debugx/Runtime/
```
注意：`~DebugxDll/Debugx/Debugx.csproj` 硬编码了 `UnityEngine.dll` 的 HintPath（`D:\Engine\Unity\2021.3.6f1\...`）——若构建无法解析 `UnityEngine`，请改成本地 Unity 安装路径。没有构建后自动拷贝步骤；拷贝是手动的。

## 运行时架构

两个命名空间：`DebugxLog`（核心，DLL 与 Unity 侧共用）与 `DebugxLog.Editor`（编辑器工具），外加 `DebugxLog.Tools`。

- **`Debugx`**（DLL）—— 静态日志 API。`Log/LogWarning/LogError(int key | string signature, message, showTime, showNetTag)`。持有运行时开关状态（`enableLog`、`enableLogMember`、`logThisKeyMemberOnly`、各成员开关）。`LogAdm*` 是绕过成员开关的插件内部通道（仍受 `DEBUG_X` 门控）——不要在应用代码中使用。
- **`DebugxProjectSettings`**（DLL）—— *真正的* 运行时配置对象。单例，惰性从 `Resources.Load("DebugxProjectSettings")` 加载；资源缺失时回退到默认实例（绝不返回 null）。也持有常量：`DebugxTag = "[Debugx]"`、预设成员键。
- **`DebugxProjectSettingsAsset`**（Unity `ScriptableObject`，位于 `Resources/`）—— 可编辑、可序列化的工程配置。其 `ApplyTo(DebugxProjectSettings)` 是把资源数据拷入 DLL 运行时设置的桥梁。**设置的双重性：** 在 `Application.isEditor` 下，`ApplyTo` 的大多数值取自 `DebugxStaticData`（各用户的 `EditorPrefs`/`PlayerPrefs`）而非序列化的资源字段——于是编辑器中每个开发者的本地开关优先，而构建版使用已提交的资源值。
- **`DebugxManager`**（`MonoBehaviour`）—— 运行时通过 `[RuntimeInitializeOnLoadMethod]` 自动创建（仅当定义了 `DEBUG_X`）。驱动 `Debugx.OnAwake/OnDestroy`，设置平台相关的 `LogOutput.DirectoryPath`（编辑器 → 工程 `Logs/`），并启停文件录制。`Create()` 为 `virtual`，供项目扩展。
- **`DebugxStaticData`** —— 所有编辑器/用户偏好状态（`*Prefs` = 各用户 EditorPrefs，`*Set` = 默认值）、tooltip，以及通过 `IsChineseSimplified` 提供的本地化（中/英）UI 文本。

## 成员系统

“成员”（member）是一个日志分类，含 `int key`、`signature`（名称）、颜色、header 与启用标志。预设成员固定：`Normal`（key `-1`）、`Master`（key `-2`）、`Admin`（key `0`，即 `LogAdm` 通道）。自定义成员使用正数 key。`KeyValid` = `key > 0`。成员在 **ProjectSettings > Debugx**（工程级，编辑资源）中配置，在 **Preferences > Debugx** 中按用户切换；运行时切换通过 **Window > Debugx > DebugxConsole**。

## 代码生成

`DebugxLoggerCodeGenerator`（Editor）生成 `Assets/Plugins/Debugx/Runtime/DebugxLogger.cs` —— 一个静态类，含各成员的便捷方法（`LogNormal`、`LogBlur`、`LogWarningMaster`……），转发到 `Debugx.Log(key, …)`。这是**生成文件，不要手改**；通过菜单 **Tools > Debugx > Regenerate DebugxLogger Class** 重新生成（成员配置变化时也会自动运行）。方法后缀由成员 signature 转 PascalCase 并做碰撞后缀处理。

`DEBUGX_IN_UPM` 门控：整个生成文件包在 `#if !DEBUGX_IN_UPM` 内。Runtime asmdef 通过一条以 `com.blurfeng.debugx` 为键的 `versionDefines` 定义 `DEBUGX_IN_UPM`，于是当使用者**以 UPM 包**安装插件时，包内自带的 `DebugxLogger` 被编译掉，只保留生成到使用者自己 `Assets/` 中的那份——避免重复类错误。在本开发仓库中插件位于 `Assets/`（非 `Packages/`），故 `DEBUGX_IN_UPM` *未* 定义，生成的类处于激活状态。

## 约定

- **务必**为新增的公开日志侧 API 加 `[Conditional("DEBUG_X")]` 门控。
- 修改 `DebugxProjectSettings.DebugxTag` 时必须同步更新 `LogOutput` 中匹配的正则（两者必须保持一致；tag 不得包含正则特殊字符）。
- 注释与 XML doc 全程双语（先英文后中文）——向核心文件添加内容时保持这一风格。
- **版本号升级**：
  - **包版本**（`package.json` 的 `version` 与 `CHANGELOG.md`）每次发布都更新。
  - **未发布的改动记入 `## [Unreleased]`**：有更新但尚未发布（不升版本号）时，也必须在 `CHANGELOG.md` 顶部的 `## [Unreleased]` 段落下记录；发布时再把该段改为 `## [x.y.z] - 日期`。
  - **DLL 版本号独立管理，仅当 DLL 实际更新（即改动 `~DebugxDll/` 核心代码并重建 DLL）时才更新**——涉及 `~DebugxDll/Debugx/Debugx.cs` 的头部注释块与 `Version:`、以及 `AssemblyInfo.cs`。只改 Unity 侧代码（不动 DLL）的发布，只需升 `package.json` + `CHANGELOG.md`，DLL 版本保持不变。
  - 版本号格式为 `major.newFeature.featureOrUpdate.bugfix`（major = 破坏性变更）。
  - 提交信息使用中文方括号标签：`【Bugfix】`、`【dll】`、`【Version】`、`【Docs】`。

## 发布流程

日常开发在 `dev` 上积累，改动记入 CHANGELOG 的 `## [Unreleased]`。**只有用户明确说“发布”时才执行本流程**，不要主动提议。发布 = 定版本 → `main` 快进到 `dev` → 打 Tag → 导出 `.unitypackage` → 创建 GitHub Release。仓库 `blurfeng/debugx`，Tag 为轻量标签、名字是纯版本号（如 `2.4.4`，无 `v`）。

1. **前置检查**：`git status` 干净且位于 `dev`；`## [Unreleased]` 有内容。版本号若用户没给，按上文版本号格式推断，**先向用户确认版本号后再继续**。
2. **定版（dev）**：把 `## [Unreleased]` 改为 `## [x.y.z] - YYYY-MM-DD`；`package.json` 的 `version` 改为 `x.y.z`（DLL 版本按上文规则，未重建 DLL 就不动）。提交 `【Version】 x.y.z`。
3. **合入 main（不切分支）**：
   ```sh
   git push origin dev
   git push origin dev:main      # 非快进会被拒绝：此时停下问用户，不要强推
   git fetch origin main:main    # 同步本地 main
   ```
4. **打 Tag**：`git tag x.y.z && git push origin x.y.z`（若 Tag 已存在且指向别处，停下问用户，不要重打）。
5. **导出 `.unitypackage`**：导出 `Assets/Plugins/Debugx`（递归），文件名为 `Debugx_x.y.z.unitypackage`，放在 scratchpad 等仓库外的位置，**不进仓库**。
   - **编辑器开着时**：用 UnityMCP 的 `execute_code` 执行 `AssetDatabase.Refresh(); AssetDatabase.ExportPackage("Assets/Plugins/Debugx", <输出路径>, ExportPackageOptions.Recurse);`，并返回文件大小。若返回 `no_unity_session`，读资源 `mcpforunity://instances`，`instance_count` 为 0 就请用户在 MCP for Unity 窗口里连接会话。
   - **编辑器关着时**：用 Unity 命令行（编辑器开着会锁住工程，不能这样用；也不要替用户关编辑器）。编辑器版本取自 `ProjectSettings/ProjectVersion.txt`，本机 Hub 的安装目录是 `F:\Program Files\Unity\Hub\Editor\<版本>\`：
     ```sh
     "F:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -nographics -quit \
       -projectPath "F:/ProjectUnity/Debugx" -exportPackage Assets/Plugins/Debugx <输出路径>/Debugx_x.y.z.unitypackage \
       -logFile <输出路径>/export.log
     ```
     退出码非 0 或文件不存在时，查看 `export.log`。
6. **发布说明**：发布说明不是只写本版本，而是**自上一个 GitHub Release 以来的所有 CHANGELOG 段**（中间可能有没单独发布的版本）。用 `gh release list -R blurfeng/debugx -L 1` 查上一个 Release，把 `CHANGELOG.md` 中从 `[x.y.z]` 到该版本之前（不含）的各段**原文照贴**（保留 `## [版本] - 日期` 标题，按版本分节），写入仓库外的临时文件。
7. **创建 Release**：
   ```sh
   gh release create x.y.z -R blurfeng/debugx --verify-tag --title "Debugx vx.y.z" \
     --notes-file <说明文件> <输出路径>/Debugx_x.y.z.unitypackage
   ```
   默认即为 Latest。
8. **确认**：`gh release view x.y.z -R blurfeng/debugx` 确认附件已上传（state `uploaded`），把链接告诉用户。

`gh` 使用本机 `gh auth login` 存在系统凭据库里的 OAuth token，与浏览器登录状态无关；推送用 SSH 远程 `git@github.com-blur:blurfeng/debugx.git`。

## 测试 / 运行

没有自动化测试套件。验证靠手动：在 Unity 中打开工程（2022.3+，包目标 `unity: 2022.3`），确保 `DEBUG_X` 在工程的 scripting-define 符号中，进入 Play 模式，使用 `Assets/Source/DebugxTest.cs` 与 **DebugxConsole > Test** 开关（`EnableAwakeTestLog`、`EnableUpdateTestLog`）确认输出。编辑器中日志文件落在工程根目录的 `Logs/` 文件夹。
