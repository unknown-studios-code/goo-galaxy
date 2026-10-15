using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace GooGalaxy.Tests.EditMode.Build
{
    // Pins the mobile Player Settings so a settings change that would break store uploads or device builds fails here
    // instead of on a build machine.
    [TestFixture]
    public class MobilePlayerSettingsTests
    {
        private const string ApplicationId = "com.unknownstudios.googalaxy";

        [TestCaseSource(nameof(AppIdTargets))]
        public void GetApplicationIdentifier_EachTarget_IsTheStoreIdentifier(NamedBuildTarget target)
        {
            // GIVEN

            // WHEN
            string identifier = PlayerSettings.GetApplicationIdentifier(target);

            // THEN
            Assert.That(identifier, Is.EqualTo(ApplicationId), $"{target.TargetName} app ID changed — this identifier is permanent once uploaded to a store.");
        }

        [TestCaseSource(nameof(MobileTargets))]
        public void GetScriptingBackend_MobileTarget_IsIl2Cpp(NamedBuildTarget target)
        {
            // GIVEN

            // WHEN
            ScriptingImplementation backend = PlayerSettings.GetScriptingBackend(target);

            // THEN
            Assert.That(backend, Is.EqualTo(ScriptingImplementation.IL2CPP), $"{target.TargetName} must build with IL2CPP.");
        }

        [TestCaseSource(nameof(MobileTargets))]
        public void GetManagedStrippingLevel_MobileTarget_IsMinimal(NamedBuildTarget target)
        {
            // GIVEN

            // WHEN
            ManagedStrippingLevel level = PlayerSettings.GetManagedStrippingLevel(target);

            // THEN
            Assert.That(
                level,
                Is.EqualTo(ManagedStrippingLevel.Minimal),
                $"{target.TargetName} stripping level changed — above Minimal, IL2CPP can strip reflection-only types (MissingMethodException on device, never in the editor). Raise it only with a device smoke test, then update this pin."
            );
        }

        [Test]
        public void TargetArchitectures_Android_IsArm64Only()
        {
            // GIVEN

            // WHEN
            AndroidArchitecture architectures = PlayerSettings.Android.targetArchitectures;

            // THEN
            Assert.That(architectures, Is.EqualTo(AndroidArchitecture.ARM64), "Android must ship ARM64 only.");
        }

        [Test]
        public void TargetSdkVersion_Android_IsApi36()
        {
            // GIVEN

            // WHEN
            AndroidSdkVersions version = PlayerSettings.Android.targetSdkVersion;

            // THEN
            Assert.That(
                version,
                Is.EqualTo(AndroidSdkVersions.AndroidApiLevel36),
                "Android target API changed — Google Play rejects new apps and updates targeting below API 36; move it only with the Play requirement."
            );
        }

        [Test]
        public void MinSdkVersion_Android_IsApi25()
        {
            // GIVEN

            // WHEN
            AndroidSdkVersions version = PlayerSettings.Android.minSdkVersion;

            // THEN
            Assert.That(
                version,
                Is.EqualTo(AndroidSdkVersions.AndroidApiLevel25),
                "Android minimum API changed — it decides which devices can install the game."
            );
        }

        [Test]
        public void GetUseDefaultGraphicsAPIs_Android_IsFalse()
        {
            // GIVEN

            // WHEN
            bool usesDefaults = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);

            // THEN
            Assert.That(usesDefaults, Is.False, "Android graphics APIs must be explicit, otherwise the ordered list is ignored.");
        }

        [Test]
        public void GetGraphicsAPIs_Android_IsVulkanThenOpenGles3()
        {
            // GIVEN

            // WHEN
            GraphicsDeviceType[] apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);

            // THEN
            Assert.That(
                apis,
                Is.EqualTo(new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 }),
                "Android must try Vulkan first and fall back to OpenGL ES 3, in that order."
            );
        }

        [Test]
        public void TextureCompressionFormats_Android_StartsWithAstc()
        {
            // GIVEN

            // WHEN
            TextureCompressionFormat[] formats = PlayerSettings.Android.textureCompressionFormats;

            // THEN
            Assert.That(
                formats[0],
                Is.EqualTo(TextureCompressionFormat.ASTC),
                "Android default texture compression must be ASTC; the import presets assume it."
            );
        }

        [Test]
        public void DefaultInterfaceOrientation_Project_IsPortrait()
        {
            // GIVEN

            // WHEN
            UIOrientation orientation = PlayerSettings.defaultInterfaceOrientation;

            // THEN
            Assert.That(orientation, Is.EqualTo(UIOrientation.Portrait), "The game is portrait-only.");
        }

        [Test]
        public void AllowedAutorotateToPortrait_Project_IsEnabled()
        {
            // GIVEN

            // WHEN
            bool isAllowed = PlayerSettings.allowedAutorotateToPortrait;

            // THEN
            Assert.That(isAllowed, Is.True, "Portrait must stay an allowed orientation.");
        }

        [Test]
        public void AllowedAutorotateToPortraitUpsideDown_Project_IsDisabled()
        {
            // GIVEN

            // WHEN
            bool isAllowed = PlayerSettings.allowedAutorotateToPortraitUpsideDown;

            // THEN
            Assert.That(
                isAllowed,
                Is.False,
                "Upside-down autorotation is enabled — inert while Default Orientation is Portrait, but it would let the board turn 180° if orientation ever moves to Auto Rotation."
            );
        }

        [Test]
        public void AllowedAutorotateToLandscapeLeft_Project_IsDisabled()
        {
            // GIVEN

            // WHEN
            bool isAllowed = PlayerSettings.allowedAutorotateToLandscapeLeft;

            // THEN
            Assert.That(
                isAllowed,
                Is.False,
                "Landscape-left autorotation is enabled — inert while Default Orientation is Portrait, but it would let the board rotate sideways if orientation ever moves to Auto Rotation."
            );
        }

        [Test]
        public void AllowedAutorotateToLandscapeRight_Project_IsDisabled()
        {
            // GIVEN

            // WHEN
            bool isAllowed = PlayerSettings.allowedAutorotateToLandscapeRight;

            // THEN
            Assert.That(
                isAllowed,
                Is.False,
                "Landscape-right autorotation is enabled — inert while Default Orientation is Portrait, but it would let the board rotate sideways if orientation ever moves to Auto Rotation."
            );
        }

        [Test]
        public void TargetOsVersionString_Ios_Is15()
        {
            // GIVEN

            // WHEN
            string version = PlayerSettings.iOS.targetOSVersionString;

            // THEN
            Assert.That(version, Is.EqualTo("15.0"), "iOS minimum OS changed — it decides which devices can install the game.");
        }

        [Test]
        public void UseCustomKeystore_Android_IsFalse()
        {
            // GIVEN

            // WHEN
            bool usesCustomKeystore = PlayerSettings.Android.useCustomKeystore;

            // THEN
            Assert.That(
                usesCustomKeystore,
                Is.False,
                "A custom keystore is enabled in the project — Android signing must come only from CI, never from a committed keystore setting."
            );
        }

        [Test]
        public void KeystoreName_Android_IsEmpty()
        {
            // GIVEN

            // WHEN
            string keystoreName = PlayerSettings.Android.keystoreName;

            // THEN
            Assert.That(keystoreName, Is.Empty, "A keystore path is committed in Player Settings — signing must come only from CI.");
        }

        [Test]
        public void AppleEnableAutomaticSigning_Ios_IsFalse()
        {
            // GIVEN

            // WHEN
            bool usesAutomaticSigning = PlayerSettings.iOS.appleEnableAutomaticSigning;

            // THEN
            Assert.That(usesAutomaticSigning, Is.False, "iOS automatic signing is on — configure_signing.py relies on manual signing in CI.");
        }

        [Test]
        public void ForceInternetPermission_Android_IsTrue()
        {
            // GIVEN

            // WHEN
            bool isForced = PlayerSettings.Android.forceInternetPermission;

            // THEN
            Assert.That(
                isForced,
                Is.True,
                "Internet permission is no longer forced — Auto detection only sees managed networking APIs and can miss Unity Transport's native sockets, leaving the Android client offline."
            );
        }

        [Test]
        public void PreferredInstallLocation_Android_IsAuto()
        {
            // GIVEN

            // WHEN
            AndroidPreferredInstallLocation location = PlayerSettings.Android.preferredInstallLocation;

            // THEN
            Assert.That(
                location,
                Is.EqualTo(AndroidPreferredInstallLocation.Auto),
                "Android preferred install location changed from Auto — Prefer External lets the OS place the app on removable storage, where it is unavailable whenever the card is unmounted; restore Auto in Player Settings > Other Settings."
            );
        }

        [TestCase(LogType.Log)]
        [TestCase(LogType.Warning)]
        [TestCase(LogType.Error)]
        [TestCase(LogType.Assert)]
        [TestCase(LogType.Exception)]
        public void GetStackTraceLogType_EachLogType_IsScriptOnly(LogType logType)
        {
            // GIVEN

            // WHEN
            StackTraceLogType actual = PlayerSettings.GetStackTraceLogType(logType);

            // THEN
            Assert.That(
                actual,
                Is.EqualTo(StackTraceLogType.ScriptOnly),
                $"{logType} stack trace type changed — the project setting also drives the editor console and development builds; release players strip Log and Warning at runtime in LogStackTraceInitializer."
            );
        }

        private static IEnumerable<NamedBuildTarget> AppIdTargets()
        {
            yield return NamedBuildTarget.Android;
            yield return NamedBuildTarget.iOS;
            yield return NamedBuildTarget.Standalone;
        }

        private static IEnumerable<NamedBuildTarget> MobileTargets()
        {
            yield return NamedBuildTarget.Android;
            yield return NamedBuildTarget.iOS;
        }
    }
}
