# Debugx 专用 Console 开发需求案

> 状态：**v1.0（决策已确认）**。本文档在整合五个专题草稿与一轮完整性评审后产出，评审发现的章节间矛盾已在正文就地统一；§9 的 4 个开放决策已由你拍板并回写（见 §2 与 §9 决策记录）。开发前置仅剩一个 **P0 技术预研(Spike)**（见 §10），验证通过后即冻结数据契约开工。

---

## 1. 背景与目标

- **现状**：现有 `DebugxConsole`（`Assets/Plugins/Debugx/Editor/DebugxConsole.cs`）只是一个 IMGUI **运行时开关控制面板**（`enableLog`/`enableLogMember`/`logThisKeyMemberOnly` + 逐成员开关 + Test 开关），**完全不具备日志查看能力**——没有日志列表、详情、堆栈、搜索。因此本项目是**从零构建一个日志查看器**，而非改造。
- **目标**：做一个可替代 Unity 原生 Console 的 **Debugx 专用 Console**，具备原生 Console 的**全部**功能，并叠加 Debugx 成员维度的扩展筛选与着色。
- **两套显示、一套模型**：v1 先交付 **Editor 版**（UIToolkit）；后续再做**运行时 Debug 包内 Console UI**。两者只差"显示层"，日志的采集/缓冲/去重折叠/筛选/搜索/堆栈解析等**模型与数据层必须抽成 Runtime 可复用库**。

## 2. 已确认的项目决策（前置）

| # | 决策 | 结论 |
|---|---|---|
| D1 | 日志捕获范围 | 捕获**全部** Unity 日志（含非 Debugx 的 `Debug.Log`、引擎、第三方）。非 Debugx 归"未分类/Uncategorized"；界面提供"仅显示 Debugx"过滤开关。 |
| D2 | Editor UI 技术 | **UIToolkit / UIElements**（非 IMGUI）。 |
| D3 | DLL 改动 | 在核心 DLL 的 `Debugx.LogCreator` 内新增**结构化日志事件**，携带成员元数据；需按 CLAUDE.md 的 DLL 工作流重编译并回拷 DLL。 |
| D4 | v1 范围 | **一次性对标原生 Console 全部功能** + Debugx 成员扩展。 |
| D5 | 复用约束 | 模型/数据层放 Runtime 程序集、零 `UnityEditor` 依赖；Editor 版与运行时版只差显示层。凡依赖 `UnityEditor` 的能力（开源码、Play 暂停、监听重编译等）严格隔离到 Editor 专属层。 |
| D6 | 堆栈来源/去重 | **线程内 FIFO 配对**：`logMessageReceivedThreaded` 拿可靠 Unity 堆栈 + 结构化事件按同线程 FIFO 补成员元数据。保住双击跳转、去重可靠。开工前先做 Spike 验证同步时序（§10）。 |
| D7 | 旧面板去留 | 新 Console **吸收**旧 `DebugxConsole.cs` 的全部开关（含 Test）并接管菜单入口，随后**移除**旧文件（你已同意删代码）。 |
| D8 | 历史日志文件源 | Console **同时**支持读取历史日志文件作为数据源：Debugx 落盘 `DebugxLog.log`、Unity `Editor.log`、真机 `Player.log`（见 §6-C，注意成员元数据限制）。 |
| D9 | 运行时显示技术 | 运行时版用 **Runtime UIToolkit**（UIDocument/PanelSettings），与 Editor 版共享心智；包体目标 `unity:2022.3` 满足其可用性。 |

## 3. 总体架构与分层

