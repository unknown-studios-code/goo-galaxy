using System;
using System.Globalization;
using System.Text;
using GooGalaxy.Runtime.Shared.Types;

namespace GooGalaxy.Runtime.Analytics.Models
{
    /// <summary>
    /// Writes an <see cref="AnalyticsRecord" /> as one line of JSON, in schema version <see cref="SchemaVersion" />,
    /// into a caller-owned <see cref="StringBuilder" />.
    /// </summary>
    /// <remarks>
    /// Every line opens with the same four keys, in this order: <c>"v"</c> the schema version, <c>"t"</c> the
    /// session timestamp in milliseconds, <c>"m"</c> the match ordinal, and <c>"e"</c> the snake_case event name.
    /// The event's own keys follow, and the line ends with a single <c>\n</c>, so a file of them is JSON Lines.
    /// <para>
    /// Output is culture-invariant: integers are written digit by digit and floats are formatted through the
    /// invariant culture, so a device set to a comma-decimal locale writes the same bytes as any other. Enums are
    /// written by <b>name</b> as a JSON string, from constant tables rather than reflection; a value no table knows —
    /// one cast from an out-of-range integer — is written as its bare JSON number instead, so it is still recoverable
    /// and still distinguishable from every named value. A non-finite float is written as <c>null</c> so the line
    /// stays valid JSON. Strings — card ids and the session's device context — are escaped for quotes, backslashes
    /// and control characters.
    /// </para>
    /// <para>
    /// Formatting itself allocates nothing: integers, floats and enum names are written from stack buffers and
    /// constant strings. The only allocation is the builder growing past its capacity, which the caller controls.
    /// </para>
    /// </remarks>
    public static class AnalyticsRecordFormatter
    {
        /// <summary>The value every line carries under <c>"v"</c>. Raise it whenever a key is renamed or removed.</summary>
        public const int SchemaVersion = 1;

        public const string SessionStartEventName = "session_start";
        public const string SessionEndEventName = "session_end";
        public const string MatchStartEventName = "match_start";
        public const string MatchEndEventName = "match_end";
        public const string CardDeployedEventName = "card_deployed";
        public const string CardPlayRejectedEventName = "card_play_rejected";
        public const string EnergySpentEventName = "energy_spent";
        public const string ConversionEventName = "conversion_event";
        public const string AbilityResolvedEventName = "ability_resolved";
        public const string CardDiscardedEventName = "card_discarded";
        public const string PhaseChangedEventName = "phase_changed";
        public const string MoveExecutedEventName = "move_executed";

        private const string VersionKey = "v";
        private const string TimestampKey = "t";
        private const string MatchKey = "m";
        private const string EventKey = "e";
        private const string PlayerKey = "p";
        private const string CardKey = "card";
        private const string HexQKey = "q";
        private const string HexRKey = "r";
        private const string CostKey = "cost";
        private const string ReasonKey = "reason";
        private const string EnergyAfterKey = "after";
        private const string SuccessKey = "ok";
        private const string ConvertedKey = "converted";
        private const string StrippedKey = "stripped";
        private const string AffectedKey = "affected";
        private const string HexesKey = "hexes";
        private const string DestroyedKey = "destroyed";
        private const string SlotKey = "slot";
        private const string PhaseKey = "phase";
        private const string MoveKey = "move";
        private const string SourceHexQKey = "sq";
        private const string SourceHexRKey = "sr";
        private const string UnitKey = "unit";
        private const string WinnerKey = "winner";
        private const string PlayerOneScoreKey = "p1_score";
        private const string PlayerTwoScoreKey = "p2_score";
        private const string DurationKey = "dur_ms";
        private const string OvertimeKey = "overtime";
        private const string MatchesKey = "matches";
        private const string SeedKey = "seed";
        private const string PlayerOneControlKey = "p1_ctrl";
        private const string PlayerTwoControlKey = "p2_ctrl";
        private const string StandardSecondsKey = "std_s";
        private const string OvertimeSecondsKey = "ot_s";
        private const string DeviceKey = "device";
        private const string OperatingSystemKey = "os";
        private const string AppVersionKey = "app";

        private const string JsonNull = "null";
        private const string JsonTrue = "true";
        private const string JsonFalse = "false";
        private const string HexDigits = "0123456789abcdef";

        // long.MaxValue has 19 decimal digits; the sign is appended separately.
        private const int MaxInt64Digits = 19;

        private const string Int64MinValueDigits = "9223372036854775808";

        // The longest invariant "G" float is 14 characters ("-3.4028235E+38"); the headroom costs nothing.
        private const int MaxFloatCharacters = 32;

