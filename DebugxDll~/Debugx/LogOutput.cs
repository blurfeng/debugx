using System;
using System.Globalization;
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
        // color 标签匹配任意 <color=...>（含 6/8 位十六进制与命名颜色），不再只认 6 位十六进制。
        // The color pattern matches any <color=...> (6/8-digit hex or named colors), not just 6-digit hex.
        private static readonly string _tagEscaped = Regex.Escape(DebugxProjectSettings.DebugxTag);
        private static readonly Regex _regexMessageCut = new Regex($@"<color=[^>]*>|</color>|{_tagEscaped}", RegexOptions.Compiled);

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

            string savePath = Path.Combine(DirectoryPath, FileNameFull);

            // PC directory: C:\Users\UserName\AppData\LocalLow\DefaultCompany\ProjectName
            // PC目录为：C:\Users\UserName\AppData\LocalLow\DefaultCompany\ProjectName

            if (string.IsNullOrEmpty(savePath)) return;

            FileInfo fileInfo = new FileInfo(savePath);

            // Create folder.
            // 创建文件夹。
            _directoryPath = fileInfo.DirectoryName;
            if (!string.IsNullOrEmpty(_directoryPath) && !Directory.Exists(_directoryPath))
            {
                Directory.CreateDirectory(_directoryPath);
            }

            try
            {
                // 在 _locker 内切换写入流并发布 _savePath，与可能来自后台线程的 LogCallBack 互斥；
                // _savePath 仅在写入流就绪后才发布，维持 "_savePath 非空 ⇒ _writer 有效" 的不变式。
                // Swap the writer and publish _savePath inside _locker so a possibly background-thread LogCallBack cannot
                // race the dispose/create. Publish _savePath only after the writer is ready to keep the invariant
                // "_savePath non-null ⇒ _writer valid".
                lock (_locker)
                {
                    if (_writer != null)
                    {
                        _writer.Dispose();
                        _writer = null;
                    }

                    // 保持文件流常驻，减少每条日志的打开/关闭开销。
                    FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(fs, new UTF8Encoding(false));
                    // 关闭自动刷盘，避免每条日志一次同步磁盘写；Log 级别缓冲，Warning/Error 及 RecordOver 时显式 Flush。
                    // Disable auto-flush to avoid a sync disk write per line; plain logs buffer, warnings/errors and RecordOver flush explicitly.
                    _writer.AutoFlush = false;
                    _savePath = savePath;
                }
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

            // 在 _locker 内关闭写入流并清空 _savePath，与在途的 LogCallBack 互斥，避免向已释放的流写入。
            // 清空 _savePath 后，任何后续 LogCallBack 都会因 _writer/_savePath 为空而不再触碰文件。
            // Close the writer and clear _savePath inside _locker so an in-flight LogCallBack cannot write to a disposed
            // stream. Once _savePath is cleared, any later LogCallBack stops touching the file.
            string savePath;
            lock (_locker)
            {
                if (_writer != null)
                {
                    _writer.Flush();
                    _writer.Dispose();
                    _writer = null;
                }

                savePath = _savePath;
                _savePath = null;
            }

            if (string.IsNullOrEmpty(savePath) || !File.Exists(savePath))
            {
                return;
            }

            try
            {
                FileInfo fileInfo = new FileInfo(savePath);
                if (fileInfo.Length == 0)
                {
                    fileInfo.Delete();
                }
                else
                {
                    // Rename the printed file.
                    // 将打印的文件重命名。
                    string stamp = DateTime.Now.ToString("yyyy.MM.dd-HH.mm.ss", CultureInfo.InvariantCulture);
                    string filePath = Path.Combine(_directoryPath, $"{FileName}-{stamp}{FileType}");
                    int suffix = 1;
                    while (File.Exists(filePath))
                    {
                        filePath = Path.Combine(_directoryPath, $"{FileName}-{stamp}-{suffix}{FileType}");
                        suffix++;
                    }

                    Debugx.LogAdmWarning($"logOutput over, file path : {filePath}");
                    File.Move(savePath, filePath);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError("[Debugx] LogOutput.RecordOver 执行失败。" + ex.Message);
            }
        }

        private static void LogCallBack(string message, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(_savePath)) return;

            message = message ?? string.Empty;
            // tag 由文档保证不含正则特殊字符，用 Ordinal 子串查找替代正则，省掉每条非 Debugx 日志的正则开销并明确文化无关。
            // The tag is documented to contain no regex metacharacters; an Ordinal substring check replaces the regex,
            // avoiding per-line regex cost for non-Debugx logs and making the comparison culture-invariant.
            if (!RecordAllNonDebugxLogs
                && message.IndexOf(DebugxProjectSettings.DebugxTag, StringComparison.Ordinal) < 0) return;

            lock (_locker)
            {
                // Trim color code.
                // 裁剪Color代码。
                message = _regexMessageCut.Replace(message, "");

                string strTime = DateTime.Now.ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                _logBuilder.Append('[').Append(strTime).Append("][").Append(Time.frameCount).Append(']').Append(message);

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
                        // _writer 与 _savePath 在 _locker 内成对发布/清空，锁内 _writer 为空即表示录制已停止，直接跳过写入。
                        // _writer and _savePath are published/cleared together under _locker; a null _writer here means
                        // recording has stopped, so the line is simply skipped.
                        if (_writer != null)
                        {
                            _writer.WriteLine(_logBuilder.ToString());
                            // 重要日志（Warning/Error/Exception/Assert）立即刷盘，保证崩溃时不丢失。
                            // Flush important logs (Warning/Error/Exception/Assert) immediately so they survive a crash.
                            if (type != LogType.Log)
                                _writer.Flush();
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
