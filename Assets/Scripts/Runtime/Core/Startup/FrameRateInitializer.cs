using UnityEngine;

namespace GooGalaxy.Runtime.Core.Startup
{
    // Android and iOS ignore QualitySettings.vSyncCount and cap the game at 30 FPS until a target is set. Desktop and
    // the editor keep the platform default, so vSync paces standalone builds and PlayMode timing stays as it was.
    internal static class FrameRateInitializer
    {
        internal const int MobileTargetFrameRate = 60;
        internal const int PlatformDefaultFrameRate = -1;

        internal static int ResolveTargetFrameRate(bool isMobilePlatform)
        {
            return isMobilePlatform ? MobileTargetFrameRate : PlatformDefaultFrameRate;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyTargetFrameRate()
        {
            Application.targetFrameRate = ResolveTargetFrameRate(Application.isMobilePlatform);
        }
    }
}