        /// <summary>Returns the snake_case name an event type is written under.</summary>
        /// <param name="type">The event type.</param>
        /// <returns>The event name, or null for <see cref="AnalyticsEventType.None" /> and any undeclared value.</returns>
        public static string GetEventName(AnalyticsEventType type)
        {
            return type switch
            {
                AnalyticsEventType.SessionStart => SessionStartEventName,
                AnalyticsEventType.SessionEnd => SessionEndEventName,
                AnalyticsEventType.MatchStart => MatchStartEventName,
                AnalyticsEventType.MatchEnd => MatchEndEventName,
                AnalyticsEventType.CardDeployed => CardDeployedEventName,
                AnalyticsEventType.CardPlayRejected => CardPlayRejectedEventName,
                AnalyticsEventType.EnergySpent => EnergySpentEventName,
                AnalyticsEventType.ConversionEvent => ConversionEventName,
                AnalyticsEventType.AbilityResolved => AbilityResolvedEventName,
                AnalyticsEventType.CardDiscarded => CardDiscardedEventName,
                AnalyticsEventType.PhaseChanged => PhaseChangedEventName,
                AnalyticsEventType.MoveExecuted => MoveExecutedEventName,
                _ => null,
            };
        }

        /// <summary>Appends one record as one JSON line, newline included.</summary>
        /// <param name="builder">Where the line is appended. Never cleared here; the caller owns its lifetime.</param>
        /// <param name="record">The record to write.</param>
        /// <param name="session">
        /// The session the record belongs to. Only a <see cref="AnalyticsEventType.SessionStart" /> line reads it.
        /// </param>
        /// <returns>
        /// True when a line was appended; false, with the builder untouched, for a record whose type has no event
        /// name — <see cref="AnalyticsEventType.None" /> or an undeclared value.
        /// </returns>
        /// <exception cref="ArgumentNullException">The builder is null.</exception>
        public static bool TryAppendLine(StringBuilder builder, in AnalyticsRecord record, in AnalyticsSession session)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            string eventName = GetEventName(record.Type);

            if (eventName == null)
            {
                return false;
            }

            builder.Append('{');
            AppendKey(builder, VersionKey, isFirst: true);
            AppendInteger(builder, SchemaVersion);
            AppendIntegerField(builder, TimestampKey, record.TimestampMs);
            AppendIntegerField(builder, MatchKey, record.MatchOrdinal);
            AppendStringField(builder, EventKey, eventName);
            AppendPayload(builder, in record, in session);
            builder.Append('}');
            builder.Append('\n');

            return true;
        }