```
┌──────────────────────────────────────────────────────────────┐
│ 显示层（可替换）                                                 │
│  ├─ Editor 版：DebugxConsoleWindow (EditorWindow + UIToolkit)   │ ← v1 主体
│  └─ 运行时版：Debug 包内 Console UI（后续阶段，§8）              │
├──────────────────────────────────────────────────────────────┤
│ 编辑器专属能力层（Editor 程序集，仅 Editor 版用）                 │
│  ├─ 源码跳转（打开 文件:行）                                     │
│  ├─ Play/Pause/Recompile/Build 状态监听与联动                   │
│  └─ EditorPrefs 持久化适配                                      │
├──────────────────────────────────────────────────────────────┤
│ 共享模型 / 数据层（Runtime 程序集，禁止 using UnityEditor）       │ ← D5 核心
│  ├─ DebugxLogEntry     统一日志条目                             │
│  ├─ 采集器 Collector   （结构化事件 + logMessageReceivedThreaded）│
│  ├─ 环形缓冲 RingBuffer（+ 背压/上限）                          │
│  ├─ 折叠去重 Collapser                                         │
│  ├─ 过滤器 Filter / 搜索 Search                                │
│  ├─ 堆栈解析 StackParser（解析，不打开文件）                     │
│  └─ 统计 Statistics                                            │
├──────────────────────────────────────────────────────────────┤
│ 日志来源                                                        │
│  ├─ Debugx.LogCreator 结构化事件（D3，带成员元数据）             │
│  └─ Application.logMessageReceivedThreaded（全部日志 + 堆栈）    │
└──────────────────────────────────────────────────────────────┘
```

**程序集（asmdef）划分（推荐）**

| 层 | asmdef | 目录 | 依赖 |
|---|---|---|---|
| 共享模型层 | **新增 `DebugxLog.Console`** | `Assets/Plugins/Debugx/Runtime/Console/` | 仅 `UnityEngine` + 现有 Runtime asmdef（取 `ActionHandler<T>`、DLL 的判定 API） |
| Editor 视图层 | 并入现有 Editor asmdef | `Assets/Plugins/Debugx/Editor/Console/` | `DebugxLog.Console` + Runtime + `UnityEditor` |
| 运行时视图层（后续） | 未来新增 `DebugxLog.Console.Runtime.UI` | `Assets/Plugins/Debugx/Runtime/ConsoleUI/` | `DebugxLog.Console` + UIToolkit/uGUI |

> 退化方案：若倾向最小改动，共享模型层可直接放进现有 Runtime asmdef，不新建 asmdef。两者皆可，推荐独立 asmdef 以显式化边界、便于运行时按需引用。

**判据（贯穿全案）**：一个类若需要 `using UnityEditor;` 才能编译，它就**不属于**共享模型层，必须留在 Editor 专属层。

---

## 4. 核心数据契约

### 4.1 DLL 结构化事件 `DebugxRawLog`（D3）

**注入点**：`DebugxDll~/Debugx/Debugx.cs` 中三个 `LogCreator` 重载汇聚的私有三参方法 `LogCreator(LogType type, DebugxMemberInfo info, object message, bool showTime, bool showNetTag)`（约 552 行）——这是**唯一**同时持有「成员元数据 `info` + 原始 `message` + 即将拼出的最终显示串」的位置。事件**仅在此触发一次**，前置两个重载不得各自触发。

**触发时机**：完成 `_logSb` 拼串、即将调用 `unityLogger.Log` 之前；**锁内取值（`finalText`）、锁外派发**（避免订阅者回调内再打日志导致 `_logSbLocker` 重入死锁）；建议先派发事件再 `unityLogger.Log`，并用 `try/catch` 隔离订阅者异常。

**承载形式**：`ActionHandler<T>` 位于 Runtime 程序集、DLL 无法引用，故 DLL 内用**原生 `public static event Action<DebugxRawLog>`**（Runtime 侧可再包一层 `ActionHandler` 转发，但那属显示/适配层）。载荷为 `readonly struct`（值类型、零堆分配）：

```csharp
namespace DebugxLog
{
    public enum DebugxLogCategory { Member, Admin, Unregistered }

    public readonly struct DebugxRawLog
    {
        // 成员元数据（来自 DebugxMemberInfo）
        public readonly int    Key;               // >0 自定义；-1 Normal；-2 Master；0 Admin；未注册=哨兵 int.MinValue
        public readonly string Signature;
        public readonly string ColorHex;          // 六位 hex，无 '#'；空=无色
        public readonly string Header;            // haveHeader ? header : null
        public readonly bool   LogSignatureShown;
        public readonly DebugxLogCategory Category; // Member / Admin / Unregistered，Console 直接用无需推断
        // 日志本体
        public readonly LogType LogType;          // Debugx 只产生 Log/Warning/Error
        public readonly object  RawMessage;       // 原始 message（未着色、未加任何前缀）
        public readonly string  FinalText;        // 拼好的最终显示串（含 [Debugx]/netTag/time/[Sig:]/<color>）
        public readonly bool    ShowNetTag, ShowTime, IsServer; // netTag 在触发点对 _serverCheckDelegate 求值落为枚举/布尔
        public readonly DateTime TimestampUtc;    // 线程安全；frameCount 见 §4.3 线程约定，不放入本结构
        // 注：堆栈不放入本结构，见 §4.3
    }

    public static partial class Debugx
    {
        public static event Action<DebugxRawLog> OnRawLog;
        public static bool IsDebugxTagged(string message); // 供 Runtime 层复用同源标签判定（见 §4.3）
    }
}
```

