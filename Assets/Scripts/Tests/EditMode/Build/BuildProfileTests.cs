using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build.Profile;

namespace GooGalaxy.Tests.EditMode.Build
{
    // Top-level fields are read through SerializedObject. The platform settings are a [SerializeReference] whose concrete types
    // live in the Android and iOS module assemblies, which CI editors do not have, so they are read from the asset's own YAML.
    // A missing class or key fails with its name: a Unity upgrade that renames one is then diagnosable.
    [TestFixture]
    public class BuildProfileTests
    {
        private const string AndroidReleasePath = "Assets/Settings/Build Profiles/Android-Release.asset";
        private const string IosReleasePath = "Assets/Settings/Build Profiles/iOS-Release.asset";
        private const string AndroidDevelopmentPath = "Assets/Settings/Build Profiles/Android-Development.asset";
        private const string IosDevelopmentPath = "Assets/Settings/Build Profiles/iOS-Development.asset";
        private const string AndroidSettingsClass = "AndroidPlatformBuildSettings";
        private const string IosSettingsClass = "iOSPlatformSettings";
        private const int Lz4HcCompression = 3;
        private const string NoOverridesReason =
            "every profile must build the same game as Player Settings: both Android release paths (signed release and release validation) build without a profile, while the PR builds and every iOS build go through one, so an override would make those builds diverge from what ships; remove the override from the profile.";

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        [TestCase(AndroidDevelopmentPath)]
        [TestCase(IosDevelopmentPath)]
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
        [TestCase(IosDevelopmentPath, BuildTarget.iOS)]
        public void BuildTarget_EachProfile_MatchesItsPlatform(string path, BuildTarget expected)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            int target = profile.Find("m_BuildTarget").intValue;

            // THEN
            Assert.That(target, Is.EqualTo((int)expected), $"{path} targets the wrong platform.");
        }

        [TestCase(AndroidReleasePath, AndroidSettingsClass)]
        [TestCase(IosReleasePath, IosSettingsClass)]
        public void Development_ReleaseProfile_IsFalse(string path, string settingsClass)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            bool isDevelopment = profile.ReadPlatformValue(settingsClass, "m_Development") != 0;

            // THEN
            Assert.That(isDevelopment, Is.False, $"{path} is a release profile and must not produce a development build.");
        }

        [TestCase(AndroidDevelopmentPath, AndroidSettingsClass)]
        [TestCase(IosDevelopmentPath, IosSettingsClass)]
        public void Development_DevelopmentProfile_IsTrue(string path, string settingsClass)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            bool isDevelopment = profile.ReadPlatformValue(settingsClass, "m_Development") != 0;

            // THEN
            Assert.That(isDevelopment, Is.True, $"{path} is a development profile and must produce a development build.");
        }

        [Test]
        public void BuildAppBundle_AndroidRelease_IsTrue()
        {
            // GIVEN
            using var profile = new ProfileReader(AndroidReleasePath);

            // WHEN
            bool buildsBundle = profile.ReadPlatformValue(AndroidSettingsClass, "m_BuildAppBundle") != 0;

            // THEN
            Assert.That(
                buildsBundle,
                Is.True,
                "Android-Release no longer builds an App Bundle — Google Play accepts only .aab uploads; re-enable Build App Bundle in the profile."
            );
        }

        [Test]
        public void BuildAppBundle_AndroidDevelopment_IsFalse()
        {
            // GIVEN
            using var profile = new ProfileReader(AndroidDevelopmentPath);

            // WHEN
            bool buildsBundle = profile.ReadPlatformValue(AndroidSettingsClass, "m_BuildAppBundle") != 0;

            // THEN
            Assert.That(
                buildsBundle,
                Is.False,
                "Android-Development builds an App Bundle — PR CI uploads it as an installable APK for testers, and an App Bundle cannot be sideloaded; turn Build App Bundle off in the profile."
            );
        }

        [TestCase(AndroidReleasePath, AndroidSettingsClass)]
        [TestCase(IosReleasePath, IosSettingsClass)]
        [TestCase(AndroidDevelopmentPath, AndroidSettingsClass)]
        [TestCase(IosDevelopmentPath, IosSettingsClass)]
        public void ConnectProfiler_EveryProfile_IsFalse(string path, string settingsClass)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            bool connectsProfiler = profile.ReadPlatformValue(settingsClass, "m_ConnectProfiler") != 0;

            // THEN
            Assert.That(
                connectsProfiler,
                Is.False,
                $"{path} autoconnects the profiler again — CI artifacts would try to reach the runner's IP at startup; local builds add ConnectWithProfiler through npm run unity:build:android. Turn Autoconnect Profiler off in the profile."
            );
        }

        [TestCase(AndroidReleasePath, AndroidSettingsClass)]
        [TestCase(IosReleasePath, IosSettingsClass)]
        public void CompressionType_ReleaseProfile_IsLz4Hc(string path, string settingsClass)
        {
            // GIVEN
            using var profile = new ProfileReader(path);

            // WHEN
            int compression = profile.ReadPlatformValue(settingsClass, "m_CompressionType");

            // THEN
            Assert.That(
                compression,
                Is.EqualTo(Lz4HcCompression),
                $"{path} no longer compresses with LZ4HC (3) — every build that activates this profile grows (the signed iOS release and the iOS release validation export; no Android workflow applies Android-Release). Set Compression Method back to LZ4HC in the profile."
            );
        }

        [TestCase(AndroidReleasePath)]
        [TestCase(IosReleasePath)]
        [TestCase(AndroidDevelopmentPath)]
        [TestCase(IosDevelopmentPath)]
        public void OverrideGlobalSceneList_EveryProfile_IsFalse(string path)
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
        [TestCase(AndroidDevelopmentPath)]
        [TestCase(IosDevelopmentPath)]
        public void HasScriptingDefines_EveryProfile_IsFalse(string path)
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
        [TestCase(AndroidDevelopmentPath)]
        [TestCase(IosDevelopmentPath)]
        public void PlayerSettingsYaml_EveryProfile_HoldsNoOverrides(string path)
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
            private readonly string _path;

            public ProfileReader(string path)
            {
                _path = path;
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

            public int ReadPlatformValue(string settingsClass, string key)
            {
                string[] lines = File.ReadAllLines(_path);
                int start = System.Array.FindIndex(lines, line => line.Contains($"type: {{class: {settingsClass},"));
                Assert.That(
                    start,
                    Is.GreaterThanOrEqualTo(0),
                    $"{_path} has no '{settingsClass}' platform settings — Unity renamed the type or the profile targets another platform; update this test."
                );

                string prefix = $"        {key}:";
                for (int i = start + 1; i < lines.Length && !lines[i].TrimStart().StartsWith("- rid:"); i++)
                {
                    if (lines[i].StartsWith(prefix))
                    {
                        return int.Parse(lines[i][prefix.Length..].Trim());
                    }
                }

                Assert.Fail($"{_path} '{settingsClass}' no longer stores '{key}' — Unity renamed it; update this test.");
                return -1;
            }

            public void Dispose()
            {
                _serialized.Dispose();
            }
        }
    }
}