        private static void AppendPayload(StringBuilder builder, in AnalyticsRecord record, in AnalyticsSession session)
        {
            switch (record.Type)
            {
                case AnalyticsEventType.SessionStart:
                    AppendStringField(builder, DeviceKey, session.DeviceModel);
                    AppendStringField(builder, OperatingSystemKey, session.OperatingSystem);
                    AppendStringField(builder, AppVersionKey, session.AppVersion);
                    break;

                case AnalyticsEventType.SessionEnd:
                    AppendIntegerField(builder, DurationKey, record.DurationMs);
                    AppendIntegerField(builder, MatchesKey, record.SlotA);
                    break;

                case AnalyticsEventType.MatchStart:
                    AppendIntegerField(builder, SeedKey, record.SlotA);
                    AppendEnumField(builder, PlayerOneControlKey, GetName((PlayerControl)record.SlotB), record.SlotB);
                    AppendEnumField(builder, PlayerTwoControlKey, GetName((PlayerControl)record.SlotC), record.SlotC);
                    AppendFloatField(builder, StandardSecondsKey, record.MeasureA);
                    AppendFloatField(builder, OvertimeSecondsKey, record.MeasureB);
                    break;

                case AnalyticsEventType.MatchEnd:
                    AppendIntegerField(builder, WinnerKey, record.SlotA);
                    AppendEnumField(builder, ReasonKey, GetName((MatchEndReason)record.SlotB), record.SlotB);
                    AppendIntegerField(builder, PlayerOneScoreKey, record.SlotC);
                    AppendIntegerField(builder, PlayerTwoScoreKey, record.SlotD);
                    AppendIntegerField(builder, DurationKey, record.DurationMs);
                    AppendBooleanField(builder, OvertimeKey, record.Flag);
                    break;

                case AnalyticsEventType.CardDeployed:
                    AppendIntegerField(builder, PlayerKey, record.PlayerId);
                    AppendStringField(builder, CardKey, record.Card.Value);
                    AppendIntegerField(builder, HexQKey, record.Hex.Q);
                    AppendIntegerField(builder, HexRKey, record.Hex.R);
                    AppendIntegerField(builder, CostKey, record.SlotA);
                    break;

                case AnalyticsEventType.CardPlayRejected:
                    AppendIntegerField(builder, PlayerKey, record.PlayerId);
                    AppendStringField(builder, CardKey, record.Card.Value);
                    AppendEnumField(builder, ReasonKey, GetName((CardPlayResult)record.SlotA), record.SlotA);
                    break;

                case AnalyticsEventType.EnergySpent:
                    AppendIntegerField(builder, PlayerKey, record.PlayerId);
                    AppendFloatField(builder, EnergyAfterKey, record.MeasureA);
                    AppendBooleanField(builder, SuccessKey, record.Flag);
                    break;

                case AnalyticsEventType.ConversionEvent:
                    AppendIntegerField(builder, PlayerKey, record.PlayerId);
                    AppendIntegerField(builder, ConvertedKey, record.SlotA);
                    AppendIntegerField(builder, StrippedKey, record.SlotB);
                    break;

                case AnalyticsEventType.AbilityResolved:
                    AppendIntegerField(builder, PlayerKey, record.PlayerId);
                    AppendIntegerField(builder, AffectedKey, record.SlotA);
                    AppendIntegerField(builder, HexesKey, record.SlotB);
                    AppendIntegerField(builder, DestroyedKey, record.SlotC);
                    break;

                case AnalyticsEventType.CardDiscarded:
                    AppendIntegerField(builder, PlayerKey, record.PlayerId);
                    AppendStringField(builder, CardKey, record.Card.Value);
                    AppendIntegerField(builder, SlotKey, record.SlotA);
                    break;

                case AnalyticsEventType.PhaseChanged:
                    AppendEnumField(builder, PhaseKey, GetName((MatchPhase)record.SlotA), record.SlotA);
                    break;

                case AnalyticsEventType.MoveExecuted:
                    AppendIntegerField(builder, PlayerKey, record.PlayerId);
                    AppendEnumField(builder, MoveKey, GetName((MoveType)record.SlotA), record.SlotA);
                    AppendIntegerField(builder, SourceHexQKey, record.SourceHex.Q);
                    AppendIntegerField(builder, SourceHexRKey, record.SourceHex.R);
                    AppendIntegerField(builder, HexQKey, record.Hex.Q);
                    AppendIntegerField(builder, HexRKey, record.Hex.R);
                    AppendIntegerField(builder, UnitKey, record.SlotB);
                    break;
            }
        }

        private static string GetName(PlayerControl value)
        {
            return value switch
            {
                PlayerControl.Unassigned => nameof(PlayerControl.Unassigned),
                PlayerControl.LocalHuman => nameof(PlayerControl.LocalHuman),
                PlayerControl.RemoteHuman => nameof(PlayerControl.RemoteHuman),
                PlayerControl.Machine => nameof(PlayerControl.Machine),
                _ => null,
            };
        }

        private static string GetName(MatchEndReason value)
        {
            return value switch
            {
                MatchEndReason.None => nameof(MatchEndReason.None),
                MatchEndReason.TimeLimit => nameof(MatchEndReason.TimeLimit),
                MatchEndReason.Domination => nameof(MatchEndReason.Domination),
                MatchEndReason.Draw => nameof(MatchEndReason.Draw),
                MatchEndReason.Surrender => nameof(MatchEndReason.Surrender),
                _ => null,
            };
        }

        private static string GetName(MatchPhase value)
        {
            return value switch
            {
                MatchPhase.None => nameof(MatchPhase.None),
                MatchPhase.Loading => nameof(MatchPhase.Loading),
                MatchPhase.Countdown => nameof(MatchPhase.Countdown),
                MatchPhase.Standard => nameof(MatchPhase.Standard),
                MatchPhase.OvertimeCheck => nameof(MatchPhase.OvertimeCheck),
                MatchPhase.Overtime => nameof(MatchPhase.Overtime),
                MatchPhase.Ended => nameof(MatchPhase.Ended),
                MatchPhase.Results => nameof(MatchPhase.Results),
                _ => null,
            };
        }

