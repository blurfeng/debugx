using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace DebugxLog.Console.Runtime
{
    /// <summary>
    /// The runtime, on-device Debugx Console: a Runtime-UIToolkit log viewer that drives the SAME shared
    /// <see cref="DebugxLogStore"/> as the Editor Console (<c>DebugxConsoleWindow</c>). This class is display-only —
    /// all capture / buffering / filtering / collapsing / search / statistics live in the shared model layer
    /// (<c>DebugxLog.Console</c>) and are reused unchanged. Editor-only powers (source navigation, Error Pause,
    /// Clear-on-Recompile) are absent by design; the runtime degrades to text-only stack frames.
    ///
    /// It self-mounts at play start (DEBUG_X only) onto a DontDestroyOnLoad GameObject carrying a <see cref="UIDocument"/>
    /// whose <see cref="PanelSettings"/> is loaded from <c>Resources/Console</c>. The panel starts hidden behind a small
    /// floating button; the backquote key toggles it on desktop.
    ///
    /// 运行时、设备端的 Debugx Console：一个 Runtime-UIToolkit 日志查看器，驱动与 Editor 版 Console
    /// （<c>DebugxConsoleWindow</c>）相同的共享 <see cref="DebugxLogStore"/>。本类只有显示层——采集/缓冲/过滤/折叠/搜索/统计
    /// 都在共享模型层（<c>DebugxLog.Console</c>）里原样复用。Editor 专属能力（源码跳转、错误暂停、重编译清空）按设计缺省；
    /// 运行时降级为仅文本堆栈。游戏启动时自挂载（仅 DEBUG_X），挂到带 <see cref="UIDocument"/> 的 DontDestroyOnLoad
    /// GameObject 上，其 <see cref="PanelSettings"/> 从 <c>Resources/Console</c> 加载。面板初始隐藏在一个小悬浮按钮后，
    /// 桌面端反引号键开合。
    /// </summary>
    [DisallowMultipleComponent]
    public partial class DebugxRuntimeConsole : MonoBehaviour
    {
        // The PanelSettings asset name, loaded from any Resources folder (user-authored: Resources/Console.asset).
        // PanelSettings 资源名，从任意 Resources 目录加载（用户创建：Resources/Console.asset）。
        private const string PanelSettingsResource = "Console";

        // The bundled fallback ThemeStyleSheet name (package-internal: Resources/DebugxRuntimeTheme.tss). Assigned to
        // the PanelSettings when it has no theme set — e.g. the authored theme lived outside the package and wasn't
        // shipped as UPM. Loaded by name, so no cross-package GUID reference is involved.
        // 包内自带的回退主题名（包内部：Resources/DebugxRuntimeTheme.tss）。当 PanelSettings 未设置主题时赋给它——例如原
        // 主题在包外、未随 UPM 发布。按名加载，不涉及任何跨包 GUID 引用。
        private const string ThemeResource = "DebugxRuntimeTheme";

        // High sorting order so the console renders above the game's own runtime UI panels.
        // 高排序序，使 Console 渲染在游戏自身运行时 UI 面板之上。
        private const float PanelSortingOrder = 30000f;

        private static DebugxRuntimeConsole _instance;

        // Early-capture store, created at BeforeSceneLoad — i.e. BEFORE DebugxManager's AfterSceneLoad Awake — so logs
        // emitted before this console's GameObject even exists (e.g. DebugxManager.Awake) are still captured. Only the
        // collector subscription runs that early; entries queue in the collector until OnEnable hands this store to the
        // instance and the first Update pumps them into the buffer. See EarlyCapture. Without this, the runtime console
        // (unlike the Editor Console, whose collector is already subscribed / whose logs persist across domain reloads)
        // would miss everything logged before AfterSceneLoad, because the Unity log channels have no history.
        // 提前采集 store，在 BeforeSceneLoad 创建——即早于 DebugxManager 的 AfterSceneLoad Awake——使本 Console 的 GameObject
        // 尚未存在时发出的日志（如 DebugxManager.Awake）也能被采集。此时只做采集器订阅；条目在采集器里排队，直到 OnEnable 把该
        // store 交接给实例、首帧 Update 将其排入缓冲。见 EarlyCapture。没有它，运行时 Console（不同于 Editor 版——其采集器早已
        // 订阅、日志还能跨域重载留存）会漏掉 AfterSceneLoad 之前的一切，因为 Unity 日志通道没有历史。
        private static DebugxLogStore _earlyStore;

#if DEBUG_X
        // Subscribe to the log channels as early as possible (before scenes load, hence before DebugxManager.Awake's
        // AfterSceneLoad callback), so early logs land in the collector's queue and are drained by the console's first
        // Pump. Only the subscription happens here; the panel UI is still built later in Bootstrap. Gated by the same
        // opt-out as Bootstrap so a disabled runtime console captures nothing.
        // 尽早订阅日志通道（在场景加载前，故早于 DebugxManager.Awake 的 AfterSceneLoad 回调），使早期日志进入采集器队列，由
        // Console 首帧 Pump 排空。此处仅订阅；面板 UI 仍在 Bootstrap 里稍后构建。与 Bootstrap 同一开关门控，关闭时不采集。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EarlyCapture()
        {
            if (_earlyStore != null || _instance != null || !Application.isPlaying) return;
            if (!DebugxStaticData.RuntimeConsoleEnabled) return;

            _earlyStore = new DebugxLogStore(DebugxRuntimeConsoleStyle.RuntimeBufferCapacity);
            _earlyStore.Start();
        }

        // Auto-created after the first scene loads, only in Play. Mirrors DebugxManager's [RuntimeInitializeOnLoadMethod]
        // bootstrap; no scene setup or prefab is required.
        // 首个场景加载后自动创建，仅 Play 期。对齐 DebugxManager 的 [RuntimeInitializeOnLoadMethod] 引导；无需场景配置或预制体。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null || !Application.isPlaying) return;

            // User opt-out (Editor Console > Runtime > "Enable in-game runtime Console"): skip self-creation when off.
            // Read once here, so toggling it applies on the next entry to Play. 用户可在 Editor Console > Runtime >
            // “启用游戏内运行时 Console” 关闭：关闭时不自建。此处读取一次，故改动在下次进入 Play 生效。
            if (!DebugxStaticData.RuntimeConsoleEnabled) return;

            PanelSettings panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            if (panelSettings == null)
            {
                // No console will be created, so release the early-capture subscription (see EarlyCapture) — otherwise it
                // would keep collecting with no consumer to pump it. 不会创建 Console，故释放提前采集订阅（见 EarlyCapture）——
                // 否则它会一直采集却无人 Pump。
                _earlyStore?.Stop();
                _earlyStore = null;
                Debug.LogWarning(
                    $"[Debugx] 运行时 Console 未启用：未找到 PanelSettings 'Resources/{PanelSettingsResource}'。" +
                    " 请在任意 Resources 目录下创建 UI Toolkit > Panel Settings Asset 并命名为 Console。");
                return;
            }

            // When installed as a UPM package, the PanelSettings' theme (themeUss) may point at a Theme Style Sheet
            // that lived in the dev project (Assets/UI Toolkit/UnityThemes/...) and is NOT shipped inside the package,
            // so it resolves to null in the consumer project. A runtime panel with no theme refuses to render
            // ("No Theme Style Sheet set to PanelSettings ..."). Fall back to the package-bundled default theme,
            // loaded by name so no cross-package GUID is involved. Only fill in when unset, so a theme the user
            // deliberately assigned is respected. Assigned BEFORE UIDocument.panelSettings so no warning is emitted.
            // 作为 UPM 包安装时，PanelSettings 的主题（themeUss）可能指向开发工程里（Assets/UI Toolkit/UnityThemes/...）、
            // 未随包发布的主题样式表，在消费者项目里解析为 null。缺少主题的运行时面板拒绝渲染
            // （"No Theme Style Sheet set to PanelSettings ..."）。回退到包内自带的默认主题，按名加载、不涉及跨包 GUID。
            // 仅在未设置时填入，以尊重用户特意指定的主题。在赋值 UIDocument.panelSettings 之前完成，故不会触发警告。
            if (panelSettings.themeStyleSheet == null)
            {
                ThemeStyleSheet theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
                if (theme != null)
                {
                    panelSettings.themeStyleSheet = theme;
                }
                else
                {
                    Debug.LogWarning(
                        $"[Debugx] 运行时 Console 主题缺失：PanelSettings '{PanelSettingsResource}' 未设置 Theme Style" +
                        $" Sheet，且未找到包内回退主题 'Resources/{ThemeResource}'。面板可能无法正常渲染。");
                }
            }

            // Create inactive first so PanelSettings is assigned BEFORE UIDocument.OnEnable builds the panel.
            // 先建为未激活，使 PanelSettings 在 UIDocument.OnEnable 建面板之前就已赋值。
            var go = new GameObject("DebugxRuntimeConsole");
            go.SetActive(false);
            DontDestroyOnLoad(go);

            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            doc.sortingOrder = PanelSortingOrder;

            _instance = go.AddComponent<DebugxRuntimeConsole>();
            _instance._document = doc;

            go.SetActive(true);
        }
#endif

        private UIDocument _document;
        private DebugxLogStore _store;
        private readonly LogFilterCriteria _criteria = new LogFilterCriteria();
        private readonly List<CollapsedRow> _rows = new List<CollapsedRow>();

        private bool _uiBuilt;
        private bool _visible;

#if ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
        // Multi-finger tap debounce: fires once when the finger count first reaches the threshold, re-armed only after
        // every finger lifts (so holding fingers down or adjusting them doesn't retoggle). Shared by both input backends;
        // guarded so it isn't an unused field (CS0414) when neither input backend is compiled in.
        // 多指点击防抖：手指数首次达到阈值时触发一次，仅在全部手指抬起后重新武装（故按住或调整手指不会反复开合）。新旧输入系统共用；
        // 加守卫避免在两套输入都未编译进来时成为未使用字段（CS0414）。
        private bool _multiTouchArmed = true;
#endif

        private void OnEnable()
        {
            // Reuse the early-capture store (subscribed at BeforeSceneLoad) so logs from before this GameObject existed —
            // e.g. DebugxManager.Awake — are not lost. Fall back to a fresh store if early capture didn't run (e.g. the
            // console was enabled after BeforeSceneLoad). Start() is idempotent, so reusing an already-started store is safe.
            // 复用提前采集的 store（在 BeforeSceneLoad 订阅），使本 GameObject 存在之前的日志——如 DebugxManager.Awake——不丢失。
            // 若提前采集未运行（如在 BeforeSceneLoad 之后才启用 Console）则退回新建。Start() 幂等，复用已启动的 store 也安全。
            _store = _earlyStore ?? new DebugxLogStore(DebugxRuntimeConsoleStyle.RuntimeBufferCapacity);
            _earlyStore = null;
            _store.Start();

            // Load persisted toolbar state here — before the toolbar is built — so each control syncs its initial value
            // and the filter applies from the first frame. 在此加载持久化的工具栏状态——早于工具栏构建——使各控件同步初始值、
            // 过滤从首帧即生效。
            LoadViewPrefs();
        }

        private void OnDisable()
        {
            SaveViewPrefs(); // persist toolbar state on teardown (play exit / GameObject destroy). 拆卸时（退出 Play/销毁）持久化工具栏状态。
            _store?.Stop();
        }

        // Also persist when the app is paused, so a backgrounded-then-killed mobile app keeps its toolbar state (OnDisable
        // may not run in that case). 应用暂停时也持久化，使移动端“后台后被杀”仍保留工具栏状态（此时 OnDisable 可能不执行）。
        private void OnApplicationPause(bool pause)
        {
            if (pause) SaveViewPrefs();
        }

        private void Update()
        {
            if (_store == null) return;

            _store.Pump(); // keep capturing even while the panel is hidden. 即使面板隐藏也持续采集。

            // UIDocument.rootVisualElement can take a frame to become available; retry until it does.
            // rootVisualElement 可能延后一帧才可用；重试直到可用。
            if (!_uiBuilt) TryBuildUI();

            HandleToggleInput();

            if (_uiBuilt && _visible && _store.TryRebuildView())
                RefreshView();
        }

        private void TryBuildUI()
        {
            VisualElement root = _document != null ? _document.rootVisualElement : null;
            if (root == null) return;

            BuildUI(root);
            UpdateCountButtonStates();
            ApplyCriteria();
            ApplyVisible();
            _uiBuilt = true;
        }

        private void HandleToggleInput()
        {
            // Backquote toggles the console on desktop; a multi-finger tap toggles it on touch devices (plus the floating
            // button). The new Input System path takes priority via #if/#elif, so a project with Both input backends
            // enabled compiles only the new path and never toggles twice in one frame (open→close); a project with only
            // the legacy Input Manager falls back to it. 反引号键在桌面端开合 Console；触屏端多指点击开合（外加悬浮按钮）。
            // 用 #if/#elif 让新输入系统优先：同时启用两套输入（Both）的工程只编译新输入分支，不会同一帧开→关抵消；仅旧输入的工程回退到旧输入。
#if ENABLE_INPUT_SYSTEM
            if (ReadToggleInputSystem())
                SetVisible(!_visible);
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (ReadToggleLegacy())
                SetVisible(!_visible);
#endif
        }

#if ENABLE_INPUT_SYSTEM
        // New Input System reader: backquote key + multi-finger tap. Keyboard/Touchscreen can be null when the matching
        // device is absent, so both are null-checked. Active fingers are those whose press is held this frame.
        // 新输入系统读取：反引号键 + 多指点击。无对应设备时 Keyboard/Touchscreen 可能为 null，故判空。活跃手指即本帧处于按下状态的触点。
        private bool ReadToggleInputSystem()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.backquoteKey.wasPressedThisFrame)
                return true;

            int activeTouchCount = 0;
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    if (touches[i].press.isPressed)
                        activeTouchCount++;
                }
            }

            return UpdateMultiTouchArm(activeTouchCount);
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER && !ENABLE_INPUT_SYSTEM
        // Legacy Input Manager reader: backquote key + multi-finger tap. Compiled only when the new Input System is not
        // active (Both routes through the new path above). 旧 Input Manager 读取：反引号键 + 多指点击。仅在未启用新输入系统时编译
        // （Both 走上面的新输入分支）。
        private bool ReadToggleLegacy()
        {
            if (Input.GetKeyDown(KeyCode.BackQuote))
                return true;

            return UpdateMultiTouchArm(Input.touchCount);
        }
