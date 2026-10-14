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

        /// <summary>A card was played that has no sound of its own in the card cue table.</summary>
        CardFallback = 1,

        /// <summary>A unit cloned itself onto a neighbouring hex.</summary>
        CloneMove = 2,

        /// <summary>A unit jumped to a new hex, leaving its old one.</summary>
        JumpMove = 3,

        /// <summary>A landing converted exactly one unit.</summary>
        ConversionSingle = 4,

        /// <summary>A landing converted a small group of units.</summary>
        ConversionChain = 5,

        /// <summary>A landing converted a large group of units.</summary>
        ConversionMass = 6,

        /// <summary>A landing stripped the armor from at least one unit instead of converting it.</summary>
        ArmorStrip = 7,

        /// <summary>A deployment froze a unit.</summary>
        Freeze = 8,

        /// <summary>A deployment rooted a unit.</summary>
        Root = 9,

        /// <summary>A deployment left a hazard on the board.</summary>
        HazardSpawned = 10,

        /// <summary>A unit blew itself up, on landing or when its fuse ran out.</summary>
        Detonation = 11,

        /// <summary>The local player discarded a card.</summary>
        Discard = 12,

        /// <summary>The match went into overtime.</summary>
        OvertimeWarning = 13,

        /// <summary>The board refused a card the local player tried to play.</summary>
        PlayRejected = 14,

        /// <summary>The local player picked a card or a unit.</summary>
        Select = 15,

        /// <summary>The local player started dragging their selection.</summary>
        DragStart = 16,

        /// <summary>The local player let go of their selection where it could not act.</summary>
        DropRejected = 17,
    }
}
