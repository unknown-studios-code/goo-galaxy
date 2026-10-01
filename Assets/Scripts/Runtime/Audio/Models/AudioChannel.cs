namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>A volume channel a player can set independently, backed by one VCA in the FMOD Studio project.</summary>
    public enum AudioChannel
    {
        /// <summary>Everything the game plays.</summary>
        Master = 0,

        /// <summary>Music and stingers.</summary>
        Music = 1,

        /// <summary>In-match sound effects.</summary>
        Sfx = 2,

        /// <summary>Menu and HUD feedback.</summary>
        Ui = 3,
    }
}
