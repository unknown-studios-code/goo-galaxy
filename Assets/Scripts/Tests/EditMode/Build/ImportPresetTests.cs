using NUnit.Framework;
using UnityEditor;
using UnityEditor.Presets;

namespace GooGalaxy.Tests.EditMode.Build
{
    [TestFixture]
    public class ImportPresetTests
    {
        private const string PresetFolder = "Assets/Settings/Presets/";
        private const string SpriteTexturePath = "Assets/Art/Sprites/Board/SpriteHex.png";
        private const string AtlasPath = "Assets/Art/Sprites/Board/BoardSprites.spriteatlasv2";

        [Test]
        public void GetDefaultPresetsForType_TextureImporter_HasThreeRegistrations()
        {
            // GIVEN

            // WHEN
            DefaultPreset[] registrations = GetRegistrations(SpriteTexturePath);

            // THEN
            Assert.That(registrations, Has.Length.EqualTo(3), "TextureImporter registrations changed in Project Settings > Preset Manager.");
        }

        [TestCase(0, "**/*_Normal.*", "TextureNormal.preset")]
        [TestCase(1, "Assets/Art/Sprites/**", "TextureSingleSprite.preset")]
        [TestCase(2, "Assets/Art/**", "TextureAlbedo.preset")]
        public void GetDefaultPresetsForType_TextureImporter_RegistersFilterToPresetInOrder(int index, string filter, string presetFile)
        {
            // GIVEN

            // WHEN
            DefaultPreset registration = GetRegistrations(SpriteTexturePath)[index];

            // THEN
            Assert.That(
                $"{registration.filter} -> {AssetDatabase.GetAssetPath(registration.preset)}",
                Is.EqualTo($"glob:\"{filter}\" -> {PresetFolder}{presetFile}"),
                $"TextureImporter registration {index} changed — order decides which preset wins."
            );
        }

        [Test]
        public void GetDefaultPresetsForType_SpriteAtlasImporter_RegistersTheAtlasPreset()
        {
            // GIVEN

            // WHEN
            DefaultPreset[] registrations = GetRegistrations(AtlasPath);

            // THEN
            Assert.That(
                $"{registrations[0].filter} -> {AssetDatabase.GetAssetPath(registrations[0].preset)}",
                Is.EqualTo($"glob:\"Assets/Art/**\" -> {PresetFolder}TextureSpriteAtlas.preset"),
                "SpriteAtlasImporter registration changed in Project Settings > Preset Manager."
            );
        }

        [TestCase(SpriteTexturePath, 3)]
        [TestCase(AtlasPath, 1)]
        public void GetDefaultPresetsForType_EachRegistration_IsEnabled(string importerPath, int expectedCount)
        {
            // GIVEN

            // WHEN
            int enabledCount = CountEnabled(GetRegistrations(importerPath));

            // THEN
            Assert.That(enabledCount, Is.EqualTo(expectedCount), $"A registration for {importerPath} is disabled.");
        }

        [TestCase("TextureSingleSprite.preset", "Android", 50)]
        [TestCase("TextureSingleSprite.preset", "iOS", 50)]
        [TestCase("TextureAlbedo.preset", "Android", 50)]
        [TestCase("TextureAlbedo.preset", "iOS", 50)]
        [TestCase("TextureNormal.preset", "Android", 48)]
        [TestCase("TextureNormal.preset", "iOS", 48)]
        [TestCase("TextureSpriteAtlas.preset", "Android", 50)]
        [TestCase("TextureSpriteAtlas.preset", "iOS", 50)]
        public void TextureFormat_MobilePlatform_IsTheAstcBlockSize(string presetFile, string platform, int expectedFormat)
        {
            // GIVEN
            Preset preset = LoadPreset(presetFile);

            // WHEN
            int format = ReadPlatformInt(preset, platform, "m_TextureFormat");

            // THEN
            Assert.That(format, Is.EqualTo(expectedFormat), $"{presetFile} {platform} format changed (50 = ASTC 6x6, 48 = ASTC 4x4).");
        }

