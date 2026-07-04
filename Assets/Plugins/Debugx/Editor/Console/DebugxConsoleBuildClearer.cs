using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DebugxLog.Console.Editor
{
    /// <summary>
    /// Build pre-process hook: on build start, clears any open Debugx Console window whose "Clear on Build" is enabled.
    /// 构建预处理钩子：构建开始时，对开启了“构建时清空”的 Debugx Console 窗口执行清空。
    /// </summary>
    public class DebugxConsoleBuildClearer : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (DebugxConsoleWindow window in Resources.FindObjectsOfTypeAll<DebugxConsoleWindow>())
                window.ClearForBuildIfEnabled();
        }
    }
}