**约束**：
- **零成本关闭**：触发处写 `var h = OnRawLog; if (h != null) { var raw = ...; h.Invoke(raw); }`——未订阅时连 struct 都不构造；`DEBUG_X` 未定义时整条 `LogCreator` 链被 `[Conditional]` 裁掉。
- **向后兼容**：不改动任何现有公开 API 签名；`unityLogger.Log(type, finalText)` 行为不变；`[HideInCallstack]` + DLL 边界带来的"堆栈停在业务调用点"红利不受影响。
- **新增 `Debugx.IsDebugxTagged(string)`**：把"是否 `[Debugx]` 标签"的判定收敛到 DLL 一处，Runtime 层调用它做去重判据，**避免各处复制正则**（CLAUDE.md 要求 tag 与正则同步）。
- **落地**：改 `DebugxDll~/Debugx/` 后 `msbuild Debugx.sln /p:Configuration=Release`，回拷 `DebugxLog.dll` + `.xml` 到 `Assets/Plugins/Debugx/Runtime/`（注意 `Debugx.csproj` 的 `UnityEngine.dll` HintPath 需匹配本机 Unity 路径）；四处版本号同步（`package.json` / `Debugx.cs` 头 + Version + Update log / `AssemblyInfo.cs` / `CHANGELOG.md`），本次为"新功能"位递增。

### 4.2 统一日志条目 `DebugxLogEntry`

Console 的唯一原子数据单元，容纳两类来源（Debugx 结构化事件 / 非 Debugx 兜底），非 Debugx 条目的成员专有字段取空。**放共享模型层，不引用 `UnityEditor`，颜色以 `colorHex` 字符串承载**（`ColorUtility.HtmlToColor` 属 `UnityEngine`，解析在显示层做）。

关键字段：`logType`、`rawMessage`(原文，保留 `<color>`/标签)、`message`(剥离富文本与 `[Debugx]` 的纯净文本，供搜索/折叠/去重)、`stackTrace`(原始)、`parsedFrames`(惰性解析)、`memberKey/memberSignature/colorHex/header/logSignature`、`netTag`、`isDebugx`、`isAdminChannel`、`category`、`timestamp`、`frameCount`、`sequenceId`(原子自增，稳定排序/配对锚点)、`collapseCount`(可变)。

> **统一冲突裁决（评审发现草稿矛盾）**：`message` 字段**同时保留两版**——`rawMessage`(原文，显示上色用) + `message`(剥离版，搜索/折叠 key 用)。这样既能还原用户自己写的 `<color>`，又能稳定匹配。

### 4.3 双通道采集与去重（全案地基，已定为线程内 FIFO 配对 · D6）

一条 Debugx 日志会**同时**出现在两条通路：结构化事件（带元数据、无堆栈）与 `logMessageReceived`（带堆栈、message 是拼好的 `finalText`）。必须去重且"结构化优先补元数据、回调补堆栈"。

**决定方案（线程内 FIFO 配对 · D6，开工前经 §10 Spike 验证）**：
- Console 订阅 `Application.logMessageReceivedThreaded`（**同步、在产生日志的原线程回调**）捕获全部日志与堆栈。
- DLL 结构化事件在 `unityLogger.Log` **之前**、同线程同步触发；因此"结构化事件 N → 其对应的 threaded 回调 N"在同一线程上严格相邻、顺序稳定。
- 采集器为每个线程维护一个 **pending FIFO**：结构化事件把元数据入队；紧随其后的 threaded 回调若 `Debugx.IsDebugxTagged(message)==true`，则出队合并（元数据 + 该回调的堆栈）产出一条完整 Debugx 条目；未命中标签的回调 → Uncategorized 条目。
- 优点：拿到**可靠的 Unity 堆栈**（保住双击/点帧跳源码），元数据完整，且**不依赖脆弱的"帧+消息指纹"猜测**（规避"同帧同文本多次打印"的误配）。
- 风险：依赖"事件与回调同线程同步相邻"的时序保证，**需一个技术预研(Spike)先验证**（见 §10）。

