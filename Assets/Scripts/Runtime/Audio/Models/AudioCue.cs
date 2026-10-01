namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>A short, fire-and-forget sound an <see cref="Interfaces.IAudioService" /> plays on request.</summary>
    /// <remarks>
    /// Values are explicit because each one is serialized into the cue table of an <c>AudioConfigSO</c>: adding a
    /// member is safe, renumbering one silently rebinds the authored table to a different sound.
    /// </remarks>
    public enum AudioCue
    {
        /// <summary>A UI button was pressed.</summary>
        ButtonPress = 0,
    }
}