        private static string GetName(CardPlayResult value)
        {
            return value switch
            {
                CardPlayResult.Success => nameof(CardPlayResult.Success),
                CardPlayResult.UnknownPlayer => nameof(CardPlayResult.UnknownPlayer),
                CardPlayResult.SlotOutOfRange => nameof(CardPlayResult.SlotOutOfRange),
                CardPlayResult.CardNotFound => nameof(CardPlayResult.CardNotFound),
                CardPlayResult.InvalidTargetCount => nameof(CardPlayResult.InvalidTargetCount),
                CardPlayResult.InsufficientEnergy => nameof(CardPlayResult.InsufficientEnergy),
                CardPlayResult.IllegalPlacement => nameof(CardPlayResult.IllegalPlacement),
                CardPlayResult.BoardUnavailable => nameof(CardPlayResult.BoardUnavailable),
                CardPlayResult.ResolverBusy => nameof(CardPlayResult.ResolverBusy),
                CardPlayResult.MatchNotInPlay => nameof(CardPlayResult.MatchNotInPlay),
                _ => null,
            };
        }

        private static string GetName(MoveType value)
        {
            return value switch
            {
                MoveType.Deploy => nameof(MoveType.Deploy),
                MoveType.Clone => nameof(MoveType.Clone),
                MoveType.Jump => nameof(MoveType.Jump),
                _ => null,
            };
        }

        // A value with no name is written as its bare number, never as a made-up string — see the class remarks.
        private static void AppendEnumField(StringBuilder builder, string key, string name, int value)
        {
            if (name == null)
            {
                AppendIntegerField(builder, key, value);
                return;
            }

            AppendStringField(builder, key, name);
        }

        private static void AppendIntegerField(StringBuilder builder, string key, long value)
        {
            AppendKey(builder, key, isFirst: false);
            AppendInteger(builder, value);
        }

        private static void AppendFloatField(StringBuilder builder, string key, float value)
        {
            AppendKey(builder, key, isFirst: false);

            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                builder.Append(JsonNull);
                return;
            }

            Span<char> characters = stackalloc char[MaxFloatCharacters];

            if (value.TryFormat(characters, out int written, default, CultureInfo.InvariantCulture))
            {
                builder.Append(characters[..written]);
                return;
            }

            builder.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void AppendBooleanField(StringBuilder builder, string key, bool value)
        {
            AppendKey(builder, key, isFirst: false);
            builder.Append(value ? JsonTrue : JsonFalse);
        }

        private static void AppendStringField(StringBuilder builder, string key, string value)
        {
            AppendKey(builder, key, isFirst: false);
            AppendEscapedString(builder, value);
        }

        // Keys are compile-time constants from this class and need no escaping.
        private static void AppendKey(StringBuilder builder, string key, bool isFirst)
        {
            if (!isFirst)
            {
                builder.Append(',');
            }

            builder.Append('"');
            builder.Append(key);
            builder.Append('"');
            builder.Append(':');
        }

        // StringBuilder.Append(long) formats through the current culture, which is what this avoids: the digits are
        // produced by hand into a stack buffer, so the output is invariant and allocation-free.
        private static void AppendInteger(StringBuilder builder, long value)
        {
            if (value == 0)
            {
                builder.Append('0');
                return;
            }

            if (value < 0)
            {
                builder.Append('-');

                if (value == long.MinValue)
                {
                    builder.Append(Int64MinValueDigits);
                    return;
                }

                value = -value;
            }

            Span<char> digits = stackalloc char[MaxInt64Digits];
            int position = digits.Length;

            while (value > 0)
            {
                position--;
                digits[position] = (char)('0' + (value % 10));
                value /= 10;
            }

            builder.Append(digits[position..]);
        }

        private static void AppendEscapedString(StringBuilder builder, string value)
        {
            builder.Append('"');

            if (value != null)
            {
                for (int i = 0; i < value.Length; i++)
                {
                    AppendEscapedCharacter(builder, value[i]);
                }
            }

            builder.Append('"');
        }

        private static void AppendEscapedCharacter(StringBuilder builder, char character)
        {
            switch (character)
            {
                case '"':
                    builder.Append('\\').Append('"');
                    return;
                case '\\':
                    builder.Append('\\').Append('\\');
                    return;
                case '\n':
                    builder.Append('\\').Append('n');
                    return;
                case '\r':
                    builder.Append('\\').Append('r');
                    return;
                case '\t':
                    builder.Append('\\').Append('t');
                    return;
                case '\b':
                    builder.Append('\\').Append('b');
                    return;
                case '\f':
                    builder.Append('\\').Append('f');
                    return;
            }

            if (character < ' ')
            {
                builder.Append('\\').Append('u').Append('0').Append('0');
                builder.Append(HexDigits[character >> 4]);
                builder.Append(HexDigits[character & 0xF]);
                return;
            }

            builder.Append(character);
        }
    }
}