**线程约定**：`DateTime.Now`/`sequenceId`(用 `Interlocked`) 线程安全；`Time.frameCount` 是主线程 API，**不在后台线程/DLL 事件里读取**——`frameCount` 由主线程消费侧尽力赋值（后台条目允许近似）。跨线程只做"入队"，剥标签/配对/折叠/写缓冲/统计全部收敛到主线程单线程执行，故缓冲/折叠/统计**内部无需加锁**。

**背压**：pending FIFO 与入队队列都要有**上限 + drop-oldest**，并记录"丢弃计数"提示，避免后台日志洪泛时 `EditorApplication.update` 失焦降频导致队列无界增长 OOM。

**与 `LogOutput` 的关系**：`LogOutput` 现有 `logMessageReceived` 订阅是**写文件**用途，Console 是**另行订阅**读取，二者互不干扰；`LogOutput` 文件落盘逻辑不动。

---

## 5. 共享模型层模块（Runtime，零 `UnityEditor` 依赖）

| 模块 | 职责 | 对外契约（概念） |
|---|---|---|
| `ILogCollector` | 双通道采集 + 去重合流，唯一数据入口 | `event/ActionHandler<DebugxLogEntry> OnEntry; Start(); Stop();`（订阅幂等，参考 `LogOutput._isSubscribed`） |
| `LogRingBuffer` | 定容环形缓冲，超容丢最旧；维护分类型/分成员计数 | `Add(); Snapshot(); Clear(); Capacity; Count; OnChanged` |
| `LogCollapser` | **全局**去重折叠（对标原生，非仅相邻），累加 `collapseCount` | key 默认 `(logType, message)`；Debugx 扩展可选 `(logType, memberKey, message)`；堆栈是否入 key 由开关控制（默认否） |
| `LogFilter` | 把所有筛选维度组合成单一 `Predicate<DebugxLogEntry>` | 输入纯 POCO `LogFilterCriteria { showLog/showWarning/showError, onlyDebugx, showAdmin, ISet<int> memberKeys, SearchQuery }` |
| `LogSearchMatcher` | 子串(默认)/正则(可选)/大小写开关；可选搜堆栈 | `IsMatch(entry, SearchQuery)`；正则编译缓存，非法正则回退子串并回状态 |
| `StackTraceParser` | 堆栈文本 → 帧列表（纯字符串解析，**不打开文件**） | `IReadOnlyList<StackFrameInfo> Parse(string)`；`StackFrameInfo{symbol,filePath,line}`，兼容 `at File:line` 与 `(at Assets/..cs:line)` 两种格式 |
| `LogStatistics` | 各 LogType / 各成员 / 总数计数 | 增量维护为主（`OnEntryAdded/OnCleared`），供工具栏角标 |
| `FileLogImporter` | 解析 `.log` 文件 → `DebugxLogEntry` 流（D8）。含两套解析器：Debugx 落盘格式 `[MM-dd HH:mm:ss][frameCount]msg[stack]`、Unity `Editor.log`/`Player.log` 多行格式 | `IEnumerable<DebugxLogEntry> Import(string path, LogFileFormat fmt)`；纯字符串解析，路径获取（Editor.log/Player.log 系统路径）由平台/Editor 层提供 |

> `LogFilterCriteria`/`SearchQuery` 是纯数据结构，由 Editor 版与运行时版共同构造，UI 只负责把控件状态填进去。
> "根据帧打开源码"**不在** `StackTraceParser`——解析(共享) vs 打开(Editor 专属)是本案分层的典型样例。
> Editor 能力注入共享层用**明确接口**：`ISourceNavigator`(打开文件:行)、`IPlayModeController`(Play 暂停)、`ICompilationWatcher`(重编译/构建监听)。Editor 显示层注册真实现，运行时版不注册→静默降级。

