using System;
using FMODUnity;
using UnityEngine;

namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>One row of an <see cref="Data.AudioConfigSO" />'s card cue table: which FMOD event plays when a card is played.</summary>
    /// <remarks>The fields are public because there is nothing here to guard; a [SerializeField] private field would serialize identically.</remarks>
    [Serializable]
    public struct CardCueEntry
    {
        [Tooltip(
            "The id of the card this row answers, exactly as the card's data asset spells it. Author each card once; when a card appears twice, "
                + "the first row wins. A card with no row plays the CardFallback cue."
        )]
        public string CardId;

        [Tooltip("The FMOD event played when the card is played. Left unset, the card plays the CardFallback cue and one warning is logged.")]
        public EventReference Event;
    }
}
