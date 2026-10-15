using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace GooGalaxy.Tests.EditMode.Build
{
    // BuildProfile exposes its target and platform settings only as internal members, so they are read through
    // SerializedObject. A missing property fails with its name: a Unity upgrade that renames one is then diagnosable.
    [TestFixture]
    public class BuildProfileTests
    {
        private const string AndroidReleasePath = "Assets/Settings/Build Profiles/Android-Release.asset";
        private const string IosReleasePath = "Assets/Settings/Build Profiles/iOS-Release.asset";
        private const string AndroidDevelopmentPath = "Assets/Settings/Build Profiles/Android-Development.asset";
        private const int Lz4HcCompression = 3;
        private const string NoOverridesReason =
            "the release CI path builds Android without the profile and both iOS workflows build through iOS-Release, so a release profile must carry no overrides.";

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        [TestCase(AndroidDevelopmentPath)]
        public void LoadAssetAtPath_EachProfile_Loads(string path)
        {
            // GIVEN

            // WHEN
            BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);

            // THEN
            Assert.That(profile != null, Is.True, $"Build profile is missing or failed to import: {path}");
        }

        [TestCase(AndroidReleasePath, BuildTarget.Android)]
        [TestCase(IosReleasePath, BuildTarget.iOS)]
        [TestCase(AndroidDevelopmentPath, BuildTarget.Android)]
        public void BuildTarget_EachProfile_MatchesItsPlatform(string path, BuildTarget expected)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            int target = profile.Find("m_BuildTarget").intValue;

            // THEN
            Assert.That(target, Is.EqualTo((int)expected), $"{path} targets the wrong platform.");
        }

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        public void Development_ReleaseProfile_IsFalse(string path)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            bool isDevelopment = profile.Find("m_PlatformBuildProfile.m_Development").boolValue;

            // THEN
            Assert.That(isDevelopment, Is.False, $"{path} is a release profile and must not produce a development build.");
        }

        [Test]
        public void Development_AndroidDevelopment_IsTrue()
        {
            // GIVEN
            using var profile = new ProfileReader(AndroidDevelopmentPath);

            // WHEN
            bool isDevelopment = profile.Find("m_PlatformBuildProfile.m_Development").boolValue;

            // THEN
            Assert.That(isDevelopment, Is.True, "The development profile must produce a development build.");
        }

        [Test]
        public void BuildAppBundle_AndroidRelease_IsTrue()
        {
            // GIVEN
            using var profile = new ProfileReader(AndroidReleasePath);

            // WHEN
            bool buildsBundle = profile.Find("m_PlatformBuildProfile.m_BuildAppBundle").boolValue;

            // THEN
            Assert.That(
                buildsBundle,
                Is.True,
                "Android-Release no longer builds an App Bundle — Google Play accepts only .aab uploads; re-enable Build App Bundle in the profile."
            );
        }

        [Test]
        public void ConnectProfiler_AndroidDevelopment_IsTrue()
        {
            // GIVEN
            using var profile = new ProfileReader(AndroidDevelopmentPath);

            // WHEN
            bool connectsProfiler = profile.Find("m_PlatformBuildProfile.m_ConnectProfiler").boolValue;

            // THEN
            Assert.That(connectsProfiler, Is.True, "The development profile must autoconnect the profiler.");
        }

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        public void CompressionType_ReleaseProfile_IsLz4Hc(string path)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            int compression = profile.Find("m_PlatformBuildProfile.m_CompressionType").intValue;

            // THEN
            Assert.That(
                compression,
                Is.EqualTo(Lz4HcCompression),
                $"{path} no longer compresses with LZ4HC (3) — every build that activates this profile grows (the iOS release and both PR builds; the signed Android path does not apply the profile)."
            );
        }

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        public void OverrideGlobalSceneList_ReleaseProfile_IsFalse(string path)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            bool overridesScenes = profile.Find("m_OverrideGlobalSceneList").boolValue;

            // THEN
            Assert.That(overridesScenes, Is.False, $"{path} overrides the scene list — {NoOverridesReason}");
        }

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        public void HasScriptingDefines_ReleaseProfile_IsFalse(string path)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            bool hasDefines = profile.Find("m_HasScriptingDefines").boolValue;

            // THEN
            Assert.That(hasDefines, Is.False, $"{path} defines scripting symbols — {NoOverridesReason}");
        }

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        public void PlayerSettingsYaml_ReleaseProfile_HoldsNoOverrides(string path)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            int overrideCount = profile.Find("m_PlayerSettingsYaml.m_Settings").arraySize;

            // THEN
            Assert.That(overrideCount, Is.EqualTo(0), $"{path} overrides Player Settings — {NoOverridesReason}");
        }

        private sealed class ProfileReader : System.IDisposable
        {
            private readonly SerializedObject _serialized;

            public ProfileReader(string path)
            {
                BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(path);
                Assert.That(profile != null, Is.True, $"Build profile is missing or failed to import: {path}");
                _serialized = new SerializedObject(profile);
            }

            public SerializedProperty Find(string propertyPath)
            {
                SerializedProperty property = _serialized.FindProperty(propertyPath);
                Assert.That(property, Is.Not.Null, $"BuildProfile no longer serializes '{propertyPath}' — Unity renamed it; update this test.");
                return property;
            }

            public void Dispose()
            {
                _serialized.Dispose();
            }
        }
    }
}
