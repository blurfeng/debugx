using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Debugx Console — log persistence across domain reloads (script recompiles, play-mode entry). The buffer is
    /// non-serialized in-memory state, so without this every recompile would wipe the console. Entries are stashed in
    /// <see cref="SessionState"/> (a serialized string that survives domain reloads but is cleared on editor restart)
    /// on <c>OnDisable</c> and restored on <c>OnEnable</c> — which brackets both domain reloads and window close/reopen.
    ///
    /// This is the mechanism that makes "Clear on Recompile" meaningful when unchecked: with it off, the buffer is
    /// dumped (non-empty) and restored; with it on, the buffer was already cleared before the dump, so nothing returns.
    /// Compile entries are excluded — they are re-sourced from LogEntries each reload (see
    /// <see cref="EditorLogEntriesMirror"/>), so persisting them would duplicate them and resurrect stale ones.
    ///
    /// Debugx Console —— 日志跨域重载（脚本重编译、进入 Play）持久化。缓冲是非序列化的内存状态，若无此机制每次重编译都会清空
    /// 控制台。条目在 <c>OnDisable</c> 存入 <see cref="SessionState"/>（一个跨域重载存活、编辑器重启后清除的序列化字符串），
    /// 在 <c>OnEnable</c> 恢复——覆盖域重载与关/开窗口两种情况。这也是「重编译时清空」不勾选才有意义的关键：不勾时缓冲被
    /// （非空）转存并恢复；勾选时缓冲在转存前已被清空，故不会恢复。编译条目被排除——它们每次重载由 LogEntries 重新拉取
    /// （见 <see cref="EditorLogEntriesMirror"/>），持久化会导致重复并复活过期条目。
    /// </summary>
    public partial class DebugxConsoleWindow
    {
        // SessionState survives domain reloads within the editor session and is cleared on editor restart — exactly the
        // lifetime we want. Kept out of the window's serialized layout so it doesn't bloat layout files.
        // SessionState 在编辑器会话内跨域重载存活、重启后清除——正是我们要的生命周期。不进窗口序列化布局，避免撑大布局文件。
        private const string PersistKey = PrefPrefix + "PersistedLogs";

        // Cap on persisted entries (newest kept). Bounds the SessionState blob; older entries are dropped across a reload.
        // 持久化条目上限（保留最新的）。限制 SessionState 体积；更旧的条目在重载时丢弃。
        private const int MaxPersistEntries = 2000;

        [Serializable]
        private struct PersistedEntry
        {
            public int logType;
            public string richText;
            public string plainText;
            public string stackTrace;
            public int memberKey;
            public string memberSignature;
            public string colorHex;
            public string header;
            public bool logSignatureShown;
            public int netTag;
            public bool isDebugx;
            public int category;
            public long timestampTicks;
            public int frameCount;
            public long sequenceId;
        }

        [Serializable]
        private class PersistPayload
        {
            public List<PersistedEntry> entries = new List<PersistedEntry>();
        }

        // Dump the buffer (excluding compile entries) to SessionState. Called from OnDisable.
        // 将缓冲（排除编译条目）转存到 SessionState。在 OnDisable 调用。
        private void SavePersistedLogs()
        {
            if (_store == null) return;

            LogRingBuffer buf = _store.Buffer;
            var payload = new PersistPayload();

            // Walk newest -> oldest, keep up to MaxPersistEntries non-compile entries, then reverse back to oldest -> newest.
            // 从最新到最旧遍历，保留至多 MaxPersistEntries 条非编译条目，再反转回 最旧->最新。
            for (int i = buf.Count - 1; i >= 0 && payload.entries.Count < MaxPersistEntries; i--)
            {
                DebugxLogEntry e = buf[i];
                if (e.Category == LogEntryCategory.Compile) continue;
                payload.entries.Add(ToPersisted(e));
            }
            payload.entries.Reverse();

            SessionState.SetString(PersistKey, JsonUtility.ToJson(payload));
        }

        // Restore persisted entries into the (fresh) buffer. Called from OnEnable after the store is started.
        // 将持久化条目恢复进（全新）缓冲。在 OnEnable、store 启动后调用。
        private void RestorePersistedLogs()
        {
            if (_store == null) return;
            string json = SessionState.GetString(PersistKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return;

            PersistPayload payload;
            try { payload = JsonUtility.FromJson<PersistPayload>(json); }
            catch { return; }
            if (payload?.entries == null || payload.entries.Count == 0) return;

            long maxSeq = 0;
            foreach (PersistedEntry p in payload.entries)
            {
                DebugxLogEntry entry = FromPersisted(p);
                if (entry.SequenceId > maxSeq) maxSeq = entry.SequenceId;
                _store.Buffer.Add(entry);
            }

            // The fresh collector restarts its sequence at 0; push it past the restored ids so newly-captured entries
            // don't collide with them (SequenceId must stay globally unique for selection tracking across eviction).
            // 全新采集器的序号从 0 起；将其推过已恢复的 id，避免新捕获条目与之碰撞（SequenceId 须保持全局唯一，供跨淘汰的选中跟踪）。
            _store.Collector.SeedSequence(maxSeq);

            _store.MarkViewDirty();
        }

        private static PersistedEntry ToPersisted(DebugxLogEntry e) => new PersistedEntry
        {
            logType = (int)e.LogType,
            richText = e.RichText,
            plainText = e.PlainText,
            stackTrace = e.StackTrace,
            memberKey = e.MemberKey,
            memberSignature = e.MemberSignature,
            colorHex = e.ColorHex,
            header = e.Header,
            logSignatureShown = e.LogSignatureShown,
            netTag = (int)e.NetTag,
            isDebugx = e.IsDebugx,
            category = (int)e.Category,
            timestampTicks = e.Timestamp.Ticks,
            frameCount = e.FrameCount,
            sequenceId = e.SequenceId,
        };

        private static DebugxLogEntry FromPersisted(PersistedEntry p) => new DebugxLogEntry(
            (LogType)p.logType, p.richText, p.plainText, p.stackTrace,
            p.memberKey, p.memberSignature, p.colorHex, p.header, p.logSignatureShown,
            (NetTag)p.netTag, p.isDebugx, (LogEntryCategory)p.category,
            new DateTime(p.timestampTicks), p.frameCount, p.sequenceId);
    }
}
