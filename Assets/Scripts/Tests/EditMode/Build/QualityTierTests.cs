using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace GooGalaxy.Tests.EditMode.Build
{
    [TestFixture]
    public class QualityTierTests
    {
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        private const string PipelineFolder = "Assets/Settings/Rendering/Pipeline/";
        private const int BalancedIndex = 1;

        private static readonly string[] _tierNames = { "Performant", "Balanced", "HighFidelity" };

        [Test]
        public void Names_Project_AreThePerformantBalancedHighFidelityTiersInOrder()
        {
            // GIVEN

            // WHEN
            string[] names = QualitySettings.names;

            // THEN
            Assert.That(
                names,
                Is.EqualTo(_tierNames),
                "Quality levels changed — m_PerPlatformDefaultQuality stores each platform's starting tier by index, so a reorder silently moves Android and iOS off Balanced."
            );
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void GetRenderPipelineAssetAt_EachTier_IsAssigned(int level)
        {
            // GIVEN

            // WHEN
            RenderPipelineAsset asset = QualitySettings.GetRenderPipelineAssetAt(level);

            // THEN
            Assert.That(asset != null, Is.True, $"Quality level {level} has no render pipeline asset, so it falls back to the Graphics default.");
        }

        [TestCase(0, "URP-Performant.asset")]
        [TestCase(1, "URP-Balanced.asset")]
        [TestCase(2, "URP-HighFidelity.asset")]
        public void GetRenderPipelineAssetAt_EachTier_IsTheMatchingTierAsset(int level, string assetFile)
        {
            // GIVEN

            // WHEN
            string path = AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(level));

            // THEN
            Assert.That(path, Is.EqualTo(PipelineFolder + assetFile), $"Quality level {level} points at the wrong pipeline asset.");
        }

        [TestCase("Android")]
        [TestCase("iPhone")]
        public void PerPlatformDefaultQuality_MobilePlatform_IsTheBalancedTier(string platform)
        {
            // GIVEN
            // WHEN
            int defaultIndex = ReadPerPlatformDefault(platform);

            // THEN
            Assert.That(defaultIndex, Is.EqualTo(BalancedIndex), $"{platform} no longer starts on the Balanced tier.");
        }

        private static int ReadPerPlatformDefault(string platform)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath);
            using var settings = new SerializedObject(assets[0]);
            SerializedProperty entries = settings.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("first").stringValue == platform)
                {
                    return entry.FindPropertyRelative("second").intValue;
                }
            }

            Assert.Fail($"QualitySettings.asset has no m_PerPlatformDefaultQuality entry for '{platform}'.");
            return -1;
        }
    }
}
