using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GooGalaxy.Tests.EditMode.Build
{
    [TestFixture]
    public class RenderPipelineFeatureTests
    {
        private const string PipelineFolder = "Assets/Settings/Rendering/Pipeline/";
        private const string GlobalSettingsPath = PipelineFolder + "UniversalRenderPipelineGlobalSettings.asset";
        private const string ExpectedVolumeProfilePath = "Assets/Settings/Profiles/Volume/DefaultVolumeProfile.asset";

        [TestCase("URP-Performant.asset", "m_SupportsTerrainHoles")]
        [TestCase("URP-Balanced.asset", "m_SupportsTerrainHoles")]
        [TestCase("URP-HighFidelity.asset", "m_SupportsTerrainHoles")]
        [TestCase("URP-Performant.asset", "m_SupportsLightCookies")]
        [TestCase("URP-Balanced.asset", "m_SupportsLightCookies")]
        [TestCase("URP-HighFidelity.asset", "m_SupportsLightCookies")]
        [TestCase("URP-Performant.asset", "m_SupportDataDrivenLensFlare")]
        [TestCase("URP-Balanced.asset", "m_SupportDataDrivenLensFlare")]
        [TestCase("URP-HighFidelity.asset", "m_SupportDataDrivenLensFlare")]
        [TestCase("URP-Performant.asset", "m_SupportScreenSpaceLensFlare")]
        [TestCase("URP-Balanced.asset", "m_SupportScreenSpaceLensFlare")]
        [TestCase("URP-HighFidelity.asset", "m_SupportScreenSpaceLensFlare")]
        public void UnusedFeature_EachTierAsset_IsDisabled(string assetFile, string featureProperty)
        {
            // GIVEN
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(PipelineFolder + assetFile);
            Assert.That(asset != null, Is.True, $"Pipeline asset is missing or failed to import: {PipelineFolder}{assetFile}");
            using var serialized = new SerializedObject(asset);

            // WHEN
            bool isEnabled = FindFeature(serialized, assetFile, featureProperty).boolValue;

            // THEN
            Assert.That(
                isEnabled,
                Is.False,
                $"{assetFile} enables {featureProperty}, which the game does not use — it adds shader variants and cost on mobile."
            );
        }

        [Test]
        public void DefaultVolumeProfile_GlobalSettings_IsTheCommittedProfile()
        {
            // GIVEN
            Object settings = AssetDatabase.LoadAssetAtPath<Object>(GlobalSettingsPath);
            Assert.That(settings != null, Is.True, $"URP global settings are missing or failed to import: {GlobalSettingsPath}");
            using var serialized = new SerializedObject(settings);

            // WHEN
            string path = AssetDatabase.GetAssetPath(FindVolumeProfile(serialized).objectReferenceValue);

            // THEN
            Assert.That(
                path,
                Is.EqualTo(ExpectedVolumeProfilePath),
                $"The URP default volume profile no longer resolves to {ExpectedVolumeProfilePath} — a dangling reference makes every build create a stray Assets/DefaultVolumeProfile.asset; if the profile was moved on purpose, update this path."
            );
        }

        private static SerializedProperty FindFeature(SerializedObject serialized, string assetFile, string featureProperty)
        {
            SerializedProperty feature = serialized.FindProperty(featureProperty);
            Assert.That(feature, Is.Not.Null, $"{assetFile} no longer serializes '{featureProperty}' — Unity renamed it; update this test.");
            return feature;
        }

        private static SerializedProperty FindVolumeProfile(SerializedObject serialized)
        {
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.Next(true))
            {
                if (iterator.propertyPath.EndsWith(".m_VolumeProfile"))
                {
                    return iterator.Copy();
                }
            }

            Assert.Fail("URP global settings no longer serialize an 'm_VolumeProfile' reference — Unity renamed it; update this test.");
            return null;
        }
    }
}
