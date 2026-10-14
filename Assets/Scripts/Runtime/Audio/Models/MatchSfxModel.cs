using System.Collections.Generic;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;

namespace GooGalaxy.Runtime.Audio.Models
{
    /// <summary>
    /// The engine-free rules behind the match's sound effects: given one fact the match has already published, which sound
    /// — if any — answers it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Truthful by construction.</b> Every input is a fact that has already happened — a play the board accepted or
    /// refused, a conversion that resolved, an effect <see cref="AbilityResult.ResolvedEffects" /> says took hold — so a
    /// sound is never chosen for something a card merely authored, and a refused play is heard as a refusal, never as the
    /// play. Each <c>TryResolve*</c> method answers false when nothing should play.
    /// </para>
    /// <para>
    /// <b>Whose ears.</b> Board facts are heard from both sides: a card played, a move, a conversion, a condition, a hazard,
    /// a detonation and the overtime warning all play whoever caused them. A refused play and a discard play only for the
    /// local seat, resolved at <see cref="ApplyMatchStarted" /> through <see cref="LocalSeatResolver" /> exactly as the
    /// music does; with no seat driven on this device they stay silent and the shared cues still play. Interaction feedback
    /// is local by construction and needs no seat at all.
    /// </para>
    /// <para>
    /// <b>Latches.</b> A deployment that freezes or roots several units raises one <c>StatusApplied</c> per unit, and is
    /// heard once per condition: the first <see cref="StatusType" /> of each kind plays, and the latch stays set until the
    /// owner calls <see cref="ResetDeploymentLatch" />. The owner calls it on every fact that opens or closes a deployment —
    /// a move executed, a card play attempted, an ability resolved — so a latch set by one deployment never mutes the next,
    /// even when one of those facts never arrives. The overtime warning plays once per match and is rearmed by
    /// <see cref="ApplyMatchStarted" />.
    /// </para>
    /// <para>
    /// <b>One instance serves every match of its owner's lifetime</b>, and every method is allocation-free, because each one
    /// runs on the input path or inside a board resolution.
    /// </para>
    /// </remarks>
    public class MatchSfxModel
    {
        /// <summary>The fewest converted units that count as a <see cref="ConversionTier.Chain" />.</summary>
        public const int ChainConversionMinimum = 2;

        /// <summary>The fewest converted units that count as a <see cref="ConversionTier.Mass" />.</summary>
        public const int MassConversionMinimum = 5;

        private int _localPlayerId = PlayerSlot.UnassignedId;
        private int _heardStatusMask;
        private bool _hasLocalSeat;
        private bool _hasWarnedOvertime;

        /// <summary>
        /// Whether the current match names a seat driven by the person holding this device. Without one, the local-only
        /// cues — a refused play, a discard — stay silent.
        /// </summary>
        public bool HasLocalSeat => _hasLocalSeat;

        /// <summary>The local seat's player id, or <see cref="PlayerSlot.UnassignedId" /> before any match has been announced.</summary>
        public int LocalPlayerId => _localPlayerId;

        /// <summary>Grades how many units one landing converted.</summary>
        /// <param name="convertedCount">How many units the landing converted. Zero or less is <see cref="ConversionTier.None" />.</param>
        /// <returns>The tier, by <see cref="ChainConversionMinimum" /> and <see cref="MassConversionMinimum" />.</returns>
        public static ConversionTier ResolveConversionTier(int convertedCount)
        {
            if (convertedCount >= MassConversionMinimum)
            {
                return ConversionTier.Mass;
            }

            if (convertedCount >= ChainConversionMinimum)
            {
                return ConversionTier.Chain;
            }

            return convertedCount > 0 ? ConversionTier.Single : ConversionTier.None;
        }

        /// <summary>Starts a newly announced match: resolves the local seat and rearms every latch.</summary>
        /// <param name="config">The configuration the match was announced with.</param>
        public void ApplyMatchStarted(in MatchConfiguration config)
        {
            _hasLocalSeat = LocalSeatResolver.TryResolve(in config, out PlayerSlot home, out _);
            _localPlayerId = home.Id;
            _heardStatusMask = 0;
            _hasWarnedOvertime = false;
        }

