using System;
using FMODUnity;
using UnityEngine;

namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>One row of an <see cref="Data.AudioConfigSO" />'s cue table: which FMOD event plays for a cue.</summary>
    /// <remarks>The fields are public because there is nothing here to guard; a [SerializeField] private field would serialize identically.</remarks>
    [Serializable]
    public struct AudioCueEntry
    {
        [Tooltip("The cue this row answers. Author each cue once; when a cue appears twice, the first row wins.")]
        public AudioCue Cue;

        [Tooltip("The FMOD event played for the cue. Left unset, the cue stays silent and one warning is logged the first time it is asked for.")]
        public EventReference Event;
    }
}
