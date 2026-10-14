using System;
using System.Globalization;
using System.IO;
using System.Threading;
using FMOD.Studio;
using FMODUnity;
using GooGalaxy.Runtime.Audio.Data;
using GooGalaxy.Runtime.Audio.Interfaces;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Constants;
using UnityEngine;

namespace GooGalaxy.Runtime.Audio.Services
{
    /// <summary>
    /// The FMOD for Unity implementation of <see cref="IAudioService" />, and the only type in the game that calls into
    /// FMOD. Everything it plays is named by an <see cref="AudioConfigSO" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>FMOD is started lazily</b>, on the first call that needs it, never in the constructor: the container builds this
    /// service in scenes and tests that have no FMOD Studio project linked and no bank built, and those must not pay for,
    /// or fail on, an engine they never use.
    /// </para>
    /// <para>
    /// <b>Every fault degrades rather than throws.</b> An engine that will not start, or a missing config, logs one warning
    /// and silences the whole service for its lifetime. A missing bank, event, global parameter or VCA logs one warning
    /// and silences only that item. An event left unset on the config is the expected state while the FMOD Studio project
    /// is still being authored, and is reported the same way: at most once per cue, track or stinger. This service logs
    /// only warnings; FMOD's own error callback still logs an error when a bank, event, parameter or VCA is genuinely
    /// named but missing, because that is content drift rather than an unauthored project.
    /// </para>
    /// <para>
    /// <b>Allocation.</b> Parameter ids and VCA handles are looked up by name once, which marshals a string, and cached;
    /// after that <see cref="SetParameter" /> and <see cref="SetVolume" /> are allocation-free, which is what lets the music
    /// controller call them from clock and score handlers. See <see cref="IAudioService.SetParameter" /> for the order a
    /// caller must load banks in.
    /// </para>
    /// <para>
    /// Owned by the container as a singleton; <see cref="Dispose" /> stops the music and unloads the banks this instance
    /// loaded when the scope is destroyed. It holds no static state.
    /// </para>
    /// </remarks>
    public sealed class FmodAudioService : IAudioService, IDisposable
    {
        private const string MasterChannelPath = "vca:/Master";
        private const string MusicChannelPath = "vca:/Music";
        private const string SfxChannelPath = "vca:/SFX";
        private const string UiChannelPath = "vca:/UI";
        private const float DefaultLinearVolume = 1f;

        // Bounded so a leaked RuntimeManager.loadingBanksRef (see IsBankPending) cannot spin this wait forever — 5
        // seconds at 60fps, generous next to how fast a local bank actually loads.
        private const int MaxBankWaitFrames = 300;

        private readonly AudioConfigSO _config;
        private readonly PARAMETER_ID[] _parameterIds;
        private readonly LookupState[] _parameterStates;
        private readonly VCA[] _channels;
        private readonly LookupState[] _channelStates;
        private readonly float[] _requestedVolumes;
        private readonly BankState[] _bankStates;
        private readonly string[] _bankNames;
        private readonly bool[] _hasReportedCue;
        private readonly bool[] _hasReportedMusic;
        private readonly bool[] _hasReportedStinger;
        private readonly bool[] _hasReportedParameter;
        private readonly bool[] _hasReportedChannel;

        private FMOD.Studio.System _studioSystem;
        private EventInstance _musicInstance;
        private MusicState _musicState;
        private EngineState _engineState;
        private bool _isDisposed;

        /// <summary>Creates the service without touching FMOD.</summary>
        /// <param name="config">
        /// Names every event, bank and parameter the service plays. May be null, in which case the service stays silent and
        /// says so once, on first use.
        /// </param>
        public FmodAudioService(AudioConfigSO config)
        {
            _config = config;

            int parameterCount = GetSlotCount<AudioParameter>();
            int channelCount = GetSlotCount<AudioChannel>();
            int bankCount = GetSlotCount<AudioBank>();

            _parameterIds = new PARAMETER_ID[parameterCount];
            _parameterStates = new LookupState[parameterCount];
            _channels = new VCA[channelCount];
            _channelStates = new LookupState[channelCount];
            _requestedVolumes = new float[channelCount];
            _bankStates = new BankState[bankCount];
            _bankNames = new string[bankCount];
            _hasReportedCue = new bool[GetSlotCount<AudioCue>()];
            _hasReportedMusic = new bool[GetSlotCount<MusicState>()];
            _hasReportedStinger = new bool[GetSlotCount<MatchStinger>()];
            _hasReportedParameter = new bool[parameterCount];
            _hasReportedChannel = new bool[channelCount];

            Array.Fill(_requestedVolumes, DefaultLinearVolume);
        }

