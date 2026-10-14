using System.Collections.Generic;
using GooGalaxy.Runtime.Audio.Interfaces;
using GooGalaxy.Runtime.Audio.Models;
using GooGalaxy.Runtime.Shared.Commands;
using GooGalaxy.Runtime.Shared.Constants;
using GooGalaxy.Runtime.Shared.Events;
using GooGalaxy.Runtime.Shared.Interfaces;
using GooGalaxy.Runtime.Shared.Types;
using GooGalaxy.Runtime.Shared.Utils;
using UnityEngine;
using VContainer;

namespace GooGalaxy.Runtime.Audio.Controllers
{
    /// <summary>
    /// Plays the match's sound effects: listens to the facts <see cref="MatchEvents" /> publishes and to the local player's
    /// own gestures, and plays whatever <see cref="MatchSfxModel" /> says answers each one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Audio only listens.</b> Nothing calls this to report that something happened; every sound follows a fact that has
    /// already been published, so a sound is never ahead of — or instead of — the thing it describes. Handlers read their
    /// payloads and never touch the board, so running inside a resolution's dispatch is safe.
    /// </para>
    /// <para>
    /// <b>Every handler allocates nothing after warm-up.</b> Payload collections are read by <c>Count</c> only, the model is a handful of
    /// comparisons, and the service plays an already-loaded event. Local only: <see cref="MatchEvents" /> replicates nothing,
    /// so each device hears its own copy of every fact.
    /// </para>
    /// <para>
    /// <b>No bank gating.</b> Every fact this plays from is published once play has opened, after the countdown the match
    /// bank loads during; a cue asked for before the load finishes is reported once by the service and stays silent. The
    /// end-of-match stinger stays with <see cref="AdaptiveMusicController" />, which is why <c>MatchEnded</c> is not
    /// subscribed here.
    /// </para>
    /// <para>
    /// <b>The gesture source is optional.</b> A null <see cref="IInteractionFeedbackSource" /> leaves the selection, drag and
    /// drop cues silent and every board cue playing — the container always supplies one in a match scene, so null only
    /// happens where the component is built by hand. A null <see cref="IAudioService" /> leaves the whole component inert,
    /// after one warning.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public class MatchSfxController : MonoBehaviour
    {
        private readonly MatchSfxModel _model = new();

        private IAudioService _audioService;
        private IInteractionFeedbackSource _feedbackSource;

        internal MatchSfxModel Model => _model;

        private bool IsConfigured => _audioService != null;

        /// <remarks>
        /// Must run before <c>OnEnable</c>, which is where the gesture source is subscribed to — the container injects a
        /// registered scene component while its scope wakes, ahead of the component's own callbacks.
        /// </remarks>
        [Inject]
        public void Construct(IAudioService audioService, IInteractionFeedbackSource feedbackSource)
        {
            if (audioService == null)
            {
                Debug.LogWarning(AudioLogMessages.SfxServiceMissing, this);
                return;
            }

            _audioService = audioService;
            _feedbackSource = feedbackSource;
        }

        protected void OnEnable()
        {
            MatchEvents.MatchStarted += HandleMatchStarted;
            MatchEvents.MatchPhaseChanged += HandleMatchPhaseChanged;
            MatchEvents.CardPlayAttempted += HandleCardPlayAttempted;
            MatchEvents.MoveExecuted += HandleMoveExecuted;
            MatchEvents.ConversionResolved += HandleConversionResolved;
            MatchEvents.StatusApplied += HandleStatusApplied;
            MatchEvents.AbilityResolved += HandleAbilityResolved;
            MatchEvents.FuseExpired += HandleFuseExpired;
            MatchEvents.CardDiscarded += HandleCardDiscarded;

            if (!UnityReference.IsUnavailable(_feedbackSource))
            {
                _feedbackSource.FeedbackRaised += HandleFeedbackRaised;
            }
        }

        protected void OnDisable()
        {
            MatchEvents.MatchStarted -= HandleMatchStarted;
            MatchEvents.MatchPhaseChanged -= HandleMatchPhaseChanged;
            MatchEvents.CardPlayAttempted -= HandleCardPlayAttempted;
            MatchEvents.MoveExecuted -= HandleMoveExecuted;
            MatchEvents.ConversionResolved -= HandleConversionResolved;
            MatchEvents.StatusApplied -= HandleStatusApplied;
            MatchEvents.AbilityResolved -= HandleAbilityResolved;
            MatchEvents.FuseExpired -= HandleFuseExpired;
            MatchEvents.CardDiscarded -= HandleCardDiscarded;

            // A reference comparison on purpose: the event lives on the managed object, so unsubscribing stays correct even
            // when the source was destroyed first during scene teardown.
            if (_feedbackSource != null)
            {
                _feedbackSource.FeedbackRaised -= HandleFeedbackRaised;
            }
        }

        private void PlayCue(bool hasCue, AudioCue cue)
        {
            if (!hasCue)
            {
                return;
            }

            _audioService.PlayOneShot(cue);
        }

        private void HandleMatchStarted(MatchConfiguration config)
        {
            if (!IsConfigured)
            {
                return;
            }

            _model.ApplyMatchStarted(in config);
        }

        private void HandleMatchPhaseChanged(MatchPhase phase)
        {
            if (!IsConfigured)
            {
                return;
            }

            PlayCue(_model.TryResolvePhase(phase, out AudioCue cue), cue);
        }

        private void HandleCardPlayAttempted(CardPlayAttempt attempt)
        {
            if (!IsConfigured)
            {
                return;
            }

            // Published after the play fully resolved, so it also closes a Protocol's deployment, which has no move.
            _model.ResetDeploymentLatch();

            if (_model.TryResolveCardPlayed(in attempt, out CardId card))
            {
                _audioService.PlayCardCue(card);
                return;
            }

            PlayCue(_model.TryResolvePlayRejected(in attempt, out AudioCue cue), cue);
        }

        private void HandleMoveExecuted(MoveCommand command, IReadOnlyList<HexCoordinates> affectedCoordinates)
        {
            if (!IsConfigured)
            {
                return;
            }

            // Published before the deployment's conditions, so the latch starts clear even if the last deployment never closed.
            _model.ResetDeploymentLatch();
            PlayCue(_model.TryResolveMove(in command, out AudioCue cue), cue);
        }

        private void HandleConversionResolved(int actingPlayerId, ConversionResult result)
        {
            if (!IsConfigured)
            {
                return;
            }

            PlayCue(_model.TryResolveConversion(in result, out AudioCue conversionCue), conversionCue);
            PlayCue(_model.TryResolveArmorStrip(in result, out AudioCue armorCue), armorCue);
        }

        private void HandleStatusApplied(StatusChange change)
        {
            if (!IsConfigured)
            {
                return;
            }

            PlayCue(_model.TryResolveStatusApplied(in change, out AudioCue cue), cue);
        }

        private void HandleAbilityResolved(int actingPlayerId, AbilityResult result)
        {
            if (!IsConfigured)
            {
                return;
            }

            PlayCue(_model.TryResolveHazardSpawned(in result, out AudioCue hazardCue), hazardCue);
            PlayCue(_model.TryResolveDetonation(in result, out AudioCue detonationCue), detonationCue);
            _model.ResetDeploymentLatch();
        }

        private void HandleFuseExpired(int unitId, int playerId)
        {
            if (!IsConfigured)
            {
                return;
            }

            PlayCue(_model.TryResolveFuseExpired(out AudioCue cue), cue);
        }

        private void HandleCardDiscarded(int playerId, CardId discardedCard, int slotIndex)
        {
            if (!IsConfigured)
            {
                return;
            }

            PlayCue(_model.TryResolveCardDiscarded(playerId, out AudioCue cue), cue);
        }

        private void HandleFeedbackRaised(InteractionFeedback feedback)
        {
            if (!IsConfigured)
            {
                return;
            }

            PlayCue(_model.TryResolveInteraction(feedback, out AudioCue cue), cue);
        }
    }
}
