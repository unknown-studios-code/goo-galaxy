using System;
using GooGalaxy.Runtime.Audio.Models;

namespace GooGalaxy.Runtime.Audio.Interfaces
{
    /// <summary>
    /// Reports whether a bank's content is ready to play, so a consumer can wait for it without loading the bank itself.
    /// </summary>
    /// <remarks>
    /// One owner loads and unloads each bank; everything else only asks. "Loaded" means the load has finished — a bank the
    /// audio engine could not find also finishes, and then simply stays silent (see <see cref="IAudioService.LoadBankAsync" />).
    /// Every member is called and raised on the main thread.
    /// </remarks>
    public interface IAudioBankStatus
    {
        /// <summary>Raised once each time a bank finishes loading, carrying that bank.</summary>
        /// <remarks>Raised after <see cref="IsLoaded" /> already answers true for it, so a subscriber may play from it at once.</remarks>
        public event Action<AudioBank> BankLoaded;

        /// <summary>Reports whether a bank has finished loading and has not been unloaded since.</summary>
        /// <param name="bank">The bank to ask about.</param>
        /// <returns>True once the bank's load has finished; false while it is still loading, before it is asked for, and after it is unloaded.</returns>
        public bool IsLoaded(AudioBank bank);
    }
}