---

## 6. 功能清单（对标原生全部 + Debugx 扩展）

> **适用层**：`共享模型` / `运行时也需`(显示层，两端共用渲染契约) / `Editor专属`(必须隔离)。
> **顺序**：P0(核心骨架) / P1(对标完整度) / P2(打磨)。**均为 v1 必做，仅表开发次序。**

### (A) 对标原生 Unity Console

| 编号 | 功能 | 适用层 | 顺序 |
|---|---|---|---|
| A1 | 全量捕获 + 结构化事件消费 + 环形缓冲(全字段) + 堆栈解析 + 线程安全入队 | 共享模型 | P0 |
| A2.1 | Clear（清空缓冲/视图/计数/折叠） | 共享模型 | P0 |
| A2.2 | Clear 按钮**带下拉**：Clear on Play / on Recompile / on Build 三勾选项 | Editor专属 | P1(Build→P2) |
| A2.3 | Collapse 折叠（**全局**去重 + 计数徽标） | 共享模型 | P0 |
| A2.4 | Error Pause（Error/Exception 时 `EditorApplication.isPaused`；不含普通 Assert） | Editor专属 | P1 |
| A2.5 | Log / Warning / Error 三个计数过滤按钮（带图标 + 数量，999+ 溢出）；**Assert/Exception 并入 Error 档，无独立开关** | 共享模型 | P0 |
| A3.1 | 搜索框（子串，大小写不敏感）+ 与类型/成员过滤 AND 叠加 | 共享模型 | P0 |
| A3.2 | 搜索命中高亮 | 运行时也需 | P1 |
| A4.1 | 双栏布局（`TwoPaneSplitView`，列表 + 详情/堆栈，可拖分隔） | 运行时也需 | P0 |
| A4.2 | 条目图标（Log/Warn/Error）+ 折叠计数徽标 | 运行时也需 | P0 |
| A4.3 | 虚拟化列表（UIToolkit `ListView`，`FixedHeight`，绑过滤后索引） | 运行时也需 | P0 |
| A4.4 | 选中 + 详情面板（完整 message + 成员信息 + 堆栈）+ 详情独立滚动 | 运行时也需 | P0 |
| A4.5 | 键盘上下键导航 | 运行时也需 | P1 |
| A4.6 | 自动滚到底 + 用户上滚时暂停跟随 | 运行时也需 | P1 |
| A4.7 | 条目多行预览（行数可配，对标原生 1–10 行） | 运行时也需 | P2 |
| A4.8 | 时间戳/帧号显示（可选） | 共享模型 | P1 |
| A4.9 | 富文本/颜色渲染（`Label.enableRichText`，保留成员着色） | 运行时也需 | P0 |
| A5.1 | 双击条目跳源码（跳"首个 `Assets/` 用户帧"；非 Debugx 日志同此规则） | Editor专属 | P0 |
| A5.2 | 点击堆栈帧跳源码 + 源码可打开性判定 | Editor专属 | P0/P1 |
| A5.3 | 堆栈显示模式 ScriptOnly / Full（右键切换）；注意与 `Application.SetStackTraceLogType` 的交互（见风险 R5） | 共享(数据)+Editor(菜单) | P1 |
| A6.1 | 右键 Copy（单条）/ Copy 全部 | 运行时也需 | P1/P2 |
| A6.2 | Open Editor Log / Player Log | Editor专属 | P2 |
| A6.3 | 空状态占位提示 | 运行时也需 | P2 |

### (B) Debugx 成员维度扩展

