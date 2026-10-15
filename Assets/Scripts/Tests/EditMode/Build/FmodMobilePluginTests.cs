using NUnit.Framework;
using UnityEditor;

namespace GooGalaxy.Tests.EditMode.Build
{
    [TestFixture]
    public class FmodMobilePluginTests
    {
        private const string AndroidLibFolder = "Assets/Plugins/FMOD/platforms/android/lib/";
        private const string IosLibFolder = "Assets/Plugins/FMOD/platforms/ios/lib/";

        [TestCase(AndroidLibFolder + "arm64-v8a/libresonanceaudio.so", BuildTarget.Android)]
        [TestCase(AndroidLibFolder + "armeabi-v7a/libresonanceaudio.so", BuildTarget.Android)]
        [TestCase(AndroidLibFolder + "x86/libresonanceaudio.so", BuildTarget.Android)]
        [TestCase(AndroidLibFolder + "x86_64/libresonanceaudio.so", BuildTarget.Android)]
        [TestCase(IosLibFolder + "libresonanceaudio.a", BuildTarget.iOS)]
        public void GetCompatibleWithPlatform_ResonanceLibrary_IsFalse(string path, BuildTarget platform)
        {
            // GIVEN
            var importer = AssetImporter.GetAtPath(path) as PluginImporter;
            Assert.That(importer, Is.Not.Null, $"No plugin importer at {path}; the FMOD plugin moved or was updated.");

            // WHEN
            bool isCompatible = importer.GetCompatibleWithPlatform(platform);

            // THEN
            Assert.That(
                isCompatible,
                Is.False,
                $"{path} ships on {platform} again — FMOD's Resonance spatializer is unused, so the library is dead weight (850 KB on arm64-v8a); untick {platform} in its plugin importer."
            );
        }
    }
}