        /// <summary>Decides whether a play attempt is answered by the played card's own sound.</summary>
        /// <remarks>Only a play the board accepted, by either player.</remarks>
        /// <param name="attempt">The attempt, as <c>MatchEvents.CardPlayAttempted</c> published it.</param>
        /// <param name="card">The card whose sound to play; meaningful only when this returns true.</param>
        /// <returns>True when the attempt succeeded.</returns>
        public bool TryResolveCardPlayed(in CardPlayAttempt attempt, out CardId card)
        {
            card = attempt.CardId;

            return attempt.Result == CardPlayResult.Success;
        }

        /// <summary>Decides whether a play attempt is answered by the refusal sound.</summary>
        /// <remarks>
        /// Only for the local seat, and only for the refusals a player causes by aiming or paying wrongly —
        /// <see cref="CardPlayResult.IllegalPlacement" />, <see cref="CardPlayResult.InsufficientEnergy" /> and
        /// <see cref="CardPlayResult.InvalidTargetCount" />. Contention and wiring faults, such as
        /// <see cref="CardPlayResult.ResolverBusy" />, are not the player's mistake and stay silent.
        /// </remarks>
        /// <param name="attempt">The attempt, as <c>MatchEvents.CardPlayAttempted</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True when the local player's play was refused for one of those reasons.</returns>
        public bool TryResolvePlayRejected(in CardPlayAttempt attempt, out AudioCue cue)
        {
            cue = AudioCue.PlayRejected;

            if (!IsLocalPlayer(attempt.PlayerId))
            {
                return false;
            }

            return attempt.Result is CardPlayResult.IllegalPlacement or CardPlayResult.InsufficientEnergy or CardPlayResult.InvalidTargetCount;
        }

