using UnityEngine;

public class DebugxTest : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        Debugx.LogBlur("Test Log Blur On Start.");
        Debugx.LogWarningBlur("Test LogWarning Blur On Start.");
        Debugx.LogErrorBlur("Test LogError Blur On Start.");
    }
}
