using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DebugxLog.Tools
{
    /// <summary>
    /// Utility class for outputting logs to a local .txt file.
    /// 输出Log到本地txt文件工具类。
    /// </summary>
    public static class LogOutput
    {
        private static DebugxProjectSettings Settings => DebugxProjectSettings.Instance;
        private static bool Enable => Settings != null && Settings.logOutput;
        private static bool LogStackTrace => Settings != null && Settings.enableLogStackTrace;
        private static bool WarningStackTrace => Settings != null && Settings.enableWarningStackTrace;
        private static bool ErrorStackTrace => Settings != null && Settings.enableErrorStackTrace;
        private static bool RecordAllNonDebugxLogs => Settings != null && Settings.recordAllNonDebugxLogs;

        private const string FileName = "DebugxLog";
        private const string FileNameFull = "DebugxLog.log";
        private const string FileType = ".log";
        private static string _directoryPath;
        
        /// <summary>
        /// Output folder path.
        /// 输出文件夹路径。
        /// </summary>
        public static string DirectoryPath
        {
            get => _directoryPath;
            set { if (!string.IsNullOrEmpty(value)) _directoryPath = value; }
        }
        private static string _savePath;
        private static readonly System.Object _locker = new System.Object();
        private static readonly StringBuilder _logBuilder = new StringBuilder();
        private static StreamWriter _writer;
        private static bool _isSubscribed;

        // Regular expression used to trim color code.
        // 用于裁剪color代码的正则表达式。
        // 标签正则由 DebugxProjectSettings.DebugxTag 常量动态构造，避免标签改动后与硬编码正则不同步。
        // The tag regex is built from the DebugxProjectSettings.DebugxTag constant to stay in sync if the tag changes.
        private static readonly string _tagEscaped = Regex.Escape(DebugxProjectSettings.DebugxTag);
        private static readonly Regex _regexMessageCut = new Regex($@"<color=#([\S.]{{6}})>|</color>|{_tagEscaped}");
        private static readonly Regex _regexRecordMessageTag = new Regex(_tagEscaped);

        /// <summary>
        /// Start of logging.
        /// 记录开始。
        /// </summary>
        public static void RecordStart()
        {
            if (!Enable) return;

            if (string.IsNullOrEmpty(_directoryPath))
            {
                _directoryPath = Application.persistentDataPath;
            }

            _savePath = Path.Combine(DirectoryPath, FileNameFull);

            // PC directory: C:\Users\UserName\AppData\LocalLow\DefaultCompany\ProjectName
            // PC目录为：C:\Users\UserName\AppData\LocalLow\DefaultCompany\ProjectName

            if (string.IsNullOrEmpty(_savePath)) return;

            FileInfo fileInfo = new FileInfo(_savePath);

            // Create folder.
            // 创建文件夹。
            _directoryPath = fileInfo.DirectoryName;
            if (!string.IsNullOrEmpty(_directoryPath) && !Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }

            try
            {
                if (_writer != null)
                {
                    _writer.Dispose();
                    _writer = null;
                }

                // 保持文件流常驻，减少每条日志的打开/关闭开销。
                FileStream fs = new FileStream(_savePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                _writer = new StreamWriter(fs, new UTF8Encoding(false));
                // 关闭自动刷盘，避免每条日志一次同步磁盘写；Log 级别缓冲，Warning/Error 及 RecordOver 时显式 Flush。
                // Disable auto-flush to avoid a sync disk write per line; plain logs buffer, warnings/errors and RecordOver flush explicitly.
                _writer.AutoFlush = false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Debugx] LogOutput.RecordStart 无法打开日志文件。" + ex.Message);
                return;
            }

            if (!_isSubscribed)
            {
                Application.logMessageReceived += LogCallBack;
                _isSubscribed = true;
            }
        }

        /// <summary>
        /// End of logging.
        /// 记录结束。
        /// </summary>
        public static void RecordOver()
        {
            if (_isSubscribed)
            {
                Application.logMessageReceived -= LogCallBack;
                _isSubscribed = false;
            }

            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }

            if (string.IsNullOrEmpty(_savePath) || !File.Exists(_savePath))
            {
                _savePath = null;
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(_savePath);
                if (fileInfo.Length == 0)
                {
                    fileInfo.Delete();
                }
                else
                {
                    // Rename the printed file.
                    // 将打印的文件重命名。
                    string stamp = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss");
                    string filePath = Path.Combine(_directoryPath, $"{FileName}-{stamp}{FileType}");
                    int suffix = 1;
                    while (File.Exists(filePath))
                    {
                        filePath = Path.Combine(_directoryPath, $"{FileName}-{stamp}-{suffix}{FileType}");
                        suffix++;
                    }

                    Debugx.LogAdmWarning($"logOutput over, file path : {filePath}");
                    File.Move(_savePath, filePath);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Debugx] LogOutput.RecordOver 执行失败。" + ex.Message);
            }

            _savePath = null;
        }

        private static void LogCallBack(string message, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(_savePath)) return;

            message = message ?? string.Empty;
            if (!RecordAllNonDebugxLogs && !_regexRecordMessageTag.IsMatch(message)) return;

            lock (_locker)
            {
                // Trim color code.
                // 裁剪Color代码。
                message = _regexMessageCut.Replace(message, "");

                string strTime = DateTime.Now.ToString("MM-dd HH:mm:ss");
                string log = $"[{strTime}][{Time.frameCount}]{message}";
                _logBuilder.Append(log);

                // Whether to log battle tracking.
                // 是否记录对战跟踪。
                if (type == LogType.Log && LogStackTrace
                    || type == LogType.Warning && WarningStackTrace
                    || type == LogType.Error && ErrorStackTrace)
                {
                    _logBuilder.Append(stackTrace);
                    _logBuilder.Append("\n");
                }

                if (_logBuilder.Length > 0)
                {
                    try
                    {
                        if (_writer != null)
                        {
                            _writer.WriteLine(_logBuilder.ToString());
                            // 重要日志（Warning/Error/Exception/Assert）立即刷盘，保证崩溃时不丢失。
                            // Flush important logs (Warning/Error/Exception/Assert) immediately so they survive a crash.
                            if (type != LogType.Log)
                                _writer.Flush();
                        }
                        else
                        {
                            using (StreamWriter sw = File.AppendText(_savePath))
                            {
                                sw.WriteLine(_logBuilder.ToString());
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError("[Debugx] LogOutput 写入失败。" + ex.Message);
                    }

                    _logBuilder.Length = 0;
                }

                HandleDrawLogs(message, type);
            }
        }

        #region Draw Logs 绘制Logs

        private struct DrawLogInfo
        {
            public string Message;
            public LogType Type;
        }

        private static bool DrawLogToScreen => Settings != null && Settings.drawLogToScreen;
        private static bool RestrictDrawLogCount => Settings != null && Settings.restrictDrawLogCount;
        private static int MaxDrawLogs => Settings != null ? Mathf.Max(Settings.maxDrawLogs, 1) : 100;


        private static readonly List<DrawLogInfo> _drawLogs = new List<DrawLogInfo>();
        private static Vector2 _scrollPosition;
        private static bool _collapse;// Collapse or expand the entire interface. 折叠或打开整个界面。
        private static bool _collapseRepetition;// Collapse duplicate information. 折叠重复信息。

        // Window settings. 窗口设置。
        private const int Margin = 10;
        private static readonly Rect _titleBarRect = new Rect(0, 0, 1000, 20);
        private static Rect _windowRect;

        /// <summary>
        /// Render GUI.
        /// 绘制GUI。
        /// </summary>
        public static void DrawGUI()
        {
            if (!DrawLogToScreen)
            {
                return;
            }

            _windowRect = _collapse ? new Rect(10, 10, Screen.width * 0.4f - (Margin * 5), 40f) : new Rect(10, 10, Screen.width * 0.4f - (Margin * 5), Screen.height * 0.3f - (Margin * 6));
            _windowRect = GUILayout.Window(19940223, _windowRect, DrawConsoleWindow, "Debugx Logs");
        }

        private static Color GetLogColor(LogType logType)
        {
            switch (logType)
            {
                case LogType.Error:
                    return Color.red;
                case LogType.Assert:
                    break;
                case LogType.Warning:
                    return Color.yellow;
                case LogType.Log:
                    break;
                case LogType.Exception:
                    return Color.red;
            }

            return Color.white;
        }

        /// <summary>  
        /// Displays a window that lists the recorded logs.
        /// 显示一个窗口用于展示日志。
        /// </summary>  
        /// <param name="windowID">Window ID.</param>  
        private static void DrawConsoleWindow(int windowID)
        {
            DrawToolbar();
            if (!_collapse)
                DrawLogsList();

            // Allow the window to be dragged by its title bar.
            // 允许拖动窗口。
            GUI.DragWindow(_titleBarRect);
        }

        /// <summary>  
        /// Displays a scrollable list of logs.
        /// 显示可滚动的日志列表。
        /// </summary>  
        private static void DrawLogsList()
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true);

            // Iterate through the recorded logs.  
            for (var i = 0; i < _drawLogs.Count; i++)
            {
                var log = _drawLogs[i];
                // Destroy(logs[i - 1]);
                // Combine identical messages if collapse option is chosen.
                if (_collapseRepetition && i > 0)
                {
                    var previousMessage = _drawLogs[i - 1].Message;

                    if (log.Message == previousMessage)
                    {
                        continue;
                    }
                }

                GUI.contentColor = GetLogColor(log.Type);
                GUILayout.Label(log.Message);
            }

            GUILayout.EndScrollView();

            // Ensure GUI colour is reset before drawing other components.
            GUI.contentColor = Color.white;
        }

        /// <summary>  
        /// Displays options for filtering and changing the logs list.
        /// 绘制工具栏。
        /// </summary>  
        private static void DrawToolbar()
        {
            GUILayout.BeginHorizontal();

            if (GUILayout.Button(_collapse ? "Open" : "Collapse"))
            {
                _collapse = !_collapse;
            }
            if (GUILayout.Button("Clear"))
            {
                _drawLogs.Clear();
            }

            _collapseRepetition = GUILayout.Toggle(_collapseRepetition, "Collapse Repetition", GUILayout.ExpandWidth(false));

            GUILayout.EndHorizontal();
        }

        /// <summary>  
        /// Records a log from the log callback.
        /// 回调，记录一条日志到绘制日志列表。
        /// </summary>  
        /// <param name="message">Message.</param>  
        /// <param name="type">Type of message (error, exception, warning, assert).</param>
        private static void HandleDrawLogs(string message, LogType type)
        {
            if (!DrawLogToScreen)
            {
                return;
            }

            _drawLogs.Add(new DrawLogInfo
            {
                Message = message,
                Type = type,
            });

            TrimExcessLogs();
        }

        /// <summary>  
        /// Removes old logs that exceed the maximum number allowed.
        /// 删除超出最大数量的旧日志。
        /// </summary>  
        private static void TrimExcessLogs()
        {
            if (!RestrictDrawLogCount)
            {
                return;
            }

            var amountToRemove = Mathf.Max(_drawLogs.Count - MaxDrawLogs, 0);

            if (amountToRemove == 0)
            {
                return;
            }

            _drawLogs.RemoveRange(0, amountToRemove);
        }

        #endregion
    }
}
