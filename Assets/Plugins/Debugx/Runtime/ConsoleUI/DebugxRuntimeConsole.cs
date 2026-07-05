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

#if DEBUG_X
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
            _store = new DebugxLogStore(DebugxRuntimeConsoleStyle.RuntimeBufferCapacity);
            _store.Start();
        }

        private void OnDisable()
        {
            _store?.Stop();
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
