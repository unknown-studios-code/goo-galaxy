using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Analytics.Models
{
    /// <summary>
    /// One captured gameplay fact, held by value until the next flush writes it out. Built only through the
    /// <c>For*</c> factories, one per <see cref="AnalyticsEventType" />, so no caller ever has to know which payload
    /// slot a field lives in.
    /// </summary>
    /// <remarks>
    /// <b>Capture is allocation-free.</b> Every field is a value type, and the one reference it can hold is the
    /// string a <see cref="CardId" /> already wraps — nothing is formatted or concatenated until
    /// <see cref="AnalyticsRecordFormatter" /> runs at flush time.
    /// <para>
    /// The payload slots are generic on purpose and assembly-internal: their meaning depends on
    /// <see cref="Type" />, and the factories and the formatter are the only two places that mapping is written. A
    /// slot an event does not use holds its default.
    /// </para>
    /// </remarks>
    public readonly struct AnalyticsRecord
    {
        private AnalyticsRecord(
            AnalyticsEventType type,
            long timestampMs,
            int matchOrdinal,
            int playerId,
            int slotA = 0,
            int slotB = 0,
            int slotC = 0,
            int slotD = 0,
            float measureA = 0f,
            float measureB = 0f,
            long durationMs = 0L,
            CardId card = default,
            HexCoordinates hex = default,
            bool flag = false
        )
        {
            Type = type;
            TimestampMs = timestampMs;
            MatchOrdinal = matchOrdinal;
            PlayerId = playerId;
            SlotA = slotA;
            SlotB = slotB;
            SlotC = slotC;
            SlotD = slotD;
            MeasureA = measureA;
            MeasureB = measureB;
            DurationMs = durationMs;
            Card = card;
            Hex = hex;
            Flag = flag;
        }

        public AnalyticsEventType Type { get; }

        /// <summary>Milliseconds of real time since the capture session opened, unaffected by time scale.</summary>
        public long TimestampMs { get; }

        /// <summary>
        /// Which match of the session this record belongs to, counting from one; zero before the first match starts.
        /// </summary>
        public int MatchOrdinal { get; }

        /// <summary>The acting player, or zero for an event no single player caused.</summary>
        public int PlayerId { get; }

        internal int SlotA { get; }

        internal int SlotB { get; }

        internal int SlotC { get; }

        internal int SlotD { get; }

        internal float MeasureA { get; }

        internal float MeasureB { get; }

        internal long DurationMs { get; }

        internal CardId Card { get; }

        internal HexCoordinates Hex { get; }

        internal bool Flag { get; }

        /// <summary>
        /// Builds the record that opens a session, stamped at zero. The device context travels on the session, not here.
        /// </summary>
        public static AnalyticsRecord ForSessionStart()
        {
            return new AnalyticsRecord(AnalyticsEventType.SessionStart, 0L, 0, 0);
        }

        /// <summary>Builds the record that closes a session.</summary>
        /// <remarks>
        /// The record's <see cref="MatchOrdinal" /> is the match count, since ordinals count from one: the last match
        /// started is also how many the session saw.
        /// </remarks>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="durationMs">How long the session ran, in real-time milliseconds.</param>
        /// <param name="matchCount">How many matches started during the session.</param>
        public static AnalyticsRecord ForSessionEnd(long timestampMs, long durationMs, int matchCount)
        {
            return new AnalyticsRecord(AnalyticsEventType.SessionEnd, timestampMs, matchCount, 0, slotA: matchCount, durationMs: durationMs);
        }

        /// <summary>Builds the record for a match being announced.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal this match was given.</param>
        /// <param name="configuration">The configuration the match was announced with.</param>
        public static AnalyticsRecord ForMatchStart(long timestampMs, int matchOrdinal, in MatchConfiguration configuration)
        {
            return new AnalyticsRecord(
                AnalyticsEventType.MatchStart,
                timestampMs,
                matchOrdinal,
                0,
                slotA: configuration.Seed,
                slotB: (int)configuration.PlayerOne.Control,
                slotC: (int)configuration.PlayerTwo.Control,
                measureA: configuration.StandardDurationSeconds,
                measureB: configuration.OvertimeDurationSeconds
            );
        }

        /// <summary>Builds the record for a match ending.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match that ended.</param>
        /// <param name="outcome">Who won and what ended it.</param>
        /// <param name="playerOneScore">The last unit count published for the first seat.</param>
        /// <param name="playerTwoScore">The last unit count published for the second seat.</param>
        /// <param name="durationMs">Real-time milliseconds from the match being announced to it ending.</param>
        /// <param name="hasEnteredOvertime">Whether the match reached <see cref="MatchPhase.Overtime" />.</param>
        public static AnalyticsRecord ForMatchEnd(
            long timestampMs,
            int matchOrdinal,
            in MatchOutcome outcome,
            int playerOneScore,
            int playerTwoScore,
            long durationMs,
            bool hasEnteredOvertime
        )
        {
            return new AnalyticsRecord(
                AnalyticsEventType.MatchEnd,
                timestampMs,
                matchOrdinal,
                0,
                slotA: outcome.WinnerPlayerId,
                slotB: (int)outcome.Reason,
                slotC: playerOneScore,
                slotD: playerTwoScore,
                durationMs: durationMs,
                flag: hasEnteredOvertime
            );
        }

        /// <summary>Builds the record for a card play the board accepted.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match being played.</param>
        /// <param name="playerId">The player who played the card.</param>
        /// <param name="card">The card played.</param>
        /// <param name="target">The first target hex — the deploy hex, or a Protocol's cluster centre.</param>
        /// <param name="energyCost">The card's authored Energy cost.</param>
        public static AnalyticsRecord ForCardDeployed(long timestampMs, int matchOrdinal, int playerId, CardId card, HexCoordinates target, int energyCost)
        {
            return new AnalyticsRecord(AnalyticsEventType.CardDeployed, timestampMs, matchOrdinal, playerId, slotA: energyCost, card: card, hex: target);
        }

        /// <summary>Builds the record for a card play that was refused.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match being played.</param>
        /// <param name="playerId">The player who attempted the play.</param>
        /// <param name="card">The card in the played slot, or <see cref="CardId.Empty" /> when it never resolved.</param>
        /// <param name="reason">Why the play was refused.</param>
        public static AnalyticsRecord ForCardPlayRejected(long timestampMs, int matchOrdinal, int playerId, CardId card, CardPlayResult reason)
        {
            return new AnalyticsRecord(AnalyticsEventType.CardPlayRejected, timestampMs, matchOrdinal, playerId, slotA: (int)reason, card: card);
        }

        /// <summary>Builds the record for an Energy spend attempt.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match being played.</param>
        /// <param name="playerId">The player who attempted the spend.</param>
        /// <param name="energyAfter">The player's balance after the attempt.</param>
        /// <param name="wasSuccessful">Whether the Energy was actually deducted.</param>
        public static AnalyticsRecord ForEnergySpent(long timestampMs, int matchOrdinal, int playerId, float energyAfter, bool wasSuccessful)
        {
            return new AnalyticsRecord(AnalyticsEventType.EnergySpent, timestampMs, matchOrdinal, playerId, measureA: energyAfter, flag: wasSuccessful);
        }

        /// <summary>Builds the record for a landing's conversions.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match being played.</param>
        /// <param name="playerId">The player whose landing converted.</param>
        /// <param name="convertedCount">How many units changed owner.</param>
        /// <param name="strippedCount">How many armored units spent their armor instead.</param>
        public static AnalyticsRecord ForConversion(long timestampMs, int matchOrdinal, int playerId, int convertedCount, int strippedCount)
        {
            return new AnalyticsRecord(AnalyticsEventType.ConversionEvent, timestampMs, matchOrdinal, playerId, slotA: convertedCount, slotB: strippedCount);
        }

        /// <summary>Builds the record for a deployment's impacts resolving.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match being played.</param>
        /// <param name="playerId">The player whose deployment resolved the impacts.</param>
        /// <param name="affectedUnitCount">How many units an impact applied a status to.</param>
        /// <param name="affectedHexCount">How many hexes had their state changed.</param>
        /// <param name="destroyedUnitCount">How many units a self-destruct impact removed.</param>
        public static AnalyticsRecord ForAbilityResolved(
            long timestampMs,
            int matchOrdinal,
            int playerId,
            int affectedUnitCount,
            int affectedHexCount,
            int destroyedUnitCount
        )
        {
            return new AnalyticsRecord(
                AnalyticsEventType.AbilityResolved,
                timestampMs,
                matchOrdinal,
                playerId,
                slotA: affectedUnitCount,
                slotB: affectedHexCount,
                slotC: destroyedUnitCount
            );
        }

        /// <summary>Builds the record for a discard.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match being played.</param>
        /// <param name="playerId">The player who discarded.</param>
        /// <param name="card">The card that left the hand.</param>
        /// <param name="slotIndex">The zero-based hand slot it left.</param>
        public static AnalyticsRecord ForCardDiscarded(long timestampMs, int matchOrdinal, int playerId, CardId card, int slotIndex)
        {
            return new AnalyticsRecord(AnalyticsEventType.CardDiscarded, timestampMs, matchOrdinal, playerId, slotA: slotIndex, card: card);
        }

        /// <summary>Builds the record for a phase transition.</summary>
        /// <param name="timestampMs">Milliseconds since the session opened.</param>
        /// <param name="matchOrdinal">The ordinal of the match in progress.</param>
        /// <param name="phase">The phase just entered.</param>
        public static AnalyticsRecord ForPhaseChanged(long timestampMs, int matchOrdinal, MatchPhase phase)
        {
            return new AnalyticsRecord(AnalyticsEventType.PhaseChanged, timestampMs, matchOrdinal, 0, slotA: (int)phase);
        }
    }
}