#endif

#if ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
        // Multi-finger tap debounce: returns true once when the active-finger count first reaches the threshold, re-armed
        // only after every finger lifts. Shared by both input backends. 多指点击防抖：活跃手指数首次达到阈值时返回一次 true，
        // 仅在全部手指抬起后重新武装。新旧输入系统共用。
        private bool UpdateMultiTouchArm(int activeTouchCount)
        {
            if (activeTouchCount >= DebugxRuntimeConsoleStyle.SummonTouchCount)
            {
                if (_multiTouchArmed)
                {
                    _multiTouchArmed = false;
                    return true;
                }
            }
            else if (activeTouchCount == 0)
            {
                _multiTouchArmed = true; // re-arm only once every finger has lifted. 仅在全部手指抬起后重新武装。
            }

            return false;
        }
#endif

        /// <summary>
        /// Show or hide the console panel. Public so game code can bind its own gesture / hotkey (e.g. shake-to-open).
        /// 显示或隐藏 Console 面板。public 以便游戏代码绑定自己的手势/热键（如摇一摇唤出）。
        /// </summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyVisible();
            if (visible && _uiBuilt)
            {
                _store.MarkViewDirty();
                _store.TryRebuildView();
                RefreshView();
            }
        }

        private void ApplyVisible()
        {
            if (_panelRoot != null) _panelRoot.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_openButton != null) _openButton.style.display = _visible ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