| 编号 | 功能 | 适用层 | 顺序 |
|---|---|---|---|
| B1 | 按成员 key/签名筛选（工具栏多选下拉，菜单项带成员色块）+ "未分类"项 | 共享模型 | P0 |
| B2 | 按成员颜色着色显示（取 `DebugxMemberInfo.color`，无色回退 logType 默认色） | 运行时也需 | P0 |
| B3 | "仅显示 Debugx"过滤开关（隐藏 Uncategorized） | 共享模型 | P0 |
| B4 | 未分类日志分组（中性色/统一图标，可单独过滤） | 共享模型 | P0 |
| B5 | LogAdm 管理通道(Admin key=0)专属标识；预设成员(Normal/Master/Admin) vs 自定义(`KeyValid`=key>0)视觉区分 | 运行时也需 | P1/P2 |
| B6 | 成员 header 展示（`haveHeader` 时 `header : message`） | 运行时也需 | P1 |
| B7 | 网络标签 Server/Client 展示 + 可按其过滤 | 共享模型 | P1 |
| B8 | 运行时源头开关整合进工具栏（**独立分组**，与"显示过滤"区分）：`enableLog`/`enableLogMember`/`logThisKeyMemberOnly` + 逐成员 `SetMemberEnable`；**仅 Play 期可用**，非 Play 期 `SetEnabled(false)` 并提示 | 运行时也需 | P1 |
| B9 | 成员元数据面板（选中条目的 key/signature/color/header/logSignature） | 运行时也需 | P2 |

> **关键语义区分**：B1/B3/B7 是**显示层过滤**（只改视图，不影响真实打印）；B8 是**运行时源头开关**（改 DLL 状态，影响是否真的打印/写文件）。UI 必须分组并加不同标题，避免用户混淆"没看到日志"是被过滤了还是根本没产生。

### (C) 历史日志文件数据源（D8 确认）

| 编号 | 功能 | 适用层 | 顺序 |
|---|---|---|---|
| C1 | 打开/导入 Debugx 落盘文件 `DebugxLog.log`，解析为 `DebugxLogEntry`（格式见 `LogOutput`：`[MM-dd HH:mm:ss][frameCount]msg[stack]`） | 共享模型 | P1 |
| C2 | 打开 Unity `Editor.log`（提供其各平台默认路径快捷定位）解析多行条目 + 堆栈 | 共享(解析)+Editor(取路径) | P1 |
| C3 | 打开真机 `Player.log`（各平台 `persistentDataPath`/系统路径，或开发者拷回的任意 `.log`） | 共享(解析)+平台(取路径) | P2 |
| C4 | 文件源与实时源统一进同一列表/筛选/搜索/堆栈视图；顶部可切换"实时 / 文件"数据源或标注来源 | 共享模型 | P1 |

> **重要限制**：`LogOutput` 落盘时已剥掉 `[Debugx]` 标签与 `<color>`，故从 `DebugxLog.log` 反解的历史 Debugx 日志**丢失成员 key/颜色等元数据**，只能按纯文本 + 类型 + 时间 + 帧 + 堆栈展示，**成员维度筛选对历史文件不可用**；`Editor.log`/`Player.log` 本就是 Unity 原始日志、无 Debugx 成员信息，同样按"未分类"展示。若要历史文件也保留成员维度，需将 `LogOutput` 落盘格式改为可解析结构（额外记 key/signature），属独立可选改动（风险 R9）。

### 加分项（非 v1 必做，供后续排期）
整行成员分色背景 / 成员 Solo 与批量静音 / 日志导出 / 正则搜索 / Pin 收藏条目 / 堆栈内 Debugx 内部帧折叠 / 命名过滤预设 / Server-Client 分色列。

---

## 7. Editor 版（UIToolkit）

