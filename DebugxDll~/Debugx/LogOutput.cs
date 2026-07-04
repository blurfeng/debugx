using System;
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
            }
        }
    }
}