        internal MusicState MusicState => _musicState;

        internal bool IsEngineUnavailable => _engineState == EngineState.Unavailable;

        public void PlayOneShot(AudioCue cue)
        {
            int slot = (int)cue;

            if (!IsSlot(slot, _hasReportedCue.Length) || !TryEnsureEngine())
            {
                return;
            }

            // A cue with no row reads as an unset reference, which is exactly how it is reported.
            _ = _config.TryGetCueEvent(cue, out EventReference reference);

            if (TryStartEvent(reference, _hasReportedCue, slot, cue, out EventInstance instance))
            {
                instance.release();
            }
        }

        public void PlayMusic(MusicState state)
        {
            if (state == MusicState.None)
            {
                StopMusic(true);
                return;
            }

            int slot = (int)state;

            if ((state == _musicState) || !IsSlot(slot, _hasReportedMusic.Length) || !TryEnsureEngine())
            {
                return;
            }

            StopMusic(true);

            if (!TryStartEvent(_config.GetMusicEvent(state), _hasReportedMusic, slot, state, out EventInstance instance))
            {
                return;
            }

            _musicInstance = instance;
            _musicState = state;
        }

        public void StopMusic(bool allowFadeOut)
        {
            _musicState = MusicState.None;

            if (_musicInstance.isValid())
            {
                _musicInstance.stop(allowFadeOut ? FMOD.Studio.STOP_MODE.ALLOWFADEOUT : FMOD.Studio.STOP_MODE.IMMEDIATE);
                _musicInstance.release();
            }

            _musicInstance.clearHandle();
        }

        public void PlayStinger(MatchStinger stinger)
        {
            if (stinger == MatchStinger.None)
            {
                return;
            }

            int slot = (int)stinger;

            if (!IsSlot(slot, _hasReportedStinger.Length) || !TryEnsureEngine())
            {
                return;
            }

            if (TryStartEvent(_config.GetStingerEvent(stinger), _hasReportedStinger, slot, stinger, out EventInstance instance))
            {
                instance.release();
            }
        }

        public void SetParameter(AudioParameter parameter, float value)
        {
            int slot = (int)parameter;

            if (!IsSlot(slot, _parameterStates.Length) || !TryEnsureEngine())
            {
                return;
            }

            if (TryGetParameterId(parameter, slot, out PARAMETER_ID id))
            {
                _studioSystem.setParameterByID(id, value);
            }
        }

        public void SetVolume(AudioChannel channel, float linearVolume)
        {
            int slot = (int)channel;

            if (!IsSlot(slot, _channelStates.Length))
            {
                return;
            }

            float volume = Mathf.Clamp01(linearVolume);

            _requestedVolumes[slot] = volume;

            if (TryEnsureEngine() && TryGetChannel(channel, slot, out VCA vca))
            {
                vca.setVolume(volume);
            }
        }

        public float GetVolume(AudioChannel channel)
        {
            int slot = (int)channel;

            if (!IsSlot(slot, _channelStates.Length))
            {
                return DefaultLinearVolume;
            }

            if (TryEnsureEngine() && TryGetChannel(channel, slot, out VCA vca) && (vca.getVolume(out float volume) == FMOD.RESULT.OK))
            {
                return volume;
            }

            return _requestedVolumes[slot];
        }

        /// <inheritdoc />
        /// <remarks>
        /// Loads through <c>RuntimeManager.LoadBank</c> with sample loading requested, then polls once per frame, for up
        /// to <see cref="MaxBankWaitFrames" /> frames, until the bank is registered and no sample data is still loading.
        /// Unloading or disposing while this waits ends the wait without reporting a failure.
        /// </remarks>
        public async Awaitable LoadBankAsync(AudioBank bank, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            int slot = (int)bank;

            if (!IsSlot(slot, _bankStates.Length) || !TryEnsureEngine())
            {
                return;
            }

            if ((_bankStates[slot] == BankState.Unloaded) && !TryBeginBankLoad(bank, slot))
            {
                return;
            }

            if (_bankStates[slot] != BankState.Loading)
            {
                return;
            }

            int framesWaited = 0;

            while (IsBankPending(_bankNames[slot]) && (framesWaited < MaxBankWaitFrames))
            {
                framesWaited++;
                await Awaitable.NextFrameAsync(token);
            }

            CompleteBankLoad(slot);
        }