        /// <summary>Decides which sound a move that has already been applied makes.</summary>
        /// <remarks>A Deploy is silent here: the card played is what <see cref="TryResolveCardPlayed" /> answers.</remarks>
        /// <param name="command">The move, as <c>MatchEvents.MoveExecuted</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True for a Clone or a Jump.</returns>
        public bool TryResolveMove(in MoveCommand command, out AudioCue cue)
        {
            switch (command.Type)
            {
                case MoveType.Clone:
                    cue = AudioCue.CloneMove;
                    return true;
                case MoveType.Jump:
                    cue = AudioCue.JumpMove;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        /// <summary>Decides which sound a landing's conversions make, graded by <see cref="ResolveConversionTier" />.</summary>
        /// <remarks>Reads <c>Count</c> only, so the publisher's buffers are never iterated or retained.</remarks>
        /// <param name="result">What the landing did, as <c>MatchEvents.ConversionResolved</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True when at least one unit was converted.</returns>
        public bool TryResolveConversion(in ConversionResult result, out AudioCue cue)
        {
            switch (ResolveConversionTier(GetCount(result.ConvertedUnitIds)))
            {
                case ConversionTier.Single:
                    cue = AudioCue.ConversionSingle;
                    return true;
                case ConversionTier.Chain:
                    cue = AudioCue.ConversionChain;
                    return true;
                case ConversionTier.Mass:
                    cue = AudioCue.ConversionMass;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        /// <summary>Decides whether a landing's conversions also make the armor-strip sound.</summary>
        /// <remarks>Independent of <see cref="TryResolveConversion" />: one landing can convert some units and strip others.</remarks>
        /// <param name="result">What the landing did, as <c>MatchEvents.ConversionResolved</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True when at least one unit spent its armor.</returns>
        public bool TryResolveArmorStrip(in ConversionResult result, out AudioCue cue)
        {
            cue = AudioCue.ArmorStrip;

            return GetCount(result.ArmorStrippedUnitIds) > 0;
        }

        /// <summary>Decides whether a condition landing on a unit makes a sound, and latches it for the deployment.</summary>
        /// <remarks>
        /// Plays for the first application of each <see cref="StatusType" /> until <see cref="ResetDeploymentLatch" />
        /// clears the latch; every later one in the same deployment, refreshes included, is silent. A condition with no
        /// sound of its own never latches.
        /// </remarks>
        /// <param name="change">The application, as <c>MatchEvents.StatusApplied</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True for the deployment's first Frozen or first Rooted.</returns>
        public bool TryResolveStatusApplied(in StatusChange change, out AudioCue cue)
        {
            switch (change.Status)
            {
                case StatusType.Frozen:
                    cue = AudioCue.Freeze;
                    break;
                case StatusType.Rooted:
                    cue = AudioCue.Root;
                    break;
                default:
                    cue = default;
                    return false;
            }

            int statusBit = 1 << (int)change.Status;

            if ((_heardStatusMask & statusBit) != 0)
            {
                return false;
            }

            _heardStatusMask |= statusBit;

            return true;
        }

        /// <summary>Decides whether a deployment's impacts placed a hazard worth hearing.</summary>
        /// <param name="result">What the impacts did, as <c>MatchEvents.AbilityResolved</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True when <see cref="ImpactEffectFlags.SpawnHazard" /> took hold.</returns>
        public bool TryResolveHazardSpawned(in AbilityResult result, out AudioCue cue)
        {
            cue = AudioCue.HazardSpawned;

            return (result.ResolvedEffects & ImpactEffectFlags.SpawnHazard) != 0;
        }

        /// <summary>Decides whether a deployment's impacts blew its acting unit up.</summary>
        /// <param name="result">What the impacts did, as <c>MatchEvents.AbilityResolved</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True when <see cref="ImpactEffectFlags.SelfDestruct" /> took hold.</returns>
        public bool TryResolveDetonation(in AbilityResult result, out AudioCue cue)
        {
            cue = AudioCue.Detonation;

            return (result.ResolvedEffects & ImpactEffectFlags.SelfDestruct) != 0;
        }

        /// <summary>Clears the condition latch, so the next deployment's first condition of each kind is heard again.</summary>
        /// <remarks>Idempotent; safe to call at the start or end of a deployment, and more than once.</remarks>
        public void ResetDeploymentLatch()
        {
            _heardStatusMask = 0;
        }

        /// <summary>Decides which sound a fuse running out makes.</summary>
        /// <param name="cue">The sound to play.</param>
        /// <returns>Always true: a fuse only expires by removing the unit it was on.</returns>
        public bool TryResolveFuseExpired(out AudioCue cue)
        {
            cue = AudioCue.Detonation;

            return true;
        }

        /// <summary>Decides whether a discard makes a sound.</summary>
        /// <param name="playerId">The player who discarded, as <c>MatchEvents.CardDiscarded</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True only for the local seat's own discard.</returns>
        public bool TryResolveCardDiscarded(int playerId, out AudioCue cue)
        {
            cue = AudioCue.Discard;

            return IsLocalPlayer(playerId);
        }

        /// <summary>Decides whether entering a phase makes a sound, and latches the overtime warning for the match.</summary>
        /// <param name="phase">The phase just entered, as <c>MatchEvents.MatchPhaseChanged</c> published it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True the first time this match enters <see cref="MatchPhase.Overtime" />.</returns>
        public bool TryResolvePhase(MatchPhase phase, out AudioCue cue)
        {
            cue = AudioCue.OvertimeWarning;

            if ((phase != MatchPhase.Overtime) || _hasWarnedOvertime)
            {
                return false;
            }

            _hasWarnedOvertime = true;

            return true;
        }

        /// <summary>Decides which sound acknowledges a step of the local player's own gesture.</summary>
        /// <param name="feedback">The step, as an <c>IInteractionFeedbackSource</c> raised it.</param>
        /// <param name="cue">The sound to play.</param>
        /// <returns>True for a selection, a drag starting, or a drop refused; false for a deliberate clear.</returns>
        public bool TryResolveInteraction(InteractionFeedback feedback, out AudioCue cue)
        {
            switch (feedback)
            {
                case InteractionFeedback.Selected:
                    cue = AudioCue.Select;
                    return true;
                case InteractionFeedback.DragStarted:
                    cue = AudioCue.DragStart;
                    return true;
                case InteractionFeedback.DropRejected:
                    cue = AudioCue.DropRejected;
                    return true;
                default:
                    cue = default;
                    return false;
            }
        }

        private static int GetCount(IReadOnlyList<int> unitIds)
        {
            return unitIds == null ? 0 : unitIds.Count;
        }

        private bool IsLocalPlayer(int playerId)
        {
            return _hasLocalSeat && (playerId == _localPlayerId);
        }
    }
}
