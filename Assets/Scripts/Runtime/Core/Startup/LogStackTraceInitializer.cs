using UnityEngine;

namespace GooGalaxy.Runtime.Core.Startup
{
    // Capturing a stack trace allocates on every Debug.Log and LogWarning on device, so release players drop them for those
    // two types only. Development builds and the editor keep the project setting, so the console and device logs still
    // lead back to the source line.
    internal static class LogStackTraceInitializer
    {
        internal static void ApplyStackTraceLogTypes(bool isDebugBuild)
        {
            if (isDebugBuild)
            {
                return;
            }

            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyForCurrentBuild()
        {
            ApplyStackTraceLogTypes(Debug.isDebugBuild);
        }
    }
}