        /// <inheritdoc />
        /// <remarks>
        /// WORKAROUND: unloading a bank still on FMOD's async load path — Android with <c>AndroidUseOBB</c> enabled, off
        /// in this project today — is a no-op in <c>RuntimeManager</c>: the bank has not reached its loaded-banks table
        /// yet, so nothing is removed, and the coroutine that registers it later leaks a reference nothing here ever
        /// releases. Latent until OBB delivery is turned on; revisit by deferring the unload until the load is
        /// observably complete.
        /// </remarks>
        public void UnloadBank(AudioBank bank)
        {
            int slot = (int)bank;

            if (!IsSlot(slot, _bankStates.Length))
            {
                return;
            }

            BankState state = _bankStates[slot];

            if (state is not BankState.Loading and not BankState.Loaded)
            {
                return;
            }

            _bankStates[slot] = BankState.Unloaded;

            // WORKAROUND: RuntimeManager recreates itself when touched after teardown, so a scope destroyed while
            // quitting must not reach it.
            if (RuntimeManager.IsInitialized)
            {
                RuntimeManager.UnloadBank(_bankNames[slot]);
            }
        }

        /// <inheritdoc />
        /// <remarks>Stops the music immediately rather than fading it, since the banks it plays from are unloaded next.</remarks>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            StopMusic(false);

            for (int slot = 0; slot < _bankStates.Length; slot++)
            {
                UnloadBank((AudioBank)slot);
            }

            _isDisposed = true;
        }

        private static bool IsSlot(int slot, int slotCount)
        {
            return (slot >= 0) && (slot < slotCount);
        }

        // Sized by the highest declared value rather than the member count, so a gap in an enum's numbering can never
        // index past the end of a cache.
        private static int GetSlotCount<TEnum>()
            where TEnum : struct, Enum
        {
            Array values = Enum.GetValues(typeof(TEnum));
            int highest = -1;

            for (int i = 0; i < values.Length; i++)
            {
                highest = Math.Max(highest, Convert.ToInt32(values.GetValue(i), CultureInfo.InvariantCulture));
            }

            return highest + 1;
        }

        // Checked before the message is formatted, so a silent reference asked for on every button press builds its
        // warning once rather than on every call.
        private static bool TryClaimReport(bool[] hasReported, int slot)
        {
            if (hasReported[slot])
            {
                return false;
            }

            hasReported[slot] = true;

            return true;
        }

        private static string GetChannelPath(AudioChannel channel)
        {
            return channel switch
            {
                AudioChannel.Master => MasterChannelPath,
                AudioChannel.Music => MusicChannelPath,
                AudioChannel.Sfx => SfxChannelPath,
                AudioChannel.Ui => UiChannelPath,
                _ => null,
            };
        }

        // WORKAROUND: RuntimeManager.LoadBank has no existence check of its own — a missing bank reaches FMOD's own
        // ERROR_CALLBACK, which logs an error this service cannot suppress. This mirrors RuntimeManager's own path
        // resolution (RuntimeManager.LoadBank, Platform.GetBankFolder) for the one configuration it actually is a plain
        // filesystem path in: a player using the StreamingAssets import type, off Android, where the folder is packed
        // inside the APK and File.Exists would false-negative on a bank that is genuinely there. The Editor is skipped
        // too: there FMOD reads banks from the linked Studio project's Build folder (PlatformPlayInEditor.GetBankFolder,
        // internal), not from StreamingAssets, so a StreamingAssets check reports a built bank as missing. Everywhere
        // the check is skipped, RuntimeManager.LoadBank is asked directly, exactly as before this method existed.
        private static bool TryResolveBankFilePath(string bankName, out string bankPath)
        {
            bankPath = null;

            if (Application.isEditor || (Application.platform == RuntimePlatform.Android) || (Settings.Instance.ImportType != ImportType.StreamingAssets))
            {
                return false;
            }

            string bankFolder = Application.streamingAssetsPath;

#if !UNITY_EDITOR
            if (!string.IsNullOrEmpty(Settings.Instance.TargetSubFolder))
            {
                bankFolder = RuntimeUtils.GetCommonPlatformPath(Path.Combine(bankFolder, Settings.Instance.TargetSubFolder));
            }
#endif

            bankPath = Path.GetExtension(bankName) == ".bank" ? $"{bankFolder}/{bankName}" : $"{bankFolder}/{bankName}.bank";

            return true;
        }