        [TestCase("TextureSingleSprite.preset", "Android")]
        [TestCase("TextureSingleSprite.preset", "iOS")]
        [TestCase("TextureAlbedo.preset", "Android")]
        [TestCase("TextureAlbedo.preset", "iOS")]
        [TestCase("TextureNormal.preset", "Android")]
        [TestCase("TextureNormal.preset", "iOS")]
        [TestCase("TextureSpriteAtlas.preset", "Android")]
        [TestCase("TextureSpriteAtlas.preset", "iOS")]
        public void Overridden_MobilePlatform_IsOn(string presetFile, string platform)
        {
            // GIVEN
            Preset preset = LoadPreset(presetFile);

            // WHEN
            int isOverridden = ReadPlatformInt(preset, platform, "m_Overridden");

            // THEN
            Assert.That(isOverridden, Is.EqualTo(1), $"{presetFile} {platform} override is off, so the platform format is ignored on import.");
        }

        [TestCase("TextureSingleSprite.preset", "Android", 512)]
        [TestCase("TextureSingleSprite.preset", "iOS", 512)]
        [TestCase("TextureAlbedo.preset", "Android", 1024)]
        [TestCase("TextureAlbedo.preset", "iOS", 1024)]
        [TestCase("TextureNormal.preset", "Android", 1024)]
        [TestCase("TextureNormal.preset", "iOS", 1024)]
        [TestCase("TextureSpriteAtlas.preset", "Android", 1024)]
        [TestCase("TextureSpriteAtlas.preset", "iOS", 1024)]
        public void MaxTextureSize_MobilePlatform_IsTheBudgetedSize(string presetFile, string platform, int expectedSize)
        {
            // GIVEN
            Preset preset = LoadPreset(presetFile);

            // WHEN
            int size = ReadPlatformInt(preset, platform, "m_MaxTextureSize");

            // THEN
            Assert.That(size, Is.EqualTo(expectedSize), $"{presetFile} {platform} max texture size changed — it caps memory on device.");
        }

        private static DefaultPreset[] GetRegistrations(string importerPath)
        {
            var importer = AssetImporter.GetAtPath(importerPath);
            Assert.That(importer != null, Is.True, $"No importer at {importerPath}; the sample asset moved.");
            return Preset.GetDefaultPresetsForType(new PresetType(importer));
        }

        private static int CountEnabled(DefaultPreset[] registrations)
        {
            int count = 0;
            for (int i = 0; i < registrations.Length; i++)
            {
                if (registrations[i].enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private static Preset LoadPreset(string presetFile)
        {
            Preset preset = AssetDatabase.LoadAssetAtPath<Preset>(PresetFolder + presetFile);
            Assert.That(preset != null, Is.True, $"Preset is missing or failed to import: {PresetFolder}{presetFile}");
            return preset;
        }

        private static int ReadPlatformInt(Preset preset, string platform, string field)
        {
            const string buildTargetSuffix = ".m_BuildTarget";

            PropertyModification[] modifications = preset.PropertyModifications;
            string prefix = null;

            // Platform entries are not in a fixed order across presets (TextureSpriteAtlas lists Android before iOS),
            // so the entry is found by m_BuildTarget.
            for (int i = 0; i < modifications.Length; i++)
            {
                string path = modifications[i].propertyPath;
                if (path.StartsWith("m_PlatformSettings.Array.data[") && path.EndsWith(buildTargetSuffix) && modifications[i].value == platform)
                {
                    prefix = path[..^buildTargetSuffix.Length];
                    break;
                }
            }

            Assert.That(prefix, Is.Not.Null, $"Preset '{preset.name}' has no platform entry for '{platform}'.");

            string fieldPath = $"{prefix}.{field}";
            for (int i = 0; i < modifications.Length; i++)
            {
                if (modifications[i].propertyPath == fieldPath)
                {
                    return int.Parse(modifications[i].value);
                }
            }

            Assert.Fail($"Preset '{preset.name}' {platform} entry has no '{field}' modification.");
            return -1;
        }
    }
}
