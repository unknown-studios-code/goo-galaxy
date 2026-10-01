using System.Threading;
using GooGalaxy.Runtime.Audio.Models;
using UnityEngine;

namespace GooGalaxy.Runtime.Audio.Interfaces
{
    /// <summary>
    /// Plays the game's sound: one-shot cues, the single music track, match stingers, global parameters, volume channels
    /// and bank loading. Callers name what they want in game terms and never see the audio engine behind it.
    /// </summary>
    /// <remarks>
    /// <b>An implementation never breaks gameplay.</b> When the engine cannot start, or a bank, event, parameter or
    /// channel is missing, it reports the fault once and turns that capability into a silent no-op for the rest of its
    /// lifetime — no method here throws for missing content. Every call is made on the main thread.
    /// </remarks>
    public interface IAudioService
    {
        /// <summary>Plays a short sound once and forgets it.</summary>
        /// <param name="cue">The sound to play.</param>
        public void PlayOneShot(AudioCue cue);

        /// <summary>Starts a music track, replacing the one playing.</summary>
        /// <remarks>
        /// Exactly one track is owned at a time. Asking for the track already playing does nothing, so it does not restart;
        /// asking for another stops the current one first, letting it fade out. <see cref="MusicState.None" /> is the same
        /// as <see cref="StopMusic" /> with a fade-out.
        /// </remarks>
        /// <param name="state">The track to play.</param>
        public void PlayMusic(MusicState state);

        /// <summary>Stops the music track, if one is playing, and releases it.</summary>
        /// <param name="allowFadeOut">True to let the track play its authored fade-out; false to cut it immediately.</param>
        public void StopMusic(bool allowFadeOut);

        /// <summary>Plays the end-of-match flourish once. It does not replace or stop the music track.</summary>
        /// <param name="stinger">The flourish to play.</param>
        public void PlayStinger(MatchStinger stinger);

        /// <summary>Sets a global parameter of the audio project.</summary>
        /// <remarks>
        /// Call only after <see cref="LoadBankAsync" /> has completed for the bank that defines the parameter: an
        /// implementation may resolve it once per bank load, and a lookup that fails before any bank has loaded is
        /// retried the next time a bank finishes loading, never before.
        /// </remarks>
        /// <param name="parameter">The parameter to set.</param>
        /// <param name="value">The new value, in the range the audio project authors for that parameter.</param>
        public void SetParameter(AudioParameter parameter, float value);

        /// <summary>Sets the volume of one channel.</summary>
        /// <param name="channel">The channel to set.</param>
        /// <param name="linearVolume">Linear gain from 0 (silent) to 1 (as authored). Values outside that range are clamped.</param>
        public void SetVolume(AudioChannel channel, float linearVolume);

        /// <summary>Reads the volume of one channel.</summary>
        /// <param name="channel">The channel to read.</param>
        /// <returns>
        /// Linear gain from 0 to 1. When the channel is unavailable, the last value passed to <see cref="SetVolume" /> for it,
        /// or 1 if none was — so a settings screen keeps what the player chose even with audio off.
        /// </returns>
        public float GetVolume(AudioChannel channel);

        /// <summary>Loads a bank and its sample data, completing once the samples are ready to play without a hitch.</summary>
        /// <remarks>
        /// Loading a bank that is already loaded, or already loading, completes as soon as it is ready and takes no second
        /// reference. A bank that cannot be loaded completes normally, after reporting the fault once; what it holds then
        /// stays silent. Only cancellation throws.
        /// </remarks>
        /// <param name="bank">The bank to load.</param>
        /// <param name="token">Cancels the wait, not the load; pass the caller's <c>destroyCancellationToken</c>.</param>
        /// <returns>An awaitable that completes when the bank is ready or has failed.</returns>
        /// <exception cref="System.OperationCanceledException">
        /// The token was already cancelled on entry, or was cancelled before the bank was ready.
        /// </exception>
        public Awaitable LoadBankAsync(AudioBank bank, CancellationToken token);

        /// <summary>Unloads a bank loaded through <see cref="LoadBankAsync" />. Does nothing for a bank that is not loaded.</summary>
        /// <param name="bank">The bank to unload.</param>
        public void UnloadBank(AudioBank bank);
    }
}
