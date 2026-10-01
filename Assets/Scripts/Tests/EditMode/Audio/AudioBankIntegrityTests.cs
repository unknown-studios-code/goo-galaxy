using FMODUnity;
using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Models;
using NUnit.Framework;
using UnityEditor;

namespace GooGalaxy.Tests.EditMode.Audio
{
    // Checks the authored Audio Config against the banks built from the FMOD Studio project, so a renamed event, a
    // moved event or a bank that was never rebuilt fails here instead of playing silence on a device.
    [TestFixture]
    public class AudioBankIntegrityTests
    {
        private const string DefaultConfigPath = "Assets/Data/Audio/DefaultAudioConfig.asset";

        private AudioConfigSO _config;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // Reads the built .bank files only, so it needs no FMOD Studio install — CI included.
            EventManager.RefreshBanks();
            _config = AssetDatabase.LoadAssetAtPath<AudioConfigSO>(DefaultConfigPath);
        }

        [Test]
        public void GetMusicEvent_MatchTrack_ResolvesToAnEventInTheMatchBank()
        {
            // GIVEN
            string bankName = _config.GetBankName(AudioBank.Match);

            // WHEN
            EditorEventRef musicEvent = EventManager.EventFromGUID(_config.GetMusicEvent(MusicState.Match).Guid);

            // THEN
            Assert.That(IsInBank(musicEvent, bankName), Is.True);
        }

        [TestCase(MatchStinger.Victory)]
        [TestCase(MatchStinger.Defeat)]
        [TestCase(MatchStinger.Draw)]
        public void GetStingerEvent_EachStinger_ResolvesToAnEventInTheMatchBank(MatchStinger stinger)
        {
            // GIVEN
            string bankName = _config.GetBankName(AudioBank.Match);

            // WHEN
            EditorEventRef stingerEvent = EventManager.EventFromGUID(_config.GetStingerEvent(stinger).Guid);

            // THEN
            Assert.That(IsInBank(stingerEvent, bankName), Is.True);
        }

        [TestCase(AudioCue.ButtonPress)]
        public void TryGetCueEvent_EachCue_ResolvesToAnEventInTheMatchBank(AudioCue cue)
        {
            // GIVEN
            string bankName = _config.GetBankName(AudioBank.Match);
            _config.TryGetCueEvent(cue, out EventReference reference);

            // WHEN
            EditorEventRef cueEvent = EventManager.EventFromGUID(reference.Guid);

            // THEN
            Assert.That(IsInBank(cueEvent, bankName), Is.True);
        }

        [TestCase(AudioParameter.MatchIntensity)]
        [TestCase(AudioParameter.TerritoryBalance)]
        public void GetParameterName_EachParameter_NamesAGlobalParameterInTheBuiltBanks(AudioParameter parameter)
        {
            // GIVEN
            string parameterName = _config.GetParameterName(parameter);

            // WHEN
            EditorParamRef builtParameter = EventManager.ParamFromPath(parameterName);

            // THEN
            Assert.That(builtParameter != null && builtParameter.IsGlobal, Is.True);
        }

        private static bool IsInBank(EditorEventRef eventRef, string bankName)
        {
            if (eventRef == null)
            {
                return false;
            }

            for (int i = 0; i < eventRef.Banks.Count; i++)
            {
                if (eventRef.Banks[i].Name == bankName)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