- **窗口**：`DebugxConsoleWindow : EditorWindow`，`CreateGUI()` 构建视觉树（UXML + USS，提供 Dark/Light 变量；成员色运行时内联注入）。接管菜单 `Window/Debugx/DebugxConsole`。
- **布局**：外层横向 `TwoPaneSplitView`(成员侧栏可折叠 / 主体) 套内层竖向 `TwoPaneSplitView`(虚拟化列表 / 详情堆栈)；顶部 `Toolbar`。
- **列表虚拟化**：`ListView` + `FixedHeight`；`makeItem` 建行模板(图标+成员色条+文本+折叠徽标+时间)，`bindItem` 只回填数据、**零分配**（禁止 `bindItem` 内 `new` 子元素）。绑定过滤后索引列表，筛选/搜索/折叠只重算索引不复制条目。
- **刷新**：脏标记 + `EditorApplication.update` 节流；有新日志才刷（优先 `RefreshItems`/增量，避免全量 `Rebuild`）；空闲零重绘；UI 刷新只在主线程。
- **源码跳转**：`StackTraceParser` 出帧 → `ISourceNavigator`(Editor 实现，`InternalEditorUtility.OpenFileAtLineExternal`/`AssetDatabase.OpenAsset`)；双击取首个 `Assets/` 用户帧；解析阶段过滤 `DebugxLog.*`/`*Logger` 帧，与 `[HideInCallstack]` 对齐。
- **Play/重编译联动**：`playModeStateChanged`/`CompilationPipeline`/`AssemblyReloadEvents` 都在 Editor 专属层，通过接口回调驱动共享层 Clear，共享层不引用 `UnityEditor`。
- **持久化**：工具栏各开关、搜索、成员多选、分栏比例、侧栏折叠态存 `EditorPrefs`（键前缀 `Debugx.Console.`），封装在 Editor 层，共享层只收纯数据。
- **迁移（D7）**：新窗口**吸收**旧 `DebugxConsole.cs` 的开关能力（含 Test 开关）并接管菜单入口，随后**移除**旧 `DebugxConsole.cs`（你已同意删代码；删除动作在实现阶段执行，git 提交由你操作）。成员定义仍在 ProjectSettings>Debugx(IMGUI) 维护，Console **只消费不修改**成员定义，可提供"打开 ProjectSettings"快捷入口；成员变更经 `DebugxProjectSettingsAsset.OnApplyTo` 通知窗口刷新成员列表。
- **本地化**：沿用 `DebugxStaticData.IsChineseSimplified` 三元字符串，UXML 静态文本留 `name` 在 `CreateGUI` 回填。

## 8. 运行时版复用规划（后续阶段，边界现在钉死）

- **目标**：`DEBUG_X` 的 Debug 包在设备屏幕上提供可交互 Console（查看/搜索/按成员与类型过滤/看堆栈文本/折叠），**取代**现有 `LogOutput.DrawGUI`（当前 `OnGUI`、仅 100 条、只存 Message+LogType 的简陋屏显）。
- **复用**：§5 全部模块完全复用（采集/缓冲/折叠/过滤/搜索/堆栈解析/统计）。
- **降级**（无 `UnityEditor`）：源码跳转、Clear on Recompile、Pause on Error 缺省；堆栈帧仅文本展示不可跳。仍可用：成员着色、网络标签、时间戳、成员/类型过滤、搜索、折叠、堆栈文本、导出/复制。
- **显示技术（D9）**：Runtime UIToolkit（UIDocument/PanelSettings），与 Editor 版共享布局/数据绑定心智，天然虚拟化（`ListView`）；包体目标 `unity:2022.3` 满足其可用性。共享模型层与 UI 技术无关，故该选择只影响薄薄的显示层。
- **唤出**：移动端多指长按/摇一摇、桌面/主机快捷键或手柄组合、可选角落半透明热区；统一走"切换可见性"入口，全部 `DEBUG_X` 门控。
- **容量/性能**：定容环形缓冲（移动端默认 500–1000、桌面 2000–5000，可配）；堆栈是内存大头，建议按 LogType 差异化保留（如 Log 级不留或截断，Warn/Error 保留）；高频路径避免每条分配；长列表必须虚拟化。

---

## 9. 决策记录（原开放项，已确认）

| 项 | 结论 | 落点 |
|---|---|---|
| Q1 堆栈来源/去重 | **线程内 FIFO 配对**（`logMessageReceivedThreaded` 堆栈 + 结构化事件同线程 FIFO 补元数据），开工前 Spike 验证 | D6 · §4.3 · §10 |
| Q2 旧面板去留 | **吸收后移除**旧 `DebugxConsole.cs` | D7 · §7 |
| Q3 历史日志文件源 | **同时支持** `DebugxLog.log` / `Editor.log` / `Player.log`（含成员元数据限制） | D8 · §6-C · §5 |
| Q4 运行时显示技术 | **Runtime UIToolkit** | D9 · §8 |

> 其余评审分歧已按推荐默认统一（见文末"已采纳默认"）。这些默认无需单独确认，如有异议再提。

## 10. 建议开发路线

