namespace GooGalaxy.Runtime.Shared.Constants
{
    /// <summary>
    /// Console text for the <c>Runtime.Audio</c> assembly: an audio engine that would not start, FMOD content the game
    /// asked for and could not find, the wiring the music needs, and authoring faults on the Audio Config asset.
    /// </summary>
    /// <remarks>
    /// Every message says what has gone quiet and for how long, so a silent build is not mistaken for a muted device, and
    /// names where to look. Every message that takes arguments carries the <c>Format</c> suffix.
    /// </remarks>
    public static class AudioLogMessages
    {
        public const string ServiceConfigMissing =
            "FmodAudioService was created without an AudioConfigSO, so no sound will play until this scene unloads; gameplay is unaffected. "
            + "Assign an Audio Config asset to the Audio Config field of the scene's GameLifetimeScope.";

        public const string ServiceNotPlaying =
            "FmodAudioService was used outside Play Mode, where FMOD's RuntimeManager is unavailable, so it stays silent for the rest of its "
            + "lifetime. This is expected in EditMode tests; anywhere else it means editor code reached the service.";

        public const string EngineInitializationFailedFormat =
            "FMOD failed to initialise: {0} No sound will play until this scene unloads; gameplay is unaffected. "
            + "Check FMOD > Edit Settings, and that the FMOD native libraries for this platform are imported.";

        public const string EngineSystemInvalid =
            "FMOD's Studio system is not valid after initialisation, so no sound will play until this scene unloads; gameplay is unaffected. "
            + "Check the FMOD console output above this line for the underlying failure.";

        public const string EventUnassignedFormat =
            "Audio Config '{0}' has no FMOD event assigned for {1} {2}, so it stays silent until this scene unloads; gameplay is unaffected. "
            + "Assign the event on the asset once the FMOD Studio project authors it.";

        public const string EventNotFoundFormat =
            "The FMOD event assigned for {0} ({1}) could not be played: {2}. It stays silent until this scene unloads; gameplay is unaffected. "
            + "Check that the event still exists in the FMOD Studio project and that the bank holding it is built and loaded.";

        public const string BankNameMissingFormat =
            "Audio Config '{0}' names no FMOD bank for {1}, so nothing in it will play until this scene unloads; gameplay is unaffected. "
            + "Enter the bank name exactly as FMOD Studio builds it, without the .bank extension.";

        public const string BankFileMissingFormat =
            "FMOD bank '{0}' was not found at '{1}', so everything in it stays silent until this scene unloads; gameplay is unaffected. "
            + "Build the bank in the FMOD Studio project, or check the name against the Audio Config asset.";

        public const string BankLoadFailedFormat =
            "FMOD could not load bank '{0}': {1} Everything in it stays silent until this scene unloads; gameplay is unaffected. "
            + "Check that the bank is built for this platform and named exactly as the Audio Config asset says.";

        public const string BankNotLoadedFormat =
            "FMOD bank '{0}' did not finish loading, so everything in it stays silent until this scene unloads; gameplay is unaffected. "
            + "Check the FMOD console output above this line, and that the bank is built for this platform.";

        public const string ParameterNameMissingFormat =
            "Audio Config '{0}' names no FMOD global parameter for {1}, so it will not shape the music until this scene unloads; gameplay is "
            + "unaffected. Enter the parameter name exactly as the FMOD Studio project spells it.";

        public const string ParameterNotFoundFormat =
            "FMOD global parameter '{0}' was not found ({1}), so it will not shape the music until this scene unloads; gameplay is unaffected. "
            + "Check that it is a global parameter, not an event one, and that the bank defining it is loaded before the match starts.";

        public const string ChannelNotFoundFormat =
            "FMOD VCA '{0}' was not found ({1}), so the {2} volume cannot be changed until this scene unloads; the player's choice is kept but "
            + "not heard. Check that the FMOD Studio project defines the VCA at exactly that path and that its bank is loaded.";

        public const string MusicConfigMissing =
            "AdaptiveMusicController has no AudioConfigSO, so match music stays off until this scene unloads; gameplay is unaffected. "
            + "Assign an Audio Config asset to the Audio Config field of the scene's GameLifetimeScope.";

        public const string MusicServiceMissing =
            "AdaptiveMusicController was injected with a null IAudioService, so match music stays off until this scene unloads; gameplay is "
            + "unaffected. Check what GameLifetimeScope registers as IAudioService.";

        public const string MusicStartFailed =
            "Starting the match track failed after an unexpected exception (logged below), so match music stays off until this scene unloads; "
            + "gameplay is unaffected. The controller does not retry — check the stack trace for the cause.";

        public const string ConfigIntensityOutOfRangeFormat =
            "Audio Config '{0}' authors a match intensity outside 0..1, the range of the FMOD MatchIntensity parameter. "
            + "It was clamped into range; check the Match Intensity fields.";

        public const string ConfigThresholdsOutOfOrderFormat =
            "Audio Config '{0}' authors a negative band threshold, or a mid band that ends before the early band does. "
            + "The threshold was raised to the nearest legal value; check the elapsed-seconds and final-window fields.";

        public const string ConfigIntensitiesNotAscendingFormat =
            "Audio Config '{0}' authors a match intensity that falls as the match goes on (idle, early, mid, late, final window, overtime). "
            + "It was kept as authored; confirm the drop is intended.";

        public const string ConfigNameMissingFormat =
            "Audio Config '{0}' has no value in its {1} field, so the FMOD content it names cannot be found. Enter the name exactly as FMOD "
            + "Studio spells it.";
    }
}