        private static void ResetMissingLookups(LookupState[] states)
        {
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i] == LookupState.Missing)
                {
                    states[i] = LookupState.Unresolved;
                }
            }
        }

        private bool TryEnsureEngine()
        {
            // Checked first: a disposed service must never report Ready, or a call made after Dispose (a lingering
            // reference held past scope teardown) would start an event instance or take a bank reference nothing here
            // will ever release.
            if (_isDisposed)
            {
                return false;
            }

            if (_engineState == EngineState.Ready)
            {
                return true;
            }

            if (_engineState == EngineState.Unavailable)
            {
                return false;
            }

            return TryStartEngine();
        }

        private bool TryStartEngine()
        {
            if (_config == null)
            {
                MarkEngineUnavailable(AudioLogMessages.ServiceConfigMissing);
                return false;
            }

            // WORKAROUND: RuntimeManager logs an error and hands back nothing when touched outside Play Mode, which is
            // where EditMode tests construct this service.
            if (!Application.isPlaying)
            {
                MarkEngineUnavailable(AudioLogMessages.ServiceNotPlaying);
                return false;
            }

            try
            {
                _studioSystem = RuntimeManager.StudioSystem;
            }
            catch (SystemNotInitializedException exception)
            {
                MarkEngineUnavailable(string.Format(AudioLogMessages.EngineInitializationFailedFormat, exception.Message));
                return false;
            }

            if (!_studioSystem.isValid())
            {
                MarkEngineUnavailable(AudioLogMessages.EngineSystemInvalid);
                return false;
            }

            _engineState = EngineState.Ready;

            return true;
        }

        private void MarkEngineUnavailable(string message)
        {
            _engineState = EngineState.Unavailable;

            Debug.LogWarning(message, _config);
        }

        private bool TryStartEvent<TKey>(EventReference reference, bool[] hasReported, int slot, TKey key, out EventInstance instance)
            where TKey : struct, Enum
        {
            instance = default;

            if (reference.IsNull)
            {
                if (TryClaimReport(hasReported, slot))
                {
                    Debug.LogWarning(string.Format(AudioLogMessages.EventUnassignedFormat, _config.name, typeof(TKey).Name, key), _config);
                }

                return false;
            }

            FMOD.RESULT result = _studioSystem.getEventByID(reference.Guid, out EventDescription description);

            if (result == FMOD.RESULT.OK)
            {
                result = description.createInstance(out instance);
            }

            if (result == FMOD.RESULT.OK)
            {
                result = instance.start();
            }

            if (result == FMOD.RESULT.OK)
            {
                return true;
            }

            if (instance.isValid())
            {
                instance.release();
            }

            instance.clearHandle();

            if (TryClaimReport(hasReported, slot))
            {
                Debug.LogWarning(string.Format(AudioLogMessages.EventNotFoundFormat, key, reference, result), _config);
            }

            return false;
        }

        private bool TryGetParameterId(AudioParameter parameter, int slot, out PARAMETER_ID id)
        {
            id = _parameterIds[slot];

            if (_parameterStates[slot] != LookupState.Unresolved)
            {
                return _parameterStates[slot] == LookupState.Resolved;
            }

            string parameterName = _config.GetParameterName(parameter);

            if (string.IsNullOrWhiteSpace(parameterName))
            {
                _parameterStates[slot] = LookupState.Missing;

                if (TryClaimReport(_hasReportedParameter, slot))
                {
                    Debug.LogWarning(string.Format(AudioLogMessages.ParameterNameMissingFormat, _config.name, parameter), _config);
                }

                return false;
            }

            FMOD.RESULT result = _studioSystem.getParameterDescriptionByName(parameterName, out PARAMETER_DESCRIPTION description);

            if (result != FMOD.RESULT.OK)
            {
                _parameterStates[slot] = LookupState.Missing;

                if (TryClaimReport(_hasReportedParameter, slot))
                {
                    Debug.LogWarning(string.Format(AudioLogMessages.ParameterNotFoundFormat, parameterName, result), _config);
                }

                return false;
            }

            id = description.id;
            _parameterIds[slot] = id;
            _parameterStates[slot] = LookupState.Resolved;

            return true;
        }

        private bool TryGetChannel(AudioChannel channel, int slot, out VCA vca)
        {
            vca = _channels[slot];

            if (_channelStates[slot] != LookupState.Unresolved)
            {
                return _channelStates[slot] == LookupState.Resolved;
            }

            string path = GetChannelPath(channel);
            FMOD.RESULT result = path == null ? FMOD.RESULT.ERR_INVALID_PARAM : _studioSystem.getVCA(path, out vca);

            if (result != FMOD.RESULT.OK)
            {
                _channelStates[slot] = LookupState.Missing;

                if (TryClaimReport(_hasReportedChannel, slot))
                {
                    Debug.LogWarning(string.Format(AudioLogMessages.ChannelNotFoundFormat, path, result, channel), _config);
                }

                return false;
            }

            _channels[slot] = vca;
            _channelStates[slot] = LookupState.Resolved;

            return true;
        }

        private bool TryBeginBankLoad(AudioBank bank, int slot)
        {
            string bankName = _config.GetBankName(bank);

            if (string.IsNullOrWhiteSpace(bankName))
            {
                _bankStates[slot] = BankState.Failed;
                Debug.LogWarning(string.Format(AudioLogMessages.BankNameMissingFormat, _config.name, bank), _config);
                return false;
            }

            if (TryResolveBankFilePath(bankName, out string bankPath) && !File.Exists(bankPath))
            {
                _bankStates[slot] = BankState.Failed;
                Debug.LogWarning(string.Format(AudioLogMessages.BankFileMissingFormat, bankName, bankPath), _config);
                return false;
            }

            try
            {
                RuntimeManager.LoadBank(bankName, true);
            }
            catch (BankLoadException exception)
            {
                _bankStates[slot] = BankState.Failed;
                Debug.LogWarning(string.Format(AudioLogMessages.BankLoadFailedFormat, bankName, exception.Message), _config);
                return false;
            }

            _bankNames[slot] = bankName;
            _bankStates[slot] = BankState.Loading;

            return true;
        }

        // WORKAROUND: the bank itself loads synchronously on most platforms, but Android with OBB delivery streams it
        // in over several frames, which is why registration is waited on as well as sample data.
        private bool IsBankPending(string bankName)
        {
            if (_isDisposed || !RuntimeManager.IsInitialized)
            {
                return false;
            }

            if (!RuntimeManager.HasBankLoaded(bankName))
            {
                return !RuntimeManager.HaveAllBanksLoaded;
            }

            return RuntimeManager.AnySampleDataLoading();
        }

        private void CompleteBankLoad(int slot)
        {
            if (_bankStates[slot] != BankState.Loading)
            {
                return;
            }

            string bankName = _bankNames[slot];

            if (RuntimeManager.IsInitialized && RuntimeManager.HasBankLoaded(bankName))
            {
                _bankStates[slot] = BankState.Loaded;

                // A parameter or channel lookup that failed before any bank was loaded — including every one made at
                // startup, since FMODStudioSettings has no master banks loaded yet — is worth trying again now that
                // this bank might define it. _hasReportedParameter/_hasReportedChannel are untouched, so a lookup that
                // fails again for a real reason still warns only once.
                ResetMissingLookups(_parameterStates);
                ResetMissingLookups(_channelStates);

                return;
            }

            _bankStates[slot] = BankState.Failed;
            Debug.LogWarning(string.Format(AudioLogMessages.BankNotLoadedFormat, bankName), _config);
        }

        private enum LookupState
        {
            Unresolved = 0,
            Resolved = 1,
            Missing = 2,
        }

        private enum BankState
        {
            Unloaded = 0,
            Loading = 1,
            Loaded = 2,
            Failed = 3,
        }

        private enum EngineState
        {
            Uninitialized = 0,
            Ready = 1,
            Unavailable = 2,
        }
    }
}