1. **P0-Spike（先行技术预研，1 步）**：最小原型验证 Q1-a 的"结构化事件与 threaded 回调同线程同步相邻"时序，确认能可靠去重且拿到干净堆栈。**通过后再冻结 §4.1/§4.3 的字段与去重规则**，否则回退 Q1-b/c。
2. **DLL 改动**：按 §4.1 加 `OnRawLog` + `IsDebugxTagged`，重编译回拷、版本号四处同步。
3. **共享模型层**（§5）：新建 `DebugxLog.Console` asmdef，实现八个模块（采集/环形缓冲/折叠/过滤/搜索/堆栈解析/统计/文件导入器）+ 三个注入接口。
4. **Editor 版**（§7）：UIToolkit 窗口 + 工具栏 + 双栏 + 虚拟化 + 源码跳转 + 迁移并移除旧面板。
5. **打磨 + 历史文件源**：P1/P2 功能补齐，含 §6-C 的 `DebugxLog.log`/`Editor.log`/`Player.log` 导入与来源切换。
6. **运行时版**（§8）：后续阶段，复用模型层只写 Runtime UIToolkit 显示层。

## 11. 风险清单

| 编号 | 风险 | 等级 | 应对 |
|---|---|---|---|
| R1 | 双通道去重 + 堆栈来源方案不收敛 | **极高** | §10-Spike 先验证再冻结（Q1） |
| R2 | UIToolkit `ListView` 虚拟化 × 富文本多行行高冲突 | 高 | v1 用 `FixedHeight`；多行预览(A4.7)延后并单独评估 `DynamicHeight` |
| R3 | 堆栈解析跨格式 + 非 Debugx/Packages/无行号帧 + 非 Debugx 日志双击目标 | 中高 | 解析器兼容两种格式；双击统一取首个 `Assets/` 用户帧 |
| R4 | 全局折叠 × 过滤 × 搜索叠加 + 环形缓冲淘汰时折叠组维护 | 中高 | 增量维护可见索引，避免每帧全扫 |
| R5 | `Application.SetStackTraceLogType` 设为 None 时通道 B 无堆栈 | 中 | Q1-a 时提示此交互；Q1-b(DLL 自采)天然免疫 |
| R6 | 后台日志洪泛 + `EditorApplication.update` 失焦降频 → 队列无界 | 中 | 入队/pending 队列有界 + drop-oldest + 丢弃计数提示 |
| R7 | domain reload 清空静态缓冲（关掉 Clear on Recompile 也照丢，反直觉） | 中 | **v1 明确不保证跨重编译保留**；跨 reload 持久化列为后续可选 |
| R8 | `frameCount` 后台线程读取不可靠 | 低中 | DLL 事件不放 frameCount；主线程消费侧尽力赋值 |
| R9 | 历史文件源：`DebugxLog.log` 已被落盘时剥标签/剥色 → 成员维度丢失；`Editor.log`/`Player.log` 多行格式解析（跨条目、堆栈跨行、平台差异）易碎 | 中 | 明确历史文件仅按纯文本+类型+时间+堆栈展示、成员筛选不可用；解析器按 Unity 已知格式健壮处理、容错跳过坏行；"落盘保留成员维度"作为可选独立改动 |
| R10 | Runtime UIToolkit 在目标设备/低版本上的成熟度与性能 | 低中 | 包体目标 2022.3 可用；运行时版属后续阶段，届时在目标机型实测，必要时 uGUI 兜底（不影响共享模型层） |

---

### 附：评审已采纳的默认（无需单独确认，除非你有异议）
- `message` 同存 `rawMessage`(原文) + `message`(剥离版)；搜索/折叠用剥离版，显示用原文。
- Collapse 采用**全局**去重（对标原生），非仅相邻。
- Assert/Exception 无独立开关，并入 Error 档计数与过滤；Error Pause 对 Error/Exception 生效。
- 标签判定收敛为 DLL 的 `Debugx.IsDebugxTagged`，Runtime 层不复制正则。
- Editor 能力注入共享层统一用 `ISourceNavigator`/`IPlayModeController`/`ICompilationWatcher` 接口。
- 环形缓冲默认容量：Editor 数千级（如 5000，可配）、移动端 500–1000、桌面 2000–5000。
- 独立 `DebugxLog.Console` asmdef（退化=并入 Runtime asmdef）。
