using DebugxLog;
using UnityEngine;

public class DebugxTest : MonoBehaviour
{
    void Start()
    {
        // 使用成员专用的 DebugxLogger 方法打印日志，可以不使用 Key 参数。
        // Use member-specific DebugxLogger methods to print logs without using the Key parameter.
        DebugxLogger.LogBlur("Test Log Blur On Start.");
        DebugxLogger.LogWarningBlur("Test LogWarning Blur On Start.");
        DebugxLogger.LogErrorBlur("Test LogError Blur On Start.");
        
        // 也可以继续使用 Debugx 的通用方法打印日志，但需要传入 Key 参数。
        // You can also continue to use Debugx's general methods to print logs, but you need to pass in the Key parameter.
        Debugx.Log(1, "Test Log Blur On Start via Debugx.");
        Debugx.LogWarning(1, "Test LogWarning Blur On Start via Debugx.");
        Debugx.LogError(1, "Test LogError Blur On Start via Debugx.");
    }
}
